-- Phase 5 review, attachment and metrics dashboard contract.
-- The local harness keeps screenshots as sanitized attachments only; raw screenshot bytes are deleted by ATTACHMENT_DELETE.

CREATE TABLE review_taxonomy_version (
    taxonomy_version text NOT NULL,
    taxonomy_type text NOT NULL CHECK (taxonomy_type IN ('EXIT_REASON', 'BREACH_TYPE', 'EMOTION')),
    schema_version text NOT NULL CHECK (schema_version = 'review_taxonomy_v1'),
    content_sha256 text NOT NULL,
    published_at text NOT NULL,
    PRIMARY KEY (taxonomy_version, taxonomy_type)
);

CREATE TABLE review_taxonomy_item (
    taxonomy_version text NOT NULL,
    taxonomy_type text NOT NULL CHECK (taxonomy_type IN ('EXIT_REASON', 'BREACH_TYPE', 'EMOTION')),
    item_id text NOT NULL,
    label_vi text NOT NULL,
    item_order integer NOT NULL CHECK (item_order > 0),
    PRIMARY KEY (taxonomy_version, taxonomy_type, item_id),
    FOREIGN KEY (taxonomy_version, taxonomy_type) REFERENCES review_taxonomy_version (taxonomy_version, taxonomy_type)
);

CREATE TABLE review_taxonomy_publish_event (
    taxonomy_publish_event_id text PRIMARY KEY,
    taxonomy_type text NOT NULL CHECK (taxonomy_type IN ('EXIT_REASON', 'BREACH_TYPE', 'EMOTION')),
    event_sequence integer NOT NULL CHECK (event_sequence > 0),
    schema_version text NOT NULL CHECK (schema_version = 'review_taxonomy_publish_event_v1'),
    taxonomy_version text NOT NULL,
    recorded_at text NOT NULL,
    content_sha256 text NOT NULL,
    UNIQUE (taxonomy_type, event_sequence)
);

CREATE TABLE attachment (
    attachment_id text PRIMARY KEY,
    workspace_id text NOT NULL,
    source_upload_id text NOT NULL,
    schema_version text NOT NULL CHECK (schema_version = 'attachment_v1'),
    attachment_kind text NOT NULL CHECK (attachment_kind = 'SCREENSHOT'),
    state text NOT NULL CHECK (state IN ('ACTIVE', 'DELETED')),
    scan_status text NOT NULL CHECK (scan_status IN ('PASSED', 'FAILED')),
    content_sha256 text NOT NULL,
    size_bytes integer NOT NULL CHECK (size_bytes > 0),
    attachment_content_version_id text NOT NULL,
    attachment_content_version_schema text NOT NULL CHECK (attachment_content_version_schema = 'attachment_content_version_v1'),
    created_at text NOT NULL,
    activated_at text NULL,
    deleted_at text NULL,
    safe_error_code text NULL,
    FOREIGN KEY (source_upload_id) REFERENCES upload (upload_id)
);

CREATE TABLE attachment_state_event (
    attachment_state_event_id text PRIMARY KEY,
    workspace_id text NOT NULL,
    attachment_id text NOT NULL,
    event_sequence integer NOT NULL CHECK (event_sequence > 0),
    event_type text NOT NULL CHECK (event_type IN ('ACTIVATE', 'DELETE', 'REJECT')),
    recorded_at text NOT NULL,
    safe_reason_code text NULL,
    UNIQUE (attachment_id, event_sequence),
    FOREIGN KEY (attachment_id) REFERENCES attachment (attachment_id)
);

CREATE TABLE attachment_tombstone (
    attachment_tombstone_id text PRIMARY KEY,
    workspace_id text NOT NULL,
    attachment_id text NOT NULL,
    source_upload_id text NOT NULL,
    last_known_content_sha256 text NOT NULL,
    deleted_at text NOT NULL,
    absence_verification_id text NOT NULL,
    FOREIGN KEY (attachment_id) REFERENCES attachment (attachment_id),
    FOREIGN KEY (source_upload_id) REFERENCES upload (upload_id),
    FOREIGN KEY (absence_verification_id) REFERENCES upload_object_absence_verification (upload_object_absence_verification_id)
);

CREATE TABLE review (
    review_id text PRIMARY KEY,
    episode_id text NOT NULL,
    workspace_id text NOT NULL,
    schema_version text NOT NULL CHECK (schema_version = 'review_v1'),
    state text NOT NULL CHECK (state = 'COMPLETED'),
    created_at text NOT NULL,
    completed_at text NOT NULL,
    UNIQUE (workspace_id, episode_id),
    FOREIGN KEY (episode_id) REFERENCES trade_episode (episode_id)
);

CREATE TABLE review_revision (
    review_revision_id text PRIMARY KEY,
    workspace_id text NOT NULL,
    review_id text NOT NULL,
    revision_no integer NOT NULL CHECK (revision_no > 0),
    schema_version text NOT NULL CHECK (schema_version = 'review_revision_v1'),
    episode_projection_version integer NOT NULL CHECK (episode_projection_version > 0),
    recorded_at text NOT NULL,
    recorded_by_user_id text NOT NULL,
    idempotency_key text NOT NULL,
    exit_reason text NOT NULL,
    exit_reason_taxonomy_version text NOT NULL CHECK (exit_reason_taxonomy_version = 'exit_reason_v1'),
    exit_reason_other_text text NULL,
    rule_breach integer NOT NULL CHECK (rule_breach IN (0, 1)),
    breach_taxonomy_version text NOT NULL CHECK (breach_taxonomy_version = 'breach_type_v1'),
    breach_type_ids_json text NOT NULL,
    breach_other_text text NULL,
    stop_moved_away integer NOT NULL CHECK (stop_moved_away IN (0, 1)),
    risk_exceeded integer NOT NULL CHECK (risk_exceeded IN (0, 1)),
    required_checklist_results_json text NOT NULL,
    emotion text NULL,
    emotion_taxonomy_version text NULL CHECK (emotion_taxonomy_version IS NULL OR emotion_taxonomy_version = 'emotion_v1'),
    lesson text NULL,
    content_sha256 text NOT NULL,
    UNIQUE (workspace_id, idempotency_key),
    UNIQUE (review_id, revision_no),
    FOREIGN KEY (review_id) REFERENCES review (review_id)
);

CREATE TABLE review_revision_attachment (
    review_revision_id text NOT NULL,
    workspace_id text NOT NULL,
    attachment_id text NOT NULL,
    schema_version text NOT NULL CHECK (schema_version = 'review_revision_attachment_v1'),
    role text NOT NULL CHECK (role = 'SCREENSHOT'),
    ordinal integer NOT NULL CHECK (ordinal > 0),
    attachment_content_sha256 text NOT NULL,
    attachment_content_version_id text NOT NULL,
    created_at text NOT NULL,
    PRIMARY KEY (review_revision_id, attachment_id, role, ordinal),
    FOREIGN KEY (review_revision_id) REFERENCES review_revision (review_revision_id),
    FOREIGN KEY (attachment_id) REFERENCES attachment (attachment_id)
);

CREATE TABLE metric_snapshot (
    metric_snapshot_id text PRIMARY KEY,
    workspace_id text NOT NULL,
    weekly_cohort_id text NOT NULL,
    weekly_cohort_input_revision_id text NOT NULL,
    metric_snapshot_schema_version text NOT NULL CHECK (metric_snapshot_schema_version = 'metric_snapshot_v1'),
    weekly_lab_schema_version text NOT NULL CHECK (weekly_lab_schema_version = 'weekly_lab_v1'),
    metric_id text NOT NULL,
    metric_formula_version text NOT NULL,
    metric_algorithm_version text NOT NULL CHECK (metric_algorithm_version = 'metrics_v1'),
    metric_decimal_version text NOT NULL CHECK (metric_decimal_version = 'metrics_decimal_v1'),
    eligibility_policy_id text NOT NULL,
    dependency_version_tuple_hash text NOT NULL,
    reporting_start_at_utc text NOT NULL,
    reporting_end_at_utc text NOT NULL,
    reporting_as_of_at text NOT NULL,
    dimension_json text NOT NULL,
    phase text NULL CHECK (phase IS NULL OR phase IN ('ENTRY', 'EXIT')),
    timeframe text NULL CHECK (timeframe IS NULL OR timeframe IN ('1m', '5m')),
    value_type text NOT NULL CHECK (value_type IN ('DECIMAL', 'INTEGER', 'DURATION_MS', 'INTERVAL', 'OBJECT')),
    value_decimal text NULL,
    value_integer integer NULL,
    value_duration_ms integer NULL,
    value_interval_json text NULL,
    value_object_json text NULL,
    unit text NOT NULL,
    numerator_decimal text NULL,
    denominator_decimal text NULL,
    null_reason text NULL,
    display_state text NOT NULL CHECK (display_state IN ('NORMAL', 'UNDEFINED')),
    computation_status text NOT NULL CHECK (computation_status IN ('COMPLETE', 'PARTIAL')),
    candidate_episode_count integer NOT NULL CHECK (candidate_episode_count >= 0),
    eligible_episode_count integer NOT NULL CHECK (eligible_episode_count >= 0),
    excluded_episode_count integer NOT NULL CHECK (excluded_episode_count >= 0),
    candidate_episode_refs_json text NOT NULL,
    included_episode_refs_json text NOT NULL,
    excluded_episode_refs_json text NOT NULL,
    exclusion_reason_counts_json text NOT NULL,
    source_review_revision_ids_json text NOT NULL,
    source_context_snapshot_ids_json text NOT NULL,
    evidence_label text NOT NULL CHECK (evidence_label IN ('INSUFFICIENT', 'EXPLORATORY', 'ESTIMATED')),
    input_digest_sha256 text NOT NULL,
    computed_at text NOT NULL,
    supersedes_metric_snapshot_id text NULL,
    UNIQUE (workspace_id, metric_id, metric_formula_version, reporting_start_at_utc, reporting_end_at_utc, dimension_json, phase, timeframe, input_digest_sha256)
);
