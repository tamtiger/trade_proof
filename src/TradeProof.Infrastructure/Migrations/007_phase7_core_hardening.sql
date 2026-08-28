CREATE TABLE ai_disabled_feature_profile (
    ai_disabled_feature_profile_id text PRIMARY KEY,
    workspace_id text NOT NULL,
    schema_version text NOT NULL CHECK (schema_version = 'ai_disabled_profile_v1'),
    voice_transcription_enabled boolean NOT NULL CHECK (voice_transcription_enabled = false),
    ai_taxonomy_enabled boolean NOT NULL CHECK (ai_taxonomy_enabled = false),
    ai_weekly_summary_enabled boolean NOT NULL CHECK (ai_weekly_summary_enabled = false),
    ai_processor_configured boolean NOT NULL CHECK (ai_processor_configured = false),
    outbound_ai_route_configured boolean NOT NULL CHECK (outbound_ai_route_configured = false),
    ai_run_work_registered boolean NOT NULL CHECK (ai_run_work_registered = false),
    ai_cancel_work_registered boolean NOT NULL CHECK (ai_cancel_work_registered = false),
    ai_output_delete_work_registered boolean NOT NULL CHECK (ai_output_delete_work_registered = false),
    disabled_gate_id text NOT NULL CHECK (disabled_gate_id = 'TP-SEC:AI-00'),
    processor_state text NOT NULL CHECK (processor_state = 'DISABLED_NO_PROCESSOR'),
    recorded_at timestamptz NOT NULL,
    FOREIGN KEY (workspace_id) REFERENCES workspace (workspace_id)
);

CREATE TABLE release_hardening_evidence (
    release_hardening_evidence_id text PRIMARY KEY,
    workspace_id text NOT NULL,
    schema_version text NOT NULL CHECK (schema_version = 'release_hardening_evidence_v1'),
    gate_profile text NOT NULL CHECK (gate_profile = 'CORE_RELEASE_AI_DISABLED'),
    p0_defect_count integer NOT NULL CHECK (p0_defect_count = 0),
    p1_defect_count integer NOT NULL CHECK (p1_defect_count = 0),
    security_smoke_state text NOT NULL CHECK (security_smoke_state = 'PASS'),
    accessibility_smoke_state text NOT NULL CHECK (accessibility_smoke_state = 'PASS'),
    performance_smoke_state text NOT NULL CHECK (performance_smoke_state = 'PASS'),
    reliability_smoke_state text NOT NULL CHECK (reliability_smoke_state = 'PASS'),
    ai_dependency_state text NOT NULL CHECK (ai_dependency_state = 'BLOCKED_AND_CORE_CONTINUES'),
    core_flow_state text NOT NULL CHECK (core_flow_state = 'COMPLETE'),
    evidence_summary_json text NOT NULL,
    core_hardening_tenant_control_job_id text NOT NULL,
    recorded_at timestamptz NOT NULL,
    FOREIGN KEY (workspace_id) REFERENCES workspace (workspace_id)
);

CREATE TABLE core_release_readiness_report (
    core_release_readiness_report_id text PRIMARY KEY,
    workspace_id text NOT NULL,
    schema_version text NOT NULL CHECK (schema_version = 'core_release_readiness_v1'),
    state text NOT NULL CHECK (state = 'READY_WITH_AI_DISABLED'),
    feature_flags_json text NOT NULL,
    blocked_dependency_results_json text NOT NULL,
    p0_defect_count integer NOT NULL CHECK (p0_defect_count = 0),
    p1_defect_count integer NOT NULL CHECK (p1_defect_count = 0),
    ai_disabled_feature_profile_id text NOT NULL,
    release_hardening_evidence_id text NOT NULL,
    release_readiness_tenant_control_job_id text NOT NULL,
    published_at timestamptz NOT NULL,
    FOREIGN KEY (workspace_id) REFERENCES workspace (workspace_id),
    FOREIGN KEY (ai_disabled_feature_profile_id) REFERENCES ai_disabled_feature_profile (ai_disabled_feature_profile_id),
    FOREIGN KEY (release_hardening_evidence_id) REFERENCES release_hardening_evidence (release_hardening_evidence_id)
);
