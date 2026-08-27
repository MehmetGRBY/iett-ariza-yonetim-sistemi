-- AMAÇ: Yapay zeka önerisini, önerinin dayandığı geçmiş kayıtları ve kullanıcı geri bildirimini kalıcı tutar.
-- AI karar verici değil karar destekçidir; accepted alanı yetkilinin öneriyi kabul/ret sonucunu saklar.
BEGIN;
SET search_path TO fault_management, public;

-- Her analiz çalışmasını model/prompt sürümü, güven puanı ve tahminleriyle saklar.
CREATE TABLE IF NOT EXISTS ai_suggestions (
    id bigserial PRIMARY KEY,
    fault_id bigint NOT NULL REFERENCES faults(id),
    created_by_user_id bigint NOT NULL REFERENCES app_users(id),
    suggestion_type varchar(40) NOT NULL,
    model_name varchar(100) NOT NULL,
    prompt_version varchar(30) NOT NULL,
    status varchar(30) NOT NULL DEFAULT 'GENERATED',
    probable_cause varchar(500),
    suggested_category_id bigint REFERENCES fault_categories(id),
    recommended_intervention varchar(50),
    estimated_repair_minutes integer,
    estimated_out_of_service_minutes integer,
    confidence_score numeric(5,4),
    response_json jsonb NOT NULL,
    similar_fault_count integer NOT NULL DEFAULT 0,
    ai_available boolean NOT NULL DEFAULT true,
    created_at timestamptz NOT NULL DEFAULT now(),
    reviewed_by_user_id bigint REFERENCES app_users(id),
    reviewed_at timestamptz,
    CONSTRAINT ck_ai_suggestions_status CHECK (status IN ('GENERATED','ACCEPTED','PARTIALLY_ACCEPTED','REJECTED')),
    CONSTRAINT ck_ai_suggestions_confidence CHECK (confidence_score IS NULL OR confidence_score BETWEEN 0 AND 1)
);

CREATE TABLE IF NOT EXISTS ai_suggestion_sources (
    id bigserial PRIMARY KEY,
    ai_suggestion_id bigint NOT NULL REFERENCES ai_suggestions(id) ON DELETE CASCADE,
    source_type varchar(30) NOT NULL,
    source_id bigint NOT NULL,
    relevance_score numeric(5,4),
    created_at timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE IF NOT EXISTS ai_feedback (
    id bigserial PRIMARY KEY,
    ai_suggestion_id bigint NOT NULL REFERENCES ai_suggestions(id),
    user_id bigint NOT NULL REFERENCES app_users(id),
    feedback_type varchar(30) NOT NULL,
    comment varchar(1000),
    actual_repair_minutes integer,
    created_at timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT ck_ai_feedback_type CHECK (feedback_type IN ('ACCEPTED','PARTIALLY_ACCEPTED','REJECTED','INCORRECT'))
);

CREATE INDEX IF NOT EXISTS ix_ai_suggestions_fault_created ON ai_suggestions(fault_id, created_at DESC);
CREATE INDEX IF NOT EXISTS ix_ai_sources_suggestion ON ai_suggestion_sources(ai_suggestion_id);
CREATE INDEX IF NOT EXISTS ix_ai_feedback_suggestion ON ai_feedback(ai_suggestion_id);

GRANT SELECT, INSERT, UPDATE ON ai_suggestions, ai_suggestion_sources, ai_feedback TO iett_fault_app;
GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA fault_management TO iett_fault_app;
COMMIT;
