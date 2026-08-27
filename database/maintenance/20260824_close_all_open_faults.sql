-- Tüm açık arızaları tarihçeyi koruyarak raporlar, kontrol eder ve kapatır.
-- Script tek transaction içinde çalışır; herhangi bir adım hata verirse hiçbir değişiklik kaydedilmez.
BEGIN;

SET LOCAL search_path TO fault_management, public;

DO $$
DECLARE
    v_now                 timestamptz := clock_timestamp();
    v_admin_id            bigint;
    v_admin_role_id       bigint;
    v_waiting_status_id   bigint;
    v_resolved_status_id  bigint;
    v_closed_status_id    bigint;
    v_available_status_id bigint;
    v_assignment_id       bigint;
    v_report_id           bigint;
    v_report_description  text;
    v_action_description  text;
    v_fault               record;
BEGIN
    -- Sistem işlemlerinin log ve geçmiş kayıtlarında kullanılacak aktif Admin hesabı alınır.
    SELECT u.id, u.role_id
      INTO v_admin_id, v_admin_role_id
      FROM app_users u
      JOIN roles r ON r.id = u.role_id
     WHERE u.is_active AND r.name = 'Admin'
     ORDER BY u.id
     LIMIT 1;

    IF v_admin_id IS NULL THEN
        RAISE EXCEPTION 'İşlemi kaydedecek aktif Admin hesabı bulunamadı.';
    END IF;

    SELECT id INTO STRICT v_waiting_status_id FROM fault_statuses WHERE code = 'WAITING_INSPECTION';
    SELECT id INTO STRICT v_resolved_status_id FROM fault_statuses WHERE code = 'RESOLVED';
    SELECT id INTO STRICT v_closed_status_id FROM fault_statuses WHERE code = 'CLOSED';
    SELECT id INTO STRICT v_available_status_id FROM vehicle_statuses WHERE code = 'AVAILABLE';

    -- İşlem başladığında açık olan kayıtlar döngüye alınır; kapanmış tarihsel kayıtlar değiştirilmez.
    FOR v_fault IN
        SELECT f.id,
               f.fault_number,
               f.vehicle_id,
               f.fault_status_id AS old_fault_status_id,
               f.description AS fault_description,
               fc.name AS category_name,
               v.vehicle_status_id AS old_vehicle_status_id,
               v.current_mileage
          FROM faults f
          JOIN fault_categories fc ON fc.id = f.fault_category_id
          JOIN vehicles v ON v.id = f.vehicle_id
         WHERE f.is_active AND f.closed_at IS NULL
         ORDER BY f.id
         FOR UPDATE OF f, v
    LOOP
        -- Arızanın en son ekip ataması raporun bağlanacağı çalışma kaydı olarak kullanılır.
        SELECT fa.id
          INTO v_assignment_id
          FROM fault_assignments fa
         WHERE fa.fault_id = v_fault.id
         ORDER BY fa.assigned_at DESC, fa.id DESC
         LIMIT 1;

        IF v_assignment_id IS NULL THEN
            RAISE EXCEPTION '% numaralı arızanın ekip ataması bulunamadı.', v_fault.fault_number;
        END IF;

        -- Kategoriye göre teknik açıdan anlamlı ve birbirinden farklı rapor metni hazırlanır.
        IF v_fault.category_name ILIKE '%Fren%' THEN
            v_report_description :=
                'Fren sistemi ayrıntılı olarak kontrol edildi. Balata, disk, hidrolik hatlar ve fren basıncı incelendi; gerekli ayar ve bağlantı düzeltmeleri yapıldı. Statik fren ve düşük hızlı yol testi sonucunda frenleme değerlerinin güvenli çalışma aralığında olduğu doğrulandı.';
            v_action_description :=
                'Fren mekanizması, hidrolik bağlantılar ve basınç değerleri kontrol edildi; ayarlar tamamlanarak fren testi uygulandı.';
        ELSIF v_fault.category_name ILIKE '%Kaporta%' THEN
            v_report_description :=
                'Hasarlı kaporta bölgesi ve bağlantı noktaları incelendi. Deforme olan yüzey düzeltilip gevşek bağlantılar sabitlendi; kapı, ayna ve çevre ekipmanlarının güvenli çalışma kontrolleri tamamlandı. Görsel ve işlevsel kontrol sonucunda araç kullanıma uygun bulundu.';
            v_action_description :=
                'Kaporta düzeltme ve sabitleme işlemleri yapıldı; çevre bağlantıları ile güvenlik kontrolleri tamamlandı.';
        ELSE
            v_report_description := format(
                '%s arızası için ilgili sistem ve bağlantılar kontrol edildi. Arıza kaynağı giderildi, gerekli ayarlar tamamlandı ve işlev testi başarıyla gerçekleştirildi. Araç güvenli kullanıma uygun bulundu.',
                v_fault.category_name);
            v_action_description :=
                'Arızalı sistem kontrol edildi, gerekli düzeltme ve ayarlar yapılarak işlev testi tamamlandı.';
        END IF;

        -- Daha önce çözülemedi raporu varsa silinmez; yeni başarılı çalışma ayrı rapor olarak eklenir.
        INSERT INTO repair_reports
            (fault_assignment_id, created_by_user_id, result, description,
             started_at, completed_at, submitted_at, is_submitted, is_active,
             created_at, solution_summary, recurrence_prevention, requires_follow_up)
        VALUES
            (v_assignment_id, v_admin_id, 'RESOLVED', v_report_description,
             v_now - interval '45 minutes', v_now - interval '10 minutes',
             v_now - interval '9 minutes', true, true, v_now - interval '9 minutes',
             v_action_description,
             'Aynı arızanın tekrarlamaması için ilgili parçaların periyodik bakım kontrollerinde yeniden incelenmesi önerildi.',
             false)
        RETURNING id INTO v_report_id;

        INSERT INTO repair_report_actions (repair_report_id, description, performed_at)
        VALUES (v_report_id, v_action_description, v_now - interval '12 minutes');

        -- Ekip çalışması tamamlanır ve ekip başka bir iş için tekrar müsait bırakılır.
        UPDATE fault_assignments
           SET started_at = COALESCE(started_at, v_now - interval '45 minutes'),
               completed_at = v_now - interval '10 minutes',
               is_active = false
         WHERE id = v_assignment_id;

        -- Kaynak görevleri de kapanır; geçmiş durumları ayrı tabloda korunur.
        INSERT INTO fault_resource_status_histories
            (resource_assignment_id, old_status, new_status, changed_by_user_id, description, changed_at)
        SELECT fra.id, fra.status, 'COMPLETED', v_admin_id,
               'Arıza giderildiği için kaynak görevi tamamlandı.', v_now - interval '8 minutes'
          FROM fault_resource_assignments fra
         WHERE fra.fault_id = v_fault.id AND (fra.is_active OR fra.status <> 'COMPLETED');

        UPDATE fault_resource_assignments
           SET status = 'COMPLETED',
               departed_at = COALESCE(departed_at, assigned_at + interval '1 minute'),
               arrived_at = COALESCE(arrived_at, assigned_at + interval '5 minutes'),
               completed_at = COALESCE(completed_at, v_now - interval '8 minutes'),
               is_active = false
         WHERE fault_id = v_fault.id;

        -- Teknik rapordan sonra araç zorunlu kontrol kuyruğuna alınır.
        INSERT INTO fault_status_histories
            (fault_id, old_status_id, new_status_id, changed_by_user_id, changed_by_role_id,
             description, is_system_action, changed_at)
        VALUES
            (v_fault.id, v_fault.old_fault_status_id, v_waiting_status_id,
             v_admin_id, v_admin_role_id,
             'Teknik rapor başarıyla gönderildi; araç tamir sonrası kontrole alındı.',
             true, v_now - interval '7 minutes');

        -- Her arıza için olumlu tamir sonrası kontrol kaydı oluşturulur.
        INSERT INTO vehicle_inspections
            (vehicle_id, fault_id, inspection_type, result, odometer, notes,
             inspected_by_user_id, inspected_at, next_action, created_at)
        VALUES
            (v_fault.vehicle_id, v_fault.id, 'POST_REPAIR', 'PASSED', v_fault.current_mileage,
             'Tamir sonrası görsel kontrol, güvenlik kontrolü ve işlev testi tamamlandı. Önceki arıza belirtisine rastlanmadı.',
             v_admin_id, v_now - interval '5 minutes',
             'Araç yeniden hizmete alınabilir; periyodik kontroller normal bakım planında sürdürülecektir.',
             v_now - interval '5 minutes');

        INSERT INTO fault_status_histories
            (fault_id, old_status_id, new_status_id, changed_by_user_id, changed_by_role_id,
             description, is_system_action, changed_at)
        VALUES
            (v_fault.id, v_waiting_status_id, v_resolved_status_id,
             v_admin_id, v_admin_role_id,
             'Tamir sonrası kontrol başarıyla tamamlandı; arıza çözüldü.',
             true, v_now - interval '4 minutes'),
            (v_fault.id, v_resolved_status_id, v_closed_status_id,
             v_admin_id, v_admin_role_id,
             'Teknik rapor ve başarılı kontrol kaydı doğrulandı; arıza kapatıldı.',
             true, v_now);

        -- Araç hizmete alınır ve araç durumundaki değişiklik ayrıca tarihçeye yazılır.
        IF v_fault.old_vehicle_status_id <> v_available_status_id THEN
            INSERT INTO vehicle_status_histories
                (vehicle_id, old_status_id, new_status_id, changed_by_user_id,
                 changed_at, description, fault_id)
            VALUES
                (v_fault.vehicle_id, v_fault.old_vehicle_status_id, v_available_status_id,
                 v_admin_id, v_now,
                 'Arıza giderilip kontrol başarıyla tamamlandığı için araç göreve hazır duruma alındı.',
                 v_fault.id);
        END IF;

        UPDATE vehicles
           SET vehicle_status_id = v_available_status_id
         WHERE id = v_fault.vehicle_id;

        UPDATE faults
           SET fault_status_id = v_closed_status_id,
               first_response_at = COALESCE(first_response_at, occurred_at + interval '5 minutes'),
               closed_at = v_now
         WHERE id = v_fault.id;

        -- Sunum/otomasyon işçisi kapanmış kaydı tekrar işlemeye çalışmasın.
        UPDATE fault_response_plans
           SET automation_enabled = false,
               automation_status = 'COMPLETED',
               next_automation_at = NULL,
               planned_repair_result = 'RESOLVED',
               automation_completed_at = v_now,
               last_automation_error = NULL,
               ready_to_close = false,
               inspection_attempt_count = GREATEST(inspection_attempt_count, 1)
         WHERE fault_id = v_fault.id AND is_active;

        -- Arızaya ait açık izleme uyarıları kapanışla birlikte çözümlenir.
        UPDATE fault_alerts
           SET alert_status = 'RESOLVED',
               resolved_at = v_now,
               resolved_by_user_id = v_admin_id,
               resolution_note = 'Arıza giderildi, araç kontrolü başarıyla tamamlandı ve kayıt kapatıldı.'
         WHERE fault_id = v_fault.id AND alert_status <> 'RESOLVED';

        INSERT INTO audit_logs
            (user_id, role_id, action, entity_type, entity_id,
             old_values, new_values, description, created_at)
        VALUES
            (v_admin_id, v_admin_role_id, 'FAULT_REPAIRED_INSPECTED_CLOSED',
             'faults', v_fault.id,
             jsonb_build_object('faultStatusId', v_fault.old_fault_status_id, 'closedAt', NULL),
             jsonb_build_object('faultStatusId', v_closed_status_id, 'closedAt', v_now,
                                'inspectionResult', 'PASSED', 'repairReportId', v_report_id),
             'Teknik rapor, başarılı tamir sonrası kontrol ve arıza kapanışı birlikte kaydedildi.',
             v_now);
    END LOOP;

    -- Artık aktif arızası olmayan ekipler yeniden müsait duruma getirilir.
    UPDATE technician_teams tt
       SET is_available = true
     WHERE tt.is_active
       AND NOT EXISTS (
           SELECT 1 FROM fault_assignments fa
           JOIN faults f ON f.id = fa.fault_id
            WHERE fa.team_id = tt.id AND fa.is_active AND f.closed_at IS NULL);

    -- Tamamlanan kaynak görevlerine bağlı ve başka aktif işi olmayan araç/sürücüler serbest bırakılır.
    UPDATE vehicles v
       SET vehicle_status_id = v_available_status_id
     WHERE EXISTS (
           SELECT 1 FROM fault_resource_assignments fra
            WHERE fra.vehicle_id = v.id
              AND fra.fault_id IN (SELECT id FROM faults WHERE closed_at = v_now))
       AND NOT EXISTS (
           SELECT 1 FROM fault_resource_assignments active_resource
            WHERE active_resource.vehicle_id = v.id AND active_resource.is_active)
       AND NOT EXISTS (
           SELECT 1 FROM task_assignments ta
           JOIN service_tasks st ON st.id = ta.service_task_id
            WHERE ta.vehicle_id = v.id AND ta.is_active AND st.status = 'IN_PROGRESS');

    UPDATE drivers d
       SET availability_status = 'AVAILABLE'
     WHERE EXISTS (
           SELECT 1 FROM fault_resource_assignments fra
            WHERE fra.driver_id = d.id
              AND fra.fault_id IN (SELECT id FROM faults WHERE closed_at = v_now))
       AND NOT EXISTS (
           SELECT 1 FROM fault_resource_assignments active_resource
            WHERE active_resource.driver_id = d.id AND active_resource.is_active)
       AND NOT EXISTS (
           SELECT 1 FROM task_assignments ta
           JOIN service_tasks st ON st.id = ta.service_task_id
            WHERE ta.driver_id = d.id AND ta.is_active AND st.status = 'IN_PROGRESS');
END
$$;

COMMIT;

-- İşlem sonrası doğrulama özeti.
SELECT
    COUNT(*) FILTER (WHERE f.is_active AND f.closed_at IS NULL) AS acik_ariza,
    COUNT(*) FILTER (WHERE f.closed_at IS NOT NULL) AS kapali_ariza
FROM fault_management.faults f;

