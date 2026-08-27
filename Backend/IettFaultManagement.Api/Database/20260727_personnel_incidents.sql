-- AMAÇ: Sefer sırasında hastalanan/göreve devam edemeyen sürücüyü, yedek sürücü ve hizmet aracıyla birlikte kaydeder.
-- IF NOT EXISTS kullanımı scriptin aynı ortamda tekrar çalıştırılmasını daha güvenli hale getirir.
CREATE TABLE IF NOT EXISTS fault_management.personnel_incidents (
 id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY, event_number varchar(40) NOT NULL UNIQUE,
 driver_id bigint NOT NULL REFERENCES fault_management.drivers(id), replacement_driver_id bigint REFERENCES fault_management.drivers(id),
 vehicle_id bigint REFERENCES fault_management.vehicles(id), service_vehicle_id bigint REFERENCES fault_management.vehicles(id),
 garage_id bigint NOT NULL REFERENCES fault_management.garages(id), event_type varchar(30) NOT NULL, status varchar(30) NOT NULL,
 description varchar(1000) NOT NULL, occurred_at timestamptz NOT NULL, dispatched_at timestamptz, arrival_due_at timestamptz,
 resolved_at timestamptz, transferred_task_count integer NOT NULL DEFAULT 0,
 created_by_user_id bigint NOT NULL REFERENCES fault_management.app_users(id), created_at timestamptz NOT NULL DEFAULT now(), is_active boolean NOT NULL DEFAULT true,
 CONSTRAINT ck_personnel_incidents_type CHECK (event_type IN ('ILLNESS','EMERGENCY','UNFIT_FOR_DUTY')),
 CONSTRAINT ck_personnel_incidents_status CHECK (status IN ('WAITING_REPLACEMENT','DISPATCHED','RESOLVED','CANCELLED'))
);
-- Garajın en yeni personel olaylarını listeleyen sorguları hızlandırır.
CREATE INDEX IF NOT EXISTS ix_personnel_incidents_garage_date ON fault_management.personnel_incidents(garage_id, occurred_at DESC);
GRANT SELECT, INSERT, UPDATE ON fault_management.personnel_incidents TO iett_fault_app;
GRANT USAGE, SELECT ON SEQUENCE fault_management.personnel_incidents_id_seq TO iett_fault_app;
