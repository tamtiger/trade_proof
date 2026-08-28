-- Phase 2 secure ingestion contract.
-- Local harness uses in-memory storage; this migration freezes the intended relational surface.

CREATE TABLE object_ingest_reservation (
    object_ingest_reservation_id text PRIMARY KEY,
    workspace_id text NOT NULL,
    trading_account_id text NOT NULL,
    purpose text NOT NULL CHECK (purpose IN ('RAW_UPLOAD', 'SANITIZED_ATTACHMENT')),
    reserved_upload_id text NOT NULL,
    reserved_attachment_id text NULL,
    expected_upload_kind text NOT NULL CHECK (expected_upload_kind IN ('CSV', 'SCREENSHOT', 'VOICE')),
    adapter_contract_version text NOT NULL CHECK (adapter_contract_version = 'binance_spot_trade_history_csv_v1'),
    lease_generation integer NOT NULL CHECK (lease_generation = 1),
    provider_object_key_sha256 text NOT NULL,
    write_capability_id text NOT NULL,
    state text NOT NULL CHECK (state IN ('RESERVED', 'BYTES_RECORDED', 'TRANSFERRED', 'ABORT_VERIFIED')),
    created_at text NOT NULL,
    write_expires_at text NOT NULL,
    absence_due_at text NOT NULL,
    write_capability_consumed_at text NULL,
    transferred_at text NULL,
    finalize_tenant_control_job_id text NOT NULL,
    UNIQUE (workspace_id, write_capability_id),
    UNIQUE (workspace_id, reserved_upload_id),
    FOREIGN KEY (workspace_id, trading_account_id) REFERENCES trading_account (workspace_id, trading_account_id),
    FOREIGN KEY (finalize_tenant_control_job_id) REFERENCES tenant_control_job (tenant_control_job_id)
);

CREATE TABLE object_ingest_reservation_event (
    object_ingest_reservation_event_id text PRIMARY KEY,
    object_ingest_reservation_id text NOT NULL,
    workspace_id text NOT NULL,
    event_sequence integer NOT NULL CHECK (event_sequence > 0),
    event_type text NOT NULL CHECK (event_type IN ('RESERVE', 'RECORD_BYTES', 'TRANSFER', 'ABORT_DELETE', 'ABORT_VERIFY')),
    recorded_at text NOT NULL,
    safe_reason_code text NULL,
    UNIQUE (workspace_id, object_ingest_reservation_id, event_sequence),
    FOREIGN KEY (object_ingest_reservation_id) REFERENCES object_ingest_reservation (object_ingest_reservation_id)
);

CREATE TABLE upload (
    upload_id text PRIMARY KEY,
    workspace_id text NOT NULL,
    trading_account_id text NOT NULL,
    contract_version text NOT NULL CHECK (contract_version = 'upload_attachment_v1'),
    kind text NOT NULL CHECK (kind IN ('CSV', 'SCREENSHOT', 'VOICE')),
    adapter_contract_version text NOT NULL CHECK (adapter_contract_version = 'binance_spot_trade_history_csv_v1'),
    state text NOT NULL CHECK (state IN ('QUARANTINED', 'VALIDATING', 'ACCEPTED', 'REJECTED', 'PURGED')),
    file_sha256 text NOT NULL,
    file_size_bytes integer NOT NULL CHECK (file_size_bytes >= 0),
    created_at text NOT NULL,
    forced_purge_at text NOT NULL,
    purge_due_at text NOT NULL,
    source_object_ingest_reservation_id text NOT NULL,
    lease_generation integer NOT NULL CHECK (lease_generation = 1),
    validate_tenant_control_job_id text NOT NULL,
    purge_tenant_control_job_id text NOT NULL,
    safe_error_code text NULL,
    accepted_at text NULL,
    purged_at text NULL,
    UNIQUE (workspace_id, upload_id),
    FOREIGN KEY (workspace_id, trading_account_id) REFERENCES trading_account (workspace_id, trading_account_id),
    FOREIGN KEY (source_object_ingest_reservation_id) REFERENCES object_ingest_reservation (object_ingest_reservation_id),
    FOREIGN KEY (validate_tenant_control_job_id) REFERENCES tenant_control_job (tenant_control_job_id),
    FOREIGN KEY (purge_tenant_control_job_id) REFERENCES tenant_control_job (tenant_control_job_id)
);

CREATE TABLE upload_state_event (
    upload_state_event_id text PRIMARY KEY,
    workspace_id text NOT NULL,
    upload_id text NOT NULL,
    contract_version text NOT NULL CHECK (contract_version = 'upload_attachment_v1'),
    event_sequence integer NOT NULL CHECK (event_sequence > 0),
    event_type text NOT NULL CHECK (event_type IN ('RECEIVE', 'START_VALIDATION', 'ACCEPT', 'REJECT', 'PURGE')),
    recorded_at text NOT NULL,
    actor_type text NOT NULL CHECK (actor_type IN ('USER', 'SYSTEM')),
    actor_user_id text NULL,
    idempotency_key text NOT NULL,
    safe_reason_code text NULL,
    object_absence_verification_id text NULL,
    UNIQUE (workspace_id, upload_id, event_sequence),
    FOREIGN KEY (upload_id) REFERENCES upload (upload_id)
);

CREATE TABLE upload_object_lease (
    upload_object_lease_id text PRIMARY KEY,
    workspace_id text NOT NULL,
    upload_id text NOT NULL,
    lease_generation integer NOT NULL CHECK (lease_generation = 1),
    provider_object_version_id text NOT NULL,
    state text NOT NULL CHECK (state IN ('ACTIVE', 'ABSENCE_VERIFIED')),
    created_at text NOT NULL,
    terminal_at text NULL,
    UNIQUE (workspace_id, upload_id, lease_generation),
    FOREIGN KEY (upload_id) REFERENCES upload (upload_id)
);

CREATE TABLE upload_object_absence_verification (
    upload_object_absence_verification_id text PRIMARY KEY,
    workspace_id text NOT NULL,
    upload_id text NOT NULL,
    lease_generation integer NOT NULL CHECK (lease_generation = 1),
    last_known_sha256 text NOT NULL,
    verified_absent_at text NOT NULL,
    UNIQUE (workspace_id, upload_id, lease_generation),
    FOREIGN KEY (upload_id) REFERENCES upload (upload_id)
);

CREATE TABLE import_preview (
    import_preview_id text PRIMARY KEY,
    workspace_id text NOT NULL,
    upload_id text NOT NULL,
    trading_account_id text NOT NULL,
    schema_version text NOT NULL CHECK (schema_version = 'import_preview_v1'),
    adapter_contract_version text NOT NULL CHECK (adapter_contract_version = 'binance_spot_trade_history_csv_v1'),
    state text NOT NULL CHECK (state IN ('READY', 'CONFIRMED', 'EXPIRED', 'ABANDONED')),
    data_rows integer NOT NULL CHECK (data_rows >= 0 AND data_rows <= 100000),
    symbols_json text NOT NULL,
    first_trade_at text NULL,
    last_trade_at text NULL,
    preview_summary_sha256 text NOT NULL,
    created_at text NOT NULL,
    expires_at text NOT NULL,
    confirmed_at text NULL,
    confirmed_import_batch_id text NULL,
    safe_errors_json text NOT NULL,
    UNIQUE (workspace_id, upload_id),
    FOREIGN KEY (upload_id) REFERENCES upload (upload_id),
    FOREIGN KEY (workspace_id, trading_account_id) REFERENCES trading_account (workspace_id, trading_account_id)
);

CREATE TABLE import_batch (
    import_batch_id text PRIMARY KEY,
    workspace_id text NOT NULL,
    trading_account_id text NOT NULL,
    source_upload_id text NOT NULL,
    source_import_preview_id text NOT NULL,
    source_preview_schema_version text NOT NULL CHECK (source_preview_schema_version = 'import_preview_v1'),
    source_preview_summary_sha256 text NOT NULL,
    adapter_contract_version text NOT NULL CHECK (adapter_contract_version = 'binance_spot_trade_history_csv_v1'),
    confirmed_at text NOT NULL,
    status text NOT NULL CHECK (status IN ('UPLOADED', 'PROCESSING', 'COMPLETE', 'PARTIAL', 'NEEDS_ATTENTION', 'REJECTED')),
    data_rows integer NULL,
    reconciled_rows integer NOT NULL DEFAULT 0,
    duplicate_rows integer NOT NULL DEFAULT 0,
    accounting_pending_rows integer NOT NULL DEFAULT 0,
    quarantined_rows integer NOT NULL DEFAULT 0,
    file_error_code text NULL,
    import_tenant_control_job_id text NOT NULL,
    UNIQUE (workspace_id, source_import_preview_id),
    FOREIGN KEY (source_upload_id) REFERENCES upload (upload_id),
    FOREIGN KEY (source_import_preview_id) REFERENCES import_preview (import_preview_id),
    FOREIGN KEY (workspace_id, trading_account_id) REFERENCES trading_account (workspace_id, trading_account_id),
    FOREIGN KEY (import_tenant_control_job_id) REFERENCES tenant_control_job (tenant_control_job_id)
);

CREATE TABLE staged_fill (
    staged_fill_id text PRIMARY KEY,
    workspace_id text NOT NULL,
    trading_account_id text NOT NULL,
    import_batch_id text NOT NULL,
    source_row_number integer NOT NULL CHECK (source_row_number > 0),
    staged_fill_schema_version text NOT NULL CHECK (staged_fill_schema_version = 'staged_fill_v1'),
    instrument_catalog_version text NOT NULL,
    venue text NOT NULL CHECK (venue = 'BINANCE'),
    product_type text NOT NULL CHECK (product_type = 'SPOT'),
    venue_symbol text NOT NULL,
    side text NOT NULL CHECK (side IN ('BUY', 'SELL')),
    executed_at text NOT NULL,
    price_quote_per_base text NOT NULL,
    executed_qty_base text NOT NULL,
    gross_amount_quote text NOT NULL,
    fee_qty text NOT NULL,
    fee_asset text NOT NULL,
    source_row_fingerprint_sha256 text NOT NULL,
    canonical_signature_sha256 text NOT NULL,
    created_at text NOT NULL,
    UNIQUE (workspace_id, import_batch_id, source_row_number, staged_fill_id),
    FOREIGN KEY (import_batch_id) REFERENCES import_batch (import_batch_id),
    FOREIGN KEY (workspace_id, trading_account_id) REFERENCES trading_account (workspace_id, trading_account_id)
);

CREATE TABLE staged_fill_disposition (
    staged_fill_disposition_id text PRIMARY KEY,
    workspace_id text NOT NULL,
    staged_fill_id text NOT NULL,
    outcome text NOT NULL CHECK (outcome IN ('ADMITTED_AS_NEW', 'DISCARDED_AS_DUPLICATE')),
    normalized_fill_id text NULL,
    duplicate_of_fill_id text NULL,
    recorded_at text NOT NULL,
    UNIQUE (workspace_id, staged_fill_id),
    FOREIGN KEY (staged_fill_id) REFERENCES staged_fill (staged_fill_id)
);
