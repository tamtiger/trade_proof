-- Phase 6 Weekly Lab, product analytics, export and workspace deletion contract.
-- The local harness keeps archives and external analytics as deterministic records only.

CREATE TABLE weekly_cohort (
    weekly_cohort_id text PRIMARY KEY,
    workspace_id text NOT NULL,
    weekly_lab_schema_version text NOT NULL CHECK (weekly_lab_schema_version = 'weekly_lab_v1'),
    cohort_type text NOT NULL CHECK (cohort_type IN ('REGULAR', 'TRANSITION')),
    state text NOT NULL CHECK (state IN ('OPEN', 'LOCKED', 'SUPERSEDED')),
    workspace_timezone text NOT NULL,
    cohort_start_local text NOT NULL,
    cohort_end_local_exclusive text NOT NULL,
    reporting_start_at_utc text NOT NULL,
    reporting_end_at_utc text NOT NULL,
    locked_at text NOT NULL,
    previous_weekly_cohort_id text NULL
);

CREATE TABLE weekly_cohort_input_revision (
    weekly_cohort_input_revision_id text PRIMARY KEY,
    weekly_cohort_id text NOT NULL,
    workspace_id text NOT NULL,
    revision_no integer NOT NULL CHECK (revision_no > 0),
    weekly_lab_schema_version text NOT NULL CHECK (weekly_lab_schema_version = 'weekly_lab_v1'),
    reason text NOT NULL CHECK (reason IN ('INITIAL_LOCK', 'REVIEW_CORRECTION', 'ACCOUNTING_CORRECTION', 'CONTEXT_RECOVERY', 'DATA_BACKFILL')),
    idempotency_key text NOT NULL,
    reporting_as_of_at text NOT NULL,
    dependency_version_tuple_json text NOT NULL,
    dependency_version_tuple_hash text NOT NULL,
    episode_projection_refs_json text NOT NULL,
    review_revision_refs_json text NOT NULL,
    context_ref_matrix_json text NOT NULL,
    metric_snapshot_refs_json text NOT NULL,
    input_digest_sha256 text NOT NULL,
    UNIQUE (weekly_cohort_id, revision_no),
    FOREIGN KEY (weekly_cohort_id) REFERENCES weekly_cohort (weekly_cohort_id)
);

CREATE TABLE weekly_report_revision (
    weekly_report_revision_id text PRIMARY KEY,
    weekly_report_id text NOT NULL,
    workspace_id text NOT NULL,
    weekly_cohort_id text NOT NULL,
    weekly_cohort_input_revision_id text NOT NULL,
    revision_no integer NOT NULL CHECK (revision_no > 0),
    status text NOT NULL CHECK (status IN ('PUBLISHED', 'SUPERSEDED')),
    weekly_lab_schema_version text NOT NULL CHECK (weekly_lab_schema_version = 'weekly_lab_v1'),
    renderer_id text NOT NULL CHECK (renderer_id = 'weekly_lab_renderer_v1'),
    locale text NOT NULL CHECK (locale = 'vi-VN'),
    metric_snapshot_ids_json text NOT NULL,
    section_payload_json text NOT NULL,
    rendered_sections_json text NOT NULL,
    content_sha256 text NOT NULL,
    published_at text NOT NULL,
    supersedes_report_revision_id text NULL,
    next_weekly_cohort_id text NOT NULL,
    UNIQUE (weekly_report_id, revision_no),
    FOREIGN KEY (weekly_cohort_id) REFERENCES weekly_cohort (weekly_cohort_id),
    FOREIGN KEY (weekly_cohort_input_revision_id) REFERENCES weekly_cohort_input_revision (weekly_cohort_input_revision_id)
);

CREATE TABLE behavioral_experiment_revision (
    behavioral_experiment_revision_id text PRIMARY KEY,
    behavioral_experiment_id text NOT NULL,
    workspace_id text NOT NULL,
    revision_no integer NOT NULL CHECK (revision_no > 0),
    taxonomy_version text NOT NULL CHECK (taxonomy_version = 'behavioral_experiment_v1'),
    experiment_type_id text NOT NULL,
    state text NOT NULL CHECK (state IN ('PROPOSED', 'CONFIRMED', 'CANCELLED')),
    target_weekly_cohort_id text NOT NULL,
    source_weekly_report_revision_id text NOT NULL,
    proposal_text text NOT NULL,
    recorded_at text NOT NULL,
    recorded_by_user_id text NOT NULL,
    idempotency_key text NOT NULL,
    content_sha256 text NOT NULL,
    UNIQUE (behavioral_experiment_id, revision_no),
    FOREIGN KEY (target_weekly_cohort_id) REFERENCES weekly_cohort (weekly_cohort_id),
    FOREIGN KEY (source_weekly_report_revision_id) REFERENCES weekly_report_revision (weekly_report_revision_id)
);

CREATE TABLE weekly_review_completion (
    weekly_review_completion_id text PRIMARY KEY,
    workspace_id text NOT NULL,
    schema_version text NOT NULL CHECK (schema_version = 'weekly_review_completion_v1'),
    weekly_cohort_id text NOT NULL,
    weekly_report_revision_id text NOT NULL,
    behavioral_experiment_revision_id text NOT NULL,
    completed_at text NOT NULL,
    idempotency_key text NOT NULL,
    content_sha256 text NOT NULL,
    FOREIGN KEY (weekly_cohort_id) REFERENCES weekly_cohort (weekly_cohort_id),
    FOREIGN KEY (weekly_report_revision_id) REFERENCES weekly_report_revision (weekly_report_revision_id),
    FOREIGN KEY (behavioral_experiment_revision_id) REFERENCES behavioral_experiment_revision (behavioral_experiment_revision_id)
);

CREATE TABLE workspace_product_metric_snapshot (
    workspace_product_metric_snapshot_id text PRIMARY KEY,
    workspace_id text NOT NULL,
    schema_version text NOT NULL CHECK (schema_version = 'workspace_product_metric_snapshot_v1'),
    metric_dictionary_version text NOT NULL CHECK (metric_dictionary_version = 'product_metrics_v1'),
    metric_id text NOT NULL,
    reporting_start_at_utc text NOT NULL,
    reporting_end_at_utc text NOT NULL,
    reporting_as_of_at text NOT NULL,
    value_type text NOT NULL CHECK (value_type IN ('INTEGER', 'DECIMAL', 'OBJECT')),
    value_integer integer NULL,
    value_decimal text NULL,
    null_reason text NULL,
    source_event_refs_json text NOT NULL,
    input_digest_sha256 text NOT NULL
);

CREATE TABLE internal_aggregate_product_metric_snapshot (
    internal_aggregate_product_metric_snapshot_id text PRIMARY KEY,
    schema_version text NOT NULL CHECK (schema_version = 'internal_aggregate_product_metric_snapshot_v1'),
    metric_dictionary_version text NOT NULL CHECK (metric_dictionary_version = 'product_metrics_v1'),
    metric_id text NOT NULL,
    reporting_start_at_utc text NOT NULL,
    reporting_end_at_utc text NOT NULL,
    reporting_as_of_at text NOT NULL,
    workspace_count integer NOT NULL CHECK (workspace_count >= 0),
    value_type text NOT NULL CHECK (value_type IN ('INTEGER', 'DECIMAL', 'OBJECT')),
    value_integer integer NULL,
    value_decimal text NULL,
    null_reason text NULL CHECK (null_reason IS NULL OR null_reason = 'PRIVACY_THRESHOLD'),
    source_workspace_refs_json text NOT NULL,
    input_digest_sha256 text NOT NULL
);

CREATE TABLE product_analytics_external_projection (
    external_analytics_projection_id text PRIMARY KEY,
    workspace_id text NOT NULL,
    product_analytics_event_id text NOT NULL,
    schema_version text NOT NULL CHECK (schema_version = 'product_analytics_external_v1'),
    state text NOT NULL CHECK (state IN ('PROJECTED', 'PURGED')),
    payload_json text NOT NULL,
    payload_sha256 text NOT NULL,
    projected_at text NOT NULL
);

CREATE TABLE external_analytics_purge (
    external_analytics_purge_id text PRIMARY KEY,
    workspace_id text NOT NULL,
    external_analytics_projection_id text NOT NULL,
    work_type text NOT NULL CHECK (work_type = 'ANALYTICS_PURGE'),
    state text NOT NULL CHECK (state = 'ABSENCE_VERIFIED'),
    absence_digest_sha256 text NOT NULL,
    purged_at text NOT NULL
);

CREATE TABLE tradeproof_export (
    tradeproof_export_id text PRIMARY KEY,
    workspace_id text NOT NULL,
    weekly_report_revision_id text NOT NULL,
    export_schema_version text NOT NULL CHECK (export_schema_version = 'tradeproof_export_v1'),
    export_job_schema_version text NOT NULL CHECK (export_job_schema_version = 'tradeproof_export_job_v1'),
    state text NOT NULL CHECK (state IN ('READY', 'EXPIRED', 'REVOKED_BY_DELETION')),
    service_class text NOT NULL CHECK (service_class IN ('STANDARD', 'OVERSIZE')),
    export_as_of_at text NOT NULL,
    generated_at text NOT NULL,
    expires_at text NOT NULL,
    manifest_json text NOT NULL,
    csv_entries_json text NOT NULL,
    content_sha256 text NOT NULL,
    export_expiry_tenant_control_job_id text NOT NULL,
    FOREIGN KEY (weekly_report_revision_id) REFERENCES weekly_report_revision (weekly_report_revision_id)
);

CREATE TABLE export_round_trip_validation (
    export_round_trip_validation_id text PRIMARY KEY,
    workspace_id text NOT NULL,
    tradeproof_export_id text NOT NULL,
    reader_profile_version text NOT NULL CHECK (reader_profile_version = 'tradeproof_export_round_trip_v1'),
    passed integer NOT NULL CHECK (passed IN (0, 1)),
    checked_content_sha256 text NOT NULL,
    validated_at text NOT NULL
);

CREATE TABLE export_expiry (
    export_expiry_record_id text PRIMARY KEY,
    workspace_id text NOT NULL,
    tradeproof_export_id text NOT NULL,
    work_type text NOT NULL CHECK (work_type = 'EXPORT_EXPIRY'),
    state text NOT NULL CHECK (state = 'ABSENCE_VERIFIED'),
    absence_digest_sha256 text NOT NULL,
    expired_at text NOT NULL
);

CREATE TABLE workspace_deletion (
    workspace_deletion_id text PRIMARY KEY,
    workspace_id text NOT NULL,
    schema_version text NOT NULL CHECK (schema_version = 'workspace_deletion_v1'),
    state text NOT NULL CHECK (state IN ('FENCED', 'DELETED')),
    guard_generation integer NOT NULL CHECK (guard_generation > 0),
    requested_at text NOT NULL,
    completed_at text NULL,
    requested_by_user_id text NOT NULL,
    content_sha256 text NOT NULL
);

CREATE TABLE workspace_deletion_target (
    workspace_deletion_target_id text PRIMARY KEY,
    workspace_deletion_id text NOT NULL,
    workspace_id text NOT NULL,
    ordinal integer NOT NULL CHECK (ordinal > 0),
    target_type text NOT NULL CHECK (target_type IN ('PRIMARY_TENANT_DATA', 'EXPORT_ARCHIVES', 'TEMPORARY_OBJECTS', 'EXTERNAL_ANALYTICS')),
    state text NOT NULL CHECK (state IN ('FENCED', 'ABSENCE_VERIFIED')),
    updated_at text NOT NULL,
    evidence_sha256 text NOT NULL,
    FOREIGN KEY (workspace_deletion_id) REFERENCES workspace_deletion (workspace_deletion_id)
);

CREATE TABLE workspace_deletion_tombstone (
    workspace_deletion_tombstone_id text PRIMARY KEY,
    workspace_deletion_id text NOT NULL,
    workspace_id text NOT NULL,
    guard_generation integer NOT NULL CHECK (guard_generation > 0),
    tombstoned_at text NOT NULL,
    evidence_sha256 text NOT NULL,
    FOREIGN KEY (workspace_deletion_id) REFERENCES workspace_deletion (workspace_deletion_id)
);
