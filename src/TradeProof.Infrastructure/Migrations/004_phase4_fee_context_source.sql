-- Phase 4 fee conversion and market context source contract.
-- Local harness stores deterministic public market bars; no signed exchange data is represented here.

ALTER TABLE fee_conversion ADD COLUMN market_bar_ids_json text NULL;
ALTER TABLE fee_conversion ADD COLUMN market_bar_source_observation_ids_json text NULL;
ALTER TABLE fee_conversion ADD COLUMN market_conversion_catalog_version text NULL;
ALTER TABLE fee_conversion ADD COLUMN conversion_path_json text NULL;

CREATE TABLE market_conversion_catalog_version (
    catalog_version text NOT NULL,
    venue_symbol text NOT NULL,
    base_asset text NOT NULL,
    quote_asset text NOT NULL,
    purpose text NOT NULL CHECK (purpose = 'FEE_CONVERSION_ONLY'),
    valid_from text NOT NULL,
    valid_to_exclusive text NULL,
    conversion_supported integer NOT NULL CHECK (conversion_supported IN (0, 1)),
    content_sha256 text NOT NULL,
    published_at text NOT NULL,
    PRIMARY KEY (catalog_version, venue_symbol, valid_from),
    CHECK ((base_asset = 'USDT') <> (quote_asset = 'USDT'))
);

CREATE TABLE market_data_ingestion_batch (
    ingestion_batch_id text PRIMARY KEY,
    source_venue text NOT NULL CHECK (source_venue = 'BINANCE'),
    product_type text NOT NULL CHECK (product_type = 'SPOT'),
    source_base_url text NOT NULL CHECK (source_base_url = 'https://data-api.binance.vision'),
    fetcher_version text NOT NULL CHECK (fetcher_version = 'binance-public-kline-local-v1'),
    started_at text NOT NULL,
    completed_at text NULL,
    status text NOT NULL CHECK (status IN ('RUNNING', 'COMPLETE', 'PARTIAL', 'FAILED'))
);

CREATE TABLE market_data_source_request (
    source_request_id text PRIMARY KEY,
    ingestion_batch_id text NOT NULL,
    retry_attempt integer NOT NULL CHECK (retry_attempt > 0),
    source_base_url text NOT NULL CHECK (source_base_url = 'https://data-api.binance.vision'),
    http_method text NOT NULL CHECK (http_method = 'GET'),
    path text NOT NULL CHECK (path = '/api/v3/klines'),
    symbol text NOT NULL,
    timeframe text NOT NULL CHECK (timeframe IN ('1m', '5m')),
    time_zone integer NOT NULL CHECK (time_zone = 0),
    start_time text NOT NULL,
    end_time text NOT NULL,
    limit_count integer NOT NULL CHECK (limit_count = 1000),
    requested_at text NOT NULL,
    fetched_at text NULL,
    http_status integer NULL,
    response_sha256 text NULL,
    response_row_count integer NULL CHECK (response_row_count IS NULL OR response_row_count >= 0),
    request_metadata_hash text NOT NULL,
    FOREIGN KEY (ingestion_batch_id) REFERENCES market_data_ingestion_batch (ingestion_batch_id)
);

CREATE TABLE market_bar_revision (
    market_bar_revision_id text PRIMARY KEY,
    source_venue text NOT NULL CHECK (source_venue = 'BINANCE'),
    product_type text NOT NULL CHECK (product_type = 'SPOT'),
    symbol text NOT NULL,
    timeframe text NOT NULL CHECK (timeframe IN ('1m', '5m')),
    open_at text NOT NULL,
    bar_end_exclusive text NOT NULL,
    close text NOT NULL,
    volume text NOT NULL,
    content_sha256 text NOT NULL,
    created_at text NOT NULL,
    UNIQUE (source_venue, product_type, symbol, timeframe, open_at, content_sha256)
);

CREATE TABLE market_bar_source_observation (
    source_observation_id text PRIMARY KEY,
    source_request_id text NOT NULL,
    market_bar_revision_id text NOT NULL,
    response_row_index integer NOT NULL CHECK (response_row_index > 0),
    observation_sequence integer NOT NULL CHECK (observation_sequence > 0),
    UNIQUE (market_bar_revision_id, observation_sequence),
    UNIQUE (source_request_id, response_row_index),
    FOREIGN KEY (source_request_id) REFERENCES market_data_source_request (source_request_id),
    FOREIGN KEY (market_bar_revision_id) REFERENCES market_bar_revision (market_bar_revision_id)
);

CREATE TABLE context_algorithm_release (
    context_algorithm_release_id text PRIMARY KEY,
    algorithm_version text NOT NULL CHECK (algorithm_version = 'mce-binance-spot-v1.0.0'),
    parameter_set_id text NOT NULL CHECK (parameter_set_id = 'mce-default-v1'),
    calculation_contract_version text NOT NULL,
    calculation_contract_sha256 text NOT NULL,
    implementation_artifact_sha256 text NOT NULL,
    parameter_payload_sha256 text NOT NULL,
    release_sha256 text NOT NULL,
    registered_at text NOT NULL,
    UNIQUE (algorithm_version, parameter_set_id)
);

CREATE TABLE context_episode_trigger (
    context_episode_trigger_id text PRIMARY KEY,
    workspace_id text NOT NULL,
    episode_id text NOT NULL,
    projection_version integer NOT NULL CHECK (projection_version > 0),
    phase text NOT NULL CHECK (phase IN ('ENTRY', 'EXIT')),
    event_fill_id text NOT NULL,
    source_event_sequence integer NOT NULL CHECK (source_event_sequence > 0),
    content_sha256 text NOT NULL,
    created_at text NOT NULL,
    UNIQUE (workspace_id, episode_id, projection_version, phase),
    FOREIGN KEY (episode_id, projection_version) REFERENCES trade_episode_projection (episode_id, projection_version),
    FOREIGN KEY (event_fill_id) REFERENCES normalized_fill (fill_id)
);

CREATE TABLE manual_context_recompute_request (
    manual_context_recompute_request_id text PRIMARY KEY,
    workspace_id text NOT NULL,
    episode_id text NOT NULL,
    projection_version integer NOT NULL CHECK (projection_version > 0),
    phase text NOT NULL CHECK (phase IN ('ENTRY', 'EXIT')),
    timeframe text NOT NULL CHECK (timeframe IN ('1m', '5m')),
    source_event_sequence integer NOT NULL CHECK (source_event_sequence > 0),
    event_fill_id text NOT NULL,
    algorithm_version text NOT NULL CHECK (algorithm_version = 'mce-binance-spot-v1.0.0'),
    parameter_set_id text NOT NULL CHECK (parameter_set_id = 'mce-default-v1'),
    actor_user_id text NOT NULL,
    idempotency_key text NOT NULL,
    request_sha256 text NOT NULL,
    requested_at text NOT NULL,
    UNIQUE (workspace_id, idempotency_key),
    FOREIGN KEY (episode_id, projection_version) REFERENCES trade_episode_projection (episode_id, projection_version),
    FOREIGN KEY (event_fill_id) REFERENCES normalized_fill (fill_id)
);

CREATE TABLE context_snapshot (
    snapshot_id text PRIMARY KEY,
    workspace_id text NOT NULL,
    episode_id text NOT NULL,
    projection_version integer NOT NULL CHECK (projection_version > 0),
    snapshot_revision_no integer NOT NULL CHECK (snapshot_revision_no > 0),
    phase text NOT NULL CHECK (phase IN ('ENTRY', 'EXIT')),
    event_fill_id text NOT NULL,
    event_sequence integer NOT NULL CHECK (event_sequence > 0),
    event_at text NOT NULL,
    event_time_end_exclusive text NOT NULL,
    event_timestamp_precision text NOT NULL CHECK (event_timestamp_precision IN ('SECOND', 'MILLISECOND')),
    reference_price text NULL,
    venue text NOT NULL CHECK (venue = 'BINANCE'),
    product_type text NOT NULL CHECK (product_type = 'SPOT'),
    symbol text NOT NULL,
    timeframe text NOT NULL CHECK (timeframe IN ('1m', '5m')),
    timezone text NOT NULL CHECK (timezone = 'UTC'),
    as_of_at text NOT NULL,
    cutoff_at text NOT NULL,
    target_bar_open_at text NULL,
    quality text NOT NULL CHECK (quality IN ('COMPLETE', 'PARTIAL', 'UNRELIABLE')),
    quality_reasons_json text NOT NULL,
    missing_intervals_json text NOT NULL,
    aggregation_eligible integer NOT NULL CHECK (aggregation_eligible IN (0, 1)),
    algorithm_version text NOT NULL CHECK (algorithm_version = 'mce-binance-spot-v1.0.0'),
    parameter_set_id text NOT NULL CHECK (parameter_set_id = 'mce-default-v1'),
    selector_algorithm_version text NOT NULL CHECK (selector_algorithm_version = 'market_bar_as_of_v1'),
    input_bar_revision_ids_json text NOT NULL,
    input_bar_source_observation_ids_json text NOT NULL,
    input_bar_resolution_ids_json text NOT NULL,
    source_request_ids_json text NOT NULL,
    source_ingestion_batch_ids_json text NOT NULL,
    input_hash text NOT NULL,
    provenance_hash text NOT NULL,
    computed_at text NOT NULL,
    supersedes_snapshot_id text NULL,
    recompute_reason text NULL,
    UNIQUE (workspace_id, episode_id, projection_version, phase, timeframe, algorithm_version, parameter_set_id, snapshot_revision_no),
    UNIQUE (workspace_id, episode_id, projection_version, phase, timeframe, algorithm_version, parameter_set_id, input_hash),
    FOREIGN KEY (episode_id, projection_version) REFERENCES trade_episode_projection (episode_id, projection_version),
    FOREIGN KEY (event_fill_id) REFERENCES normalized_fill (fill_id)
);
