# Weekly Lab Contract

- **Document ID:** `TP-LAB`
- **Version:** 1.0.0
- **Status:** Implementation baseline
- **Updated:** 2026-08-27
- **Scope:** Weekly cohort, deterministic MetricSnapshot, WeeklyReport, behavioral experiment, report export projection and supporting product metrics

## 1. Purpose and normative ownership

This document is the authoritative contract for turning immutable accounting and market-context artifacts into a deterministic Weekly Lab. It owns cohort scheduling, report inputs, report revisions, presentation recipes, behavioral experiments and report-specific export records.

The words **MUST**, **MUST NOT**, **SHOULD** and **MAY** are normative.

The exact identifiers owned by this document are:

| Domain | Exact identifier | Persisted on |
|---|---|---|
| Weekly Lab schema and recipe | `weekly_lab_v1` | Every cohort input revision, MetricSnapshot and WeeklyReportRevision |
| Deterministic renderer | `weekly_lab_renderer_v1` | Every WeeklyReportRevision |
| MetricSnapshot envelope | `metric_snapshot_v1` | Every non-north-star weekly MetricSnapshot |
| Behavioral experiment taxonomy | `behavioral_experiment_v1` | Every BehavioralExperimentRevision |
| Report export projection | `weekly_lab_export_projection_v1` | Every exported TP-LAB record set |
| Supporting product-metric dictionary | `product_metrics_v1` | Every workspace/internal supporting metric snapshot |
| Product measurement study | `product_measurement_study_v1` | Every immutable ProductMeasurementStudy definition |
| Product measurement enrollment | `product_measurement_study_enrollment_v1` | Every ProductMeasurementStudyEnrollmentVersion |
| Product measurement start command | `start_product_measurement_v1` | Every ProductMeasurementRun start-request digest |
| Product measurement run | `product_measurement_run_v1` | Every ProductMeasurementRun and ProductMeasurementRunStateEvent |
| Product analytics source event | `product_analytics_event_v1` | Every first-party ProductAnalyticsEvent |
| External product analytics projection | `product_analytics_external_v1` | Every ProductAnalyticsExternalProjection and processor request envelope |
| External analytics suppression receipt | `product_analytics_external_suppression_receipt_v1` | Every preprojection ProductAnalyticsExternalSuppressionReceipt |
| External analytics deletion inventory | `product_analytics_external_deletion_inventory_v1` | Every frozen processor-generation deletion inventory and absence-evidence hash basis |
| Workspace product metric snapshot | `workspace_product_metric_snapshot_v1` | Every tenant-owned WorkspaceProductMetricSnapshot |
| Internal aggregate metric snapshot | `internal_aggregate_product_metric_snapshot_v1` | Every service-owned cross-workspace aggregate |
| Internal aggregate cohort retirement | `internal_aggregate_cohort_retirement_v1` | Every append-only InternalAggregateCohortRetirement |

Do not create aliases such as `weekly-lab-v1`, `lab-v1` or `weekly_renderer_v1`. A change to field semantics, section membership, selection order, renderer wording, cohort membership, experiment taxonomy, a denominator, measurement-study/enrollment resolution, ProductMeasurementRun state/deadline semantics or its closed abandonment taxonomy requires the applicable identifier to be bumped.

### 1.1. Dependency boundaries

- `TP-ACC` remains authoritative for fill ordering, episode projections, plan proof, Review revisions, financial formulas, family eligibility and `verified_review_week_rate_v1`.
- `TP-MCE` remains authoritative for ContextSnapshot formulas, phases, timeframes, quality and context provenance.
- `TP-SEC` remains authoritative for authorization, retention, AI grounding, attachment security, deletion and download controls.
- `TP-LAB` MUST NOT recalculate financial or context values in the client or renderer. It selects and references outputs from the authoritative engines.
- AI summary output is not part of the deterministic WeeklyReport content hash. An AiRun references an exact published WeeklyReportRevision and its MetricSnapshot IDs under `TP-SEC`.

## 2. Scope and non-goals

### 2.1. In scope

- Monday-local regular cohorts and clipped timezone-transition cohorts.
- Immutable cohort input revisions with exact source references and hashes.
- A generic immutable MetricSnapshot envelope for Weekly Lab metrics.
- A single homogeneous dependency-version tuple per report revision.
- Deterministic report sections, ordering, null behavior, sample labels and drill-down.
- A user-confirmed behavioral experiment for the next regular cohort.
- Weekly-review completion preconditions compatible with `TP-ACC`.
- Canonical TP-LAB export records and supporting product-metric definitions.

### 2.2. Out of scope

- Financial, P&L, R, fee, accounting or context formulas already owned by `TP-ACC` or `TP-MCE`.
- Causal inference, strategy ranking, signal generation, position sizing or automatic experiment selection.
- HTML/PDF archive packaging, download delivery and archive manifest structure. Those belong to `TP-EXP`; this document defines the canonical TP-LAB projection that `TP-EXP` must package.
- Comparing data from different workspaces, accounts, venues, reporting currencies or incompatible algorithm versions.

## 3. Common deterministic conventions

### 3.1. Time and intervals

- Persisted timestamps are UTC RFC 3339 with millisecond precision.
- A time interval is half-open `[start, end)`.
- Local boundaries are stored as ISO local date-time strings without an offset together with an IANA timezone and TZDB version.
- Conversion from a local boundary uses the stored TZDB version. For an ambiguous local time, choose the earlier UTC instant. For a nonexistent local time, move forward to the first valid instant. Persist `boundary_resolution = EXACT | AMBIGUOUS_EARLIER | GAP_FORWARD` for each boundary.
- A timestamp exactly equal to `cohort_end_at_utc` belongs to the next cohort.

### 3.2. Canonical serialization and hashes

- Objects used in a digest are serialized with RFC 8785 JSON Canonicalization Scheme and hashed with SHA-256.
- SHA-256 values are lowercase hexadecimal.
- Decimal values in canonical JSON are strings using `-?(0|[1-9][0-9]*)(\.[0-9]+)?`, never exponent notation. Negative zero is canonicalized to `0`; trailing fractional zeros are removed unless an authoritative domain contract requires a fixed persisted scale.
- Arithmetic owned by `product_metrics_v1` uses arbitrary-precision integers/rationals, never IEEE-754 binary floating point. Unless section 13 defines an exact finite result, division rounds once at the final persisted boundary to scale 18 with `ROUND_HALF_EVEN`, then applies the canonical decimal-string rule above.
- Entity-reference arrays are sorted by the exact order specified in this document before hashing.
- No query, renderer or export may rely on database insertion order, map iteration order, locale collation or random values.

### 3.3. Stable reference shapes

An episode projection reference always has this shape:

```json
{
  "episodeId": "<opaque-id>",
  "projectionVersion": 3
}
```

Episode projection references sort by `episodeId` ascending using Unicode code-point order, then `projectionVersion` ascending. Review, context, metric, report and experiment IDs sort by ID ascending unless a recipe defines a stronger order.

### 3.4. Tenant-owned row invariant

Every TP-LAB tenant-owned table row MUST carry a direct, immutable, non-null `workspace_id`, including aggregate headers, revisions, state events, schedule events, generation attempts, snapshots, joins and active-pointer projections. A child-to-parent relation uses a composite foreign key `(workspace_id, parent_id)` to a matching composite candidate key; a bare parent ID is insufficient. Idempotency and logical uniqueness are workspace-scoped. No job may infer workspace ownership by traversing an unverified parent ID, and a database constraint or equivalent tenant policy MUST reject a cross-workspace relation.

Every TP-LAB append-only state stream below uses a positive `event_sequence` starting at 1 and contiguous within its declared aggregate. The next value is allocated in the same transaction under an aggregate lock; `(workspace_id, aggregate_id, event_sequence)` is unique and `recorded_at` is nondecreasing by sequence. Current/as-of replay filters `recorded_at <= T`, then uses the greatest sequence; a timestamp or opaque event ID never breaks a semantic-state tie.

## 4. Weekly cohort calendar

### 4.1. WeeklyCohort header

`WeeklyCohort` is the stable time-boundary identity. It does not contain mutable business results.

```text
weekly_cohort_id
workspace_id
user_id
cohort_sequence
cohort_key_sha256
cohort_type                    REGULAR | TRANSITION
state                          SCHEDULED | OPEN | LOCK_PENDING | LOCKED | SUPERSEDED
workspace_timezone
tzdb_version
cohort_start_local
cohort_end_local_exclusive
start_boundary_resolution
end_boundary_resolution
cohort_start_at_utc
cohort_end_at_utc
regular_week_start_local_date  nullable for TRANSITION
previous_weekly_cohort_id      nullable only for first cohort
timezone_change_schedule_id    nullable unless TRANSITION or first new-zone REGULAR cohort
north_star_eligible_cohort
completion_eligible_cohort
initial_reporting_as_of_at     nullable until LOCKED
locked_at                      nullable until LOCKED
created_at
```

Invariants:

1. `(workspace_id, cohort_sequence)` and `(workspace_id, cohort_key_sha256)` are unique.
2. For every cohort after the first, `cohort_start_at_utc = previous.cohort_end_at_utc`.
3. Non-superseded cohort headers for one workspace MUST NOT overlap or leave a gap.
4. `cohort_start_at_utc < cohort_end_at_utc`.
5. `REGULAR` starts at Monday 00:00:00 local and ends at the next Monday 00:00:00 local in the same timezone/TZDB snapshot.
6. `REGULAR` has `north_star_eligible_cohort = true` and `completion_eligible_cohort = true`.
7. `TRANSITION` has both booleans `false`; it can have a deterministic Lab report for continuity but cannot enter a north-star denominator, receive WeeklyReviewCompletion or be an experiment target/source.
8. `SCHEDULED`, `OPEN` and derived `LOCK_PENDING` are resolved against an explicit trusted as-of time by the exact resolver below. `LOCKED` requires an append-only LOCK event recorded at or after the end. `SUPERSEDED` is permitted only for a future SCHEDULED header replaced by a timezone schedule. State projection may be cached, but callers/exporters must recompute it for their as-of cutoff.
9. At initial lock, `initial_reporting_as_of_at = locked_at`, both assigned by the same trusted UTC clock in the lock transaction.

`cohort_key_sha256` is the SHA-256 of this canonical object:

```json
{
  "cohortEndAtUtc": "...",
  "cohortStartAtUtc": "...",
  "cohortType": "REGULAR",
  "tzdbVersion": "...",
  "userId": "...",
  "workspaceId": "...",
  "workspaceTimezone": "..."
}
```

Lifecycle is append-only. The `state` field is a derived projection from:

```text
weekly_cohort_state_event_id
workspace_id
weekly_cohort_id
event_sequence                  positive integer
event_type                     SCHEDULE | OPEN | LOCK | SUPERSEDE
recorded_at
actor_type                     SYSTEM | USER
actor_user_id                  nullable for SYSTEM
idempotency_key
reason_code
```

`(workspace_id, weekly_cohort_state_event_id)` is a candidate key, `(workspace_id, weekly_cohort_id)` is a composite foreign key, and `(workspace_id, weekly_cohort_id, event_sequence)` plus `(workspace_id, idempotency_key)` are unique.

For any trusted as-of `T`, first retain only events with `recorded_at <= T`, then resolve exactly in this precedence order:

1. a visible valid SUPERSEDE event -> `SUPERSEDED`;
2. otherwise a visible valid LOCK event -> `LOCKED`;
3. otherwise `T < cohort_start_at_utc` -> `SCHEDULED`;
4. otherwise `cohort_start_at_utc <= T < cohort_end_at_utc` -> `OPEN`;
5. otherwise `T >= cohort_end_at_utc` -> `LOCK_PENDING`.

`LOCK_PENDING` is a derived closed-for-initial-membership state, not an event type. It exposes scheduler delay without extending the reporting interval, accepting a WeeklyReviewCompletion, or pretending a report is published; `initial_reporting_as_of_at` and `locked_at` remain null. The lock worker can transition only `LOCK_PENDING -> LOCKED`, committing LOCK plus the initial input revision atomically and assigning `initial_reporting_as_of_at = locked_at = LOCK.recorded_at`. Its exact TP-SEC `COHORT_LOCK` payload is `{ "cohortEndAtUtc": ts, "cohortSequence": int, "cohortStartAtUtc": ts }`: `cohortSequence` equals this header's `cohort_sequence`, and both timestamps are byte-identical canonical copies of this header's UTC bounds. The state-event `event_sequence` is never substituted. Retry uses the same logical boundary key and byte-identical payload.

OPEN is an optional observability event emitted at or after the start but strictly before the end. A delayed scheduler that first observes the cohort at/after its end skips OPEN and runs lock; an OPEN event never overrides the as-of resolver. OPEN uses only the synchronous scheduler transaction below; it is not a queued work result. SUPERSEDE is valid only when recorded before the cohort start and before any LOCK; LOCK is valid only at/after the end and in absence of SUPERSEDE. Invalid event/order is rejected at write time.

Every `verified_review_week_rate_v1` user-week drill-down owned by `TP-ACC` MUST reference the matching `REGULAR` `weekly_cohort_id` and use its exact timezone, TZDB and local/UTC bounds. A TRANSITION cohort cannot be represented as an eligible user-week.

### 4.2. Initial and regular scheduling

- At onboarding confirmation, the same guarded command transaction creates the current `REGULAR` cohort and at least the immediately following `REGULAR` cohort from the confirmed timezone.
- Subsequent regular headers are created before their start so an experiment can reference the next regular cohort.
- Header creation, optional OPEN and timezone APPLY are strictly synchronous, no-queue scheduler commands. Each transaction locks Workspace, captures its current `deletion_guard_generation`, requires ACTIVE, performs no external call, and immediately before commit requires Workspace still ACTIVE with current generation equal to the captured value while retaining the same lock. Its cohort/header/state writes are the whole result; no outbox, callback or post-transaction worker may materialize a later row. A tick selected or queued in process memory before TP-SEC FENCE has no durable entitlement and, if it reaches the lock after FENCE, commits nothing. Retry with the same boundary/event tuple returns the existing rows.
- Candidate episode membership uses the lower bound of `TradeEpisodeProjection.closed_at` from `TP-ACC`:

```text
cohort_start_at_utc <= closed_at < cohort_end_at_utc
```

- Import time, Review time, report generation time and closing timestamp end-exclusive are not membership inputs.

### 4.3. TimezoneChangeSchedule

A timezone change is scheduled; it never rewrites an `OPEN` or `LOCKED` boundary.

```text
timezone_change_schedule_id
workspace_id
user_id
old_timezone
old_tzdb_version
new_timezone
new_tzdb_version
requested_at
effective_at
new_regular_start_local
new_regular_start_at_utc
state                          SCHEDULED | CANCELLED | APPLIED
actor_user_id
idempotency_key
created_at
```

The schedule `state` is also a projection from append-only events:

```text
timezone_change_schedule_state_event_id
workspace_id
timezone_change_schedule_id
event_sequence                  positive integer
event_type                     SCHEDULE | CANCEL | APPLY
recorded_at
actor_user_id                  nullable for SYSTEM
idempotency_key
reason_code
```

The event has a composite foreign key `(workspace_id, timezone_change_schedule_id)`, unique `(workspace_id, timezone_change_schedule_id, event_sequence)` and workspace-scoped idempotency. State is the greatest visible sequence. APPLY, superseding future old-zone headers and creating the optional transition/new-zone headers are one synchronous guarded transaction under the exact lock/ACTIVE/generation-CAS rule above; it has no queued or post-transaction result.

Rules:

1. `effective_at` is the first old-zone Monday 00:00 local boundary strictly after `requested_at`, resolved with `old_tzdb_version`.
2. The old-zone regular cohort ends exactly at `effective_at`.
3. `new_regular_start_at_utc` is the first resolved Monday 00:00 in `new_timezone` whose UTC instant is greater than or equal to `effective_at`.
4. If `new_regular_start_at_utc > effective_at`, create exactly one `TRANSITION` cohort `[effective_at, new_regular_start_at_utc)` using the new timezone for local display. This clipped interval prevents both overlap and gap.
5. If the instants are equal, do not create a zero-length transition cohort.
6. Starting at `new_regular_start_at_utc`, create normal Monday-local `REGULAR` cohorts in the new timezone.
7. Future old-zone headers that have not opened are superseded by schedule events; started or locked headers are never changed.
8. A workspace can have at most one non-terminal timezone change. A second request must cancel the first before its effective boundary or fail `TIMEZONE_CHANGE_ALREADY_SCHEDULED`.
9. A timezone-change request and retry use `(workspace_id, idempotency_key)` uniqueness.
10. A TZDB upgrade never mutates existing cohort boundaries. Each new header persists the TZDB version used to resolve it.

Because a `TRANSITION` cohort is not a full local user-week, its episodes are visible in its Lab report but receive exclusion reason `TRANSITION_COHORT` from north-star computation.

## 5. Immutable cohort input revisions

### 5.1. Why input revisions are separate

The cohort header fixes time. A report input revision fixes the business artifacts selected at a reporting as-of. A correction or recovered context creates a new input revision and report revision; it does not mutate the original cohort, snapshot or report.

### 5.2. WeeklyCohortInputRevision schema

```text
weekly_cohort_input_revision_id
weekly_cohort_id
workspace_id
revision_no
weekly_lab_schema_version      weekly_lab_v1
reason                         INITIAL_LOCK | REVIEW_CORRECTION | ACCOUNTING_CORRECTION |
                               CONTEXT_RECOVERY | DATA_BACKFILL
idempotency_key
reporting_as_of_at
cohort_locked_at
revision_locked_at
supersedes_input_revision_id   nullable for revision 1
correction_group_id            nullable unless multiple cohorts are affected
episode_projection_refs_json
review_revision_refs_json
context_ref_matrix_json
referenced_taxonomy_versions_json
input_digest_sha256
created_at
```

`revision_no` starts at 1 and is contiguous within a cohort. All rows are immutable. `(workspace_id,idempotency_key)` is unique; keys are 1-128 ASCII `[A-Za-z0-9._:-]`. `cohort_locked_at` always copies the immutable WeeklyCohort LOCK timestamp. `revision_locked_at = created_at` is the trusted commit/evaluation timestamp captured for this input revision; it can differ from the initial cohort lock and never rewrites it.

`input_digest_sha256` is the SHA-256 of this canonical object; input-revision ID/number, cohort/revision lock times, creation time and supersede links are deliberately outside the digest:

```json
{
  "cohortKeySha256": "...",
  "contextRefMatrix": [],
  "episodeProjectionRefs": [],
  "reason": "INITIAL_LOCK",
  "referencedTaxonomyVersions": [],
  "reportingAsOfAt": "...",
  "reviewRevisionRefs": [],
  "weeklyLabSchemaVersion": "weekly_lab_v1"
}
```

### 5.3. Exact source selection

`episode_projection_refs_json` contains every episode projection selected as active at `reporting_as_of_at` whose `closed_at` falls in the cohort boundary, including projections later excluded from a metric. Each entry carries:

```json
{
  "accountingQuality": "COMPLETE",
  "closedAt": "...",
  "ledgerAlgorithmVersion": "wac_episode_v1",
  "planProofRuleVersion": "plan_proof_v1",
  "projectionAlgorithmVersion": "episode_projection_v1",
  "recordKey": { "episode_id": "...", "projection_version": 3 }
}
```

Entries are unique and sorted by `(closedAt, episode_id, projection_version)`. Every copied field equals the same-workspace projection selected by the exact active half-open interval at `reporting_as_of_at`; scalar IDs, a later current version or an OPEN projection are rejected.

`review_revision_refs_json` has exactly one entry per selected episode projection. The selector first resolves Review state at `reporting_as_of_at` from TP-ACC append-only revision/state events, then chooses the latest ReviewRevision that is `COMPLETED` at that cutoff and pins the exact same `{ episode_id, episode_projection_version }`. A Review completed for an older projection is never reused after replay; it produces `selectionStatus = RECONFIRM_REQUIRED` until a completed revision confirms the selected projection. No completed Review produces `MISSING`.

```json
{
  "projectionRecordKey": { "episode_id": "...", "projection_version": 3 },
  "reviewRecordKey": null,
  "reviewRevisionRecordKey": null,
  "revisionNo": null,
  "selectionStatus": "MISSING",
  "staleReviewRevisionRecordKey": null
}
```

`selectionStatus` is exactly `COMPLETED | MISSING | RECONFIRM_REQUIRED`. For `COMPLETED`, both selected Review keys/revision number are non-null, stale key is null, and the Review revision pins `projectionRecordKey`. For `MISSING`, both selected keys, revision number and stale key are null. For `RECONFIRM_REQUIRED`, selected keys/revision number are null and stale key is non-null, pointing to the greatest visible completed prior-projection revision by `(recorded_at, revision_no)`; a tie is invalid. The array has exactly one entry per episode entry in the same order. All referenced Review rows use composite workspace/episode/projection ownership checks. The complete shape, including null/status/stale reference, participates in `input_digest_sha256`.

Non-null key shapes are exact: `reviewRecordKey = { "review_id": id }`; both selected and stale revision keys are `{ "review_revision_id": id }`. Selected Review header/revision must link each other and the same projection/workspace; stale revision must link the same episode but a different historical projection. Scalar, partial or cross-linked keys reject the input revision.

`context_ref_matrix_json` has exactly four slots for every episode reference, ordered `ENTRY/5m`, `ENTRY/1m`, `EXIT/5m`, `EXIT/1m`:

```json
{
  "projectionRecordKey": { "episode_id": "...", "projection_version": 3 },
  "slots": [
    {
      "availabilityDecisionRecordKey": null,
      "contextSnapshotRecordKey": { "id": "..." },
      "phase": "ENTRY",
      "quality": "COMPLETE",
      "reasonCode": null,
      "status": "AVAILABLE",
      "timeframe": "5m"
    },
    {
      "availabilityDecisionRecordKey": { "context_availability_decision_id": "..." },
      "contextSnapshotRecordKey": null,
      "phase": "ENTRY",
      "quality": null,
      "reasonCode": "JOB_PENDING",
      "status": "PENDING",
      "timeframe": "1m"
    },
    {
      "availabilityDecisionRecordKey": { "context_availability_decision_id": "..." },
      "contextSnapshotRecordKey": null,
      "phase": "EXIT",
      "quality": null,
      "reasonCode": "SOURCE_UNAVAILABLE",
      "status": "MISSING",
      "timeframe": "5m"
    },
    {
      "availabilityDecisionRecordKey": { "context_availability_decision_id": "..." },
      "contextSnapshotRecordKey": null,
      "phase": "EXIT",
      "quality": null,
      "reasonCode": "PROJECTION_NOT_CONTEXT_READY",
      "status": "NOT_APPLICABLE",
      "timeframe": "1m"
    }
  ]
}
```

The matrix array has exactly one entry per episode entry in the same order. Every `slots` array has exactly four entries in literal order `ENTRY/5m`, `ENTRY/1m`, `EXIT/5m`, `EXIT/1m`. Slot `status` and null coupling are exact:

- `AVAILABLE`: snapshot key non-null, availability-decision key null; `quality` is `COMPLETE | PARTIAL | UNRELIABLE`; reason is respectively `null | QUALITY_PARTIAL | QUALITY_UNRELIABLE`.
- `PENDING`: snapshot key/quality null, decision key non-null and reason exactly `JOB_PENDING`.
- `MISSING`: snapshot key/quality null, decision key non-null and reason one of `SOURCE_FAILED | SOURCE_UNAVAILABLE | EXACT_SNAPSHOT_NOT_FOUND | VERSION_MISMATCH`.
- `NOT_APPLICABLE`: snapshot key/quality null, decision key non-null and reason exactly `PROJECTION_NOT_CONTEXT_READY`.

Only `AVAILABLE` with `quality = COMPLETE` is aggregation eligible. A PARTIAL or UNRELIABLE snapshot ID remains referenced for coverage and drill-down but is never included in a context performance metric.

For an `AVAILABLE` slot, the referenced TP-MCE ContextSnapshot MUST match the matrix and selected projection on all of: direct `workspaceId`, `tradeEpisodeId`, `episodeProjectionVersion`, `phase`, `timeframe`, `algorithmVersion`, `parameterSetId`, and the exact phase event identity (`eventFillId`, `eventSequence`, `eventAt`, `eventTimeEndExclusive`, timestamp precision and `asOfAt`) derived from that projection. The reference uses a composite workspace/episode/projection foreign key or equivalent constrained key. Selection never follows an unconstrained "current ContextSnapshot" pointer. A snapshot from an older projection, another phase/timeframe, another dependency tuple or another workspace is ineligible; the slot remains `PENDING`/`MISSING` with a stable reason until the exact snapshot exists. Its ID MUST NOT enter source IDs, coverage numerator or aggregates.

Context visibility cutoff is exact: INITIAL_LOCK/REVIEW_CORRECTION/ACCOUNTING_CORRECTION/DATA_BACKFILL use `reporting_as_of_at`; CONTEXT_RECOVERY uses its new `revision_locked_at` while preserving the old business as-of. Within each exact `(workspace, episode, projection, phase, timeframe, algorithmVersion, parameterSetId)` chain, select the greatest contiguous `snapshotRevisionNo` with `computedAt <= context_visibility_cutoff`; the selected revision's supersede chain must be complete. Two leaves, a revision gap, or choosing a later-at-cutoff/current snapshot is invalid. This selector depends on the TP-MCE revision sequence contract and is replayed identically in export.

When no snapshot is selected, the input transaction inserts an immutable `ContextAvailabilityDecision` and references it from the slot:

```text
context_availability_decision_id
workspace_id
weekly_cohort_input_revision_id
episode_id
episode_projection_version
phase                         ENTRY | EXIT
timeframe                     1m | 5m
context_visibility_cutoff_at
status                        PENDING | MISSING | NOT_APPLICABLE
reason_code
observed_at
content_sha256
```

There is exactly one decision for each non-AVAILABLE slot and none for AVAILABLE. Composite FKs bind workspace/input/projection; phase/timeframe/cutoff/status/reason equal the slot, and `observed_at = revision_locked_at`. The writer classifies under the same source-chain lock: NOT_APPLICABLE only for TP-MCE's durable accounting-not-ready predicate; MISSING only for a durable terminal source outcome or no exact/version-compatible snapshot at cutoff; otherwise PENDING. Internal queue/job IDs are neither source nor exported. `content_sha256` hashes RFC 8785 exact object `{ "contextVisibilityCutoffAt", "episodeProjectionRecordKey", "phase", "reasonCode", "status", "timeframe", "weeklyCohortInputRevisionRecordKey", "workspaceId" }`; keys are exactly `{ "episode_id", "projection_version" }` and `{ "weekly_cohort_input_revision_id" }`. ID/observed time/hash are outside. Decision rows are exported and make old availability assertions stable even after recovery.

Decision identity is RFC 9562 UUIDv5 namespace `9e118111-4b3f-5d12-9af4-8f427daa597d`, with UTF-8 name `context_availability_decision_v1\u0000<workspace_id>\u0000<weekly_cohort_id>\u0000<revision_no-base10>\u0000<episode_id>\u0000<projection_version-base10>\u0000<phase>\u0000<timeframe>` using lowercase UUIDs and no whitespace/BOM/trailing NUL. Database unique key is `(workspace_id, weekly_cohort_input_revision_id, episode_id, episode_projection_version, phase, timeframe)`. Input revision and its decisions are inserted atomically with deferred composite FKs; a partial set cannot commit. Retry same input idempotency key returns the original revision/decisions without allocating a revision number or ID; changed command payload fails `INPUT_REVISION_IDEMPOTENCY_CONFLICT`. Decision rows prohibit UPDATE/DELETE.

`referenced_taxonomy_versions_json` is the sorted unique exact set consumed by selected completed Reviews plus the fixed experiment taxonomy:

```json
[
  {
    "recordKey": { "taxonomy_version": "breach_type_v1" },
    "recordType": "REVIEW_TAXONOMY_VERSION",
    "taxonomyType": "BREACH_TYPE"
  },
  {
    "recordKey": { "taxonomy_version": "behavioral_experiment_v1" },
    "recordType": "BEHAVIORAL_EXPERIMENT_TAXONOMY_VERSION",
    "taxonomyType": "BEHAVIORAL_EXPERIMENT"
  }
]
```

Review `taxonomyType` is `EXIT_REASON | BREACH_TYPE | EMOTION`; behavioral entry uses the exact values shown. Include every distinct Review taxonomy version referenced by a selected ReviewRevision and exactly one behavioral v1 entry. Sort by `(recordType, taxonomyType, taxonomy_version)` using Unicode code-point order. Each typed key resolves the matching immutable public version/type and complete item set; no current-version substitution, duplicate or unused extra entry is allowed.

### 5.4. Revision reasons and as-of behavior

- `INITIAL_LOCK`: `reporting_as_of_at = cohort_locked_at = revision_locked_at` in the cohort LOCK transaction.
- `CONTEXT_RECOVERY`: has a new `revision_locked_at`, but inherits the superseded revision's `reporting_as_of_at`, episode refs and Review refs exactly; only context slots and the input digest may change. A later Review must not leak into a context-only recovery.
- `REVIEW_CORRECTION`, `ACCOUNTING_CORRECTION` and `DATA_BACKFILL`: use a new trusted `reporting_as_of_at = revision_locked_at > superseded.reporting_as_of_at` and rerun all selection rules, while `cohort_locked_at` remains the original cohort LOCK time.
- If accounting correction moves an episode across a cohort boundary, create new input revisions for every affected cohort under one `correction_group_id`. The current-input pointers and corresponding current-report pointers switch atomically only after every affected report revision is ready. Historical revisions remain unchanged.
- A retry with the same explicit idempotency key returns the existing input revision, decisions and original `revision_locked_at`. A distinct key whose final `(weekly_cohort_id, reason, reporting_as_of_at, input_digest_sha256)` matches an existing revision returns that existing logical revision; changed bytes under a reused key fail conflict.

## 6. Homogeneous dependency version tuple

Every WeeklyReportRevision freezes exactly one dependency tuple:

```json
{
  "contextAlgorithmVersion": "mce-binance-spot-v1.0.0",
  "contextParameterSetId": "mce-default-v1",
  "episodeProjectionAlgorithmVersion": "episode_projection_v1",
  "feeConversionAlgorithmVersion": "fee_conversion_v1",
  "ledgerAlgorithmVersion": "wac_episode_v1",
  "metricAlgorithmVersion": "metrics_v1",
  "normalizedFillSchemaVersion": "normalized_fill_v1",
  "planChecklistSchemaVersion": "plan_checklist_v1",
  "planProofRuleVersion": "plan_proof_v1",
  "setupLabelNormalizerVersion": "setup_label_key_v1",
  "setupPresetSchemaVersion": "setup_preset_v1",
  "weeklyLabSchemaVersion": "weekly_lab_v1"
}
```

`dependency_version_tuple_hash` is the SHA-256 of the canonical object.

Rules:

1. Every core accounting projection, frozen plan/setup/checklist dependency and every MetricSnapshot referenced by a report revision MUST match the tuple exactly.
2. Core accounting version mismatch blocks publication with `REPORT_DEPENDENCY_VERSION_MISMATCH`; the orchestrator must replay or select a coherent approved tuple. It must not publish a mixed core cohort and must not split one report into hidden version populations.
3. ContextSnapshot used in an aggregate must match both context tuple fields. A missing or mismatched context artifact does not block non-context sections; it increments context coverage/exclusion counters and can make that context panel `UNAVAILABLE`.
4. Review and experiment taxonomy versions are immutable source-data versions, not calculation algorithms. The exact sorted set used by a report is persisted separately in `referenced_taxonomy_versions_json`.
5. Recompute under a changed tuple creates a new cohort input revision, MetricSnapshots and report revision. No old artifact is updated.

## 7. Generic MetricSnapshot envelope

### 7.1. Schema

Except for the north-star snapshot owned by `TP-ACC`, every deterministic weekly metric is persisted as:

```text
metric_snapshot_id
workspace_id
weekly_cohort_id
weekly_cohort_input_revision_id
metric_snapshot_schema_version   metric_snapshot_v1
weekly_lab_schema_version        weekly_lab_v1
metric_id
metric_formula_version
metric_algorithm_version         metrics_v1
eligibility_policy_id
dependency_version_tuple_hash
reporting_start_local
reporting_end_local_exclusive
workspace_timezone
tzdb_version
reporting_start_at_utc
reporting_end_at_utc
reporting_as_of_at
dimension_json
phase                            ENTRY | EXIT | null
timeframe                        1m | 5m | null
value_type                       DECIMAL | INTEGER | DURATION_MS | INTERVAL | OBJECT
value_decimal                    nullable
value_integer                    nullable
value_duration_ms                nullable
value_interval_json              nullable
value_object_json                nullable
unit
numerator_decimal                nullable
denominator_decimal              nullable
null_reason                      nullable
display_state                    NORMAL | POSITIVE_INFINITY | UNDEFINED | UNAVAILABLE
computation_status               COMPLETE | UNAVAILABLE
candidate_episode_count
eligible_episode_count
excluded_episode_count
candidate_episode_refs_json
included_episode_refs_json
excluded_episode_refs_json
exclusion_reason_counts_json
source_review_revision_ids_json
source_context_snapshot_ids_json
evidence_label                   INSUFFICIENT | EXPLORATORY | ESTIMATED
input_digest_sha256
computed_at
supersedes_metric_snapshot_id    nullable
```

Every snapshot has composite same-workspace FKs to its cohort and input revision. `reporting_as_of_at = owning_input.reporting_as_of_at`; local/UTC bounds, timezone and TZDB copy the owning WeeklyCohort exactly. `weekly_cohort_id` must equal the input's cohort. `computed_at` is a trusted commit timestamp at or after `owning_input.revision_locked_at`; it is visibility metadata and never substitutes for the business as-of.

### 7.2. Value and null invariants

`dimension_json` is one exact object: `{ "dimensionType": "OVERALL" }`; `{ "dimensionType": "SETUP", "setupId": id-or-"UNKNOWN" }`; `{ "dimensionType": "RULE_BREACH", "ruleBreach": boolean }`; `{ "breachTaxonomyVersion": version, "breachTypeId": id, "dimensionType": "BREACH_TYPE" }`; `{ "dimensionType": "CONTEXT_REGIME", "regimeCode": code }`; or `{ "dimensionType": "CONTEXT_COVERAGE" }`. Only the two CONTEXT kinds require non-null `phase` and `timeframe`; every other kind requires both null. BREACH_TYPE keys resolve an exact referenced public taxonomy item and are used only by integer `breach_episode_count`. Unknown/extra dimension members are rejected.

`metric_formula_version`, `eligibility_policy_id`, value type, unit and null semantics are closed by this matrix. A listed dimension set is exhaustive; a metric/dimension pair not listed is invalid:

| `metric_id` | Exact `metric_formula_version` | Allowed dimension -> exact `eligibility_policy_id` | Type / unit | Mathematical-null reason |
|---|---|---|---|---|
| `accounting_completeness_rate` | `accounting_completeness_rate_v1` | OVERALL -> `closed_base_eligible_v1` | DECIMAL / `RATIO` | `NO_ELIGIBLE_EPISODE` |
| `planned_trade_rate` | `planned_trade_rate_v1` | OVERALL -> `net_eligible_v1` | DECIMAL / `RATIO` | `NO_ELIGIBLE_EPISODE` |
| `review_coverage_rate` | `review_coverage_rate_v1` | OVERALL -> `net_eligible_v1` | DECIMAL / `RATIO` | `NO_ELIGIBLE_EPISODE` |
| `mean_expectancy_r` | `mean_expectancy_r_v1` | OVERALL/SETUP -> `r_eligible_v1`; RULE_BREACH -> `planned_reviewed_r_eligible_v1`; CONTEXT_REGIME -> `context_regime_r_eligible_v1` | DECIMAL / `R` | `NO_ELIGIBLE_EPISODE` |
| `median_expectancy_r` | `median_expectancy_r_v1` | same dimension/policy mapping as mean | DECIMAL / `R` | `NO_ELIGIBLE_EPISODE` |
| `mean_r_ci_95` | `mean_r_ci_95_v1` | SETUP -> `r_eligible_v1` | INTERVAL / `R` | `INSUFFICIENT_SAMPLE` when N < 2 |
| `win_rate` | `win_rate_v1` | SETUP -> `net_eligible_v1`; RULE_BREACH -> `planned_reviewed_net_eligible_v1`; CONTEXT_REGIME -> `context_regime_net_eligible_v1` | DECIMAL / `RATIO` | `NO_ELIGIBLE_EPISODE` |
| `plan_adherence_rate` | `plan_adherence_rate_v1` | OVERALL -> `planned_reviewed_net_eligible_v1` | DECIMAL / `RATIO` | `NO_ELIGIBLE_EPISODE` |
| `rule_breach_rate` | `rule_breach_rate_v1` | OVERALL -> `planned_reviewed_net_eligible_v1` | DECIMAL / `RATIO` | `NO_ELIGIBLE_EPISODE` |
| `stop_moved_away_rate` | `stop_moved_away_rate_v1` | OVERALL -> `planned_reviewed_net_eligible_v1` | DECIMAL / `RATIO` | `NO_ELIGIBLE_EPISODE` |
| `risk_exceeded_rate` | `risk_exceeded_rate_v1` | OVERALL -> `planned_reviewed_net_eligible_v1` | DECIMAL / `RATIO` | `NO_ELIGIBLE_EPISODE` |
| `fee_drag_pct_of_gross_profit` | `fee_drag_pct_of_gross_profit_v1` | OVERALL -> `net_eligible_v1` | DECIMAL / `PERCENT` | `NO_GROSS_PROFIT` |
| `fee_pct_of_gross_turnover` | `fee_pct_of_gross_turnover_v1` | OVERALL -> `net_eligible_v1` | DECIMAL / `PERCENT` | `NO_GROSS_TURNOVER` |
| `breach_episode_count` | `breach_episode_count_v1` | BREACH_TYPE -> `planned_reviewed_net_eligible_v1` | INTEGER / `EPISODE_COUNT` | none; rows with zero are absent |
| `context_coverage_counts` | `context_coverage_counts_v1` | CONTEXT_COVERAGE -> `context_coverage_all_candidates_v1` | OBJECT / `EPISODE_COUNT` | none |
| `required_checklist_completion_rate` | `required_checklist_completion_rate_v1` | OVERALL -> `planned_reviewed_net_eligible_v1` | DECIMAL / `RATIO` | `NO_ELIGIBLE_EPISODE` |
| `stop_kept_rate` | `stop_kept_rate_v1` | OVERALL -> `planned_reviewed_net_eligible_v1` | DECIMAL / `RATIO` | `NO_ELIGIBLE_EPISODE` |
| `risk_within_plan_rate` | `risk_within_plan_rate_v1` | OVERALL -> `planned_reviewed_net_eligible_v1` | DECIMAL / `RATIO` | `NO_ELIGIBLE_EPISODE` |
| `episode_review_within_24h_rate` | `episode_review_within_24h_rate_v1` | OVERALL -> `net_eligible_v1` | DECIMAL / `RATIO` | `NO_ELIGIBLE_EPISODE` |

All DECIMAL/INTERVAL computation and numerator/denominator persistence use `TP-ACC:metrics_decimal_v1`. Formula behavior is exactly the named TP-ACC formula plus the supporting behavior formulas in section 11.2; a version label cannot redefine math. Closed eligibility policies operate after the dimension filter and add every applicable reason:

| Policy ID | Exact inclusion predicate / possible exclusion reasons |
|---|---|
| `closed_base_eligible_v1` | TP-ACC active CLOSED/as-of/cohort predicate and latest eligibility not EXCLUDE; reasons `USER_EXCLUDED`, `ELIGIBILITY_VERSION_UNRESOLVED` |
| `net_eligible_v1` | closed-base plus accounting COMPLETE, non-null net and ledger invariants; adds `ACCOUNTING_INCOMPLETE`, `FEE_CONVERSION_MISSING`, `LEDGER_INVARIANT_FAILED` |
| `r_eligible_v1` | net plus VERIFIED frozen plan and positive available planned risk; adds `PLAN_PROOF_NOT_VERIFIED`, `PLANNED_RISK_UNAVAILABLE` |
| `planned_reviewed_net_eligible_v1` | net plus VERIFIED plan and exact-projection COMPLETED Review; adds `PLAN_PROOF_NOT_VERIFIED`, `REVIEW_MISSING`, `REVIEW_RECONFIRM_REQUIRED` |
| `planned_reviewed_r_eligible_v1` | planned-reviewed-net plus positive available planned risk; additionally `PLANNED_RISK_UNAVAILABLE` |
| `context_regime_net_eligible_v1` | net plus exact AVAILABLE/COMPLETE/aggregation-eligible ContextSnapshot matching phase/timeframe/version/regime; adds the applicable `CONTEXT_MISSING`, `CONTEXT_NOT_APPLICABLE`, `CONTEXT_PARTIAL`, `CONTEXT_PENDING`, `CONTEXT_UNKNOWN`, `CONTEXT_UNRELIABLE`, `CONTEXT_VERSION_MISMATCH` |
| `context_regime_r_eligible_v1` | context-regime-net plus VERIFIED frozen plan/positive risk; adds `PLAN_PROOF_NOT_VERIFIED`, `PLANNED_RISK_UNAVAILABLE` |
| `context_coverage_all_candidates_v1` | all underlying family candidates are included exactly once; no excluded rows/reasons |

Dimension filtering is not an unrecorded eligibility policy: SETUP selects the stable setup/UNKNOWN bucket; RULE_BREACH selects exact `false|true` among planned exact-projection completed Reviews; BREACH_TYPE selects Reviews containing that exact frozen taxonomy item; CONTEXT_REGIME selects the exact known regime in its panel; CONTEXT_COVERAGE selects the panel's full underlying-family population. Items outside a dimension belong to another row and are not fabricated as exclusions; unavailable context is represented in the coverage snapshot. Within the selected dimension, all applicable policy reasons are retained and sorted as section 7.3 requires. Unknown formula/policy/version/unit/null pairing fails `METRIC_CONTRACT_MISMATCH` before snapshot/report publication.

- When a value exists, `computation_status = COMPLETE`, `display_state = NORMAL`, exactly one field matching `value_type` is non-null and `null_reason = null`.
- When formula inputs are available but the result is mathematically null, `computation_status = COMPLETE`, every value field is null, `display_state = UNDEFINED`, and `null_reason` uses the authoritative reason such as `NO_ELIGIBLE_EPISODE`, `NO_WINS`, `NO_LOSSES`, `NO_GROSS_PROFIT`, `NO_GROSS_TURNOVER` or `INSUFFICIENT_SAMPLE`. The sole `POSITIVE_INFINITY` branch is TP-ACC `profit_factor` with positive gross profit and no loss; it remains COMPLETE with all value fields null and `null_reason = NO_LOSSES`. No report metric may use POSITIVE_INFINITY in v1.
- When a required upstream non-core source is unavailable, `computation_status = UNAVAILABLE`, all value fields are null, `display_state = UNAVAILABLE`, and a stable source reason is persisted. A mathematical denominator/sample null is never relabeled as upstream UNAVAILABLE.
- Zero is a real value; it is never used in place of null.
- Numeric infinity is never serialized. The `profit_factor` behavior from `TP-ACC` is represented by null plus `display_state`.
- `numerator_decimal` and `denominator_decimal` are populated for rates/ratios when the authoritative metric exposes them; denominator zero produces null, not division by zero.

`value_type = INTERVAL` uses only `value_interval_json`, with exact object `{ "lowerDecimal": canonical-decimal, "upperDecimal": canonical-decimal }`, `lowerDecimal <= upperDecimal`, and `unit = R` for `mean_r_ci_95`; every other value field is null. OBJECT uses only `value_object_json`. For the other three types, the same-named scalar field alone is non-null. When no value exists, all five value fields are null.

### 7.3. Population and counter invariants

The three episode arrays use closed shapes:

```json
candidate_episode_refs_json = [
  { "episode_id": "...", "projection_version": 3 }
]

included_episode_refs_json = [
  { "episode_id": "...", "projection_version": 3 }
]

excluded_episode_refs_json = [{
  "episodeRecordKey": { "episode_id": "...", "projection_version": 3 },
  "primaryReason": "REVIEW_MISSING",
  "reasonCodes": ["REVIEW_MISSING"]
}]
```

Candidate keys are the exact dimension-filtered projection of `WeeklyCohortInputRevision.episode_projection_refs_json`, preserving its `(closedAt, episode_id, projection_version)` order. Included keys and excluded objects preserve that same filtered order and partition every candidate exactly once. Every key is unique and same-workspace. `reasonCodes` is nonempty, unique and sorted by Unicode code-point order; `primaryReason = reasonCodes[0]`.

The closed v1 reason set is `ACCOUNTING_INCOMPLETE | CONTEXT_MISSING | CONTEXT_NOT_APPLICABLE | CONTEXT_PARTIAL | CONTEXT_PENDING | CONTEXT_UNKNOWN | CONTEXT_UNRELIABLE | CONTEXT_VERSION_MISMATCH | ELIGIBILITY_VERSION_UNRESOLVED | FEE_CONVERSION_MISSING | LEDGER_INVARIANT_FAILED | PLAN_PROOF_NOT_VERIFIED | PLANNED_RISK_UNAVAILABLE | REVIEW_MISSING | REVIEW_RECONFIRM_REQUIRED | USER_EXCLUDED`. A metric applies every applicable reason from its persisted `eligibility_policy_id`; unknown/free-text reasons are forbidden. Context family eligibility is applied after the underlying accounting family, so one exclusion may retain both accounting and context reasons.

`exclusion_reason_counts_json` is an array sorted by `reasonCode` with exact shape `[{ "count": positive-int, "reasonCode": enum }]`. It contains exactly one row for every distinct primary reason and no zero row; counts sum to `excluded_episode_count`. Counts and arrays obey:

```text
candidate_episode_count = length(candidate_episode_refs_json)
eligible_episode_count  = length(included_episode_refs_json)
excluded_episode_count  = length(excluded_episode_refs_json)
candidate_episode_count = eligible_episode_count + excluded_episode_count
```

`source_review_revision_ids_json` is a sorted unique array of exact `{ "review_revision_id": id }`; `source_context_snapshot_ids_json` is a sorted unique array of exact `{ "id": id }`. Both sort by unsigned RFC 8785 key bytes and equal exactly the source records actually read for this metric. Review keys must occur as COMPLETED in the cohort input; Context keys must occur in AVAILABLE slots, and context performance metrics may consume only COMPLETE slots matching phase/timeframe/tuple. Non-review metrics use `[]` for Review sources; non-context metrics use `[]` for Context sources. Scalar IDs, unused extras, missing source keys and cross-workspace records reject publication.

A context section also publishes a CONTEXT_COVERAGE MetricSnapshot over the full cohort candidate population so missing/unclassified context is not hidden by a regime filter.

That snapshot is exact: `metric_id = context_coverage_counts`, `metric_formula_version = context_coverage_counts_v1`, `eligibility_policy_id = context_coverage_all_candidates_v1`, dimension CONTEXT_COVERAGE, matching non-null phase/timeframe, `value_type = OBJECT`, `unit = EPISODE_COUNT`, COMPLETE/NORMAL with no numerator/denominator/null reason. Candidate and included arrays are identical full underlying-family candidates; excluded arrays/counts are empty. `value_object_json` is:

```json
{
  "completeCount": 0,
  "missingCount": 0,
  "notApplicableCount": 0,
  "partialCount": 0,
  "pendingCount": 0,
  "totalCount": 0,
  "unknownCount": 0,
  "unreliableCount": 0,
  "versionMismatchCount": 0
}
```

Every candidate maps once: AVAILABLE/COMPLETE with a known v1 regime -> complete; AVAILABLE/COMPLETE with missing/`UNKNOWN` regime -> unknown; AVAILABLE/PARTIAL -> partial; AVAILABLE/UNRELIABLE -> unreliable; PENDING -> pending; NOT_APPLICABLE -> notApplicable; MISSING/VERSION_MISMATCH -> versionMismatch; other MISSING -> missing. The eight category counts sum exactly to `totalCount = candidate_episode_count`. All values are nonnegative safe integers, including zero. Panel state is derived only from this object: eligible COMPLETE count is `completeCount`; coverage/quality exclusions equal total minus complete.

`input_digest_sha256` is lowercase SHA-256 of RFC 8785 bytes of this exact object. Every nullable member is present; `typedValue` always has exactly these members:

```json
{
  "candidateEpisodeRecordKeys": [],
  "cohortInputDigestSha256": "...",
  "computation": {
    "candidateEpisodeCount": 0,
    "computationStatus": "COMPLETE",
    "denominatorDecimal": null,
    "displayState": "UNDEFINED",
    "eligibleEpisodeCount": 0,
    "evidenceLabel": "INSUFFICIENT",
    "excludedEpisodeCount": 0,
    "exclusionReasonCounts": [],
    "nullReason": "NO_ELIGIBLE_EPISODE",
    "numeratorDecimal": null,
    "typedValue": {
      "unit": "R",
      "valueDecimal": null,
      "valueDurationMs": null,
      "valueInteger": null,
      "valueInterval": null,
      "valueObject": null,
      "valueType": "DECIMAL"
    }
  },
  "dependencyVersionTupleHash": "...",
  "dimension": { "dimensionType": "OVERALL" },
  "eligibilityPolicyId": "...",
  "excludedEpisodes": [],
  "includedEpisodeRecordKeys": [],
  "metricAlgorithmVersion": "metrics_v1",
  "metricFormulaVersion": "...",
  "metricId": "...",
  "metricSnapshotSchemaVersion": "metric_snapshot_v1",
  "phase": null,
  "reporting": {
    "asOfAt": "...",
    "endAtUtc": "...",
    "endLocalExclusive": "...",
    "startAtUtc": "...",
    "startLocal": "...",
    "tzdbVersion": "...",
    "workspaceTimezone": "..."
  },
  "sourceContextSnapshotRecordKeys": [],
  "sourceReviewRevisionRecordKeys": [],
  "timeframe": null,
  "weeklyLabSchemaVersion": "weekly_lab_v1"
}
```

The object copies the exact persisted arrays/values and owner input digest. `valueObject` equals `value_object_json`; the other four value members copy their columns. Exactly one value member is non-null when a value exists and all are null otherwise. Digest validation also rechecks every value/null/display/computation/evidence invariant, counter and typed FK; `metric_snapshot_id`, `computed_at`, `supersedes_metric_snapshot_id` and the digest field are the only schema fields outside this basis.

### 7.4. Evidence labels

Every MetricSnapshot, including OVERALL, segmented, cost, breach-count, context-coverage and experiment-baseline snapshots, derives its mandatory `evidence_label` from its own `eligible_episode_count` with no exception:

```text
N < 2       -> INSUFFICIENT
2 <= N < 30 -> EXPLORATORY
N >= 30     -> ESTIMATED
```

Values may be displayed for `N = 1`, but the renderer MUST NOT emit comparative or directional prose. `ESTIMATED` is not a proven edge and does not authorize causal or predictive language.

## 8. WeeklyReport aggregate and lifecycle

### 8.1. Stable report header

There is exactly one report aggregate per cohort:

```text
weekly_report_id
workspace_id
user_id
weekly_cohort_id
created_at
```

`(workspace_id, weekly_cohort_id)` is unique.

### 8.2. Immutable WeeklyReportRevision

```text
weekly_report_revision_id
weekly_report_id
workspace_id
weekly_cohort_id
weekly_cohort_input_revision_id
revision_no
status                           PUBLISHED | SUPERSEDED
weekly_lab_schema_version        weekly_lab_v1
renderer_id                      weekly_lab_renderer_v1
locale                           vi-VN
dependency_version_tuple_json
dependency_version_tuple_hash
reporting_as_of_at
cohort_type                      REGULAR | TRANSITION
context_section_status           AVAILABLE | PARTIAL_COVERAGE | UNAVAILABLE | EMPTY
metric_snapshot_ids_json
section_payload_json
input_digest_sha256
content_sha256
supersedes_report_revision_id    nullable for revision 1
superseded_by_report_revision_id nullable while current
recompute_reason                 INITIAL | REVIEW_CORRECTION | ACCOUNTING_CORRECTION |
                                 CONTEXT_RECOVERY | DATA_BACKFILL | VERSION_CHANGE
published_at
```

`section_payload_json` is the only canonical report data model. `weekly_lab_renderer_v1` produces a request-time Vietnamese presentation projection from this payload and referenced snapshots; rendered output is not persisted, exported or included in `content_sha256`. Cached renderer bytes are disposable and MUST be invalidated by `(weekly_report_revision_id, renderer_id, locale, content_sha256)`; they are never accepted as source data.

The revision and payload are bound to the owning input: `reporting_as_of_at = WeeklyCohortInputRevision.reporting_as_of_at`, `input_digest_sha256 = WeeklyCohortInputRevision.input_digest_sha256`, `section_payload_json.cohortId = weekly_cohort_id`, `cohortInputRevisionId = weekly_cohort_input_revision_id`, `reportingAsOfAt = reporting_as_of_at`, and every referenced MetricSnapshot has the same cohort/input/as-of. Any copied mismatch rejects publication before hashing.

The following JSON shows member grammar. Array contents are abbreviated structural examples only; the cardinality, cell lists and state matrix immediately after it are normative. Fields shown as arrays always exist, even when empty; nullable values are explicit JSON null:

```json
{
  "cohortId": "...",
  "cohortInputRevisionId": "...",
  "reportingAsOfAt": "...",
  "schemaVersion": "weekly_lab_v1",
  "sections": [
    {
      "cells": [{ "cellId": "accounting_completeness_rate", "metricSnapshotId": "..." }],
      "cohortSummary": {
        "candidateEpisodeCount": 1,
        "cohortEndAtUtc": "...",
        "cohortEndLocalExclusive": "...",
        "cohortStartAtUtc": "...",
        "cohortStartLocal": "...",
        "cohortType": "REGULAR",
        "eligibleEpisodeCount": 1,
        "exclusionPolicyId": "closed_base_eligible_v1",
        "exclusionReasonCounts": []
      },
      "order": 1,
      "sectionId": "OVERVIEW",
      "state": "AVAILABLE"
    },
    {
      "order": 2,
      "rows": [{
        "cells": [{ "cellId": "mean_expectancy_r", "metricSnapshotId": "..." }],
        "dimensionKey": "<setup-id|UNKNOWN>",
        "displayLabel": "...",
        "labelSnapshots": [{ "label": "...", "setupRevisionId": "..." }],
        "rowOrder": 1
      }],
      "sectionId": "SETUP",
      "state": "AVAILABLE"
    },
    {
      "breachOutcomeRows": [{
        "cells": [{ "cellId": "median_expectancy_r", "metricSnapshotId": "..." }],
        "rowOrder": 1,
        "ruleBreach": false
      }],
      "breachTypeRows": [{
        "breachTaxonomyVersion": "breach_type_v1",
        "breachTypeId": "...",
        "countMetricSnapshotId": "...",
        "rowOrder": 1
      }],
      "order": 3,
      "overallCells": [{ "cellId": "review_coverage_rate", "metricSnapshotId": "..." }],
      "sectionId": "ADHERENCE",
      "state": "AVAILABLE"
    },
    {
      "cells": [{ "cellId": "fee_drag_pct_of_gross_profit", "metricSnapshotId": "..." }],
      "order": 4,
      "sectionId": "COST",
      "state": "AVAILABLE"
    },
    {
      "order": 5,
      "panels": [{
        "coverageMetricSnapshotId": "...",
        "isPrimary": true,
        "panelOrder": 1,
        "phase": "ENTRY",
        "regimeRows": [{
          "cells": [{ "cellId": "median_expectancy_r", "metricSnapshotId": "..." }],
          "regimeCode": "TREND_HIGH_VOL",
          "rowOrder": 1
        }],
        "state": "AVAILABLE",
        "timeframe": "5m"
      }],
      "sectionId": "CONTEXT",
      "state": "AVAILABLE"
    },
    {
      "items": [{
        "candidateRank": 1,
        "episodeRef": { "episodeId": "...", "projectionVersion": 3 },
        "evidenceLabel": "EXPLORATORY",
        "observationKey": "SETUP:<setup-id>",
        "rMultiple": "-1.25",
        "sourceMetricSnapshotId": "..."
      }],
      "nullReason": null,
      "order": 6,
      "sectionId": "COUNTEREXAMPLES",
      "state": "AVAILABLE"
    },
    {
      "options": [{
        "baselineMetricSnapshotId": "...",
        "behaviorId": "ARM_PLAN_BEFORE_FIRST_FILL",
        "label": "Arm plan trước first fill",
        "measurementMetricId": "planned_trade_rate",
        "optionOrder": 1
      }],
      "order": 7,
      "sectionId": "EXPERIMENT",
      "state": "ACTION_REQUIRED",
      "targetWeeklyCohortId": "...",
      "taxonomyVersion": "behavioral_experiment_v1"
    }
  ]
}
```

Every section object has only the members illustrated for its `sectionId`; `OVERVIEW` additionally requires `cohortSummary` exactly as shown. Section state is constrained below, not a free shared enum. A metric cell is exactly `{ "cellId": id, "metricSnapshotId": id }`; display values are resolved from the referenced immutable MetricSnapshot, not duplicated.

Exact cell arrays and order are:

| Location | Exact `cellId` sequence |
|---|---|
| OVERVIEW `cells` | `accounting_completeness_rate`, `planned_trade_rate`, `review_coverage_rate`, `mean_expectancy_r`, `median_expectancy_r` |
| each SETUP row `cells` | `mean_expectancy_r`, `median_expectancy_r`, `mean_r_ci_95`, `win_rate` |
| ADHERENCE `overallCells` | `review_coverage_rate`, `plan_adherence_rate`, `rule_breach_rate`, `stop_moved_away_rate`, `risk_exceeded_rate` |
| each ADHERENCE outcome row `cells` | `mean_expectancy_r`, `median_expectancy_r`, `win_rate` |
| COST `cells` | `fee_drag_pct_of_gross_profit`, `fee_pct_of_gross_turnover`, `accounting_completeness_rate` |
| each CONTEXT regime row `cells` | `mean_expectancy_r`, `median_expectancy_r`, `win_rate` |

Section/cardinality grammar is exact:

- `OVERVIEW`: state `AVAILABLE` iff candidate count > 0, otherwise `EMPTY`; five cells always exist. `cohortSummary` copies exact cohort bounds/type and the `accounting_completeness_rate` snapshot's candidate/eligible counts, `eligibility_policy_id = closed_base_eligible_v1` and exclusion reason-count array byte-for-byte. Counts therefore use the full cohort closed-projection population before completeness, never a setup/context filter; reasons sort with no zero row and sum to candidate minus eligible.
- `SETUP`: state `EMPTY` with `rows=[]` iff no candidate; otherwise `AVAILABLE` with one row per observed stable setup dimension, including `UNKNOWN`. Row cells are always the four above. Rows, labels and rowOrder follow section 9.3; `labelSnapshots` entries are exactly `{ "label": str, "setupRevisionId": id }`.
- `ADHERENCE`: state `AVAILABLE` iff candidates exist, else `EMPTY`; five overall cells and exactly two `breachOutcomeRows` always exist in `false,true` order, each with three cells. `breachTypeRows` contains exactly types with positive count, sorted taxonomy version/type ID; each count snapshot has `value_type=INTEGER`, matching dimension/type and exact count.
- `COST`: state `AVAILABLE` iff candidates exist, else `EMPTY`; all three cells always exist, with null carried by their snapshots.
- `CONTEXT`: contains exactly four panels in literal order `ENTRY/5m`, `ENTRY/1m`, `EXIT/5m`, `EXIT/1m`; first alone has `isPrimary=true`. Every panel contains exactly four regime rows in `TREND_HIGH_VOL`, `TREND_LOW_VOL`, `RANGE_HIGH_VOL`, `RANGE_LOW_VOL` order, each with three cells, plus one non-null CONTEXT_COVERAGE snapshot. Panel state follows section 9.6. Section state is `EMPTY` iff all panels EMPTY, `UNAVAILABLE` iff no panel has eligible context and candidates exist, `PARTIAL_COVERAGE` iff any panel is PARTIAL_COVERAGE or available panels coexist with unavailable panels, otherwise `AVAILABLE`; top `context_section_status` must equal it.
- `COUNTEREXAMPLES`: `AVAILABLE` requires 1..3 items and `nullReason=null`; `EMPTY` requires `items=[]` and `nullReason=NO_OPPOSITE_SIGN_EXCEPTION`. Every item has exactly the members shown, ranks contiguous from 1, unique episode refs and source snapshot closure.
- `EXPERIMENT`: REGULAR requires `ACTION_REQUIRED`, next REGULAR target, exact `behavioral_experiment_v1`, and all seven taxonomy options in `optionOrder`. `baselineMetricSnapshotId` is non-null for options 1..5 and resolves the same-input metric named by `measurementMetricId`; it is null for option 6 because success is observed only by the later WeeklyReviewCompletion, and null for OTHER because `USER_SELF_CHECK` is an explicit non-metric sentinel. TRANSITION requires `NOT_APPLICABLE`, target null, same taxonomy version and `options=[]`. No other null/state/cardinality is valid.

Every cell snapshot has the expected `metric_id`, dimension, phase/timeframe and input revision for its location. `metric_snapshot_ids_json` is the unique union of every cell, breach-count, context-coverage, counterexample source and experiment-baseline snapshot ID, sorted by lowercase canonical ID bytes. Missing/extra cell, duplicate ID where distinct location inputs differ, wrong state/cardinality, wrong row/panel order, dangling snapshot or copied summary mismatch rejects publication.

`status` and `superseded_by_report_revision_id` above are derived API/export projection fields. The immutable revision row stores its content, `supersedes_report_revision_id` and publish event; it is never updated. State is derived from append-only `WeeklyReportRevisionStateEvent` records:

```text
weekly_report_revision_state_event_id
workspace_id
weekly_report_id
weekly_report_revision_id
event_sequence                    positive integer
event_type                       PUBLISH | SUPERSEDE
caused_by_report_revision_id     nullable for PUBLISH
recorded_at
```

The event has composite foreign keys to `(workspace_id, weekly_report_id)` and `(workspace_id, weekly_report_revision_id)`. Sequence is contiguous across the whole WeeklyReport aggregate, not reset per revision; `(workspace_id, weekly_report_id, event_sequence)` is unique. A publish/supersede retry is unique in the workspace by its logical event key, and current revision replay uses greatest visible sequence.

`content_sha256` is lowercase SHA-256 of this exact RFC 8785 object; nullable lineage is present as JSON null, timestamps use canonical UTC RFC 3339 milliseconds, decimals inside nested payloads follow section 3, and ID arrays follow their declared source order:

```json
{
  "cohort_type": "REGULAR",
  "context_section_status": "AVAILABLE",
  "dependency_version_tuple_hash": "...",
  "dependency_version_tuple_json": {},
  "input_digest_sha256": "...",
  "locale": "vi-VN",
  "metric_snapshot_ids_json": [],
  "recompute_reason": "INITIAL",
  "renderer_id": "weekly_lab_renderer_v1",
  "reporting_as_of_at": "...",
  "revision_no": 1,
  "section_payload_json": {},
  "supersedes_report_revision_id": null,
  "weekly_cohort_id": "...",
  "weekly_cohort_input_revision_id": "...",
  "weekly_lab_schema_version": "weekly_lab_v1",
  "weekly_report_id": "...",
  "weekly_report_revision_id": "...",
  "workspace_id": "..."
}
```

Derived `status`, reverse `superseded_by_report_revision_id`, PUBLISH/SUPERSEDE events, `published_at` and the hash field itself are outside the basis because they are lifecycle, not immutable revision content. All other persisted revision fields are present above. A different opaque ID therefore intentionally changes this record-integrity hash; independent golden producers use the same frozen IDs and must emit identical bytes.

### 8.3. Publication and idempotency

- A generation job uses the logical key SHA-256 of `{ workspaceId, cohortInputRevisionId, dependencyVersionTupleHash, weeklyLabSchemaVersion, rendererId, locale }`.
- A retry with the same logical key returns the same report revision.
- Reusing an explicit idempotency key with a different logical key fails `REPORT_IDEMPOTENCY_CONFLICT`.
- Failed or running jobs are stored as `ReportGenerationAttempt`; they do not consume a report revision number and are not WeeklyReportRevision rows. Each attempt stores direct `workspace_id`, `weekly_cohort_input_revision_id`, logical-key hash, explicit idempotency key, attempt number, state, safe error code, started/completed timestamps and a composite workspace/input-revision foreign key. `(workspace_id, logical_key_hash, attempt_no)` and `(workspace_id, idempotency_key)` are unique.
- Publication inserts all MetricSnapshots, the new report revision, its PUBLISH event and a SUPERSEDE event for the previous current revision in one transaction. Current/superseded projections and both API lineage directions are derived from those immutable records.
- `revision_no` starts at 1 and is contiguous. Exactly one revision per report has no `superseded_by_report_revision_id`.
- A report is never published with partial core accounting snapshots or dangling IDs.
- Context outage publishes all non-context sections. Its context section is `UNAVAILABLE` with coverage reasons. Context recovery creates a new input revision and report revision with the same reporting as-of; it never mutates revision 1.
- AI failure, disablement or rejection does not change the deterministic report status or content.

## 9. Deterministic section recipe

### 9.1. Fixed section order

`section_payload_json.sections` contains exactly these IDs in this order:

1. `OVERVIEW`
2. `SETUP`
3. `ADHERENCE`
4. `COST`
5. `CONTEXT`
6. `COUNTEREXAMPLES`
7. `EXPERIMENT`

Every numeric cell references exactly one MetricSnapshot ID. Every drill-down reference is an included episode projection from that snapshot. The client MUST NOT recompute, merge or rank values.

### 9.2. Overview

Render these metrics in fixed order:

1. `accounting_completeness_rate`
2. `planned_trade_rate`
3. `review_coverage_rate`
4. `mean_expectancy_r`
5. `median_expectancy_r`

All formulas and base eligibility come from `TP-ACC`. The section additionally shows cohort type, local/UTC boundary, candidate episode count and exclusion counters. A transition report displays a persistent `TRANSITION` badge and states that it is excluded from north-star evaluation.

### 9.3. Setup observations

- Dimension key is stable `setup_id`. Missing setup uses exact bucket `UNKNOWN`.
- One row contains `mean_expectancy_r`, `median_expectancy_r`, `mean_r_ci_95` and `win_rate`; each cell keeps its own N and exclusions.
- A setup row exists if at least one cohort candidate maps to that dimension, even when all values are null.
- Historical labels come only from frozen plan revisions. If one setup ID has multiple label snapshots, persist all distinct `{setupRevisionId, label}` pairs sorted by setup revision ID. Display the label from the latest included plan revision by `(recorded_at DESC, trade_plan_revision_id DESC)`; do not join the active preset.
- Rows sort by normalized display label using Unicode NFC code-point order, then `setup_id`. Never sort by P&L, R, win rate or sample outcome.
- No row receives `best`, `worst`, `edge`, `good` or `bad` labels.

### 9.4. Adherence and rule-breach observations

The section first renders these overall metrics in fixed order:

1. `review_coverage_rate`
2. `plan_adherence_rate`
3. `rule_breach_rate`
4. `stop_moved_away_rate`
5. `risk_exceeded_rate`

The outcome comparison uses policy `tp_lab_breach_outcome_v1`:

```text
TP-ACC net_eligible
and is_planned = true
and completed ReviewRevision selected at reporting_as_of_at
```

Create exactly two rows ordered `rule_breach = false`, then `true`. Each row renders `mean_expectancy_r`, `median_expectancy_r` and `win_rate` using the authoritative TP-ACC formulas over that row's population. `MISSING` Review is excluded with `REVIEW_MISSING`; `RECONFIRM_REQUIRED` is excluded separately with `REVIEW_RECONFIRM_REQUIRED`. Neither is treated as no breach or reused from an older projection.

Breach-type counts are a separate multi-label table sorted by `(breach_taxonomy_version, breach_type_id)`. Because one Review can contain multiple breach IDs, the sum of type counts may exceed the number of breach episodes; the renderer states this and does not use the type-count sum as a denominator.

### 9.5. Cost

Render in fixed order:

1. `fee_drag_pct_of_gross_profit`
2. `fee_pct_of_gross_turnover`
3. `accounting_completeness_rate`

The two fee ratios MUST reference the same net-eligible episode set required by `TP-ACC`. `FEE_CONVERSION_MISSING` is shown in exclusion counters. A missing ratio remains null with its authoritative reason; the renderer never replaces it with zero.

### 9.6. Context

Context panels are separate populations and appear in this order:

1. `ENTRY/5m` - primary default panel
2. `ENTRY/1m`
3. `EXIT/5m`
4. `EXIT/1m`

The renderer may initially open only the primary panel, but all four panel records remain in the report payload. Phase or timeframe values are never merged.

For each panel:

- Use only ContextSnapshot with `quality = COMPLETE`, `aggregationEligible = true` and exact context versions from the report tuple.
- Apply the underlying TP-ACC family eligibility before context eligibility.
- Group by `regimeCode` in fixed order `TREND_HIGH_VOL`, `TREND_LOW_VOL`, `RANGE_HIGH_VOL`, `RANGE_LOW_VOL`.
- Render `mean_expectancy_r`, `median_expectancy_r` and `win_rate`, with independent N and exclusions per cell.
- Publish coverage counters for `COMPLETE`, `PARTIAL`, `UNRELIABLE`, `PENDING`, `MISSING`, `NOT_APPLICABLE` and `VERSION_MISMATCH` over the full cohort.
- Never place unknown or missing context into a performance bucket.

Panel state is:

- `EMPTY` iff the panel's underlying family candidate count is zero.
- `UNAVAILABLE` iff candidate count is positive and eligible COMPLETE context count is zero.
- `PARTIAL_COVERAGE` iff eligible COMPLETE count is positive and at least one candidate has a context coverage/quality exclusion.
- `AVAILABLE` iff eligible COMPLETE count is positive and context coverage/quality exclusion count is zero.

Evaluate in that order; the four predicates are mutually exclusive and exhaustive, and counts come from the panel's CONTEXT_COVERAGE MetricSnapshot.

Labels remain descriptive. They never add bullish/bearish direction, a trade signal or a causal interpretation.

### 9.7. Deterministic counterexamples

This section finds exceptions to the report's own observed median sign. It does not infer or fabricate a user's private narrative.

Eligible source observations are, in order:

1. SETUP rows sorted by the setup order in section 9.3.
2. ADHERENCE breach rows ordered `false`, `true`.
3. Primary `ENTRY/5m` CONTEXT regime rows in the regime order in section 9.6.

For each source observation:

1. Use its `median_expectancy_r` MetricSnapshot and the exact included episode set behind it.
2. If median R is positive, candidates are included episodes with `r_multiple < 0`.
3. If median R is negative, candidates are included episodes with `r_multiple > 0`.
4. If median R is zero or null, there is no opposite-sign candidate.
5. Zero-R episodes are not opposite-sign candidates.
6. Sort candidates by `abs(r_multiple) DESC`, `closed_at ASC`, `episode_id ASC` and choose the first episode not already emitted.
7. Emit at most one episode per observation and at most three counterexamples for the report.
8. Iterate observations in the fixed source order. If an observation's strongest candidate was already used, continue to its next candidate.

Each output persists source observation key, source MetricSnapshot ID, episode projection reference, exact `r_multiple`, candidate rank and evidence label. If no item is found, emit an empty list with `null_reason = NO_OPPOSITE_SIGN_EXCEPTION`. Do not substitute the nearest same-sign result.

`observationKey` has one exact ASCII grammar: setup `SETUP:<setup_id-or-UNKNOWN>`; adherence `ADHERENCE:RULE_BREACH_FALSE` or `ADHERENCE:RULE_BREACH_TRUE`; primary context `CONTEXT:ENTRY:5m:<regimeCode>`. IDs/codes are copied without escaping because v1 setup IDs are canonical UUIDs or `UNKNOWN` and regime codes contain only `[A-Z_]`. The key must resolve the exact source row and its median snapshot; unknown prefix, lowercase variant, alternate separator or non-primary panel is rejected.

The deterministic item copy is exact: `Trong cùng nhóm quan sát, episode này có R trái dấu với median của nhóm (N = <eligible_episode_count>).` The integer comes from the source MetricSnapshot. It must not say the episode disproves, causes or predicts anything.

### 9.8. Experiment section

- The published report contains the fixed v1 taxonomy options in taxonomy order, the ID of the next regular cohort and action-specific baseline refs. Options 1..5 require their exact same-input MetricSnapshot; option 6 and OTHER require null baseline under the section 8.2 matrix.
- No option is preselected, ranked or marked recommended.
- Before confirmation the section state is `ACTION_REQUIRED` for a regular cohort.
- A transition report has state `NOT_APPLICABLE` and cannot produce an experiment or WeeklyReviewCompletion.
- User selection is persisted as BehavioralExperimentRevision, not by mutating the report payload.

## 10. Renderer contract

### 10.1. Renderer input and output

`weekly_lab_renderer_v1` is a pure function of:

```text
weekly_lab_v1 section payload
referenced MetricSnapshots
frozen taxonomy labels
locale = vi-VN
```

It does not read the database, current taxonomy, current timezone, clock, AI output or feature flags.

The request-time result is an exact disposable token projection, not a durable business record:

```json
{
  "cohortBadge": "TUẦN THƯỜNG",
  "copyTokens": [{
    "copyId": "BREACH_MULTI_LABEL_NOTICE",
    "payloadPointer": "/sections/2/breachTypeRows",
    "text": "Một episode có thể có nhiều loại vi phạm; tổng theo loại có thể lớn hơn số episode và không được dùng làm mẫu số."
  }],
  "locale": "vi-VN",
  "metricTokens": [{
    "accessibleText": "Mức hoàn tất kế toán: 100,0%. Dữ liệu khám phá.",
    "displayLabel": "Mức hoàn tất kế toán",
    "displayValue": "100,0%",
    "evidenceLabel": "Dữ liệu khám phá",
    "metricSnapshotId": "...",
    "nullReasonLabel": null,
    "payloadPointer": "/sections/0/cells/0/metricSnapshotId"
  }],
  "rendererId": "weekly_lab_renderer_v1",
  "sectionHeaders": [{
    "sectionId": "OVERVIEW",
    "state": "AVAILABLE",
    "stateLabel": null,
    "title": "Tổng quan"
  }]
}
```

Root/nested objects admit no extra/missing members. `cohortBadge` is `TUẦN THƯỜNG` for REGULAR and `GIAI ĐOẠN CHUYỂN TIẾP` for TRANSITION. `sectionHeaders` has exactly seven entries in canonical section order. Section titles are respectively `Tổng quan`, `Theo setup`, `Tuân thủ`, `Chi phí`, `Bối cảnh`, `Ngoại lệ trái dấu`, `Thử nghiệm tuần tới`. `stateLabel` is null for AVAILABLE and otherwise exactly: EMPTY `Chưa có dữ liệu trong kỳ`; UNAVAILABLE `Dữ liệu nguồn chưa khả dụng`; PARTIAL_COVERAGE `Dữ liệu bối cảnh chưa đầy đủ`; ACTION_REQUIRED `Chọn một thử nghiệm cho tuần tới`; NOT_APPLICABLE `Không áp dụng cho giai đoạn chuyển tiếp`.

`copyTokens` is the complete deterministic non-metric copy projection. Each item has exactly `{ "copyId": enum, "payloadPointer": RFC6901-string, "text": string }`; no HTML or hidden member is allowed. Items appear in this exact order and cardinality:

1. `TRANSITION_NORTH_STAR_EXCLUSION` appears exactly once iff `cohortType = TRANSITION`; pointer `/sections/0/cohortSummary/cohortType`; text `Giai đoạn chuyển tiếp không được tính vào north-star.`
2. `BREACH_MULTI_LABEL_NOTICE` appears exactly once iff `/sections/2/breachTypeRows` is nonempty; pointer `/sections/2/breachTypeRows`; text `Một episode có thể có nhiều loại vi phạm; tổng theo loại có thể lớn hơn số episode và không được dùng làm mẫu số.`
3. `COUNTEREXAMPLE_ITEM` appears once for each `/sections/5/items/<zero-based-index>` in payload order; pointer is that exact item pointer; text is `Trong cùng nhóm quan sát, episode này có R trái dấu với median của nhóm (N = <eligible_episode_count>).` where the integer is copied from the item's exact `sourceMetricSnapshotId`. It is absent when the item array is empty.

No other `copyId`, pointer or copy is valid under `weekly_lab_renderer_v1`. Thus the transition statement, multi-label disclaimer and every counterexample sentence are representable inside the closed renderer result and are derived only from pinned payload/snapshot input.

`metricTokens` follows depth-first payload order and has one row for every non-null `metricSnapshotId` cell, `countMetricSnapshotId`, `coverageMetricSnapshotId` and `baselineMetricSnapshotId`; `payloadPointer` is its RFC 6901 JSON Pointer. Counterexample `sourceMetricSnapshotId` is provenance rather than another displayed value and does not create a token. No other reference may be omitted or added.

Fixed display labels by cell/reference are: `accounting_completeness_rate` -> `Mức hoàn tất kế toán`; `planned_trade_rate` -> `Tỷ lệ có plan trước fill`; `review_coverage_rate` -> `Tỷ lệ review hoàn tất`; `mean_expectancy_r` -> `R trung bình`; `median_expectancy_r` -> `R trung vị`; `mean_r_ci_95` -> `Khoảng ước lượng 95% của R trung bình`; `win_rate` -> `Tỷ lệ lãi`; `plan_adherence_rate` -> `Tỷ lệ tuân thủ plan`; `rule_breach_rate` -> `Tỷ lệ vi phạm quy tắc`; `stop_moved_away_rate` -> `Tỷ lệ dời stop xa hơn`; `risk_exceeded_rate` -> `Tỷ lệ vượt risk`; `fee_drag_pct_of_gross_profit` -> `Phí trên gross profit`; `fee_pct_of_gross_turnover` -> `Phí trên gross turnover`; breach count -> `Số episode`; context coverage -> `Độ phủ bối cảnh`; experiment baseline -> exact `<option.label> - mức hiện tại`. Unknown cell/metric label fails rendering under v1.

Evidence labels are exact: INSUFFICIENT `Chưa đủ mẫu`; EXPLORATORY `Dữ liệu khám phá`; ESTIMATED `Ước lượng mô tả`. `nullReasonLabel` is non-null iff display value comes from a null metric and equals the table in 10.3; in that case `displayValue = nullReasonLabel`. Otherwise it is null. `accessibleText` is exact `<displayLabel>: <displayValue>. <evidenceLabel>.` with one ASCII space after punctuation and no hidden markup. UI layout may consume these tokens but cannot alter their value/label semantics under this renderer ID.

### 10.2. Display formatting

- Counts: base-10 integer without decimals.
- R values and R intervals: two decimal places, round half-even, suffix `R`.
- Percentages: one decimal place, round half-even, suffix `%`.
- Durations under 72 hours: hours with one decimal place; larger durations: days with one decimal place.
- Vietnamese display uses `.` for thousands and `,` for decimal. Canonical machine values remain the decimal strings in MetricSnapshot.
- Null renders a reason-specific label; never `0`, `NaN`, `Infinity`, an empty string or a dash without an accessible explanation.
- INTERVAL renders exact `<lower>R đến <upper>R`, formatting each bound independently. `context_coverage_counts_v1` renders `<completeCount>/<totalCount> đầy đủ` with integer formatting; the detail view reads all eight named counters from the typed object without recomputing categories.

### 10.3. Stable null labels

The v1 table is closed:

| Reason | Vietnamese renderer label |
|---|---|
| `NO_ELIGIBLE_EPISODE` | `Chưa có episode đủ điều kiện` |
| `NO_ELIGIBLE_USER_WEEK` | `Chưa có tuần người dùng đủ điều kiện` |
| `INSUFFICIENT_SAMPLE` | `Chưa đủ mẫu để ước lượng` |
| `NO_WINS` | `Không có episode lãi trong mẫu` |
| `NO_LOSSES` | `Không có episode lỗ trong mẫu` |
| `NO_GROSS_PROFIT` | `Không có gross profit làm mẫu số` |
| `NO_GROSS_TURNOVER` | `Không có gross turnover làm mẫu số` |
| `CONTEXT_UNAVAILABLE` | `Bối cảnh chưa khả dụng` |
| `NO_OPPOSITE_SIGN_EXCEPTION` | `Chưa có ngoại lệ trái dấu trong mẫu` |

Changing a fixed label, number-format rule, section order or default panel requires a new renderer ID.

### 10.4. Copy policy

Allowed templates use `quan sát được`, `trong mẫu này`, `đồng xuất hiện`, `lần lượt` and `dữ liệu khám phá`.

The renderer MUST NOT output causal language, prediction, `best/worst`, buy/sell guidance, position size, win probability or a behavioral choice selected on behalf of the user. Comparative prose is suppressed for `INSUFFICIENT`; `EXPLORATORY` always includes the small-sample warning.

## 11. BehavioralExperimentRevision

### 11.1. Entity and revision schema

There is at most one BehavioralExperiment aggregate for a target regular cohort:

```text
behavioral_experiment_id
workspace_id
user_id
target_weekly_cohort_id
created_at
```

`BehavioralExperimentRevision` is append-only:

```text
behavioral_experiment_revision_id
behavioral_experiment_id
workspace_id
user_id
revision_no
source_weekly_report_revision_id
source_weekly_cohort_id
target_weekly_cohort_id
taxonomy_version                  behavioral_experiment_v1
behavior_id
measurement_metric_id
other_behavior_text              nullable
other_success_check_text         nullable
state                            PROPOSED | CONFIRMED | SUPERSEDED | CANCELLED
recorded_at
confirmed_at                     nullable unless CONFIRMED
actor_user_id
idempotency_key
supersedes_experiment_revision_id nullable for revision 1
content_sha256
```

`state` and `confirmed_at` are derived fields, not mutable revision content. Authoritative state is append-only:

```text
behavioral_experiment_state_event_id
workspace_id
behavioral_experiment_id
behavioral_experiment_revision_id
event_sequence                    positive integer
event_type                       PROPOSE | CONFIRM | SUPERSEDE | CANCEL
caused_by_experiment_revision_id nullable
recorded_at
actor_user_id
idempotency_key
```

The event has composite foreign keys to `(workspace_id, behavioral_experiment_id)` and `(workspace_id, behavioral_experiment_revision_id)`, unique `(workspace_id, behavioral_experiment_id, event_sequence)` and `(workspace_id, idempotency_key)`. Sequence is aggregate-wide across revisions; current state/revision replay uses greatest visible sequence.

A saved revision receives PROPOSE. Confirmation appends CONFIRM for that revision and, when applicable, SUPERSEDE for the previously confirmed revision in one transaction. No Revision row or StateEvent is updated or deleted.

`content_sha256` is lowercase SHA-256 of this exact RFC 8785 object; nullable members are explicit null and `recorded_at` is canonical UTC RFC 3339 milliseconds:

```json
{
  "actor_user_id": "...",
  "behavior_id": "ARM_PLAN_BEFORE_FIRST_FILL",
  "behavioral_experiment_id": "...",
  "behavioral_experiment_revision_id": "...",
  "idempotency_key": "...",
  "measurement_metric_id": "planned_trade_rate",
  "other_behavior_text": null,
  "other_success_check_text": null,
  "recorded_at": "...",
  "revision_no": 1,
  "source_weekly_cohort_id": "...",
  "source_weekly_report_revision_id": "...",
  "supersedes_experiment_revision_id": null,
  "target_weekly_cohort_id": "...",
  "taxonomy_version": "behavioral_experiment_v1",
  "user_id": "...",
  "workspace_id": "..."
}
```

Derived `state`, `confirmed_at`, state-event metadata and hash field are outside the basis. Every other immutable revision field is included; changing a source/target/taxonomy/text/idempotency member changes the hash.

### 11.2. Frozen v1 no-signal taxonomy

Behavior taxonomy is durable SHARED_PUBLIC source data owned by TP-LAB, not labels synthesized by TP-EXP:

```text
BehavioralExperimentTaxonomyVersion
taxonomy_version                 behavioral_experiment_v1
content_sha256
published_at

BehavioralExperimentTaxonomyItem
taxonomy_version                 behavioral_experiment_v1
behavior_id
label_vi
measurement_metric_id
option_order                     positive contiguous integer
```

Version key and `(taxonomy_version, behavior_id)` are unique; `(taxonomy_version, option_order)` is unique and order is exact `1..N`. The version and complete nonempty item set are inserted atomically and immutable. `content_sha256` is lowercase SHA-256 of RFC 8785 exact object `{ "items": [{ "behaviorId": id, "labelVi": label, "measurementMetricId": id, "optionOrder": int }], "taxonomyVersion": version }`, sorted `(option_order, behavior_id)`; `published_at` and hash itself are outside the basis. Reuse of a version with different bytes fails closed. V1 commands accept exactly `behavioral_experiment_v1`; changing the selected taxonomy requires a TP-LAB contract/version update, not a timestamp/lexical latest lookup.

| Order | Behavior ID | Fixed Vietnamese label | Measurement metric |
|---:|---|---|---|
| 1 | `ARM_PLAN_BEFORE_FIRST_FILL` | `Arm plan trước first fill` | `planned_trade_rate` |
| 2 | `COMPLETE_REQUIRED_CHECKLIST` | `Hoàn thành checklist bắt buộc` | `required_checklist_completion_rate` |
| 3 | `DO_NOT_MOVE_STOP_AWAY` | `Không dời stop xa invalidation` | `stop_kept_rate` |
| 4 | `DO_NOT_EXCEED_PLANNED_RISK` | `Không vượt planned risk` | `risk_within_plan_rate` |
| 5 | `COMPLETE_EPISODE_REVIEW_WITHIN_24H` | `Hoàn thành Episode Review trong 24 giờ` | `episode_review_within_24h_rate` |
| 6 | `COMPLETE_WEEKLY_REVIEW_WITHIN_72H` | `Hoàn thành Weekly Review trong 72 giờ` | `weekly_review_on_time` |
| 7 | `OTHER` | `Hành vi khác do bạn tự chọn` | `USER_SELF_CHECK` |

The first six options describe journaling and risk-discipline behavior. They do not specify an instrument, entry, exit, market direction, leverage, order or position size.

Supporting behavioral formulas are non-financial:

- `required_checklist_completion_rate`: planned reviewed net-eligible episodes where every required checklist value is true, divided by planned reviewed net-eligible episodes.
- `stop_kept_rate`: exact count ratio `(denominator_count - stop_moved_away_count) / denominator_count` over the same TP-ACC population; it copies those integer count operands and never subtracts a rounded source value; denominator zero follows the source null branch.
- `risk_within_plan_rate`: exact count ratio `(denominator_count - risk_exceeded_count) / denominator_count` over the same TP-ACC population; it copies those integer count operands and never subtracts a rounded source value; denominator zero follows the source null branch.
- `episode_review_within_24h_rate`: net-eligible closed episodes with first Review completion satisfying `closed_at <= completed_at < closed_at + 24h`, divided by net-eligible closed episodes. Missing Review remains in the denominator and is not on time.
- `weekly_review_on_time` is an outcome predicate, not a report-time MetricSnapshot: success is true only when the later source-cohort WeeklyReviewCompletion meets the strict boundary owned by `verified_review_week_rate_v1` in `TP-ACC`. It therefore has no baseline ref in the source report.
- `USER_SELF_CHECK` is a non-metric sentinel for OTHER. Success is evaluated only from the user's confirmed `other_success_check_text`; the system does not synthesize a numeric/boolean snapshot or analytics value from it.

### 11.3. OTHER limits

- `other_behavior_text` and `other_success_check_text` are both required for `OTHER`, trimmed, and each is 1-240 Unicode scalar values.
- They are null for every non-OTHER option.
- They are user-authored untrusted text, never an instruction to the renderer or AI, never sent to external product analytics, and never parsed into a trade action.
- The API has no structured fields for symbol, side, entry, exit, price, leverage or position size.
- URL markup, HTML and control characters other than newline are rejected. Secret-like content is handled under `TP-SEC`.

### 11.4. Target, confirmation and uniqueness

- The target is the immediately next chronological `REGULAR` cohort after the source report cohort. A TRANSITION cohort is skipped and cannot be a target.
- Exactly one current `CONFIRMED` revision may exist for `(workspace_id, target_weekly_cohort_id)`.
- An edit creates a new proposed revision. Confirming it atomically appends a SUPERSEDE event for the prior confirmed revision and a CONFIRM event for the new revision.
- `ConfirmBehavioralExperiment` requires `based_on_revision_no`, actor, trusted timestamp and idempotency key. A stale base fails `STALE_EXPERIMENT_REVISION`; retry returns the same confirmed revision.
- Confirmation is permitted only while the target regular cohort is `SCHEDULED` or `OPEN`. It cannot retroactively target a locked cohort.
- Cancelling creates a terminal state event; it does not delete historical revisions or a WeeklyReviewCompletion that already references one.

## 12. Weekly-review completion integration

`TP-ACC` owns the append-only WeeklyReviewCompletion event, completion deadline and north-star formula. `TP-LAB` owns the report and experiment preconditions for its foreign keys.

`CompleteWeeklyReview` requires:

1. a current `PUBLISHED` WeeklyReportRevision for the same workspace, user and regular source cohort;
2. a current user-confirmed BehavioralExperimentRevision whose source report revision is the one being reviewed and whose target is the next regular cohort;
3. exact cohort local/UTC boundary, timezone and TZDB values matching the report and the completion event;
4. a trusted server timestamp at or after `cohort_end_at_utc`;
5. an idempotency key and recent authorization under `TP-SEC` where required by the application session policy.

Ownership/status checks and insertion of WeeklyReviewCompletion occur in one transaction. The experiment must already have a CONFIRM event; this command does not implicitly confirm a proposal. Otherwise fail `WEEKLY_REVIEW_PRECONDITION_FAILED` without a partial effect.

If a report or experiment is later superseded, the completion event continues to reference the exact revision the user reviewed. A second completion for the same user-week is not created. The strict `< week_end + 72h` success rule remains exclusively defined by `TP-ACC`.

## 13. Supporting product-metric dictionary

### 13.1. Source and snapshot rules

The metrics below use exact version `product_metrics_v1`. They are not financial insights and are not rendered as trading-performance claims.

First-party canonical domain rows, the pinned measurement-study/enrollment records, `ProductMeasurementRun` lifecycle and `ProductAnalyticsEvent` are the replay source of truth. Delivery to an external analytics processor is never authoritative.

The server-side study registry is global shared configuration, never client-authored:

```text
ProductMeasurementStudy
study_id
study_version
study_schema_version             product_measurement_study_v1
active_from
active_until_exclusive
feature_policy_json
published_at
definition_sha256
```

`study_id` is a canonical lowercase RFC 9562 UUID, `study_version` starts at 1 and is contiguous per study, and every row is immutable. `active_from < active_until_exclusive`, both are canonical UTC milliseconds, and `published_at <= active_from`. Versions of one `study_id` MUST NOT have overlapping half-open active intervals. A version is ACTIVE at trusted server time `T` exactly when `active_from <= T < active_until_exclusive`; it is NOT_STARTED before that interval and RETIRED at equality or later. No mutable enabled/current flag, client clock or implicit “latest version” may extend that interval.

`feature_policy_json` is a non-empty array sorted in this fixed order: `ONBOARDING`, `QUICK_PLAN`, `QUICK_REVIEW`, `FIRST_INSIGHT`. It contains at most one item for each allowed feature and no item for a disallowed feature. Every item has exactly `{ "feature": enum, "requiredPracticeCount": integer }`; the count is in `0..3`, ONBOARDING requires `0`, and QUICK_PLAN requires `3`. QUICK_REVIEW and FIRST_INSIGHT may use `0..3`. `definition_sha256` is lowercase SHA-256 of the RFC 8785 form of this exact object, with `featurePolicies` byte-equal to stored `feature_policy_json`:

```json
{
  "activeFrom": "<utc-ms>",
  "activeUntilExclusive": "<utc-ms>",
  "featurePolicies": [
    { "feature": "ONBOARDING", "requiredPracticeCount": 0 },
    { "feature": "QUICK_PLAN", "requiredPracticeCount": 3 }
  ],
  "studyId": "<canonical-lowercase-uuid>",
  "studySchemaVersion": "product_measurement_study_v1",
  "studyVersion": 1
}
```

Assignment to a tenant is explicit. The stable header and its append-only versions are:

```text
ProductMeasurementStudyEnrollment
study_enrollment_id
workspace_id
owner_user_id
created_at

ProductMeasurementStudyEnrollmentVersion
study_enrollment_id
workspace_id
owner_user_id
enrollment_version
enrollment_schema_version        product_measurement_study_enrollment_v1
study_id
study_version
state                            ASSIGNED | RETIRED
effective_at
allowed_features_json
recorded_at
enrollment_version_sha256
```

The header has direct immutable Workspace ownership and `owner_user_id` MUST equal that Workspace's immutable owner. Every version byte-copies those three identity fields and uses composite same-workspace FKs. `enrollment_version` starts at 1 and is contiguous; version 1 is ASSIGNED, and the only later version allowed is one terminal RETIRED version 2. Reassignment/reactivation requires a new enrollment ID. Every version is immutable, `recorded_at <= effective_at`, RETIRED requires `recorded_at = effective_at`, and both versions byte-copy the same study tuple and `allowed_features_json`. The sorted, unique, non-empty allowed-feature array is a subset of the referenced definition's feature policies.

At trusted time `T`, the enrollment's exact active interval is `[max(ASSIGNED.effective_at, study.active_from), min(RETIRED.effective_at if present, study.active_until_exclusive))`. It is active only inside that interval and only for a feature present in both `allowed_features_json` and the pinned study definition. A Workspace/owner cannot have overlapping active enrollment intervals for the same `(study_id,feature)`. Assignment/retirement takes the Workspace and enrollment locks and rechecks ACTIVE/current deletion generation; a cross-workspace header/version/FK, another owner, an absent definition version, an overlapping assignment or an interval with empty intersection rejects atomically.

`enrollment_version_sha256` is lowercase SHA-256 of the RFC 8785 form of exactly `{ "allowedFeatures": allowed_features_json, "effectiveAt": effective_at, "enrollmentSchemaVersion": enrollment_schema_version, "enrollmentVersion": enrollment_version, "ownerUserId": owner_user_id, "state": state, "studyEnrollmentId": study_enrollment_id, "studyId": study_id, "studyVersion": study_version, "workspaceId": workspace_id }`. It includes no display label, operator note or mutable pointer.

An instrumented UX journey starts with this immutable tenant-owned header:

```text
ProductMeasurementRun
measurement_run_id
workspace_id
actor_user_id
run_schema_version              product_measurement_run_v1
study_enrollment_id
study_enrollment_version
study_enrollment_version_sha256
study_id
study_version
study_definition_sha256
feature                         ONBOARDING | QUICK_PLAN | QUICK_REVIEW |
                                FIRST_INSIGHT
run_mode                        PRACTICE | MEASURED
practice_index                  1 | 2 | 3 | null
started_at
deadline_at
start_idempotency_key
start_request_sha256
```

Its append-only lifecycle is:

```text
ProductMeasurementRunStateEvent
measurement_run_state_event_id
workspace_id
measurement_run_id
run_schema_version              product_measurement_run_v1
event_sequence                  1 | 2
event_type                      START | SUCCEED | ABANDON
terminal_product_analytics_event_id nullable
abandonment_reason_code         nullable
actor_type                      USER | SYSTEM
actor_user_id                   nullable for SYSTEM
recorded_at
idempotency_key
```

Header and events have direct immutable Workspace ownership and composite same-workspace FKs; header `actor_user_id` is the authenticated Workspace owner, the enrollment FK is `(workspace_id,study_enrollment_id)`, and the run pins one exact ASSIGNED enrollment version and its exact global study definition version. Its two stored hashes MUST byte-equal those immutable rows. Every state-event version byte-copies the header run-schema version. `(workspace_id,measurement_run_id)`, `(workspace_id,start_idempotency_key)`, `(workspace_id,measurement_run_id,event_sequence)` and `(workspace_id,idempotency_key)` are unique. Header fields never update. Sequence 1 is exactly START, has both terminal fields null, USER actor equal to the header actor, `recorded_at = started_at`, and idempotency key `measurement-run:<measurement_run_id>:start`. Sequence 2 is optional while work is open and, once present, is the sole terminal event; its idempotency key is exactly `measurement-run:<measurement_run_id>:terminal`. SUCCEED requires a non-null terminal ProductAnalyticsEvent, null reason and USER actor equal to the header actor. ABANDON requires a non-null terminal ProductAnalyticsEvent plus one exact reason from `USER_CANCELLED | NEGATIVE_DURATION | ZERO_DURATION | BACKGROUND_INTERRUPTED | MISSING_TERMINAL_EVENT | DURATION_OVER_30_MINUTES | TIMEOUT`; TIMEOUT alone requires SYSTEM/null actor, while every other reason requires USER/header actor. Reusing the terminal key with another type, event, reason or time fails `MEASUREMENT_RUN_TERMINAL_CONFLICT`.

`started_at` is the trusted synchronous start-commit time and `deadline_at = started_at + 30 minutes`; both use canonical UTC milliseconds. State at trusted as-of `T` is OPEN only with START, no terminal event and `T < deadline_at`; SUCCEEDED or ABANDONED is selected by sequence 2. With no sequence 2 and `T >= deadline_at`, semantic state is already ABANDONED/TIMEOUT and success is forbidden, even if the registered timeout worker has not yet materialized sequence 2. A timeout ProductAnalyticsEvent uses `occurred_at = deadline_at`; its `created_at` and state-event `recorded_at` equal the later guarded materialization commit. This occurrence/visibility split is deliberate. A FINAL product metric cannot publish while an applicable timeout control remains nonterminal, so delayed materialization creates a later deterministic revision rather than silently treating the run as missing.

`StartProductMeasurement` is a synchronous authenticated `start_product_measurement_v1` command. Its request admits exactly `{ "feature": enum, "startIdempotencyKey": string, "studyEnrollmentId": canonical-lowercase-uuid }`; Workspace and owner come only from the authenticated route/session. `study_id`, either version, `run_mode` and `practice_index` are forbidden request members. `start_request_sha256` is lowercase SHA-256 of RFC 8785 `{ "feature": feature, "startCommandSchemaVersion": "start_product_measurement_v1", "studyEnrollmentId": study_enrollment_id }`.

The server first looks up `(workspace_id,start_idempotency_key)`. A row with the same `start_request_sha256` returns that exact run even if the enrollment later retires; a changed digest conflicts. With no receipt, it locks Workspace, the same-workspace enrollment, and `(workspace_id,study_enrollment_id,enrollment_version,feature)` scope; captures trusted `started_at`; requires ACTIVE/current deletion generation and the authenticated user equal to both owners; resolves exactly one active ASSIGNED enrollment version and its pinned ACTIVE study definition at that time; validates the allowed-feature intersection and both stored hashes; derives mode/index as below; then allocates the server UUID and inserts header, START, and timeout control in one transaction. It performs no external request and has no deferred run-creation outbox. An unassigned ID, inactive/retired study or enrollment, disallowed feature, wrong owner, cross-workspace reference, hash/version mismatch, unknown/extra request member, or request selected before FENCE creates no header, event, job, fence or work sequence.

Run order is server-derived and closed within one pinned `(workspace_id,study_enrollment_id,enrollment_version,feature)` scope. Let `P = requiredPracticeCount` from the pinned feature policy. There is at most one OPEN run. If no MEASURED run exists and the exact terminal practice prefix is `1..k`: derive PRACTICE/index `k+1` when `k < P`, otherwise derive MEASURED/null when `k = P`. Any gap, duplicate, nonterminal predecessor, existing MEASURED run, or later practice attempt fails with no new run/control row; the database uniqueness keys are `(workspace_id,study_enrollment_id,enrollment_version,feature,practice_index)` for practice and one null-index MEASURED row for the same scope. Thus ONBOARDING always derives MEASURED, QUICK_PLAN derives exactly PRACTICE 1, 2, 3 then MEASURED, and neither client nor UI can skip, repeat or invent a step. A practice may terminalize as success or abandonment; the prerequisite is terminal completion, not a fabricated duration. A different start key while the derived step is already OPEN or MEASURED rejects `MEASUREMENT_RUN_START_CONFLICT`; retry is only the original key/digest path.

Each START transaction creates the exact TP-SEC registered work item:

| Contract member | Exact value |
|---|---|
| `work_item_type` | `PRODUCT_MEASUREMENT_TIMEOUT` |
| `subject_record_type` / key | `ProductMeasurementRun` / `{ "measurement_run_id": id }` |
| `operation_payload_json` | `{ "deadlineAt": ts, "feature": "ONBOARDING"|"QUICK_PLAN"|"QUICK_REVIEW"|"FIRST_INSIGHT", "measurementRunSchemaVersion": "product_measurement_run_v1", "operation": "TERMINALIZE_AT_DEADLINE" }` |
| COMPLETE predicate | Exact sequence-2 state event and its terminal ProductAnalyticsEvent are committed; `MEASUREMENT_RUN_SUCCEEDED` for SUCCEED or `MEASUREMENT_RUN_ABANDONED` for ABANDON |
| CANCELLED_DELETION | `WORKSPACE_DELETING`; publish no terminal state/ProductAnalyticsEvent and let PRIMARY_TENANT_DATA remove the run |

The subject key and payload admit exactly the members shown above. `deadlineAt`, `feature` and `measurementRunSchemaVersion` byte-copy the immutable run header; `operation` is the literal shown. The START transaction creates a USER-initiated control job for the authenticated header actor with exact `operation_idempotency_key = "measurement-run:<measurement_run_id>:timeout"`; the later worker executes under its approved system workload identity without rewriting that immutable initiator. Any missing, extra, changed or cross-run member rejects before ENQUEUE.

The chain has no `TenantExternalOperationLease`. A valid success or explicit client-abandon command locks run, timeout fence and Workspace, requires ACTIVE/current captured generation, and atomically inserts the terminal ProductAnalyticsEvent, sequence 2, timeout COMPLETE and terminal marker. The timeout worker may start only at or after `deadline_at`; it resolves the same fence, takes the same locks and, only if sequence 2 is absent, atomically inserts `measurement_abandoned` with reason TIMEOUT, ABANDON, COMPLETE and marker. Any guarded command that observes an OPEN run at equality or later performs or awaits that same timeout semantic operation; equality belongs to TIMEOUT. Success-first makes timeout retry return the existing terminal marker; timeout-first makes success fail without a second event. FENCE-first creates only CANCELLED_DELETION/marker after any in-flight non-external transaction loses the generation CAS. Every retained run therefore converges to exactly one sequence-2 SUCCEED or ABANDON; the sole no-terminal exception is a FENCE-winning deletion transaction whose CANCELLED_DELETION marker precedes removal of the complete run/state/event bundle. Thus no generic scheduler, client retry or post-FENCE callback can create a second/later terminal or leave a retained run permanently OPEN.

ProductMeasurementStudyEnrollment headers/versions, ProductMeasurementRun and its state prefix are retained until Workspace deletion and MUST be included in the owner's canonical Workspace export; every referenced immutable ProductMeasurementStudy definition is included as reference-closed shared configuration so event/metric replay closes. They store no client monotonic start/end sample, browser/user-agent data, free-text failure, content or source value; only the validated duration and closed reason live on the terminal ProductAnalyticsEvent/state. Run and enrollment records are excluded from every external analytics envelope, internal cross-workspace aggregate leaf and AI input; only the already minimized ProductAnalyticsEvent projection may leave first-party storage.

`ProductAnalyticsEvent` is append-only:

```text
product_analytics_event_id
workspace_id
actor_user_id                    nullable for SYSTEM
event_schema_version             product_analytics_event_v1
event_type
source_record_type               nullable for source-null measurement/control event
source_record_key_json           nullable with source_record_type
measurement_run_id               nullable outside instrumented UX journey
study_id                         nullable outside moderated study
run_mode                         PRACTICE | MEASURED | null
practice_index                   1 | 2 | 3 | null
payload_json
occurred_at                      trusted semantic/commit time
idempotency_key
created_at
```

Every event has direct immutable `workspace_id`; a domain source uses a typed same-workspace record key. `(workspace_id, product_analytics_event_id)` and `(workspace_id, idempotency_key)` are unique. For a domain-derived event, producer derives the idempotency key from event type plus RFC 8785 exact source key bytes and semantic occurrence; client measurement events additionally use unique `(workspace_id, event_type, measurement_run_id)`. A non-null `measurement_run_id` has a composite same-workspace FK to ProductMeasurementRun and requires `study_id`, `run_mode` and `practice_index` to byte-copy that header, including its exact null. All four fields are otherwise null. Order is `(occurred_at, product_analytics_event_id)`.

ProductAnalyticsEvent materialization is synchronous and closed. A server/domain producer inserts the event in the same database transaction that commits the named immutable source or its authoritative state/result transition. If that producer is a TP-SEC registered worker, the event is part of its guarded result transaction and must exist before that work item can commit COMPLETE/terminal marker. A client-originated measurement command locks Workspace, requires ACTIVE with the current deletion-guard generation, validates and inserts the event in that authenticated request transaction. Retry returns the same row. The sole scheduler-created event is `measurement_abandoned` from the exact registered PRODUCT_MEASUREMENT_TIMEOUT result transaction above; no generic queue, change-data-capture consumer, scheduler or transactional outbox may create a ProductAnalyticsEvent after the source/client transaction. An outbox may fan out only from an already committed event and may create only the registered external-delivery controls under their own ACTIVE/generation check. A source whose semantic `occurred_at` precedes its synchronous trusted `created_at`, including timeout occurrence at the immutable deadline, becomes visible only to a later metric evaluation; that occurrence/visibility gap is not permission for another deferred event materializer.

The exact event/payload allowlist is:

| event_type | Exact `payload_json` members |
|---|---|
| `onboarding_completed` | `{ "duration_ms": integer|null, "timezone_category": "UTC"|"UTC_PLUS"|"UTC_MINUS" }` |
| `plan_armed` | `{ "duration_ms": integer|null, "has_optional_fields": boolean, "revision_no": integer }` |
| `plan_proof_resolved` | `{ "match_method": string, "reason_code": string|null, "timing_state": "VERIFIED"|"AMBIGUOUS"|"LATE"|"UNMATCHED" }` |
| `import_previewed` | `{ "adapter_version": string, "error_count_bucket": "0"|"1_10"|"11_PLUS", "row_count_bucket": "1_500"|"501_10000"|"10001_100000" }` |
| `import_completed` | `{ "accounting_pending": integer, "duplicate": integer, "duration_ms": integer, "quarantined": integer, "reconciled": integer }` |
| `episode_closed` | `{ "accounting_quality": string, "fee_state": string, "plan_proof_state": string }` |
| `review_completed` | `{ "duration_ms": integer|null, "has_optional_note": boolean, "revision_no": integer }` |
| `file_selected` | `{ "file_size_bucket": "LE_1_MIB"|"GT_1_TO_10_MIB"|"GT_10_TO_20_MIB" }` |
| `insight_rendered` | `{ "duration_ms": integer, "insight_type": "METRIC"|"COMPLETE_CONTEXT" }`; `source_record_type/id` is required |
| `measurement_abandoned` | `{ "feature": "ONBOARDING"|"QUICK_PLAN"|"QUICK_REVIEW"|"FIRST_INSIGHT", "reason_code": "USER_CANCELLED"|"NEGATIVE_DURATION"|"ZERO_DURATION"|"BACKGROUND_INTERRUPTED"|"MISSING_TERMINAL_EVENT"|"DURATION_OVER_30_MINUTES"|"TIMEOUT" }` |
| `weekly_lab_opened` | `{ "eligible_sample_size_bucket": "0"|"1"|"2_29"|"30_PLUS", "report_revision_no": integer }` |
| `weekly_review_completed` | `{ "completion_lag_ms": integer, "experiment_taxonomy_id": string }` |
| `export_completed` | `{ "archive_schema_id": "tradeproof_export_v1", "byte_bucket": "LE_100_MIB"|"GT_100_MIB_TO_1_GIB"|"GT_1_GIB", "duration_bucket": "LE_1_HOUR"|"GT_1_TO_6_HOURS"|"GT_6_TO_24_HOURS"|"GT_24_HOURS" }` |
| `account_deletion_requested` | `{ "reason_taxonomy_id": string|null }` |

No other payload member is legal. `duration_ms` has exactly two clock domains. An instrumented SUCCEED for `onboarding_completed`, `plan_armed`, `review_completed` or `insight_rendered` requires an integer `duration_ms` in `1..1,800,000`, measured by one uninterrupted monotonic client clock and committed strictly before the run deadline. Its event type must match the run feature. A domain event outside a run has all measurement fields null and nullable duration null. A negative, zero, background-interrupted, missing-end or greater-than-30-minute measurement cannot SUCCEED: it terminalizes the run with the matching closed ABANDON reason above. When the underlying onboarding/plan/review command independently succeeds, that same transaction may still insert its ordinary non-instrumented domain event with null duration, but the failed measurement is represented only by the separate terminal `measurement_abandoned`; a terminal event is never relabeled or counted twice. Explicit client cancellation uses USER_CANCELLED. Only the registered deadline transition uses TIMEOUT. For `import_completed`, `duration_ms` is always the nonnegative integer millisecond difference `ImportBatch.finished_at - ImportBatch.started_at` from trusted server timestamps; it has no measurement run and is not subject to the UX cutoff. Abandoned runs remain in failure counters and are never silently dropped. `insight_rendered` must reference a same-workspace immutable metric or COMPLETE ContextSnapshot and pair with the same run's one earlier `file_selected` event.

Producer, source and measurement fields are closed by this matrix; no producer/source combination outside it is legal:

| `event_type` | Trusted producer | Required source | Measurement fields |
|---|---|---|---|
| `onboarding_completed` | onboarding command server | `Workspace` | either all null with `duration_ms = null`, or exact ONBOARDING run tuple terminalized as SUCCEED with valid duration |
| `plan_armed` | plan command server | `TradePlanRevision` | either all null with `duration_ms = null`, or exact QUICK_PLAN run tuple terminalized as SUCCEED with valid duration |
| `plan_proof_resolved` | episode projection worker | `TradeEpisodeProjection` | all null |
| `import_previewed` | import preview server | `Upload` | all null |
| `import_completed` | import worker at terminal commit | `ImportBatch` | all null |
| `episode_closed` | accounting projection worker | `TradeEpisodeProjection` | all null |
| `review_completed` | Review command server | `ReviewRevision` | either all null with `duration_ms = null`, or exact QUICK_REVIEW run tuple terminalized as SUCCEED with valid duration |
| `file_selected` | authenticated web client, accepted by analytics ingest | null | exact OPEN FIRST_INSIGHT run tuple; nonterminal and unique for the run |
| `insight_rendered` | authenticated web client after rendering a server record | `MetricSnapshot` or `ContextSnapshot` | exact FIRST_INSIGHT run tuple, same as its earlier `file_selected`, terminalized as SUCCEED |
| `measurement_abandoned` | authenticated client explicit terminal command, or registered PRODUCT_MEASUREMENT_TIMEOUT worker | null | exact matching run tuple, terminalized as ABANDON |
| `weekly_lab_opened` | authenticated web client after rendering report | `WeeklyReportRevision` | all null |
| `weekly_review_completed` | weekly-review command server | `WeeklyReviewCompletion` | all null |
| `export_completed` | export worker in the MARK_READY transaction, before EXPORT COMPLETE/terminal marker | null | all null |
| `account_deletion_requested` | deletion command server before Workspace enters deleting | `Workspace` | all null |

An instrumented event tuple is exactly `{measurement_run_id, study_id, run_mode, practice_index}` copied from one `product_measurement_run_v1` header; the header, not the event, carries and authenticates the pinned study/enrollment versions and hashes. `measurement_run_id` and `study_id` are non-null canonical lowercase RFC 9562 UUID strings; `run_mode` is `MEASURED` or `PRACTICE`; `practice_index` is respectively null or one of `1,2,3`. The ProductAnalyticsEvent actor equals the run actor for a USER transition; timeout alone uses null event actor and SYSTEM state actor. Success type is exactly ONBOARDING -> `onboarding_completed`, QUICK_PLAN -> `plan_armed`, QUICK_REVIEW -> `review_completed`, FIRST_INSIGHT -> `insight_rendered`. ABANDON always uses `measurement_abandoned`, with payload feature/reason equal to run/state. A non-null run ID is legal only on the one nonterminal FIRST_INSIGHT `file_selected` or on the exact ProductAnalyticsEvent referenced by sequence 2; an orphan, second terminal-capable event or terminal event not referenced back by the state row rejects. For a USER terminal, ProductAnalyticsEvent `occurred_at = created_at = state-event.recorded_at`; for TIMEOUT, only `occurred_at = run.deadline_at` while `created_at = state-event.recorded_at >= deadline_at`. The state event's terminal reference and event's run ID must point to each other in the same transaction. When the matrix says all null, all four measurement fields must be null. Every non-null domain source is the exact same-workspace immutable/versioned record involved, never an arbitrary type, current-pointer lookup or another tenant's key.

`source_record_key_json` is an ordinary canonical JSON object, not an escaped string. It MUST equal the referenced record's exact TP-EXP envelope `recordKey`, including every composite member. Initial source-key shapes are: Workspace `{ "workspace_id": id }`; TradePlanRevision `{ "trade_plan_revision_id": id }`; TradeEpisodeProjection `{ "episode_id": id, "projection_version": integer }`; Upload `{ "upload_id": id }`; ImportBatch `{ "import_batch_id": id }`; ReviewRevision `{ "review_revision_id": id }`; MetricSnapshot `{ "metric_snapshot_id": id }`; ContextSnapshot `{ "id": id }`; WeeklyReportRevision `{ "weekly_report_revision_id": id }`; WeeklyReviewCompletion `{ "weekly_review_completion_id": id }`. The type and key are both null or both non-null. Unknown/missing/extra key members, a key whose envelope type differs, or a key resolving outside `workspace_id` is rejected before event insert.

`export_completed.source_record_type` and `source_record_key_json` are always null because ExportJob/Attempt are non-exported delivery control-plane records. Its server producer derives `idempotency_key` from the exact non-exported `(workspace_id, export_job_id, READY event sequence)` and emits at most one event for that READY transition. The MARK_READY transaction inserts the ProductAnalyticsEvent before appending EXPORT COMPLETE/terminal marker, with `occurred_at = created_at = ExportJobStateEvent.MARK_READY.recorded_at`; any failure rolls back READY, the event and terminal control effects together. The ExportJob ID and READY event sequence are never copied into `payload_json` or any exported source-reference field. The only trusted-control-plane source-null branches are this event and SYSTEM/TIMEOUT `measurement_abandoned`; `file_selected` and non-TIMEOUT `measurement_abandoned` are the only client-originated source-null branches. Every other source-null combination rejects.

`account_deletion_requested` is ordered specially because no new work sequence may be allocated after TP-SEC FENCE. In the same user deletion transaction, while holding the identity/User/Workspace locks and while Workspace is still ACTIVE at the current generation, the server first inserts the idempotent event and, for every approved analytics processor, inserts the exact preprojection `ANALYTICS_DELIVERY` job/fence/ENQUEUE, `ProductAnalyticsExternalSuppressionReceipt`, COMPLETE event and terminal marker with reason `POLICY_EXCLUDED`. Only after all those rows are visible does the transaction capture `job_drain_watermark`; it then increments the guard, changes Workspace to DELETING and commits TP-SEC REQUEST/FENCE. The new work sequences are included in that watermark and already have byte-valid terminal markers. Any failure rolls back the event, all processor branches and deletion FENCE together; zero configured processors creates the event and no fabricated processor branch.

#### 13.1.1. External analytics projection

`product_analytics_external_v1` is the only permitted projection from a first-party `ProductAnalyticsEvent` to an external analytics processor. It is delivery-only, never a product-metric source, workspace export record or substitute for the first-party event. The restricted, non-exported immutable header is:

```text
ProductAnalyticsExternalProjection
product_analytics_external_projection_id
workspace_id
product_analytics_event_id
processor_registration_id
external_projection_schema_version       product_analytics_external_v1
pseudonym_generation                     positive integer
pseudonym_key_version
envelope_json
envelope_sha256
created_at
external_expires_at                      UTC-day-start(source created_at) + 90 * 24 hours
```

The header has direct immutable `workspace_id`, a composite same-workspace FK to `ProductAnalyticsEvent`, and unique `(workspace_id, product_analytics_event_id, processor_registration_id, external_projection_schema_version)`. The processor ID resolves one immutable TP-SEC-approved processor registration. For an externally eligible source with a selected rotation and future expiry, a projection header, its exact TP-SEC `ANALYTICS_DELIVERY` TenantControlJob/fence/ENQUEUE and its delayed `ANALYTICS_PURGE` TenantControlJob/fence/ENQUEUE are inserted atomically. The delivery job subject is the source ProductAnalyticsEvent and its payload names the same processor and schema version, so that tuple resolves exactly one header. The purge subject is the projection as specified below. No header may exist without both chains, and no chain may dispatch without its header.

Preprojection suppression is the sole no-header exception. For each approved processor evaluated for a source that is policy-excluded, has no unique valid rotation or has an expiry not after projection creation, one transaction creates the exact `ANALYTICS_DELIVERY` job/fence/ENQUEUE and its terminal COMPLETE event/marker with `safe_result_code = ANALYTICS_SUPPRESSED`, but creates no projection, purge job or external-operation lease. Its immutable reason is respectively `POLICY_EXCLUDED`, `PSEUDONYM_ROTATION_UNAVAILABLE` or `RETENTION_WINDOW_ELAPSED`; the reason is first-party restricted control evidence and is not added to the closed delivery payload or any external envelope. No other condition may use this branch. A missing projection paired with any START_EXTERNAL, locator, purge chain or nonterminal delivery is an invariant violation, not safe suppression.

That transaction also creates exactly one restricted, non-exported receipt:

```text
ProductAnalyticsExternalSuppressionReceipt
product_analytics_external_suppression_receipt_id
workspace_id
product_analytics_event_id
processor_registration_id
suppression_receipt_schema_version       product_analytics_external_suppression_receipt_v1
external_projection_schema_version       product_analytics_external_v1
suppression_reason                       POLICY_EXCLUDED | PSEUDONYM_ROTATION_UNAVAILABLE |
                                         RETENTION_WINDOW_ELAPSED
suppressed_at
retention_expires_at
suppression_receipt_sha256
```

The receipt has direct immutable `workspace_id`, composite same-workspace event/processor ownership and unique `(workspace_id, product_analytics_event_id, processor_registration_id, external_projection_schema_version)`. `suppressed_at` byte-equals the terminal delivery event `recorded_at` and terminal marker `terminal_at`; `retention_expires_at = suppressed_at + 30 * 24 hours`. `suppression_receipt_sha256` is lowercase SHA-256 of exact RFC 8785 `{ "externalProjectionSchemaVersion": "product_analytics_external_v1", "processorRegistrationId": id, "productAnalyticsEventId": id, "retentionExpiresAt": ts, "suppressedAt": ts, "suppressionReason": reason, "suppressionReceiptSchemaVersion": "product_analytics_external_suppression_receipt_v1", "workspaceId": id }`. A retry of the same TenantControlJob semantic tuple returns the byte-identical receipt; a changed reason/time/hash or a second receipt conflicts as `ANALYTICS_EXTERNAL_IDEMPOTENCY_CONFLICT`. It is removed at `retention_expires_at` or with earlier Workspace primary-data deletion, only after the terminal marker exists. It is never an external deletion-inventory member, product-metric/AI input, Workspace export record or operational-log payload; `ProductAnalyticsExternalSuppressionReceipt` and its schema-version literal are explicit TP-EXP exclusions.

Pseudonym rotations are restricted service configuration, not tenant rows or export data:

```text
ProductAnalyticsPseudonymRotation
processor_registration_id
pseudonym_generation
pseudonym_key_version
valid_from
valid_to_exclusive
created_at
```

For one processor, generations start at 1, are contiguous and have non-overlapping, gap-free half-open intervals while delivery is enabled. Each interval is nonempty and at most 30 * 24 hours. `(processor_registration_id, pseudonym_generation)` and `(processor_registration_id, pseudonym_key_version)` are unique. A projection transaction selects the sole row satisfying `valid_from <= created_at < valid_to_exclusive`; zero or multiple rows take the exact preprojection-suppression branch above with reason `PSEUDONYM_ROTATION_UNAVAILABLE`. Rotation never rewrites an existing projection. The version resolves secret HMAC key material only from the secret manager; key bytes never enter a database, queue, log, export or processor request. Old key material remains available until every projection in that generation is acknowledged or suppressed, every possible processor copy is deletion-verified, and the backup-verification window has elapsed; then it is destroyed while non-secret rotation metadata may remain for audit.

For `kind = WORKSPACE | ACTOR | EVENT`, construct this exact RFC 8785 object in memory:

```json
{
  "kind": "WORKSPACE",
  "processorRegistrationId": "...",
  "pseudonymGeneration": 1,
  "pseudonymKeyVersion": "...",
  "rawId": "..."
}
```

`token(kind,rawId) = base64url_no_pad(HMAC-SHA-256(secret_key, RFC8785(object)))`. The three external values are respectively `"paw_" + token(WORKSPACE, workspace_id)`, `"paa_" + token(ACTOR, actor_user_id)` and `"pae_" + token(EVENT, product_analytics_event_id)`. Base64url is RFC 4648 URL-safe encoding without padding and therefore has exactly 43 characters for the 32-byte HMAC. `actorPseudonym` is null exactly when `actor_user_id` is null; no token is computed from a null sentinel. Including processor, key version, generation and kind provides domain separation. A key/version may never be reused for another processor or generation.

`envelope_json` has exactly these members and no others:

```json
{
  "actorPseudonym": null,
  "eventPseudonym": "pae_...",
  "eventType": "weekly_lab_opened",
  "externalExpiresAt": "...",
  "occurredOnUtc": "2026-08-27",
  "payload": {
    "eligible_sample_size_bucket": "30_PLUS"
  },
  "pseudonymGeneration": 1,
  "schemaVersion": "product_analytics_external_v1",
  "workspacePseudonym": "paw_..."
}
```

`occurredOnUtc` is the ten-character UTC calendar date containing the source `occurred_at`; exact activity time is not sent. `external_expires_at` and envelope `externalExpiresAt` are exactly UTC `00:00:00.000Z` at the start of the source `ProductAnalyticsEvent.created_at` calendar date plus `90 * 24 hours`. This is a strict maximum that may retain for less than 90 elapsed days, and exposes only the source day; projection `created_at` milliseconds never enter the envelope. At equality the projection is inaccessible and no new dispatch/acknowledgement is legal. A delayed event whose computed expiry is not after projection creation is suppressed as `RETENTION_WINDOW_ELAPSED`. Pseudonym fields equal the derivation above. `pseudonym_key_version`, processor registration and rotation bounds stay first-party and are not envelope members. External payload eligibility is a second, narrower allowlist linked to the first-party rows above:

| Source `event_type` | Exact external `payload` |
|---|---|
| `onboarding_completed` | `{ "duration_ms": integer|null, "timezone_category": "UTC"|"UTC_PLUS"|"UTC_MINUS" }` copied exactly |
| `plan_armed` | `{ "duration_ms": integer|null, "has_optional_fields": boolean }` copied from the same-named source members |
| `plan_proof_resolved` | `{ "timing_state": "VERIFIED"|"AMBIGUOUS"|"LATE"|"UNMATCHED" }` copied exactly |
| `import_previewed` | `{ "error_count_bucket": "0"|"1_10"|"11_PLUS", "row_count_bucket": "1_500"|"501_10000"|"10001_100000" }` copied exactly |
| `import_completed` | `{ "accounting_pending": integer, "duplicate": integer, "duration_ms": integer, "quarantined": integer, "reconciled": integer }` copied exactly |
| `episode_closed` | `{ "accounting_quality": "COMPLETE"|"FEE_CONVERSION_MISSING"|"SEQUENCE_PENDING"|"REPLAY_PENDING"|"INVALID", "plan_proof_state": "VERIFIED"|"AMBIGUOUS"|"LATE"|"UNMATCHED" }` from the same source projection |
| `review_completed` | `{ "duration_ms": integer|null, "has_optional_note": boolean }` copied from the same-named source members |
| `file_selected` | `{ "file_size_bucket": "LE_1_MIB"|"GT_1_TO_10_MIB"|"GT_10_TO_20_MIB" }` copied exactly |
| `insight_rendered` | `{ "duration_ms": integer, "insight_type": "METRIC"|"COMPLETE_CONTEXT" }` copied exactly |
| `measurement_abandoned` | `{ "feature": "ONBOARDING"|"QUICK_PLAN"|"QUICK_REVIEW"|"FIRST_INSIGHT" }` copied exactly; first-party reason is omitted |
| `weekly_lab_opened` | `{ "eligible_sample_size_bucket": "0"|"1"|"2_29"|"30_PLUS" }` copied exactly |
| `weekly_review_completed` | `{ "completion_lag_bucket": "LE_24_HOURS"|"GT_24_TO_72_HOURS"|"GT_72_HOURS" }`; derive from nonnegative source `completion_lag_ms` using `<= 86,400,000`, `<= 259,200,000`, then greater |
| `export_completed` | `{ "byte_bucket": "LE_100_MIB"|"GT_100_MIB_TO_1_GIB"|"GT_1_GIB", "duration_bucket": "LE_1_HOUR"|"GT_1_TO_6_HOURS"|"GT_6_TO_24_HOURS"|"GT_24_HOURS" }` copied exactly |
| `account_deletion_requested` | Not externally eligible; take the preprojection-suppression branch with `POLICY_EXCLUDED` and make no projection/purge/request |

Every other first-party member is deliberately omitted, including `revision_no`, adapter/version, match/reason/fee codes, experiment taxonomy and archive schema. The external envelope never contains `workspace_id`, `actor_user_id`, `product_analytics_event_id`, idempotency key, source type/key, measurement/study/run ID, note, label, symbol, filename, URL, attachment, trade value, P&L or another nested object/array. Unknown event, member, type, negative lag, unsafe string or mismatch with the source event fails before enqueue; omission from this matrix is not permission to pass through a scalar.

`envelope_sha256` is lowercase SHA-256 of RFC 8785 `envelope_json`. Eligible creation locks the source event and processor, selects the rotation once, and persists the header, canonical bytes/digest and both work-control chains in one transaction; preprojection suppression follows the closed exception above. Every dispatch and retry sends the stored RFC 8785 bytes, not a rebuilt projection; a later key rotation or first-party schema deployment cannot change them. The TP-SEC provider operation token is transport metadata, not an envelope member. A retry must return the same provider result for the same token. A changed envelope/digest, selected generation or source payload under the existing semantic operation is `ANALYTICS_EXTERNAL_IDEMPOTENCY_CONFLICT` and sends no second event. No delivery dispatch may start at or after `external_expires_at - 24 hours`; a pending delivery at that boundary is safely suppressed only after deterministic lookup proves NOT_FOUND, otherwise its exact operation token/possible copy passes to the already-registered purge job.

An acknowledged delivery creates exactly one immutable restricted receipt:

```text
ProductAnalyticsExternalDeliveryReceipt
product_analytics_external_delivery_receipt_id
workspace_id
product_analytics_external_projection_id
processor_registration_id
provider_locator_ciphertext
provider_locator_key_version
provider_locator_sha256
provider_ack_receipt_sha256
acknowledged_at
```

The receipt has composite same-workspace/processor FKs and unique `(workspace_id, product_analytics_external_projection_id)`. Before encryption, the provider locator is exactly:

```json
{
  "eventDeleteHandle": "...",
  "eventLocator": "...",
  "generationDeleteHandle": "...",
  "providerExpiresAt": "..."
}
```

All three handles are nonempty opaque provider values with no NUL/control character; `providerExpiresAt` equals the projection's `external_expires_at`. `provider_locator_sha256` hashes the RFC 8785 plaintext, while authenticated encryption under `provider_locator_key_version` stores recoverable ciphertext; plaintext handles are never otherwise persisted, logged or exported. Locator-encryption keys may rotate, but each referenced version remains decryptable until every receipt using it is deletion-verified and its backup window elapses. `provider_ack_receipt_sha256` is lowercase SHA-256 of RFC 8785 `{ "envelopeSha256": envelope_sha256, "providerLocatorSha256": provider_locator_sha256, "providerOperationTokenSha256": hash, "providerStatus": "ACCEPTED" }`; the raw provider response is not retained. Status lookup by the exact TP-SEC provider-operation token MUST return byte-identical locator data and ACCEPTED evidence. Every receipt for the same `(workspace_id, processor_registration_id, pseudonym_generation)` must decrypt to the same `generationDeleteHandle`; mismatch is `ANALYTICS_PROVIDER_LOCATOR_CONFLICT`, disables further delivery to that processor and blocks a fabricated acknowledgement.

The delayed TP-SEC `ANALYTICS_PURGE` job created with the header has subject `ProductAnalyticsExternalProjection` and exact key `{ "product_analytics_external_projection_id": id }`. Its exact payload is `{ "externalExpiresAt": ts, "externalProjectionSha256": hash, "operation": "DELETE_VERIFY", "processorRegistrationId": id }`, where the time equals `external_expires_at` and the hash equals `envelope_sha256`. It resolves a committed locator receipt or every live delivery-operation token at execution time. It completes only after event-handle deletion plus provider absence verification as `ANALYTICS_COPY_ABSENT`, or after every delivery lookup proves definitive NOT_FOUND as `ANALYTICS_COPY_NEVER_CREATED`. `CANCELLED_DELETION` makes no later local commit and hands the exact processor/generation/known locator or unresolved operation token to the TP-SEC final `EXTERNAL_ANALYTICS` target. A generic or unfenced retention cron is forbidden.

Normal retention starts that purge job no later than `external_expires_at - 24 hours`, locks both work chains and prevents any new delivery dispatch. If delivery is still nonterminal, deterministic lookup either proves every operation NOT_FOUND or recovers the accepted locator; an accepted-but-uncommitted acknowledgement is treated as a possible copy, never as suppression. The first branch atomically completes delivery as safely suppressed and purge as `ANALYTICS_COPY_NEVER_CREATED`; the second deletes/verifies the copy, then atomically completes nonterminal delivery as safely suppressed and purge as `ANALYTICS_COPY_ABSENT`. An already acknowledged delivery remains historical ACKNOWLEDGED while purge removes its copy. Provider absence is required by `external_expires_at`; provider-enforced TTL at the same timestamp is defense in depth, not replacement evidence. After verification and after every delivery/purge external-operation lease is ENDED, the same final transaction creates required terminal markers and this minimal restricted receipt before removing the envelope and encrypted locator:

```text
ProductAnalyticsExternalDeletionReceipt
product_analytics_external_deletion_receipt_id
workspace_id
product_analytics_external_projection_id
processor_registration_id
pseudonym_generation
pseudonym_key_version
envelope_sha256
provider_locator_sha256
deletion_reason                       EXTERNAL_RETENTION_90D | WORKSPACE_DELETION
requested_at
verified_absent_at
provider_absence_receipt_sha256
```

`provider_absence_receipt_sha256` is lowercase SHA-256 of RFC 8785 `{ "externalProjectionSha256": envelope_sha256, "providerLocatorSha256": provider_locator_sha256, "providerStatus": "ABSENT", "verifiedAbsentAt": verified_absent_at }`. The receipt does not retain a pseudonym, payload or handle and remains only until Workspace deletion so later deletion inventory can prove already-absent generations. `(workspace_id, product_analytics_external_projection_id)` is unique. Its source projection ID is an opaque historical identity, deliberately not a permanent FK after projection removal. A never-dispatched suppressed projection has no provider receipt and may be removed without fabricating this receipt. Missing/ambiguous lookup, delete or absence evidence is `ANALYTICS_EXTERNAL_RETENTION_BREACH`: keep access disabled, alert, retry and do not claim deletion.

At the TP-SEC Workspace FENCE transaction, before primary tenant rows can be purged, the producer freezes one `product_analytics_external_deletion_inventory_v1` object for every immutable processor registration that could ever have received this workspace's data, including disabled/retired registrations. The processor set is snapshotted once for the deletion generation. Its digest is lowercase SHA-256 of RFC 8785 `{ "processorRegistrationIds": [ids], "workspaceId": id }`, where `ids` is the sorted unique array of those canonical processor-registration IDs. A real processor inventory carries that ID and the common digest. Exactly one `processorRegistrationId = "NONE"` inventory is legal only when `ids` is empty; a NONE inventory has no generations. Each real inventory contains every pseudonym generation with a possibly live/uncertain external copy or a retained prior-absence receipt. The exact object is:

```json
{
  "frozenProcessorRegistryDigestSha256": "...",
  "generations": [{
    "generationDeleteHandleSha256": null,
    "priorAbsenceReceipts": [{
      "externalProjectionId": "...",
      "providerAbsenceReceiptSha256": "...",
      "verifiedAbsentAt": "..."
    }],
    "projections": [{
      "envelopeSha256": "...",
      "externalProjectionId": "...",
      "providerLocatorCiphertext": null,
      "providerLocatorKeyVersion": null,
      "providerLocatorSha256": null,
      "providerOperationLookupDescriptors": [{
        "lookupHmacKeyVersion": "...",
        "operationOrdinal": 1,
        "processorRegistrationId": "...",
        "providerOperationTokenSha256": "...",
        "tenantWorkItemFenceId": "..."
      }]
    }],
    "pseudonymGeneration": 1,
    "pseudonymKeyVersion": "...",
    "workspacePseudonym": "paw_..."
  }],
  "inventorySchemaVersion": "product_analytics_external_deletion_inventory_v1",
  "processorRegistrationId": "...",
  "workspaceId": "..."
}
```

`projections` includes every non-suppressed header without a prior deletion receipt. `providerLocatorCiphertext`, `providerLocatorKeyVersion` and `providerLocatorSha256` are all null or all non-null; the non-null values byte-copy the acknowledged receipt's authenticated ciphertext, encryption-key version and plaintext hash. Ciphertext is encoded as canonical base64url without padding in this JSON. FENCE verifies decryption and the hash before freezing, so the locator, including its generation delete handle, remains recoverable after source-row purge. Locator-encryption key versions referenced by an inventory remain decryptable until this deletion target verifies absence and the backup-verification window elapses.

Each item of `providerOperationLookupDescriptors` has exactly the five members shown. It copies one live `ANALYTICS_DELIVERY` lease's positive ordinal, canonical fence/processor IDs, lookup-HMAC key version and token hash; processor equals the enclosing inventory, and the fence resolves the same workspace/projection delivery. The raw token is not persisted. It is re-derived after drain as `"tpw_" + base64url_no_pad(HMAC-SHA-256(key[lookupHmacKeyVersion], RFC8785({ "operationOrdinal": operationOrdinal, "providerRegistrationId": processorRegistrationId, "tenantWorkItemFenceId": tenantWorkItemFenceId, "workspaceId": enclosing workspaceId })))`, and its lowercase ASCII SHA-256 MUST equal `providerOperationTokenSha256` before lookup. A missing key or mismatch blocks deletion. Every referenced lookup-HMAC key version remains derivable until the target verifies absence and its backup-verification window elapses. Descriptors are the sorted unique set of every nonterminal lease that could have dispatched this projection, ordered by `(providerOperationTokenSha256, tenantWorkItemFenceId, operationOrdinal)`; an undispatched enqueue has an empty array. Compacted terminal delivery detail is valid only when its committed locator or definitive suppression already closes the branch.

`priorAbsenceReceipts` contains every retained receipt in that generation and no projection appears in both arrays. Each item has exactly the three members shown: `externalProjectionId` and `providerAbsenceReceiptSha256` copy the source receipt identity/hash, while `verifiedAbsentAt` byte-copies its trusted canonical `verified_absent_at`; this timestamp is deletion evidence, not a newly sampled time. `generationDeleteHandleSha256` is the lowercase hash of the consensus handle obtained from locally committed acknowledged locators at FENCE and is null when none is locally available; an unresolved possible copy remains represented by its lookup descriptor. All acknowledged rows and later accepted lookups must agree. `workspacePseudonym` is non-null exactly when `projections` is nonempty and equals their common pinned value; it is null for a prior-receipt-only generation, so expired evidence does not require retaining or re-deriving an old pseudonym/key. Generations sort numerically and both child arrays sort by `externalProjectionId`; duplicate projection IDs or receipt hashes reject. The arrays partition all local projection/deletion evidence for that processor and workspace. A NONE inventory has the exact same top-level members with `generations = []`, `processorRegistrationId = "NONE"` and the empty-set registry digest; it has no hidden branch members. The full inventory is Restricted derived personal data, never sent as-is, logged or exported; TP-SEC authenticated-encrypts its complete RFC 8785 plaintext, binds its lowercase SHA-256 to the matching `EXTERNAL_ANALYTICS` target and clears the ciphertext only after complete verification.

After JobDrainEvidence, account deletion decrypts the frozen inventory, resolves every lookup descriptor through the provider's deterministic status API and adds no unbound target. Every recovered locator must match the frozen envelope/processor/generation; every stored locator is decrypted and hash-verified; and all stored/recovered generation handles within one generation must be byte-identical. Any mismatch blocks deletion completion. For each live/uncertain generation it uses that sole handle to delete the workspace pseudonym generation, dispatching one opaque delete/verify operation per generation; a processor request MUST NOT contain two generations or otherwise link rotated workspace pseudonyms. A prior-receipt-only generation makes no new provider request and carries its prior evidence forward. The worker then verifies generation absence and every live/uncertain event locator from the frozen inventory. Exact absence evidence hashes this RFC 8785 object:

```json
{
  "currentProcessorRegistryDigestSha256": "...",
  "deletionInventorySha256": "...",
  "frozenProcessorRegistryDigestSha256": "...",
  "generationResults": [{
    "generationDeleteReceiptSha256": "...",
    "priorAbsenceReceiptSha256s": [],
    "providerEventAbsenceReceiptSha256s": [],
    "pseudonymGeneration": 1,
    "resolvedGenerationDeleteHandleSha256": "...",
    "verifiedAbsentAt": "..."
  }],
  "processorRegistrationId": "...",
  "workspaceDeletionId": "..."
}
```

`resolvedGenerationDeleteHandleSha256` and `generationDeleteReceiptSha256` are both non-null or both null. When the frozen generation's `generationDeleteHandleSha256` is non-null, the resolved field equals it. When it is null but one or more descriptor lookups return ACCEPTED, the resolved field is the lowercase SHA-256 of their byte-identical consensus `generationDeleteHandle`; every stored/recovered locator in the generation must yield that same value. With no accepted locator it is null. `generationDeleteReceiptSha256` hashes RFC 8785 `{ "deletionInventorySha256": hash, "generationDeleteHandleSha256": resolvedGenerationDeleteHandleSha256, "providerStatus": "DELETED", "pseudonymGeneration": int }`; the stored resolved member therefore makes this basis independently verifiable after inventory ciphertext is cleared. A member of `providerEventAbsenceReceiptSha256s` hashes exactly one normalized evidence object with common members `deletionInventorySha256`, `externalProjectionId`, `evidenceType`, `providerStatus` and `verifiedAt`: `EVENT_ABSENT` adds non-null `providerLocatorSha256` and status `ABSENT`; `OPERATION_NOT_FOUND` adds non-null `providerOperationTokenSha256` and status `NOT_FOUND`; `NEVER_DISPATCHED` adds no branch member, has status `NOT_DISPATCHED`, and is valid only when the frozen fence history has no START_EXTERNAL/lease. Branch-only members are mutually exclusive and unknown members reject.

Each inventory projection is covered exactly once by one EVENT_ABSENT result, by the complete set of its OPERATION_NOT_FOUND descriptor-token hashes, or by one NEVER_DISPATCHED result, while each prior receipt is copied exactly once into `priorAbsenceReceiptSha256s`. A generation with any stored or recovered accepted provider copy has both non-null generation-delete fields. Both are null only when either (a) every current projection has definitive NOT_FOUND/never-dispatched evidence copied into `providerEventAbsenceReceiptSha256s`, or (b) `projections` is empty and the prior-receipt array is nonempty; every other null combination is forbidden. For branch (b), `verifiedAbsentAt` equals the greatest source receipt `verified_absent_at`; otherwise it is the trusted current verification time and equals every newly normalized evidence object's `verifiedAt`. Result rows sort by `pseudonymGeneration`, receipt-hash arrays sort as lowercase ASCII, and no receipt hash is duplicated across generations.

`frozenProcessorRegistryDigestSha256` copies the inventory member. At final post-drain verification, `currentProcessorRegistryDigestSha256` is recomputed from the same closed registry object and MUST equal it; a changed processor set is an incident and requires TP-SEC versioned target-set remediation rather than omission. A real processor evidence row has its canonical ID and the complete result partition above. A NONE evidence row has `processorRegistrationId = "NONE"`, both registry digests equal the empty-set digest and `generationResults = []`; it is valid only when both the frozen and current set are empty, and it never fabricates provider/generation evidence. Unknown members reject. The evidence digest becomes the TP-SEC target's processor receipt hash. Missing generation, projection, descriptor, prior receipt, locator/key, delete result, absence result or registry proof prevents `EXTERNAL_ANALYTICS` target success and Workspace deletion completion.

`WorkspaceProductMetricSnapshot` is the only tenant-owned/exportable product-metric snapshot:

```text
workspace_product_metric_snapshot_id
workspace_id
schema_version                    workspace_product_metric_snapshot_v1
metric_dictionary_version         product_metrics_v1
metric_id
revision_no
status                            PROVISIONAL | FINAL
window_start_at
window_end_at_exclusive
evaluation_as_of_at
dimension_json
dimension_sha256
value_type                        DECIMAL | INTEGER | DURATION_MS | OBJECT
value_decimal                     nullable
value_integer                     nullable
value_duration_ms                 nullable
value_object_json                 nullable
numerator_integer                 nullable
denominator_integer               nullable
null_reason                       nullable
included_source_refs_json
excluded_source_refs_json
exclusion_reason_counts_json
input_event_digest_sha256
supersedes_snapshot_id            nullable
created_at
```

It obeys the tenant/composite-FK rule in section 3.4. `dimension_json` has one of only two exact shapes; unknown/extra/missing members reject:

```json
{ "dimensionType": "OVERALL" }
{ "dimensionType": "STUDY", "studyId": "<canonical-lowercase-uuid>" }
```

The metric-to-dimension matrix is closed:

| Metric IDs | Required dimension and source coupling |
|---|---|
| `quick_plan_duration_ms`, `quick_review_duration_ms`, `time_to_first_insight_ms` | `STUDY`; every selected ProductAnalyticsEvent closes one `product_measurement_run_v1` MEASURED run with the same non-null `study_id = studyId`, matching feature, exact pinned study/enrollment versions and valid immutable hashes; no cross-study aggregate is a Workspace snapshot |
| `verified_pre_fill_plan_coverage`, `weekly_review_completion_rate`, `import_reconciliation_coverage`, `net_metric_episode_exclusion_rate`, `weekly_active_retained_users_w4`, `weekly_active_retained_users_w8`, `episode_count_change_after_adoption` | `OVERALL`; sources follow their domain population and an instrumented ProductAnalyticsEvent with non-null `study_id` cannot be substituted |

`dimension_sha256` is lowercase SHA-256 of exact RFC 8785 `dimension_json`; no display label, cohort/member key, arbitrary filter or free text belongs in the dimension. Revisions start at 1 and are contiguous/unique per `(workspace_id, metric_id, window_start_at, window_end_at_exclusive, dimension_sha256)`; the same `input_event_digest_sha256` returns the same row. Exactly one typed value field is non-null when value exists; null has all value fields null and stable `null_reason`.

An item in `included_source_refs_json` is exactly `{ "sourceRecordKey": {}, "sourceType": "<registered-record-type>" }`. An item in `excluded_source_refs_json` is exactly `{ "reasonCode": "...", "sourceRecordKey": {}, "sourceType": "<registered-record-type>" }`. Each `sourceRecordKey` equals the referenced TP-EXP envelope `recordKey`; this includes composite TradeEpisodeProjection `{ "episode_id": id, "projection_version": integer }`. Included items sort by `(sourceType, RFC8785(sourceRecordKey) unsigned UTF-8 bytes)`; excluded items add `reasonCode` as the final Unicode-code-point tie-breaker. The arrays have no duplicate `(sourceType, canonical key bytes)`, are disjoint and partition the selected source population; `exclusion_reason_counts_json` is the exact count of excluded items grouped by `reasonCode`, with keys sorted by Unicode code point. Arrays contain only this workspace's immutable/version-specific canonical domain or ProductAnalyticsEvent references. Snapshot revisions are immutable; a late event creates a new revision. `FINAL` requires the measurement window ended and every selected domain job/run terminal at `evaluation_as_of_at`.

`InternalAggregateProductMetricSnapshot` is service-owned, restricted validation evidence across workspaces and is never user-exportable:

```text
internal_aggregate_product_metric_snapshot_id
schema_version                    internal_aggregate_product_metric_snapshot_v1
metric_dictionary_version         product_metrics_v1
metric_id
aggregate_cohort_key
revision_no
window_start_at
window_end_at_exclusive
evaluation_as_of_at
status                            PROVISIONAL | FINAL
value_object_json                 nullable
source_workspace_count
eligible_contribution_count
excluded_contribution_count
exclusion_reason_counts_json
minimum_privacy_cohort_size       10
null_reason                       nullable
contribution_digest_sha256
contribution_digest_key_version
supersedes_snapshot_id            nullable
created_at
```

It has no `workspace_id`, user/member key, business-record ID, included/excluded member list, symbol, label, P&L or free text. It consumes only immutable WorkspaceProductMetricSnapshot typed contributions/digests, never cross-tenant raw records or direct ProductAnalyticsEvent rows. `aggregate_cohort_key` names one approved immutable candidate set in the restricted analytics store; membership change requires a new key, and a dynamic "all current users" query cannot be reused under the same key. The candidate definition and its member mapping are not copied into this snapshot or any export.

The restricted store has exactly these non-exported records:

```text
InternalAggregateCohortDefinition
aggregate_cohort_key
purpose_code
window_start_at
window_end_at_exclusive
dimension_sha256
membership_digest_sha256
membership_digest_key_version
created_at
expires_at

InternalAggregateCohortMember
aggregate_cohort_key
workspace_id
included_at
eligibility_basis_code
expires_at

InternalAggregateCohortRetirement
aggregate_cohort_key
retirement_schema_version          internal_aggregate_cohort_retirement_v1
retirement_cycle_token
retirement_cycle_hmac_key_version
retired_at
retirement_sha256
```

`aggregate_cohort_key` is random opaque and encodes no workspace/member data. A definition is immutable; `(aggregate_cohort_key, workspace_id)` is unique, all member expiry values equal definition expiry. Creation locks every candidate Workspace in canonical ID order, requires each to be ACTIVE at its current deletion-guard generation, then writes the complete member set and definition atomically. No member may be added/changed under that key, and a concurrent FENCE therefore either observes the committed mapping or makes creation reject before insert.

`InternalAggregateCohortRetirement` is append-only restricted mapping control, not a member row or aggregate output. It has a same-key FK to the immutable definition and exactly one row per `aggregate_cohort_key`; `(retirement_cycle_hmac_key_version, retirement_cycle_token)` is also unique. FENCE captures one current restricted HMAC-key version for its whole transaction, then derives each row's token only in memory as:

```text
retirement_cycle_token =
  "iar_" + base64url_no_pad(
    HMAC-SHA-256(
      key[retirement_cycle_hmac_key_version],
      UTF8("internal_aggregate_cohort_retirement_v1\u0000") ||
      RFC8785({
        "aggregateCohortKey": aggregate_cohort_key,
        "workspaceDeletionGuardGeneration": positive integer,
        "workspaceDeletionId": canonical deletion ID,
        "workspaceId": canonical workspace ID
      })
    )
  )
```

The token is exactly prefix `iar_` plus 43 unpadded base64url characters. Including `aggregateCohortKey` domain-separates two definitions retired by the same deletion cycle, so neither token equality nor stored bytes link those definitions or reveal the cycle. Without the restricted key, the token is computationally unlinkable to its raw input IDs; no database FK, index or direct raw-ID lookup is defined. The HMAC input tuple and its raw Workspace/deletion IDs and guard generation are used transiently by FENCE but are never copied into this row, a row key, retirement-specific audit metadata or another aggregate object; separately governed TP-SEC deletion evidence is not referenced from the retirement. `retired_at` equals the first retiring deletion's trusted FENCE event `recorded_at`, copied in the same transaction without retaining that event/deletion reference. `retirement_sha256` is lowercase SHA-256 of exact RFC 8785 `{ "aggregateCohortKey": aggregate_cohort_key, "retiredAt": retired_at, "retirementCycleHmacKeyVersion": retirement_cycle_hmac_key_version, "retirementCycleToken": retirement_cycle_token, "retirementSchemaVersion": "internal_aggregate_cohort_retirement_v1" }`. Unknown/missing/extra members, a malformed token or a hash mismatch reject.

The current deletion derives the same token on retry; under the aggregate-key lock, matching token/version/time/hash returns the byte-identical row and any changed byte conflicts. If another Workspace in an already-retired cohort later enters deletion, its FENCE treats the existing valid retirement as the terminal predicate and does not compare, create or rewrite a token for the later cycle. The restricted HMAC key material is resolvable only by the deletion/aggregate-integrity roles and never enters storage, logs or export. A referenced key version remains available until every retirement row using it has been removed and the TP-SEC backup-verification window has elapsed; rotation never rewrites an existing row.

The Workspace FENCE transaction queries the still-restricted membership for every definition containing that workspace, acquires the same per-`aggregate_cohort_key` serialization locks in canonical key order, and inserts or validates each retirement before FENCE can commit and before any member removal. An aggregate calculation may read inputs outside its publish transaction, but final publication locks the definition and that retirement-key gap under the same serialization primitive, requires no retirement row and `now < definition.expires_at`, then inserts the next snapshot revision in that transaction. Thus publisher-first may commit a non-identifying revision before FENCE; FENCE-first or an intervening FENCE makes publish reject and no later revision can use that key. Crash/retry cannot leave FENCE committed without all matching keys retired, and member deletion never substitutes for the retirement predicate.

The retirement row contains no `workspace_id`, WorkspaceDeletion ID, guard generation, user/member ID, member token/leaf, contribution or raw-ID-bearing idempotency key. Its token is Restricted pseudorandom control evidence, not an identity or an allowed lookup into WorkspaceDeletion, its tombstone, audit or membership data. It never appears in `InternalAggregateProductMetricSnapshot`, a Workspace export, product analytics or an external/AI request. It remains until its definition and every snapshot revision under the key are no longer publishable and have reached their retention expiry; cleanup then removes retirement last with the definition, subject to the TP-SEC backup window.

For each member, construct this exact RFC 8785 leaf with canonical RFC 3339 millisecond timestamps:

```json
{
  "aggregateCohortKey": "...",
  "eligibilityBasisCode": "...",
  "expiresAt": "...",
  "includedAt": "...",
  "workspaceId": "..."
}
```

The lowercase member token is `HMAC-SHA-256(secret_key, RFC8785(member_leaf))`. Sort tokens as lowercase ASCII, then compute `membership_digest_sha256` as lowercase SHA-256 of:

```json
{
  "aggregateCohortKey": "...",
  "dimensionSha256": "...",
  "memberTokens": ["..."],
  "membershipDigestKeyVersion": "...",
  "purposeCode": "...",
  "windowEndAtExclusive": "...",
  "windowStartAt": "..."
}
```

`secret_key` is resolved only from secret manager version `membership_digest_key_version`. Member leaves/tokens and secret bytes are never persisted, logged or exported. Rotation creates new definitions with the new key version; an existing definition/digest is never recomputed. Old key material remains restricted and available through definition expiry plus backup-verification window, then is destroyed. Before expiry and absent a deletion tombstone, independent recomputation from restricted member rows must equal the stored digest; version missing/mismatch fails closed.

`expires_at` is no later than the earlier of approved study expiry and `window_end_at_exclusive + 365 days`. Only the product-validation aggregation service role may resolve member rows; every read/change is content-free audited. No row/token/key is sent to an external analytics/AI processor or workspace export. Workspace deletion removes its member row from primary storage within 24 hours after atomically persisting the retirement above and emits the normal account-deletion backup tombstone; an existing non-identifying aggregate is not rewritten. Removal is an intentional privacy break in later membership recomputation and is proven by the deletion audit/tombstone, not by retaining the deleted mapping. Any later calculation uses a new candidate-set key. Definition/member/retirement rows expire from backup within the TP-SEC 30-day backup window and restore applies deletion tombstones and retirement state before any aggregate worker can run.

If fewer than 10 distinct candidate workspaces have eligible contributions, value is null with `PRIVACY_COHORT_TOO_SMALL`. Revisions start at 1, are immutable/contiguous per `(metric_id, aggregate_cohort_key, window)` and use the digest as idempotency input; a later evaluation creates a superseding row. Access is restricted to product validation operators, logged without content; aggregate evidence retention is 365 days after the window unless an approved release-evidence policy requires a shorter period. It is excluded from `weekly_lab_export_projection_v1` and every workspace export.

External analytics receives only rotating pseudonymous keys plus the allowlisted scalar event payload above. It never receives source record IDs, notes, symbols, P&L, labels, filenames or attachment URLs; first-party events remain authoritative.

### 13.2. Source cutoff, digest and arithmetic

For a WorkspaceProductMetricSnapshot, source occurrence and source visibility are separate. A record enters a metric window by the exact occurrence field below and is queryable at an evaluation as-of only by the exact visibility field/lifecycle below:

| Metric | Exact source records | Window occurrence | Visible at `evaluation_as_of_at` when |
|---|---|---|---|
| `verified_pre_fill_plan_coverage` | active-as-of `TradeEpisodeProjection`; latest exact-projection `EpisodeMetricEligibilityEvent` when one exists | projection `closed_at` in `[window_start_at, window_end_at_exclusive)` | projection `created_at <= as-of < superseded_at`, with null superseded as +infinity; eligibility event `recorded_at <= as-of` |
| `weekly_review_completion_rate` | eligible `WeeklyCohort`; its `WeeklyReviewCompletion` when one exists | cohort `cohort_end_at_utc` in the metric window; completion is tested against that cohort's strict deadline | cohort `locked_at <= as-of`; completion `recorded_at <= as-of` |
| `quick_plan_duration_ms` | QUICK_PLAN MEASURED ProductMeasurementRun, pinned study definition/enrollment version, its terminal state event and exact `plan_armed` or `measurement_abandoned` terminal ProductAnalyticsEvent | terminal event `occurred_at` | run START and sequence 2 exist; pinned hashes/assignment-time interval are valid; terminal event `created_at <= as-of`; timeout control is terminal for FINAL |
| `quick_review_duration_ms` | QUICK_REVIEW MEASURED ProductMeasurementRun, pinned study definition/enrollment version, its terminal state event and exact `review_completed` or `measurement_abandoned` terminal ProductAnalyticsEvent | terminal event `occurred_at` | same study/enrollment/run/event/control closure as Quick Plan |
| `time_to_first_insight_ms` | FIRST_INSIGHT MEASURED ProductMeasurementRun, pinned study definition/enrollment version, its one `file_selected`, terminal state event and exact `insight_rendered` or `measurement_abandoned` terminal ProductAnalyticsEvent | `file_selected.occurred_at` starts the window occurrence; terminal must share its run | every selected event is visible, pinned hashes/assignment-time interval are valid, sequence 2 exists, and timeout control is terminal for FINAL |
| `import_reconciliation_coverage` | admitted `ImportBatch` with terminal status `COMPLETE`, `PARTIAL` or `NEEDS_ATTENTION` | `finished_at` in the metric window | terminal `finished_at <= as-of`; `finished_at` is assigned in the same commit as final status/counters |
| `net_metric_episode_exclusion_rate` | active-as-of `TradeEpisodeProjection`; latest exact-projection `EpisodeMetricEligibilityEvent` when one exists | projection `closed_at` in the metric window | same projection/event lifecycle as verified plan coverage |
| `weekly_active_retained_users_w4` / `w8` | `onboarding_completed` plus qualifying activity ProductAnalyticsEvents | each event `occurred_at` in its exact day-28/day-35 or day-56/day-63 interval | every selected event has `created_at <= as-of`; FINAL also requires as-of at or after the interval end |
| `episode_count_change_after_adoption` | first on-time `WeeklyReviewCompletion`; active-as-of `TradeEpisodeProjection`; latest exact-projection eligibility event when one exists | completion `completed_at` defines adoption; projection `closed_at` enters the exact pre/post interval | completion `recorded_at <= as-of`; projection/event use the same lifecycle above; FINAL requires the full post window ended |

`TradeEpisodeProjection.created_at/superseded_at` is owned by `TP-ACC`; `WeeklyReviewCompletion.recorded_at`, `ImportBatch.finished_at`, WeeklyCohort `locked_at`, ProductMeasurementRunStateEvent `recorded_at` and ProductAnalyticsEvent `created_at` are their authoritative commit/visibility fields. A row with an occurrence in the past but a visibility time after the evaluation as-of is absent until a later metric revision. Revisioned sources resolve to the greatest version whose active interval contains the as-of; mutable current-pointer lookup and ingestion/order time substitution are forbidden. For a journey metric, the one terminal ProductAnalyticsEvent record key is its included or excluded population member; its same-workspace run header, pinned enrollment version, referenced study definition, complete state prefix and timeout terminal marker are mandatory closure, not extra population members. A missing/mismatched run, feature, study/enrollment version or hash, assignment-time active interval, practice prefix, state event, deadline or marker is invalid source data and blocks FINAL rather than mapping to NO_MEASURED_RUN. Selected included/excluded refs cover this exact population. For these arrays, "included" means consumed as a valid metric input, not necessarily that the candidate contributes to a numerator; for example, an eligible cohort without an on-time completion remains an included denominator source.

`input_event_digest_sha256` is the lowercase SHA-256 of this RFC 8785 canonical object; the historical field name covers both events and immutable domain refs:

```json
{
  "dimension": {},
  "evaluationAsOfAt": "...",
  "excludedSourceRefs": [
    { "reasonCode": "...", "sourceRecordKey": {}, "sourceType": "..." }
  ],
  "exclusionReasonCounts": { "<reason-code>": 1 },
  "includedSourceRefs": [
    { "sourceRecordKey": {}, "sourceType": "..." }
  ],
  "metricDictionaryVersion": "product_metrics_v1",
  "metricId": "...",
  "schemaVersion": "workspace_product_metric_snapshot_v1",
  "windowEndAtExclusive": "...",
  "windowStartAt": "..."
}
```

`dimension` is exact `dimension_json`; timestamps are canonical UTC RFC 3339 milliseconds. Arrays and reason-map keys use the order above. Revision/status/value fields and `created_at` are outputs and are not in this input digest. A retry with the same complete object returns the same snapshot; any source-set, reason, dimension, window or evaluation-as-of change creates a different digest and, if persisted, the next revision.

For an InternalAggregateProductMetricSnapshot, the immutable InternalAggregateCohortDefinition pins the exact window and `dimension_sha256`; snapshot metric/window/dimension must match it. Each mapped candidate workspace resolves the greatest visible `FINAL` WorkspaceProductMetricSnapshot revision matching metric/window/dimension with `created_at <= evaluation_as_of_at`. Missing, non-final or invalid contributions become excluded candidates with stable reasons `MISSING_WORKSPACE_SNAPSHOT`, `WORKSPACE_SNAPSHOT_NOT_FINAL` or `INVALID_TYPED_CONTRIBUTION`. The aggregator transiently constructs one leaf per candidate:

```json
{
  "eligibility": "INCLUDED",
  "exclusionReason": null,
  "inputEventDigestSha256": "...",
  "revisionNo": 1,
  "typedContribution": {
    "denominatorInteger": null,
    "numeratorInteger": null,
    "valueDecimal": null,
    "valueDurationMs": 1234,
    "valueInteger": null,
    "valueObject": null,
    "valueType": "DURATION_MS"
  },
  "workspaceId": "...",
  "workspaceProductMetricSnapshotId": "..."
}
```

`typedContribution` always has those seven members; exactly the source snapshot's one active typed value field is non-null, while numerator/denominator are additionally non-null only for a ratio. For an excluded leaf, the object has the same seven top-level members, `eligibility = "EXCLUDED"`, a non-null `exclusionReason` and `typedContribution = null`. Snapshot ID/revision/digest are all null for `MISSING_WORKSPACE_SNAPSHOT`; otherwise all three identify the rejected snapshot. The leaf token is lowercase `HMAC-SHA-256(secret_key, RFC8785(leaf))`. `secret_key` comes from the secret manager version named by `contribution_digest_key_version`; key bytes and leaves/tokens are never persisted, logged or exported. Existing revisions remain pinned when a key rotates.

Sort leaf tokens as lowercase ASCII and compute `contribution_digest_sha256` as lowercase SHA-256 of this RFC 8785 object:

```json
{
  "aggregateCohortKey": "...",
  "contributionDigestKeyVersion": "...",
  "contributionTokens": ["..."],
  "evaluationAsOfAt": "...",
  "metricDictionaryVersion": "product_metrics_v1",
  "metricId": "...",
  "schemaVersion": "internal_aggregate_product_metric_snapshot_v1",
  "windowEndAtExclusive": "...",
  "windowStartAt": "..."
}
```

`source_workspace_count` equals leaf count; included/excluded counts and reason counts derive exactly from the leaves. Production persistence retains only the final digest, key-version identifier and aggregate counts/value, never candidate IDs or tokens. Golden tests use a committed conformance-only HMAC key and version; production keys never enter fixtures.

Product ratio values are `ratio18(numerator, denominator)`: denominator zero produces the stated null reason; otherwise divide the exact integers, round once to scale 18 with `ROUND_HALF_EVEN`, normalize negative zero and strip trailing zeros. Weighted aggregates sum integer numerators/denominators before this single division. For `episode_count_change_after_adoption`, each user's sortable value remains the exact rational `(postCount - preCount) / preCount`; rationals sort by exact cross-multiplication, an even-N median averages the two exact rationals, and only the final workspace `definedRatio` or internal `medianDefinedRatio` is rounded with `ratio18`. Duration median uses integer milliseconds and is therefore an exact integer or half-integer canonical decimal; nearest-rank P90 remains an exact integer. Overflow or an attempted binary-float path fails `PRODUCT_METRIC_ARITHMETIC_INVALID` without a snapshot.

### 13.3. Exact definitions

| Metric ID | Type | Exact contract | Persisted outputs |
|---|---|---|---|
| `verified_pre_fill_plan_coverage` | Product KPI | For REGULAR cohorts, verified/frozen episodes divided by `north_star_episode_eligible` episodes, using the exact episode eligibility and integer counts from `TP-ACC`; null when denominator is zero. | Workspace ratio snapshot; optional internal weighted aggregate from numerator/denominator only. |
| `weekly_review_completion_rate` | Product KPI | REGULAR eligible user-weeks with WeeklyReviewCompletion satisfying the strict TP-ACC 72-hour deadline divided by REGULAR eligible user-weeks; transition and fewer-than-3-eligible weeks are outside both numerator and denominator. | Workspace ratio snapshot; optional internal weighted aggregate. |
| `quick_plan_duration_ms` | Validation only | In a moderated usability run, start when the preset-first form is interactive and required defaults are loaded; end when successful ArmPlan response is rendered. After three practice runs, take one measured valid run per participant. Report standard median and nearest-rank P90 across at least 10 target participants. | Workspace measured-run duration contribution; internal object `{ n, medianMs, p90Ms, abandonedCount }`. |
| `quick_review_duration_ms` | Validation only | Start when the closed-episode Review form with preset taxonomies is interactive; end when successful completed Review response is rendered. Take one measured valid run per participant; report standard median and nearest-rank P90 across at least 10 target participants. | Workspace measured-run duration contribution; internal object `{ n, medianMs, p90Ms, abandonedCount }`. |
| `time_to_first_insight_ms` | Validation/performance | Start at the browser file-selection event; end when the UI first renders a non-null deterministic metric or COMPLETE ContextSnapshot with an episode drill-down after confirmed import. Preview counts, progress and raw P&L rows are not an insight. Runs abandoned before render are failures, not silently excluded. | Workspace journey duration contribution; internal object `{ n, medianMs, p90Ms, abandonedCount }`. |
| `import_reconciliation_coverage` | Operational/product | Across selected confirmed batches, `sum(RECONCILED + DUPLICATE) / sum(nonblank data_rows)` using TP-ACC row dispositions. Aggregate by weighted row counts, never mean of batch percentages; null if total denominator is zero. | Workspace ratio snapshot; internal aggregate sums row numerators/denominators. |
| `net_metric_episode_exclusion_rate` | Product quality | Active closed projections in the interval that fail `net_eligible`, divided by all active closed projections in the interval. User-audited exclusion and every accounting reason remain in numerator reason counters; null when there is no closed projection. | Workspace ratio snapshot; internal aggregate sums episode numerators/denominators. |
| `weekly_active_retained_users_w4` | Product validation | Count onboarding-cohort users with at least one server-confirmed `plan_armed`, `import_completed`, `review_completed` or `weekly_lab_opened` event in `[onboarding_at + 28d, onboarding_at + 35d)`. Evaluate only after day 35. | Workspace FINAL integer contribution `0|1`; internal aggregate-only count, no member list. |
| `weekly_active_retained_users_w8` | Product validation | Same active-event rule in `[onboarding_at + 56d, onboarding_at + 63d)`; evaluate only after day 63. | Workspace FINAL integer contribution `0|1`; internal aggregate-only count, no member list. |
| `episode_count_change_after_adoption` | Safety validation | `adoption_at` is first on-time WeeklyReviewCompletion. Count active closed episodes by `closed_at` in `[adoption_at - 28d, adoption_at)` and `[adoption_at, adoption_at + 28d)`. Per user, compute `(post - pre) / pre` only when `pre > 0`; report users with `pre = 0` separately. The pilot gate uses the standard median of per-user defined ratios after the full post window. | Workspace object `{ preCount, postCount, definedRatio }`; internal object `{ definedUserCount, preZeroUserCount, medianDefinedRatio }`. |

Workspace ratio metrics use `value_type = DECIMAL`, canonical decimal `value_decimal`, and integer numerator/denominator. Retention contributions use `INTEGER`; measured journey contributions use `DURATION_MS`; adoption uses `{ "definedRatio": decimal-string|null, "postCount": integer, "preCount": integer }` with null ratio when `preCount = 0`. Internal ratio objects are exactly `{ "denominator": integer, "numerator": integer, "value": decimal-string|null }`; retention is `{ "count": integer }`; duration is `{ "abandonedCount": integer, "medianMs": decimal-string, "n": integer, "p90Ms": integer }`; adoption is `{ "definedUserCount": integer, "medianDefinedRatio": decimal-string|null, "preZeroUserCount": integer }`. No object admits extra members. Every undefined value carries a stable null reason rather than zero/empty substitution.

The Workspace output/null matrix is closed. Every PROVISIONAL row has all four value fields and both integer count fields null with `null_reason = EVALUATION_NOT_FINAL`; it never publishes a partial KPI as a value. FINAL rows obey:

| Metric IDs | `value_type`; semantic unit | FINAL value/count coupling | Only allowed FINAL null branch |
|---|---|---|---|
| `verified_pre_fill_plan_coverage` | DECIMAL; ratio | numerator/denominator are nonnegative eligible episode counts, numerator <= denominator; denominator > 0 requires `value_decimal = round18(numerator/denominator)` and null reason | denominator = 0, numerator = 0, value null, `NO_ELIGIBLE_EPISODE` |
| `weekly_review_completion_rate` | DECIMAL; ratio | numerator/denominator are nonnegative eligible REGULAR user-week counts, numerator <= denominator; denominator > 0 uses one final round18 and null reason | denominator = 0, numerator = 0, value null, `NO_ELIGIBLE_USER_WEEK` |
| `import_reconciliation_coverage` | DECIMAL; ratio | numerator is sum RECONCILED+DUPLICATE rows and denominator is admitted nonblank rows, both nonnegative and numerator <= denominator; denominator > 0 uses one final round18 and null reason | denominator = 0, numerator = 0, value null, `NO_ADMITTED_IMPORT_ROW` |
| `net_metric_episode_exclusion_rate` | DECIMAL; ratio | numerator is non-net-eligible closed projections and denominator all selected closed projections, nonnegative/numerator <= denominator; denominator > 0 uses one final round18 and null reason | denominator = 0, numerator = 0, value null, `NO_CLOSED_EPISODE` |
| `quick_plan_duration_ms`, `quick_review_duration_ms`, `time_to_first_insight_ms` | DURATION_MS; integer milliseconds | the unique valid MEASURED run is SUCCEEDED with matching positive terminal-event `duration_ms`; `value_duration_ms` equals it, all other value/count fields and null reason are null | all value/count fields null with `MEASUREMENT_ABANDONED` exactly when that run is ABANDONED and its timeout chain is terminal; `NO_MEASURED_RUN` only when no MEASURED header exists |
| `weekly_active_retained_users_w4`, `weekly_active_retained_users_w8` | INTEGER; workspace contribution | after the exact interval, `value_integer` is exactly 0 or 1; all other value/count fields and null reason null | no onboarding source: all value/count fields null, `NO_ONBOARDING_EVENT` |
| `episode_count_change_after_adoption` | OBJECT; episode counts and ratio | after full post window, exact object has nonnegative counts; if pre > 0, `definedRatio = round18((post-pre)/pre)` and top null reason null; if pre = 0, `definedRatio = null` and top `null_reason = PRE_PERIOD_ZERO`; all scalar/count fields null | before an adoption source exists: all value/count fields null, `NO_ADOPTION_EVENT` |

The adoption `PRE_PERIOD_ZERO` row is the sole case where a non-null typed value object coexists with a non-null top-level null reason, because the object counts are defined while its nested ratio is not. For all other rows, a non-null typed value requires null `null_reason`; a whole-value null requires one exact reason above. Ratio integer counts remain populated in the denominator-zero branch so readers can distinguish real zero population from missing data. All divisions use section 3.2 arbitrary-precision rational arithmetic and one scale-18 half-even round; client, analytics and exporter may not recompute with binary float or mean-of-ratios.

For a sorted sample of size N, standard median is the middle value for odd N and arithmetic mean of the two middle values for even N. Nearest-rank P90 is element `ceil(0.90 * N)` in 1-based ascending order.

The north-star `verified_review_week_rate` is not redefined here; its only authoritative formula/version is in `TP-ACC`.

## 14. Canonical report export projection

### 14.1. Projection contents

`weekly_lab_export_projection_v1` emits these record sets for the exporting workspace:

1. all WeeklyCohort headers and state events;
2. all TimezoneChangeSchedule records and state events;
3. all WeeklyCohortInputRevision rows, including superseded revisions;
4. all non-north-star MetricSnapshots and every referenced source ID;
5. all WeeklyReport headers, revisions and state events, including superseded revisions;
6. all BehavioralExperiment headers, revisions and state events;
7. all WeeklyReviewCompletion events from `TP-ACC` with report/experiment foreign keys;
8. all immutable taxonomy versions referenced by the exported report/experiment records;
9. all same-workspace ProductMeasurementStudyEnrollment headers/versions, ProductMeasurementRun headers/state prefixes, ProductAnalyticsEvent and WorkspaceProductMetricSnapshot revisions, plus every immutable ProductMeasurementStudy definition referenced by those enrollment/run versions; never an InternalAggregateProductMetricSnapshot;
10. no AiRun, AiOutput, AiOutputReference or synthetic report-to-AI reverse-link record; those TP-SEC records live only in TP-EXP's AI/consent entry and close references in the direction AiRun/AiOutputReference -> exact WeeklyReportRevision/MetricSnapshot.

### 14.2. Projection invariants

- Export uses stored canonical values and hashes; it does not rerun accounting, context, metrics or rendering.
- All report revisions, not only the current revision, are exported.
- Every foreign key resolves inside the same export or is an explicitly typed reference to a canonical record set supplied by another domain export.
- `weekly_lab_export_projection_v1` bytes are independent of whether an AI run exists; TP-EXP may discover inbound AI references for archive closure but MUST NOT inject a reverse field/record into this projection.
- Timestamps use UTC RFC 3339 milliseconds; local boundaries retain timezone/TZDB/resolution metadata; decimals use canonical strings.
- Records sort by aggregate ID then revision number. Map keys and nested arrays follow the deterministic rules in section 3.
- Round-trip into an empty validation store preserves IDs, revision chains, current/superseded projections, source references, values and hashes byte-for-byte after canonicalization; cached/request-time renderer output is absent.
- A deleted/tombstoned source attachment or AI output is represented by its allowed tombstone/reference state; the export does not resurrect deleted binary content.

`TP-EXP` must define archive file names, envelope version, manifest, checksums, encryption, convenience CSV and signed delivery. It may package this projection but must not alter TP-LAB record semantics.

## 15. Error, retry and observability contract

### 15.1. Stable errors

| Error code | Scope | Required behavior |
|---|---|---|
| `COHORT_NOT_ENDED` | Initial lock | Reject before cohort end; no input revision |
| `COHORT_INTERVAL_CONFLICT` | Schedule/change | Reject overlap, gap or non-positive interval |
| `TIMEZONE_CHANGE_ALREADY_SCHEDULED` | Timezone | Reject second active schedule |
| `STALE_COHORT_INPUT_REVISION` | Correction | Reject stale base revision; no partial report |
| `REPORT_CORE_INPUT_NOT_READY` | Report | Core accounting refs incomplete or dangling; retry safely |
| `REPORT_DEPENDENCY_VERSION_MISMATCH` | Report | Do not publish mixed tuple; enqueue approved replay |
| `REPORT_IDEMPOTENCY_CONFLICT` | Report | Same idempotency key with different logical input; reject |
| `STALE_EXPERIMENT_REVISION` | Experiment | Reject stale confirm/edit |
| `EXPERIMENT_TARGET_INVALID` | Experiment | Reject transition, non-next or locked target |
| `EXPERIMENT_ALREADY_ACTIVE` | Experiment | Reject second current confirmed experiment for target |
| `WEEKLY_REVIEW_PRECONDITION_FAILED` | Completion | Reject ownership, report, experiment or cohort mismatch atomically |
| `MEASUREMENT_STUDY_UNAVAILABLE` | Product measurement | Reject missing/unassigned/not-yet-active/retired/disallowed/wrong-owner/cross-workspace enrollment without revealing which condition failed; create no partial row/work sequence |
| `MEASUREMENT_STUDY_INTEGRITY_CONFLICT` | Product measurement | Reject invalid interval, overlap, version/hash/policy mismatch or ambiguous active assignment; disable starts for that scope until corrected by a new immutable definition/enrollment |
| `MEASUREMENT_RUN_START_CONFLICT` | Product measurement | Reject changed start retry, duplicate OPEN/MEASURED run or invalid mode/index without a partial header/control chain |
| `PRACTICE_PREREQUISITE_MISSING` | Product measurement | Reject gapped/open/missing Quick Plan practice or practice after MEASURED; create no run/work sequence |
| `MEASUREMENT_RUN_TERMINAL_CONFLICT` | Product measurement | Preserve the first exact success/abandon terminal; reject wrong feature/reason/event/time or a second terminal effect |
| `ANALYTICS_EXTERNAL_IDEMPOTENCY_CONFLICT` | External analytics | Reject changed envelope/generation/source bytes under an existing semantic delivery; send no second event |
| `ANALYTICS_PROVIDER_LOCATOR_CONFLICT` | External analytics | Disable processor delivery and retain restricted evidence; do not acknowledge inconsistent handles |
| `ANALYTICS_EXTERNAL_RETENTION_BREACH` | External analytics | Keep access disabled, page privacy owner and retry fenced delete/verification; never fabricate absence |

User-facing errors expose the stable code and a safe Vietnamese message, not raw content, source payload or cross-workspace IDs.

### 15.2. Required operational metrics

- cohort schedule/lock lag and failed interval validation;
- report generation duration, retry, dead-letter and current revision age;
- context panel status and exclusion counts by stable reason;
- dependency tuple mismatch and replay age;
- MetricSnapshot null/exclusion counts by metric ID/version;
- experiment propose/confirm/cancel counts by taxonomy ID without OTHER text;
- weekly completion lag bucket without raw business IDs;
- export projection validation/round-trip failure;
- supporting product metric snapshot version and late-event revision count;
- ProductMeasurementRun open/timeout-materialization lag, study/enrollment rejection, terminal conflicts and timeout safe-result count by feature/study version, without enrollment, run or actor ID;
- external analytics delivery/suppression, rotation age, purge deadline, locator conflict and deletion-inventory coverage by processor/generation, without raw ID, pseudonym or handle labels.

## 16. Golden fixtures and acceptance gates

All fixtures freeze the UTC clock, timezone/TZDB, source revisions, dependency tuple and expected canonical hashes. No fixture calls a live network or AI processor.

| ID | Fixture | Exact expected behavior |
|---|---|---|
| `TP-LAB:G01_regular_hcm` | Asia/Ho_Chi_Minh Monday boundary | Exact local/UTC half-open bounds; boundary episode assigned once |
| `TP-LAB:G02_dst_spring` | America/New_York spring DST week | Regular local Mondays, 167-hour UTC duration, no gap |
| `TP-LAB:G03_dst_fall` | America/New_York fall DST week | Regular local Mondays, 169-hour UTC duration, no overlap |
| `TP-LAB:G04_timezone_change_west` | UTC to America/New_York | Change effective at next old-zone Monday; short clipped transition; contiguous new regular cohort |
| `TP-LAB:G05_timezone_change_east` | UTC to Asia/Ho_Chi_Minh | Long clipped transition to first new-zone Monday; no duplicate/gap; transition excluded from north-star/completion |
| `TP-LAB:G06_lock_retry` | Lock job retried 10 times | One cohort input revision and one digest |
| `TP-LAB:G07_mixed_core_versions` | Two core accounting algorithm tuples | Publication fails `REPORT_DEPENDENCY_VERSION_MISMATCH`; no report revision |
| `TP-LAB:G08_late_review` | Review revision recorded after initial as-of | Initial report unchanged; explicit correction creates revision 2 using new Review revision |
| `TP-LAB:G09_context_outage_recovery` | No context at publish, later COMPLETE snapshots | Revision 1 publishes non-context sections + UNAVAILABLE; revision 2 inherits as-of/accounting refs, supersedes v1 and adds context |
| `TP-LAB:G10_report_retry` | Publish command retried 10 times | Same report revision ID/content hash; no skipped revision number |
| `TP-LAB:G11_nulls_and_zero` | Zero value, denominator-zero and no-sample metrics | Zero retained; null reasons exact; no NaN/Infinity/fallback zero |
| `TP-LAB:G12_stable_ties` | Equal labels, timestamps and metric values | Exact secondary ID ordering; identical hash across locales/processes |
| `TP-LAB:G13_context_separation` | Four phase/timeframe snapshots plus wrong projection/workspace candidates | Four panels; ENTRY/5m primary; only exact workspace/episode/projection/event/tuple matches selected; no mixed phase/timeframe/version |
| `TP-LAB:G14_small_sample` | N = 0, 1, 2, 29, 30 | Exact null/evidence labels and comparative-copy suppression |
| `TP-LAB:G15_counterexample_positive` | Positive median with negative episodes | Strongest opposite-sign result selected by exact ordering |
| `TP-LAB:G16_counterexample_negative_zero` | Negative, zero and no-opposite groups | Positive exception only for negative; zero/no candidate emits exact null reason; no fabricated result |
| `TP-LAB:G17_counterexample_dedup` | One episode qualifies for several observations | At most three unique episodes following observation priority and next-candidate rule |
| `TP-LAB:G18_experiment_uniqueness` | Concurrent confirms and stale edit | Exactly one current confirmed next-regular experiment; loser gets stable error; retry idempotent |
| `TP-LAB:G19_completion_preconditions` | Wrong/superseded report, transition, wrong target, valid refs | Invalid commands have no effect; valid command persists exact report/experiment FKs; TP-ACC deadline test remains authoritative |
| `TP-LAB:G20_correction_cross_boundary` | Corrected projection crosses Monday and invalidates prior Review | Both affected input/report revisions activate atomically; no current duplicate/missing episode; old-projection Review is `RECONFIRM_REQUIRED` and excluded until exact-projection reconfirmation |
| `TP-LAB:G21_export_round_trip` | All current/superseded artifacts and tombstones | Canonical projection round-trips with identical refs, hashes and values; no disposable renderer payload is persisted/exported |
| `TP-LAB:G22_ai_disabled` | All AI flags false | Same deterministic report content/hash; no outbound AI request |
| `TP-LAB:G23_product_metrics_replay` | Duplicate events, occurrence-before-created_at events, a late-committed ImportBatch and projection whose occurrence is inside an old window, exact ratios `1/3` and `2/3`, even rational adoption median; start/retry every feature/mode, change bytes under one start key, retry one semantic run under another key, Quick Plan practice 1..3, gap/duplicate/open/post-MEASURED attempts, all abandonment reasons, valid success, explicit abandon and exact deadline `-1ms`/equality/`+1ms`; race success/timeout/FENCE, mutate run tuple/feature/state prefix/deadline, USER initiator, start/timeout idempotency key, job subject/payload, terminal safe-result code and marker, crash source and export MARK_READY transactions, then attempt deferred event materialization | Header+START+PRODUCT_MEASUREMENT_TIMEOUT chain co-commit once or are absent; byte-identical start retry returns the same run while changed or semantic-duplicate attempts reject; practice/MEASURED uniqueness and order hold. Exactly one success or abandonment wins, equality is TIMEOUT, timeout occurrence equals deadline, late created-at controls visibility, every reason is closed and no OPEN applicable job permits FINAL. FENCE-first yields only cancellation/no later event. `export_completed` co-commits before its marker. Old evaluation excludes not-yet-visible sources; later as-of creates deterministic revisions and exact rational/HALF_EVEN values; every duplicate/cross-feature/second-terminal/deferred mutation rejects |
| `TP-LAB:G24_privacy_projection` | Ten workspaces plus notes, symbol, IDs, ProductMeasurementRun/state rows and OTHER text; export one workspace; repeat internal aggregate at N=9 | Workspace export contains only its own run lifecycle/events/snapshots and closes every run/event ref; internal row retains final HMAC digest/key version but no member IDs/leaves/tokens; external payload omits run/study IDs, reason and all non-allowlisted scalars; N<10 returns privacy null |
| `TP-LAB:G25_renderer_golden` | Every section state, numeric/null/sample/copy template and JSON-pointer location | Exact request-time `weekly_lab_renderer_v1` token JSON across independent implementations; report content hash depends only on canonical section/snapshot data, never cached renderer bytes |
| `TP-LAB:G26_delayed_cohort_events` | Read before start, inside interval without OPEN, exact end before LOCK, delayed scheduler, then atomic LOCK; select an in-memory header/OPEN tick before Workspace FENCE and run both lock orders plus a generation mutation before commit | Resolver yields SCHEDULED/OPEN/LOCK_PENDING/LOCKED at exact half-open boundaries; delayed OPEN is skipped/rejected; LOCK_PENDING has null lock fields and no initial report; export pointer at each cutoff matches. Scheduler-first commits its complete transaction before FENCE; FENCE-first or generation mismatch commits no header/OPEN, and no queue/outbox writes afterward |
| `TP-LAB:G27_same_millisecond_state_order` | Cohort OPEN/LOCK, timezone SCHEDULE/APPLY, report PUBLISH/SUPERSEDE and experiment PROPOSE/CONFIRM share one millisecond; opaque IDs sort reverse commit order; race APPLY/header/supersede transaction against Workspace FENCE and crash before commit | Aggregate sequences remain contiguous/nondecreasing; source replay, current API and export pointers select greatest visible sequence independent of ID lexical order. APPLY either commits every state/header/supersede under ACTIVE/current generation before FENCE or none of them; no post-FENCE/post-transaction result materializes |
| `TP-LAB:G28_membership_digest_rotation` | Fixed restricted members/timestamps with conformance membership keys v1/v2 and retirement-cycle HMAC keys r1/r2; race one workspace's FENCE against candidate-set creation and aggregate publication; crash/retry FENCE before/after each retirement insert, retire two definitions in one cycle, delete a second member later, and mutate token prefix/length/value, HMAC key version, trusted time or hash; scan serialized rows/keys and attempt unkeyed/direct lookup joins using raw Workspace/deletion IDs, guard generation, tombstone and audit | Exact leaf HMAC tokens, membership digest, per-definition `iar_` retirement tokens and retirement hash match golden bytes across independent producers; membership v1 remains pinned/verifiable, rotation creates a new definition, and no leaf/secret persists or exports. Publisher-first may commit before FENCE; FENCE-first/intervening FENCE atomically persists exact `internal_aggregate_cohort_retirement_v1` for every matching key and rejects every later revision. Same-cycle tokens differ by definition, retry is byte-identical under its pinned key version, a later member deletion does not rewrite the first retirement, and missing/rotated key material fails closed. No raw Workspace/deletion ID, guard generation or ID substring is present in any retirement field/key/hash basis exposed at rest; without the restricted key there is no FK/index/direct join from the token to WorkspaceDeletion, tombstone, audit, member or export data |
| `TP-LAB:G29_behavior_taxonomy_bytes` | Two independent seed producers build `behavioral_experiment_v1` from the frozen seven rows and same published timestamp | Version/item keys, contiguous orders, canonical version hash and TP-EXP SHARED_PUBLIC records are byte-identical; changed label/order/metric under reused version is rejected |
| `TP-LAB:G30_revision_hash_bases` | Fixed WeeklyReportRevision and BehavioralExperimentRevision IDs/payloads with every nullable branch; mutate every basis/lifecycle field once | Two producers emit exact RFC 8785 basis bytes/SHA-256; every basis mutation changes hash, lifecycle-only status/event/published-time mutation does not rewrite immutable content hash, missing/extra/null-shape mutation rejects |
| `TP-LAB:G31_input_revision_closure` | Initial lock, late correction and context-only recovery; all Review statuses, four context slot statuses/reasons, availability decisions and taxonomy types; mutate order/null/key/as-of/lock | Exact typed arrays, four-slot/decision matrix, taxonomy closure and input digest bytes match; correction sees new sources at revision lock while recovery preserves old as-of; every dangling/cross-workspace/order/null/time/status mutation rejects |
| `TP-LAB:G32_metric_snapshot_digest` | Every dimension/value type including INTERVAL, null/display/evidence branch, BREACH_TYPE and context coverage OBJECT with multi-reason exclusions/sources; duplicate/drop/reorder/substitute one key/counter/value | Candidate partition, interval bounds, coverage partition, typed source closure, counts and RFC 8785 digest match; all one-field/order/dangling/cross-workspace/binary-float mutations reject |
| `TP-LAB:G33_product_metric_dimensions` | Every product metric with exact OVERALL/STUDY dimension, two studies sharing a window, lowercase UUID boundaries and dimension/source/hash mutations | Closed metric-to-dimension matrix, same-study source coupling, RFC 8785 dimension hash, revision identity and input digest match across producers; arbitrary filter/label, cross-study source, wrong dimension, UUID case and every unknown/extra/member mutation reject |
| `TP-LAB:G34_metric_contract_matrix` | Every report/supporting metric across every allowed dimension/policy, repeating R/count/fee ratios, all null/display branches and one mutation of formula version, policy, unit, type, numerator/denominator and reason | Exact `metrics_decimal_v1` bytes and closed metric/formula/policy/dimension/type/unit/null mapping match; unrounded-R reuse, unknown pair, denominator drift, wrong context/review population and every single-field mutation reject before report hash |
| `TP-LAB:G35_product_metric_output_matrix` | Every product metric in PROVISIONAL and each FINAL value/null branch; ratio count boundaries/half-even ties, valid/abandoned/missing runs, retention 0/1, adoption pre zero/nonzero; mutate type/value/count/reason/status | Exact typed field, numerator/denominator, `product_metrics_v1` round18 and closed null coupling match two producers; partial KPI publication, mean-of-ratios, binary float, wrong reason, extra typed field and every PRE_PERIOD_ZERO exception mutation reject |
| `TP-LAB:G36_external_analytics_rotation_deletion` | Every externally eligible event, USER/SYSTEM actor, two processors and three rotation generations; retry the same event across an exact rotation boundary and crashes before/after provider dispatch/ack; exercise all three preprojection-suppression reasons and mutate suppression tuple/reason/time/TTL/hash or add header/purge/lease; race account deletion at each event/per-processor suppression/terminal-marker/watermark/FENCE boundary; exercise source-day expiry `-1ms`/equality/`+1ms`, normal purge, FENCE with acknowledged/pending/prior-absence rows and exact empty/NONE registry; make a frozen-null descriptor lookup return ACCEPTED, compact source work/receipt rows, then mutate resolved generation-handle hash, locator ciphertext/key version/hash, lookup fence/ordinal/processor/key version/token hash, prior-receipt `verifiedAbsentAt`, registry digest, every inventory/evidence member and order | Independent producers emit exact HMAC/envelope/RFC 8785 digest bytes; same-event retry retains the pinned generation while a new boundary event rotates and different processors never share a token. Suppression atomically persists exact `product_analytics_external_suppression_receipt_v1`, terminal marker and no header/purge/lease; receipt retry is byte-identical, expires at 30 days and never enters export/inventory. Account deletion atomically creates its event plus every POLICY_EXCLUDED branch before watermark capture; all are present+terminal at FENCE or none commits, and no post-FENCE work allocation/event insert occurs. Eligible delivery has exactly one purge chain before dispatch. After source-detail purge, the encrypted inventory still decrypts each acknowledged locator, re-derives each byte-identical `tpw_` token, persists the frozen-or-recovered consensus `resolvedGenerationDeleteHandleSha256` and retains the trusted prior-absence timestamp; every key/hash/time/membership mismatch blocks deletion and receipt revalidation after ciphertext clear. Envelope expiry is exact source UTC-day start + 90 days and leaks no activity millisecond; each deletion generation is dispatched separately, every possible copy is partitioned once, and NONE succeeds only with equal frozen/current empty-registry digests. Internal/source IDs, exact activity time, omitted scalar/free text and account-deletion event never reach the processor; every mutation, missing chain/generation or ambiguous absence prevents acknowledgement/target completion |

## 17. Definition of Done

Implementation is complete only when:

1. Cohort scheduling is gap-free/overlap-free under regular weeks, DST and timezone changes.
2. Transition cohorts are visibly labeled and excluded from north-star, experiment and completion flows.
3. Cohort input, MetricSnapshot and report revisions are immutable, idempotent and fully traceable.
4. Every current report revision has one homogeneous dependency tuple and no dangling core artifact.
5. Every numeric claim resolves to one MetricSnapshot with formula version, N, included/excluded projection refs and reasons.
6. Context panels use only COMPLETE snapshots and never mix phase, timeframe, algorithm or parameter set.
7. All seven sections follow exact order, sorting, null, sample and copy rules.
8. Counterexamples follow the opposite-sign algorithm and emit no result when none exists.
9. Exactly one confirmed no-signal experiment can target the next regular cohort, and WeeklyReviewCompletion stores exact report/experiment revisions.
10. Context outage, correction and late Review produce new report revisions without changing historical reports.
11. The report-specific canonical export projection passes lossless round-trip and cross-workspace authorization tests.
12. ProductMeasurementRun/state, ProductAnalyticsEvent and workspace snapshots replay deterministically; registered timeout control closes every started journey exactly once, and cross-workspace aggregates meet minimum-cohort/privacy rules and are never included in a workspace export. External analytics uses only pinned `product_analytics_external_v1` bytes, rotating pseudonyms, fenced delivery/purge and complete processor-generation deletion evidence.
13. All `TP-LAB:G01` through `TP-LAB:G36` fixtures and relevant `TP-AT`/`TP-SEC` gates pass.

Any change to cohort boundaries, input selection, denominator, section recipe, counterexample selection, experiment taxonomy, renderer output or export semantics requires a versioned contract change and updated golden fixtures. Silent behavior changes under the v1 identifiers are forbidden.
