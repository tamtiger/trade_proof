-- Phase 1 foundation contract for PostgreSQL 17.
-- The local harness is in-memory, but these table names and constraints are the v1 persistence target.

CREATE TABLE tp_user (
    user_id text PRIMARY KEY,
    created_at timestamptz NOT NULL
);

CREATE TABLE user_identity (
    identity_id text PRIMARY KEY,
    user_id text NOT NULL UNIQUE REFERENCES tp_user(user_id),
    issuer text NOT NULL,
    subject text NOT NULL,
    provider_mode text NOT NULL CHECK (provider_mode IN ('MANAGED_DEDICATED', 'SHARED_FEDERATED')),
    identity_provider_registration_id text NOT NULL,
    identity_generation integer NOT NULL CHECK (identity_generation > 0),
    created_at timestamptz NOT NULL,
    UNIQUE (issuer, subject)
);

CREATE TABLE workspace (
    workspace_id text PRIMARY KEY,
    owner_user_id text NOT NULL UNIQUE REFERENCES tp_user(user_id),
    lifecycle_state text NOT NULL CHECK (lifecycle_state IN ('ACTIVE', 'DELETING')),
    deletion_guard_generation integer NOT NULL CHECK (deletion_guard_generation > 0),
    timezone text NOT NULL,
    created_at timestamptz NOT NULL
);

CREATE TABLE trading_account (
    workspace_id text NOT NULL REFERENCES workspace(workspace_id),
    trading_account_id text NOT NULL,
    venue text NOT NULL CHECK (venue = 'BINANCE'),
    product_type text NOT NULL CHECK (product_type = 'SPOT'),
    reporting_currency text NOT NULL CHECK (reporting_currency = 'USDT'),
    display_name text NOT NULL,
    created_at timestamptz NOT NULL,
    PRIMARY KEY (workspace_id, trading_account_id),
    UNIQUE (workspace_id)
);

CREATE TABLE audit_event (
    audit_event_id text PRIMARY KEY,
    branch text NOT NULL CHECK (branch IN ('PRE_AUTH', 'POST_AUTH')),
    event_type text NOT NULL,
    workspace_id text NULL,
    actor_user_id text NULL,
    safe_code text NOT NULL,
    recorded_at timestamptz NOT NULL,
    CHECK (
        (branch = 'PRE_AUTH' AND workspace_id IS NULL AND actor_user_id IS NULL) OR
        (branch = 'POST_AUTH' AND workspace_id IS NOT NULL AND actor_user_id IS NOT NULL)
    )
);

CREATE TABLE idempotency_receipt (
    workspace_id text NOT NULL REFERENCES workspace(workspace_id),
    command_type text NOT NULL,
    idempotency_key text NOT NULL,
    request_sha256 text NOT NULL,
    response_json jsonb NOT NULL,
    recorded_at timestamptz NOT NULL,
    PRIMARY KEY (workspace_id, command_type, idempotency_key)
);

CREATE TABLE setup_preset_revision (
    workspace_id text NOT NULL REFERENCES workspace(workspace_id),
    setup_preset_id text NOT NULL,
    revision_id text NOT NULL,
    revision_no integer NOT NULL CHECK (revision_no > 0),
    schema_version text NOT NULL CHECK (schema_version = 'setup_preset_v1'),
    label text NOT NULL,
    label_key text NOT NULL,
    checklist_schema_version text NOT NULL CHECK (checklist_schema_version = 'plan_checklist_v1'),
    checklist_json jsonb NOT NULL,
    is_system boolean NOT NULL,
    is_active boolean NOT NULL,
    recorded_at timestamptz NOT NULL,
    PRIMARY KEY (workspace_id, revision_id),
    UNIQUE (workspace_id, setup_preset_id, revision_no)
);

CREATE UNIQUE INDEX ux_setup_preset_active_label_key
ON setup_preset_revision(workspace_id, label_key)
WHERE is_active;

CREATE TABLE trade_plan (
    workspace_id text NOT NULL,
    trading_account_id text NOT NULL,
    trade_plan_id text NOT NULL,
    symbol text NOT NULL,
    state text NOT NULL CHECK (state IN ('ARMED', 'CANCELLED', 'EXPIRED', 'CONSUMED')),
    created_at timestamptz NOT NULL,
    expires_at timestamptz NOT NULL,
    PRIMARY KEY (workspace_id, trade_plan_id),
    FOREIGN KEY (workspace_id, trading_account_id)
        REFERENCES trading_account(workspace_id, trading_account_id)
);

CREATE UNIQUE INDEX ux_trade_plan_one_armed_per_symbol
ON trade_plan(workspace_id, trading_account_id, symbol)
WHERE state = 'ARMED';

CREATE TABLE trade_plan_revision (
    workspace_id text NOT NULL,
    trade_plan_id text NOT NULL,
    trade_plan_revision_id text NOT NULL,
    revision_no integer NOT NULL CHECK (revision_no > 0),
    setup_preset_revision_id text NOT NULL,
    entry_zone_low numeric(38,18) NOT NULL,
    entry_zone_high numeric(38,18) NOT NULL,
    initial_stop numeric(38,18) NOT NULL,
    planned_risk_usdt numeric(16,8) NOT NULL,
    confidence integer NOT NULL CHECK (confidence BETWEEN 1 AND 5),
    thesis text NULL,
    checklist_schema_version text NOT NULL CHECK (checklist_schema_version = 'plan_checklist_v1'),
    checklist_json jsonb NOT NULL,
    submitted_at timestamptz NOT NULL,
    content_sha256 text NOT NULL,
    PRIMARY KEY (workspace_id, trade_plan_revision_id),
    UNIQUE (workspace_id, trade_plan_id, revision_no),
    FOREIGN KEY (workspace_id, trade_plan_id)
        REFERENCES trade_plan(workspace_id, trade_plan_id),
    FOREIGN KEY (workspace_id, setup_preset_revision_id)
        REFERENCES setup_preset_revision(workspace_id, revision_id),
    CHECK (0 < entry_zone_low AND entry_zone_low <= entry_zone_high),
    CHECK (0 < initial_stop AND initial_stop < entry_zone_low),
    CHECK (planned_risk_usdt > 0)
);

CREATE TABLE tenant_control_job (
    workspace_id text NOT NULL REFERENCES workspace(workspace_id),
    tenant_control_job_id text NOT NULL,
    work_sequence bigint NOT NULL CHECK (work_sequence > 0),
    work_type text NOT NULL,
    subject_type text NOT NULL,
    subject_key_json jsonb NOT NULL,
    payload_schema_version text NOT NULL CHECK (payload_schema_version = 'tenant_control_job_payload_v1'),
    payload_digest_profile text NOT NULL,
    payload_sha256 text NOT NULL,
    payload_json jsonb NULL,
    operation_idempotency_key text NOT NULL,
    deletion_guard_generation integer NOT NULL,
    state text NOT NULL,
    compacted boolean NOT NULL DEFAULT false,
    created_at timestamptz NOT NULL,
    PRIMARY KEY (workspace_id, tenant_control_job_id),
    UNIQUE (workspace_id, work_sequence),
    UNIQUE (workspace_id, work_type, operation_idempotency_key)
);

CREATE TABLE tenant_work_item_fence (
    workspace_id text NOT NULL,
    tenant_work_item_fence_id text NOT NULL,
    tenant_control_job_id text NOT NULL,
    work_sequence bigint NOT NULL,
    state text NOT NULL,
    created_at timestamptz NOT NULL,
    PRIMARY KEY (workspace_id, tenant_work_item_fence_id),
    FOREIGN KEY (workspace_id, tenant_control_job_id)
        REFERENCES tenant_control_job(workspace_id, tenant_control_job_id),
    UNIQUE (workspace_id, tenant_control_job_id)
);

CREATE TABLE tenant_work_item_fence_event (
    workspace_id text NOT NULL,
    tenant_work_item_fence_id text NOT NULL,
    event_sequence integer NOT NULL CHECK (event_sequence > 0),
    event_type text NOT NULL,
    recorded_at timestamptz NOT NULL,
    PRIMARY KEY (workspace_id, tenant_work_item_fence_id, event_sequence),
    FOREIGN KEY (workspace_id, tenant_work_item_fence_id)
        REFERENCES tenant_work_item_fence(workspace_id, tenant_work_item_fence_id)
);

CREATE TABLE tenant_external_operation_lease (
    workspace_id text NOT NULL,
    tenant_external_operation_lease_id text NOT NULL,
    tenant_control_job_id text NOT NULL,
    provider_lookup_key text NOT NULL,
    state text NOT NULL,
    started_at timestamptz NOT NULL,
    ended_at timestamptz NULL,
    PRIMARY KEY (workspace_id, tenant_external_operation_lease_id),
    FOREIGN KEY (workspace_id, tenant_control_job_id)
        REFERENCES tenant_control_job(workspace_id, tenant_control_job_id)
);

CREATE TABLE tenant_work_item_terminal_marker (
    workspace_id text NOT NULL,
    tenant_work_item_terminal_marker_id text NOT NULL,
    tenant_control_job_id text NOT NULL,
    work_sequence bigint NOT NULL,
    work_type text NOT NULL,
    operation_payload_schema_version text NOT NULL CHECK (operation_payload_schema_version = 'tenant_control_job_payload_v1'),
    terminal_marker_digest_profile text NOT NULL CHECK (terminal_marker_digest_profile = 'tenant_work_item_terminal_marker_v1'),
    payload_digest_profile text NOT NULL,
    payload_sha256 text NOT NULL,
    result_code text NOT NULL,
    terminal_at timestamptz NOT NULL,
    PRIMARY KEY (workspace_id, tenant_work_item_terminal_marker_id),
    FOREIGN KEY (workspace_id, tenant_control_job_id)
        REFERENCES tenant_control_job(workspace_id, tenant_control_job_id),
    UNIQUE (workspace_id, work_sequence),
    UNIQUE (workspace_id, tenant_control_job_id)
);

CREATE TABLE product_measurement_run (
    workspace_id text NOT NULL REFERENCES workspace(workspace_id),
    measurement_run_id text NOT NULL,
    feature text NOT NULL CHECK (feature IN ('ONBOARDING', 'QUICK_PLAN', 'QUICK_REVIEW', 'FIRST_INSIGHT')),
    mode text NOT NULL CHECK (mode IN ('PRACTICE', 'MEASURED')),
    practice_index integer NULL,
    state text NOT NULL CHECK (state IN ('OPEN', 'SUCCEEDED', 'ABANDONED')),
    started_at timestamptz NOT NULL,
    deadline_at timestamptz NOT NULL,
    terminal_at timestamptz NULL,
    abandon_reason text NULL,
    timeout_tenant_control_job_id text NOT NULL,
    schema_version text NOT NULL CHECK (schema_version = 'product_measurement_run_v1'),
    PRIMARY KEY (workspace_id, measurement_run_id),
    CHECK (deadline_at = started_at + interval '30 minutes'),
    FOREIGN KEY (workspace_id, timeout_tenant_control_job_id)
        REFERENCES tenant_control_job(workspace_id, tenant_control_job_id)
);
