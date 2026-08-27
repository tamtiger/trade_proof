# Canonical Export Contract

- **Document ID:** `TP-EXP`
- **Version:** 1.0.0
- **Status:** Implementation baseline
- **Updated:** 2026-08-27
- **Scope:** Workspace export request, point-in-time capture, canonical archive, manifest, delivery handoff and round-trip conformance

## 1. Purpose and normative ownership

This document is the authoritative implementation contract for producing one portable, internally consistent TradeProof workspace export. It owns archive packaging, the canonical envelope and entry layout, the manifest and checksums, cutoff consistency, delivery handoff, and round-trip conformance.

The words **MUST**, **MUST NOT**, **SHOULD** and **MAY** are normative.

The exact identifiers owned or consumed by this contract are:

| Purpose | Exact identifier | Rule |
|---|---|---|
| Archive and root envelope schema | `tradeproof_export_v1` | Present in the manifest and every canonical file |
| Canonical record envelope | `tradeproof_export_record_envelope_v1` | Wraps every record in every canonical record set |
| Manifest schema | `tradeproof_export_manifest_v1` | Present only in `manifest.json` |
| Identity/account record-set schema | `tradeproof_export_identity_accounts_v1` | Present only in its canonical entry |
| Catalog/taxonomy record-set schema | `tradeproof_export_catalogs_taxonomies_v1` | Present only in its canonical entry |
| Import record-set schema | `tradeproof_export_imports_v1` | Present only in its canonical entry |
| Plan/review record-set schema | `tradeproof_export_plans_reviews_v1` | Present only in its canonical entry |
| Accounting record-set schema | `tradeproof_export_accounting_v1` | Present only in its canonical entry |
| Context/provenance record-set schema | `tradeproof_export_context_v1` | Present only in its canonical entry |
| AI/consent record-set schema | `tradeproof_export_ai_consent_v1` | Present only in its canonical entry |
| Attachment metadata schema | `tradeproof_export_attachments_v1` | Present only in its canonical entry |
| Attachment binary schema | `tradeproof_export_attachment_binary_v1` | Present on every retained binary manifest entry |
| As-of pointer schema | `tradeproof_export_pointers_v1` | Present only in its canonical entry |
| Durable tombstone schema | `tradeproof_export_tombstones_v1` | Present only in its canonical entry |
| Convenience CSV profile | `tradeproof_export_csv_v1` | Present in the manifest for every CSV entry |
| Export request/job contract | `tradeproof_export_job_v1` | Persisted on ExportRequest, ExportJob and ExportAttempt |
| First-party export notice contract | `tradeproof_export_control_feed_v1` | Persisted only on restricted noncanonical control-feed notices |
| AI artifact contract | `ai_artifact_v1` | Governs AiRun/input, AiOutputSubject/state, AiOutput/reference and confirmation projections |
| Transcript output schema | `transcript_draft_v1` | Exact TRANSCRIPT_DRAFT AiOutput schema |
| Taxonomy output schema | `taxonomy_suggestion_v1` | Exact TAXONOMY_SUGGESTION AiOutput schema |
| Weekly-summary output schema | `weekly_summary_v1` | Exact WEEKLY_SUMMARY AiOutput schema |
| Round-trip reader profile | `tradeproof_export_round_trip_v1` | Used only by conformance tooling |
| Release performance profile | `export_conformance_profile_v1` | Used only by release evidence |
| Standard SLA eligibility envelope | `export_sla_envelope_v1` | Classifies a snapshot for the 24-hour READY commitment |
| Weekly Lab projection | `weekly_lab_export_projection_v1` | Owned by `TP-LAB`; packaged unchanged here |
| Product measurement run schema | `product_measurement_run_v1` | Owned by `TP-LAB`; persisted on exported run headers/events |
| Import preview proof schema | `import_preview_v1` | Owned by `TP-ACC`; copied into ImportBatch while temporary preview rows stay excluded |
| Staged fill schema | `staged_fill_v1` | Owned by `TP-ACC`; persisted on exported unresolved/resolved multiplicity candidates |
| Taxonomy confirmation request schema | `taxonomy_suggestion_confirmation_request_v1` | Owned by `TP-SEC`; persisted on exported taxonomy confirmations |

These identifiers have no aliases. In particular, implementations MUST NOT emit `export-v1`, `tradeproof-export-v1`, `weekly-lab-v1` or an unversioned `latest` value. A semantic or layout change requires a new applicable identifier and compatibility work under section 14.

### 1.1. Authority boundaries

- `TP-EXP` owns the ZIP byte format, entry names, canonical export envelopes, cutoff and closure algorithm, manifest, checksums, export job behavior, conformance reader and release performance profile.
- `TP-SEC` owns authentication, authorization, tenant isolation, retention/deletion policy, audit policy, signed URL lifetime and the security/delivery commitment for STANDARD snapshots. This contract defines the exact STANDARD eligibility envelope and makes its 24-hour commitment measurable without weakening those controls or treating the envelope as a product quota.
- `TP-ACC` owns imports, plans, reviews, episode/accounting semantics and exact versions `binance_spot_trade_history_csv_v1`, `import_preview_v1`, `import_row_error_detail_v1`, `staged_fill_v1`, `normalized_fill_v1`, `episode_projection_v1`, `plan_proof_v1`, `fee_conversion_v1`, `wac_episode_v1`, `metrics_v1`, `metrics_decimal_v1`, `mean_r_ci_95_v1` and `verified_review_week_rate_v1`.
- `TP-ACC` also owns setup versions `setup_preset_v1`, `setup_label_key_v1` and `plan_checklist_v1`.
- `TP-MCE` owns context and public market provenance semantics, including `mce-binance-spot-v1.0.0`, `mce-default-v1` and `market_bar_as_of_v1`.
- `TP-LAB` owns weekly semantics and exact versions `weekly_lab_v1`, `weekly_lab_renderer_v1`, `metric_snapshot_v1`, `behavioral_experiment_v1`, `weekly_lab_export_projection_v1`, `product_measurement_run_v1`, `product_metrics_v1`, `product_analytics_event_v1`, `workspace_product_metric_snapshot_v1`, `internal_aggregate_product_metric_snapshot_v1` and `internal_aggregate_cohort_retirement_v1`; the last two identify strictly non-exported cross-workspace evidence.
- `TP-LAB` also owns restricted external analytics identifiers `product_analytics_external_v1`, `product_analytics_external_suppression_receipt_v1` and `product_analytics_external_deletion_inventory_v1`; section 7.9 excludes every associated control record and manifest value.
- `TP-SEC` owns upload/attachment version `upload_attachment_v1`, consent version `ai_consent_v1`, AI artifact version `ai_artifact_v1`, confirmation request version `taxonomy_suggestion_confirmation_request_v1` and output schemas `transcript_draft_v1`, `taxonomy_suggestion_v1` and `weekly_summary_v1`.
- The exporter copies stored canonical domain projections. Apart from replaying the closed pointer matrix in section 4.3, it MUST NOT recalculate accounting, context, metrics, report content, AI output or other business state.
- The round-trip reader is validation/import tooling for an isolated empty namespace. This contract does not require or authorize a production restore endpoint.

## 2. Exact meaning of a lossless export

For this contract, **lossless** means:

> Every durable canonical workspace-owned record visible at one export cutoff, every immutable or superseded durable history record needed to interpret it, its as-of current pointers, every still-retained owned binary, and the minimum reference-closed shared public records needed to verify those records are present with canonical values, IDs, revisions, versions and hashes.

Lossless does not mean that expired or deliberately deleted bytes can be recreated. In particular:

- a raw CSV purged under `TP-SEC` is not exported and cannot be restored from its row hash;
- deleted attachment bytes, AI output content, raw voice bytes or transcript-draft content are represented only by an allowed durable tombstone when such a tombstone exists; the payload-free `AiOutputSubject` header and state stream remain Restricted derived personal identity/lifecycle evidence and never restore content or become anonymous;
- an HTTP response body is not required when `TP-MCE` retains canonical bar content and the response hash/provenance needed for offline verification;
- operational/security logs, credentials and temporary job state are not canonical workspace content.

The archive MUST NOT use the words `restorable`, `original CSV included` or equivalent unless the exact retained bytes in question are present and verified. The manifest exposes `losslessScope = DURABLE_CANONICAL_STATE_AT_CUTOFF` and a sorted `purgedPayloadClasses` array so consumers cannot mistake hashes or tombstones for source bytes.

## 3. Export request, authorization and lifecycle

### 3.1. ExportRequest and ExportJob

An authenticated workspace owner creates an `ExportRequest`:

```text
export_request_id
workspace_id
actor_user_id
job_contract_version       tradeproof_export_job_v1
export_schema_id           tradeproof_export_v1
idempotency_key
requested_at
request_parameters_hash
```

V1 has no caller-selectable data subset or format. Its canonical request parameters are exactly:

```json
{
  "archiveSchemaId": "tradeproof_export_v1",
  "includeCanonicalJson": true,
  "includeConvenienceCsv": true,
  "includeRetainedAttachments": true
}
```

`request_parameters_hash` is lowercase SHA-256 of the RFC 8785 bytes of that object. `(workspace_id, idempotency_key)` is unique. A retry with the same key and hash returns the same request/job; the same key with different parameters fails `EXPORT_IDEMPOTENCY_CONFLICT`.

One `ExportJob` belongs to the request:

```text
export_job_id
export_request_id
workspace_id
job_contract_version       tradeproof_export_job_v1
state                      QUEUED | SNAPSHOTTING | MATERIALIZING | VALIDATING |
                           READY | EXPIRING | FAILED | CANCELLED | EXPIRED
service_class              UNCLASSIFIED | STANDARD | OVERSIZE
sla_envelope_id            export_sla_envelope_v1
sla_due_at                 nullable unless STANDARD
sla_classified_at          nullable until snapshot preflight completes
current_attempt_no          nullable only while QUEUED before START_SNAPSHOT
created_at
started_at                 nullable
ready_at                   nullable
terminal_at                nullable
error_code                 nullable
archive_object_version     null before MARK_READY; copied from selected attempt and retained after EXPIRED
archive_sha256             null before MARK_READY; copied from selected attempt and retained after EXPIRED
archive_size_bytes         null before MARK_READY; copied from selected attempt and retained after EXPIRED
archive_created_at         null before MARK_READY; copied from selected attempt
archive_expires_at         null before MARK_READY; exactly archive_created_at + 24 hours
```

`archive_object_version` is a server-generated opaque application handle for exactly one immutable `(provider object key, provider version/generation)` pair. It is unique within a workspace, contains no provider locator or content-derived bytes, and resolves through a restricted mapping for exact-version GET/delete/absence verification. Every attempt receives a new handle even if a provider reuses an ETag/version lexeme on another key; the mapping may be removed only after the handle's absence evidence and retained audit predicates pass.

Job creation also registers the exact restricted `TP-SEC` materialization work-control chain in one transaction with ExportRequest/ExportJob and QUEUE:

```text
TenantControlJob
  tenant_control_job_id
  workspace_id                       equals ExportJob.workspace_id
  work_item_type                     EXPORT
  subject_record_type                ExportJob
  subject_record_key_json            { "export_job_id": export_job_id }
  subject_record_key_sha256          SHA-256(RFC8785(subject_record_key_json))
  operation_payload_schema_version   tenant_control_job_payload_v1
  operation_payload_json             { "operation": "MATERIALIZE" }
  operation_payload_sha256           SHA-256(RFC8785(operation_payload_json))
  operation_idempotency_key          "export-materialize:" + exact canonical export_job_id
  created_at

TenantWorkItemFence
  tenant_work_item_fence_id
  workspace_id                       equals ExportJob.workspace_id
  work_sequence                      positive contiguous workspace sequence
  work_item_type                     EXPORT
  work_item_record_type              TenantControlJob
  work_item_record_key_json          { "tenant_control_job_id": tenant_control_job_id }
  work_item_record_key_sha256        SHA-256(RFC8785(work_item_record_key_json))
  captured_guard_generation          current positive Workspace deletion_guard_generation
  created_at
```

Sequence-1 ENQUEUE is inserted with that chain. The fence has the exact composite FK to `(workspace_id,tenant_control_job_id,EXPORT)`. Creation validates the control-job subject key against the locked same-workspace ExportJob, but deliberately creates no permanent subject FK because terminal compaction or subject deletion may remove either side at different times. The live registry is unique on `(workspace_id,operation_payload_schema_version,EXPORT,operation_idempotency_key)` and `(workspace_id,EXPORT,work_item_record_key_sha256)`; semantic uniqueness is `(workspace_id,operation_payload_schema_version,EXPORT,ExportJob,subject_record_key_sha256,operation_payload_sha256)`. Before enqueue, same-version dedup checks both live detail and compacted terminal markers. Exact retry against live detail returns the existing control job/fence; against a marker it returns the persisted ExportJob effect/deleted-result response and allocates no new work sequence. The TP-EXP producer never supplies a different operation key for the same job. Within one payload-schema namespace, a matching idempotency HMAC with changed semantic digest fails `TENANT_CONTROL_JOB_IDEMPOTENCY_CONFLICT`; a matching semantic digest returns the existing effect even under a distinct generic key. A future payload schema is a separate namespace and never aliases a v1 key/digest. Scalar/unknown/extra/cross-type keys, a payload other than the one exact object, bad adjacent hashes, duplicate/missing chain or mismatched workspace fail before enqueue. TenantControlJob, fence, events and external-operation leases are control-plane evidence and never archive entries.

State transitions are append-only `ExportJobStateEvent` records. The fields above are a derived projection. Valid transitions are:

```text
QUEUED -> SNAPSHOTTING -> MATERIALIZING -> VALIDATING -> READY -> EXPIRING -> EXPIRED
   |          |                |               |
   +----------+----------------+---------------+-> FAILED
   +----------+----------------+---------------+-> CANCELLED
              ^----------------+
                 RESTART_ATTEMPT
```

`FAILED`, `CANCELLED` and `EXPIRED` are terminal. `READY` is delivery-ready and can transition only to inaccessible cleanup state `EXPIRING`; EXPIRING can transition only to EXPIRED after verified exact-version deletion. Neither can return to processing. Retry of a failed request creates a new ExportRequest and job; an internal race restart is an additional ExportAttempt under the same non-terminal pre-READY job as section 4.5 defines.

Attempt selection is a database invariant. `(workspace_id, export_job_id, attempt_no)` is unique and every attempt has composite FK `(workspace_id, export_job_id)` to its job. A non-null job selector `(workspace_id, export_job_id, current_attempt_no)` has a composite FK to exactly one attempt. QUEUED before START_SNAPSHOT has null `current_attempt_no` and no attempt. START_SNAPSHOT resolves ExportJob -> its exact TenantControlJob subject key -> its exact fence key, locks Workspace, rechecks ACTIVE/current generation equal to `fence.captured_guard_generation`, atomically creates attempt 1 and selects it. SNAPSHOTTING, MATERIALIZING and VALIDATING require exactly the selected attempt to be RUNNING and forbid any other RUNNING attempt for the job. MARK_READY atomically changes that selected attempt to SUCCEEDED, assigns its `finished_at`, and derives READY; READY, EXPIRING and EXPIRED retain that same selected SUCCEEDED attempt. A terminal pre-READY job has no RUNNING attempt; its selected attempt, when one exists, is FAILED or ABORTED_RETRYABLE according to the terminal event. `attempt_no` is exactly 1 or 2 under the one-restart rule. Orphan attempts, multiple RUNNING attempts, a selector/control/fence mismatch, or READY backed by a non-SUCCEEDED attempt is `EXPORT_SCHEMA_VIOLATION` and cannot issue a grant.

`ready_at` is assigned once on MARK_READY and retained through EXPIRING/EXPIRED. `terminal_at` is null in READY/EXPIRING and assigned only by FAIL, CANCEL or EXPIRE. The selected attempt and copied job archive version/hash/size/creation/expiry remain immutable evidence after EXPIRED even though the object and delivery grants no longer exist. MARK_READY is forbidden when `now >= selected_attempt.archive_expires_at`.

Each ExportAttempt candidate persists `archive_created_at` only after exact object-version upload, checksum/size verification and that attempt's subject-reference registration; `archive_expires_at = archive_created_at + 24 hours`. Under the Workspace guard lock, that archive-registration transaction also creates this second restricted control chain and its sequence-1 ENQUEUE before the materialization fence may complete at READY:

```text
TenantControlJob (expiry instance)
  tenant_control_job_id
  workspace_id                       equals ExportJob.workspace_id
  work_item_type                     EXPORT_EXPIRY
  subject_record_type                ExportJob
  subject_record_key_json            { "export_job_id": export_job_id }
  subject_record_key_sha256          SHA-256(RFC8785(subject_record_key_json))
  operation_payload_schema_version   tenant_control_job_payload_v1
  operation_payload_json             {
                                       "archiveObjectVersionSha256":
                                         lowercase SHA-256(UTF8(exact archive_object_version)),
                                       "operation": "REVOKE_DELETE_VERIFY"
                                     }
  operation_payload_sha256           SHA-256(RFC8785(operation_payload_json))
  operation_idempotency_key          "export-expiry:" + export_job_id + ":" +
                                     archiveObjectVersionSha256
  created_at

TenantWorkItemFence (expiry instance)
  tenant_work_item_fence_id
  workspace_id                       equals ExportJob.workspace_id
  work_sequence                      next positive contiguous workspace sequence
  work_item_type                     EXPORT_EXPIRY
  work_item_record_type              TenantControlJob
  work_item_record_key_json          { "tenant_control_job_id": tenant_control_job_id }
  work_item_record_key_sha256        SHA-256(RFC8785(work_item_record_key_json))
  captured_guard_generation          equals current Workspace generation and
                                     the live materialization fence generation
  created_at
```

The same transaction writes the expiry outbox keyed by `(workspace_id, export_job_id, archive_object_version)`. It admits exactly one expiry chain per registered immutable version and a job may therefore have one cleaned chain for an aborted attempt plus one live chain for the selected attempt; every key/hash/idempotency/semantic-marker rule above applies with work type EXPORT_EXPIRY. `(workspace_id,archive_object_version)` and `(workspace_id,export_attempt_id,archive_object_version)` are unique. An object-version hash is only a lookup-safe binding and never substitutes for exact-version provider operations. If this transaction rolls back, that attempt's archive projection fields, subject READY eligibility, expiry chain and outbox remain absent. MARK_READY requires the selected attempt's exact nonterminal expiry chain at the same guard generation and requires every nonselected attempt's registered version already verified absent with its chain terminal `EXPORT_ARCHIVE_CLEANED`. An ABORTED_RETRYABLE/FAILED/CANCELLED attempt may omit a chain only when all of its archive fields, object registrations, subject references and expiry outbox are absent; this is the exact safe no-object predicate.

A crash after provider upload but before archive-registration commit is recovered through the materialization fence's provider lease: lookup either finds the exact immutable version, which the worker registers atomically with its expiry chain, or the materialization worker deletes and verifies it absent before FAIL/CANCEL/RESTART may complete. It never issues a second upload blindly. The EXPORT fence cannot terminalize while a provider operation is unresolved or a found object has neither an expiry-chain registration nor exact absence proof.

Normal retention begins the saga no later than `archive_expires_at - 15 minutes`; item deletion begins it immediately. If that retention trigger finds its owning attempt/job still pre-READY, one transaction first FAILs the current materialization with `EXPORT_ARCHIVE_EXPIRED_BEFORE_READY`, prevents MARK_READY and selects the pre-READY cleanup branch; it never exposes or silently refreshes the candidate version. While Workspace is ACTIVE, every step runs under the EXPORT_EXPIRY fence and rechecks its captured generation. The idempotent saga is:

1. Under the workspace guard lock, resolve and validate the expiry chain, append `START_EXPIRY`, derive EXPIRING, commit a server-side delivery-revocation barrier and transition every subject reference for that attempt from READY to REVOKED with the same trusted `revoked_at`. No archive byte is reachable after this commit.
2. Through one or more non-overlapping EXPORT_EXPIRY external-operation leases, enqueue and delete the exact immutable object version.
3. Through that same fence protocol, verify that exact version is no longer readable.
4. In one database transaction after absence evidence and with every expiry lease ENDED, transition every reference for the attempt from REVOKED to DELETED with trusted `deleted_at`, append exactly one `EXPIRE`, derive EXPIRED, and append EXPORT_EXPIRY COMPLETE/`EXPORT_ARCHIVE_EXPIRED` plus its terminal marker. Reference/index retention cleanup may occur only after this transaction.

Retry resumes the first incomplete step and never reactivates a grant. It MUST NOT append EXPIRE, mark a reference DELETED or terminalize the expiry fence before verified deletion. A missing object counts as deleted only when the pinned version and provider delete/version audit match; otherwise it alerts and retries while access remains revoked. Exact-version absence is required by `archive_expires_at`; overdue bytes page the retention owner without changing EXPIRING or the nonterminal expiry fence. A crash after object deletion but before the final transaction resumes provider lookup/absence verification and that transaction. A later subject-delete command that sees a DELETED reference performs an idempotent no-op for that archive while still completing its own source deletion receipt. ABORTED_RETRYABLE/FAILED/CANCELLED pre-READY attempts with a registered object use the same expiry fence and revoke/delete/verify cleanup; their REGISTERED references transition through REVOKED and DELETED, but the job retains its current processing/terminal state and has no START_EXPIRY/EXPIRE event for that stale version. After verified absence, COMPLETE/`EXPORT_ARCHIVE_CLEANED` plus the marker commits with reference DELETED. A safe-no-object aborted/failed/cancelled attempt has no expiry chain to terminalize.

`ExportJobStateEvent` is append-only:

```text
export_job_state_event_id
workspace_id
export_job_id
event_sequence              contiguous 1..N per job
event_type                  QUEUE | START_SNAPSHOT | CLASSIFY_STANDARD |
                            CLASSIFY_OVERSIZE | START_MATERIALIZE |
                            START_VALIDATE | RESTART_ATTEMPT | MARK_READY |
                            START_EXPIRY | FAIL | CANCEL | EXPIRE
recorded_at                 trusted UTC RFC 3339 milliseconds
actor_type                  SYSTEM | USER
actor_user_id               nullable for SYSTEM, required for USER
idempotency_key
reason_code                 nullable
error_code                  nullable unless FAIL
from_export_attempt_id      non-null only for RESTART_ATTEMPT
to_export_attempt_id        non-null only for RESTART_ATTEMPT
```

`(workspace_id, export_job_state_event_id)`, `(workspace_id, export_job_id, event_sequence)` and `(workspace_id, idempotency_key)` are unique. Composite foreign keys cover job and both non-null attempt IDs. Events order by `(event_sequence, export_job_state_event_id)`; server time is audit metadata, not the sequencing key. Transition validation and event insertion are one transaction. Retry with the same idempotency key returns the same event. RESTART_ATTEMPT is valid only before READY, at most once per job, and its `reason_code` is exactly `ATTACHMENT_CHANGED`, `SUBJECT_DELETED` or `TOMBSTONE_PENDING`. The abandoned attempt's `error_code` is exactly `EXPORT_ATTACHMENT_CHANGED` for ATTACHMENT_CHANGED and `EXPORT_SUBJECT_CHANGED` for either subject reason; the event itself keeps `error_code = null` because it is not FAIL. In one transaction it marks the old RUNNING attempt ABORTED_RETRYABLE, creates attempt 2 with a fresh cutoff/watermark, points `current_attempt_no` to it, and derives job state SNAPSHOTTING. Both attempt IDs are distinct and belong to the same job/workspace; a crash can observe neither or the complete transition, never a half-restart. A second restart condition FAILs the job with the same mapped stable error and requires a new request.

The event/state map is closed. QUEUE is event sequence 1 inserted with job creation and establishes QUEUED. START_SNAPSHOT atomically creates/selects attempt 1 and moves QUEUED -> SNAPSHOTTING. Exactly one of CLASSIFY_STANDARD or CLASSIFY_OVERSIZE is appended for each attempt while SNAPSHOTTING after same-cutoff preflight; it is a state-preserving event and assigns the current service-class fields. START_MATERIALIZE requires the current attempt's classification and moves SNAPSHOTTING -> MATERIALIZING. START_VALIDATE moves MATERIALIZING -> VALIDATING. RESTART_ATTEMPT may move SNAPSHOTTING, MATERIALIZING or VALIDATING -> SNAPSHOTTING under the rule above and atomically resets service class/due/classified time to UNCLASSIFIED/null until attempt 2 is classified; historical classification events remain. MARK_READY moves only VALIDATING -> READY and atomically succeeds the selected attempt. FAIL or CANCEL may move only QUEUED/SNAPSHOTTING/MATERIALIZING/VALIDATING to its corresponding terminal state. START_EXPIRY moves only READY -> EXPIRING; EXPIRE moves only EXPIRING -> EXPIRED. No other self-transition, skipped transition or event type is legal.

Materialization-fence termination is coupled to export processing termination. MARK_READY, FAIL or CANCEL appends that EXPORT fence's one COMPLETE after all of its START_EXTERNAL events have matching END_EXTERNAL; `safe_result_code` is respectively `EXPORT_READY`, `EXPORT_FAILED` or `EXPORT_CANCELLED`. A deletion-generation mismatch instead appends its one CANCELLED_DELETION with exact `safe_result_code = WORKSPACE_DELETING` and the job cancellation. RESTART_ATTEMPT leaves the same materialization fence nonterminal. READY retention expiry never reopens or mutates it because the separately registered EXPORT_EXPIRY fence owns revoke/delete/verify. An expiry fence reaches COMPLETE only as `EXPORT_ARCHIVE_EXPIRED` with the selected version's EXPIRE transaction, or `EXPORT_ARCHIVE_CLEANED` after verified stale/aborted/failed/cancelled pre-READY cleanup; workspace deletion uses its CANCELLED_DELETION handoff below. Every preterminal fence event has null `safe_result_code`; a missing terminal, two terminals, a wrong safe code or a terminal with an unmatched external operation blocks the applicable transition and deletion-drain evidence.

That terminal transaction also creates exactly one restricted `TenantWorkItemTerminalMarker`:

```text
tenant_work_item_terminal_marker_id
workspace_id
work_sequence
work_item_type                    EXPORT | EXPORT_EXPIRY
captured_guard_generation
terminal_event_type              COMPLETE | CANCELLED_DELETION
terminal_safe_result_code         EXPORT_READY | EXPORT_FAILED |
                                  EXPORT_CANCELLED |
                                  EXPORT_ARCHIVE_EXPIRED |
                                  EXPORT_ARCHIVE_CLEANED |
                                  WORKSPACE_DELETING
terminal_at
operation_payload_schema_version  tenant_control_job_payload_v1
terminal_marker_digest_profile    tenant_work_item_terminal_marker_v1
semantic_operation_digest_sha256
operation_idempotency_key_hmac
idempotency_hmac_key_version
source_fence_digest_sha256
```

Marker workspace/sequence/type/generation and terminal values exactly copy its fence and terminal event. Both persisted version/profile literals are mandatory; unknown, missing or aliased values fail before compaction/dedup. For each marker let `W` be its concrete `EXPORT` or `EXPORT_EXPIRY` work type. `semantic_operation_digest_sha256 = SHA256(RFC8785({ "operationPayloadSchemaVersion": "tenant_control_job_payload_v1", "operationPayloadSha256": operation_payload_sha256, "subjectRecordKeySha256": subject_record_key_sha256, "subjectRecordType": "ExportJob", "workItemType": W }))`. `operation_idempotency_key_hmac = HMAC-SHA256(key[idempotency_hmac_key_version], UTF8(workspace_id) || 0x00 || UTF8("tenant_control_job_payload_v1") || 0x00 || UTF8(W) || 0x00 || UTF8(operation_idempotency_key))`; the referenced key remains derivable until marker deletion plus the backup-verification window. Under exact `tenant_work_item_terminal_marker_v1`, source digest is lowercase SHA-256 of RFC 8785 `{ "capturedGuardGeneration": int, "eventChain": [{ "eventSequence": int, "eventType": str, "providerOperationTokenSha256": hash-or-null, "recordedAt": ts, "safeResultCode": str-or-null }...], "operationPayloadSchemaVersion": "tenant_control_job_payload_v1", "operationPayloadSha256": hash, "subjectRecordKeySha256": hash, "terminalEventType": str, "terminalMarkerDigestProfile": "tenant_work_item_terminal_marker_v1", "terminalSafeResultCode": str, "workItemType": W, "workSequence": int }`; `eventChain` is the complete contiguous event order. A payload-schema bump creates a new semantic/idempotency namespace and cannot alias v1. A terminal-marker profile bump changes only evidence serialization/source-digest verification; it preserves the payload-schema-qualified semantic digest and retry identity. EXPORT admits only its three materialization result codes; EXPORT_EXPIRY admits only its two cleanup result codes, while either may use WORKSPACE_DELETING only with CANCELLED_DELETION. A terminal event without this atomically committed marker is invalid and does not make the work sequence drain-terminal. Once the marker exists and every lease is ENDED, TP-SEC may compact TenantControlJob/fence/event/lease detail within 30 days or earlier when the domain subject is deleted. The marker deliberately retains no ExportJob/subject/provider ID, raw key, raw payload or raw idempotency value; later dedup/drain uses only its version-qualified HMAC/digests/sequence and MUST NOT require compacted detail.

Job projection times equal event times: `created_at = QUEUE.recorded_at`; `started_at` is null before and equals the first START_SNAPSHOT time after work begins; `sla_classified_at` equals the current attempt's classification event; `ready_at = MARK_READY.recorded_at`; and `terminal_at` equals FAIL, CANCEL or EXPIRE `recorded_at` only in its corresponding terminal state. `error_code` is non-null exactly for FAILED and equals FAIL.error_code. STANDARD has `sla_due_at = requested_at + 24 hours`; OVERSIZE and UNCLASSIFIED require null. Restart does not change created/started time or the absolute STANDARD deadline after reclassification.

START_EXPIRY is valid only from READY and requires reason `RETENTION_DEADLINE`, `WORKSPACE_DELETING` or `SUBJECT_DELETED`; it derives EXPIRING and atomically revokes the grant and all READY subject references for the current attempt. For RETENTION_DEADLINE/SUBJECT_DELETED it also requires the live nonterminal EXPORT_EXPIRY fence, ACTIVE Workspace and matching generation. EXPIRE is valid only from EXPIRING after durable exact-version absence evidence and after every such reference is eligible for the same atomic DELETED transition; it uses the same reason. Its normal ACTIVE-workspace transaction also terminalizes that expiry fence and marker as `EXPORT_ARCHIVE_EXPIRED`.

The outbox command has exactly `{ archiveExpiresAt, archiveObjectVersion, exportJobId, reasonCode, workspaceId }` and idempotency key `"aed_" + lowercase_hex(SHA-256(UTF8(RFC8785(payload))))`. Its exact object-version SHA-256 must equal the expiry control payload binding. Database outbox delivery and provider exact-version delete reuse that key as application idempotency while each provider call is additionally covered by its exact `tpw_` lease token. A conflicting payload/key/version hash is `EXPORT_SCHEMA_VIOLATION`; duplicate delivery resolves the same expiry chain and resumes lookup/verification without creating another state event, fence or work sequence.

### 3.2. Authorization and rate controls

- Request and download require owner authorization for the exact `workspace_id` and a managed re-authentication event no older than 10 minutes, as required by `TP-SEC`.
- Workspace scope is derived from the authenticated membership. A client-supplied workspace identifier is never trusted without ownership verification.
- The server enforces the `TP-SEC` export rate limit. Rate rejection creates no snapshot or archive.
- A signed URL is issued only for one READY archive object, one workspace and read-only download purpose. Its lifetime, archive retention and revocation are owned by `TP-SEC`.
- The URL resolves only through the revocation-aware TradeProof download gateway/CDN authorization hook. Every GET and Range GET validates token signature/15-minute expiry, recent-auth grant, job state READY, Workspace state ACTIVE, active delivery grant, current workspace/subject guard generations and the exact archive object version before returning bytes. An ordinary object-store presign or redirect that remains usable after grant/generation revocation is forbidden; an alternative provider primitive is conformant only when integration tests prove immediate exact-version denial during object-delete outage.
- The download response uses `Content-Type: application/zip` and `Content-Disposition: attachment; filename="tradeproof-export.zip"`. It never renders the archive inline.
- Authorization is checked again before issuing each signed URL. Possession of an export request ID is insufficient.
- Cross-workspace request IDs, object versions, references or signed URLs fail without revealing whether the target exists.
- Token expiry is `min(issue_time + 15 minutes, archive_expires_at)`; issuance at or after START_EXPIRY is forbidden. Each successful/denied gateway decision is audited without URL/token/content.

### 3.3. Deleting-workspace race

The workspace state transition to `deleting`, export snapshot registration, archive/expiry-chain registration and MARK_READY MUST serialize on the same workspace guard lock:

1. If `deleting` commits first, request/snapshot creation fails `EXPORT_WORKSPACE_DELETING`.
2. If an ExportAttempt registers first but `deleting` begins before READY, deletion wins: the job becomes `CANCELLED`, its EXPORT fence becomes CANCELLED_DELETION, any registered EXPORT_EXPIRY fence is revoked/handed off as below, temporary archive/object pins are destroyed, signed delivery is never issued and no automatic restart occurs.
3. If READY existed before deletion, deletion revokes its URL, cancels/hands off the EXPORT_EXPIRY fence and removes the archive under `TP-SEC`; a later download authorization fails.

MARK_READY resolves both ExportJob -> materialization TenantControlJob/fence and selected ExportAttempt+archive-version -> EXPORT_EXPIRY TenantControlJob/fence, acquires the workspace lock first, requires current Workspace state ACTIVE and current generation equal to both captured generations, then acquires subject guards in canonical order. It rejects a missing/terminal/mismatched selected expiry chain and any nonselected registered version not already verified absent/`EXPORT_ARCHIVE_CLEANED`. In one transaction it copies the selected attempt's five archive fields byte-for-byte to ExportJob, appends MARK_READY, commits the selected attempt's subject READY barrier, appends materialization COMPLETE/`EXPORT_READY` plus its marker, leaves the selected EXPORT_EXPIRY nonterminal, and activates the delivery grant.

If deletion commits first, MARK_READY cannot commit. The FENCE transaction cancels the job/attempt, revokes access, freezes every exact registered or provider-lease-discovered object into deletion inventory and requests cancellation of the live materialization/expiry fences before committing the new generation/DELETING state. If no external lease is open, each fence may append CANCELLED_DELETION/`WORKSPACE_DELETING` plus marker in that transaction. Otherwise no new dispatch is allowed: the cancellation/drain worker may only look up the already-started provider token until terminal, append its matching END_EXTERNAL/ENDED evidence, discard the provider result, then append CANCELLED_DELETION plus marker. If MARK_READY commits first, deletion performs the same handoff after atomically appending START_EXPIRY/`WORKSPACE_DELETING`, revoking the grant/references and freezing the selected version/reference set.

After that handoff no export/expiry worker may dispatch, publish or commit a result; completing lookup/END and the cancellation marker is the only allowed late control evidence. The deletion target waits those markers, then may perform exact-version delete/absence verification and update restricted cleanup projections: it moves references REVOKED -> DELETED and, for a prior READY job, appends EXPIRE/derives EXPIRED after verified absence. This deletion-owned finalization is not resumed TenantWorkItemFence work and cannot recreate a grant or tenant-domain row. For a pre-READY CANCELLED job it leaves job state unchanged. A handoff missing revocation, exact version/inventory evidence, target ownership, terminal marker or provider absence proof blocks deletion completion. The exporter never publishes a partial archive and never delays the account-deletion SLA to finish an export.

### 3.4. First-party export control-feed notices

V1 notification delivery is only an authenticated first-party in-app/control-feed projection. It sends no email, SMS, push, webhook or processor request and has no asynchronous delivery worker. Each restricted noncanonical `ExportControlFeedNotice` has exactly:

```text
export_control_feed_notice_id
workspace_id
export_job_id
notice_contract_version         tradeproof_export_control_feed_v1
notice_sequence                 positive contiguous per job
notice_type                     CLASSIFIED_STANDARD | CLASSIFIED_OVERSIZE |
                                PROCESSING_HEARTBEAT | STANDARD_SLA_MISSED |
                                READY | FAILED | CANCELLED | DOWNLOAD_AUTHORIZED
attempt_no                      nullable only before the first attempt
service_class                   UNCLASSIFIED | STANDARD | OVERSIZE
heartbeat_ordinal               positive only for PROCESSING_HEARTBEAT; otherwise null
stage                           QUEUED | SNAPSHOTTING | MATERIALIZING | VALIDATING only for
                                PROCESSING_HEARTBEAT; otherwise null
record_count_bucket             ZERO | LE_1K | LE_100K | LE_1M | LE_10M | GT_10M,
                                non-null only for classification/heartbeat
archive_byte_bucket             ZERO | LE_1MIB | LE_1GIB | LE_10GIB | LE_100GIB |
                                GT_100GIB, non-null only for classification/heartbeat
safe_error_code                 required only for FAILED or STANDARD_SLA_MISSED
download_audit_id               required only for DOWNLOAD_AUTHORIZED
created_at                      trusted UTC RFC 3339 milliseconds
idempotency_key
```

Bucket intervals are exact. Record counts map as ZERO = 0, LE_1K = 1..1,000, LE_100K = 1,001..100,000, LE_1M = 100,001..1,000,000, LE_10M = 1,000,001..10,000,000 and GT_10M > 10,000,000. Bytes map as ZERO = 0, LE_1MIB = 1..1,048,576, LE_1GIB = 1,048,577..1,073,741,824, LE_10GIB = 1,073,741,825..10,737,418,240, LE_100GIB = 10,737,418,241..107,374,182,400 and GT_100GIB > 107,374,182,400. Classification buckets use the exact same-cutoff preflight combined canonical-record count and estimated complete uncompressed archive bytes; heartbeat buckets use the current monotone staged-record and staged-uncompressed-byte counters for that attempt, or both ZERO when the job is still QUEUED with no attempt. `(workspace_id,export_job_id,notice_sequence)` and `(workspace_id,idempotency_key)` are unique, with composite same-workspace job ownership. `attempt_no`, when non-null, equals the selected attempt at insertion. Idempotency keys are exactly `export-notice:<export_job_id>:classification:<attempt_no>`, `export-notice:<export_job_id>:heartbeat:<heartbeat_ordinal>`, `export-notice:<export_job_id>:sla-missed`, `export-notice:<export_job_id>:final:<export_job_state_event.event_sequence>` or `export-notice:<export_job_id>:download:<download_audit_id>` for their respective types, with each placeholder in its canonical persisted form. A retry with the same key and byte-identical fields returns the row; changed fields fail closed. The feed exposes only this safe enum/bucket state plus the authenticated job link; it contains no filenames, symbols, values, content/hash, provider locator, signed URL or other-tenant ID.

The CLASSIFY_STANDARD/CLASSIFY_OVERSIZE event transaction inserts its one matching notice. While a job remains pre-READY, the fenced scheduler transaction at each crossed boundary `requested_at + 24 hours * heartbeat_ordinal` inserts exactly one PROCESSING_HEARTBEAT; it first resolves the live EXPORT TenantControlJob/fence, locks Workspace and rechecks ACTIVE/current captured generation. A STANDARD job still processing at `sla_due_at` inserts exactly one STANDARD_SLA_MISSED notice with `safe_error_code = EXPORT_STANDARD_SLA_MISSED` in that same guarded scheduler transaction; this is additional to, not a replacement for, its eventual final notice. MARK_READY, FAIL and CANCEL insert exactly one READY, FAILED or CANCELLED notice in their respective state/fence terminal transaction before the EXPORT marker permits detail compaction; FAILED copies the sanitized ExportJob `error_code`. There is therefore no post-terminal notifier.

After a successful revocation-aware GET authorization, the download gateway inserts DOWNLOAD_AUTHORIZED in the same transaction as its non-content download audit; its `download_audit_id` resolves that same-workspace audit row and its idempotency key is bound to that audit ID. It reports authorization, not an unverifiable client receipt of every byte. Notice rows are operational control-plane data, never an archive record or manifest version. They are inaccessible once Workspace is not ACTIVE and are removed by PRIMARY_TENANT_DATA deletion; the referenced audit remains under TP-SEC's separate audit-minimization authority. Deletion never waits for an external notification provider.

## 4. One-cutoff snapshot and reference closure

### 4.1. ExportAttempt and cutoff

Each attempt persists:

```text
export_attempt_id
export_job_id
workspace_id
attempt_no
job_contract_version       tradeproof_export_job_v1
export_as_of_at
snapshot_watermark
snapshot_engine_id
snapshot_engine_version
attachment_pin_digest       lowercase SHA-256 of exact retained pin array
generated_at               nullable until canonical non-manifest entries are final
archive_object_version     nullable until verified candidate registration
archive_sha256             nullable until verified candidate registration
archive_size_bytes         nullable until verified candidate registration
archive_created_at         nullable until verified candidate registration
archive_expires_at         nullable; exactly archive_created_at + 24 hours
started_at
finished_at                nullable
outcome                    RUNNING | ABORTED_RETRYABLE | SUCCEEDED | FAILED
error_code                 nullable
```

`exportAsOfAt` is assigned by a trusted UTC clock when a repeatable-read/MVCC snapshot is registered. `snapshotWatermark` is the database-specific commit position or equivalent opaque snapshot token. After all non-manifest entries are finalized and hashed, the worker compare-and-set assigns persisted `generated_at` exactly once and immediately serializes it as manifest `generatedAt`. A crash or checksum rebuild under the same `export_attempt_id` reuses that value; if the crash was before the successful compare-and-set, the first resuming worker may assign it. No worker may replace a non-null value. A different `generatedAt` requires a new ExportAttempt, new cutoff and new attempt ID. It therefore participates in manifest and archive bytes without creating a timing cycle. `exportAsOfAt` and `generatedAt` MUST be separate fields and normally differ.

`attempt_no` starts at 1 and is contiguous. Attempt 1 `started_at = START_SNAPSHOT.recorded_at`; attempt 2 starts at RESTART_ATTEMPT.recorded_at, and each cutoff/watermark is registered in that creation transaction. Both attempts use the owning job's one immutable materialization TenantControlJob/fence chain; RESTART_ATTEMPT requires Workspace still ACTIVE at that fence generation and never refreshes or replaces either record. An attempt's five archive fields above are jointly null before registration and jointly non-null after the atomic registration; they are immutable thereafter. The exact `(payload schema, EXPORT_EXPIRY, ExportJob subject key, object-version-hash payload)` tuple deterministically resolves its live chain or, after legal compaction, its unique semantic-digest terminal marker; ExportAttempt MUST NOT retain a permanent FK to compactable control detail. RUNNING has null `finished_at`, null `error_code` and is the selected attempt of a processing job. SUCCEEDED has `finished_at = MARK_READY.recorded_at`, null `error_code` and is selected only by READY/EXPIRING/EXPIRED; its archive tuple byte-equals the five job archive fields. ABORTED_RETRYABLE has `finished_at = RESTART_ATTEMPT.recorded_at` plus the stable restart-cause error and is never selected afterward; any registered object remains pinned to its own expiry semantic operation until CLEANED. FAILED has non-null `finished_at` equal the terminal FAIL/CANCEL time and non-null stable `error_code`. Outcome, timestamps, selector and job state are updated in the same transition transaction; a current worker compare-and-sets `(workspace_id, export_job_id, current_attempt_no, outcome=RUNNING)` and revalidates the materialization chain under the Workspace lock before any state change.

Immediately before any external request and immediately before committing any database/object result, an export worker resolves ExportJob -> the phase-appropriate TenantControlJob -> fence, locks Workspace and requires ACTIVE plus current `deletion_guard_generation = fence.captured_guard_generation`. Snapshot/materialization/object-upload work uses EXPORT; after archive registration, revoke/delete/verify work uses only EXPORT_EXPIRY. Before provider dispatch, one transaction allocates the next positive contiguous `operation_ordinal` for that fence, inserts a same-workspace `TenantExternalOperationLease` in DISPATCH_RESERVED, and appends START_EXTERNAL with the lease's token hash. The lease contains exactly `tenant_external_operation_lease_id`, `workspace_id`, `tenant_work_item_fence_id`, `operation_ordinal`, `provider_registration_id`, `lookup_hmac_key_version`, `provider_operation_token_sha256`, `state`, `start_event_sequence`, nullable `end_event_sequence`, `created_at` and nullable `ended_at`. The non-persisted provider token is exactly `"tpw_" + base64url_no_pad(HMAC-SHA256(key[lookup_hmac_key_version], RFC8785({ "operationOrdinal": operation_ordinal, "providerRegistrationId": provider_registration_id, "tenantWorkItemFenceId": tenant_work_item_fence_id, "workspaceId": workspace_id })))`; event and lease persist its lowercase SHA-256 over exact ASCII token bytes.

The approved provider receives that token as its idempotency/lookup key. Dispatch changes DISPATCH_RESERVED to DISPATCHED. A crash while reserved performs lookup and dispatches only after definitive NOT_FOUND; a crash after dispatch performs lookup until terminal. One transaction then appends matching END_EXTERNAL and changes the lease to ENDED, copying the same token hash and event sequence. Operation pairs cannot overlap within a fence, ordinals cannot gap, START/END alone carry a non-null token hash, and terminal events require every lease for that fence ENDED. Missing derivation key, ambiguous lookup or a provider without exact idempotent lookup blocks READY/cleanup and deletion drain. A materialization generation mismatch follows CANCELLED_DELETION and cancels the job/attempt; an expiry mismatch may only use the atomic workspace-deletion handoff above. Either discards a late result and cannot publish. Only the account-deletion worker may act on a DELETING Workspace. The Workspace record serialized by a successful attempt has `deletion_guard_generation` equal to both fences' captured generation; disagreement is `EXPORT_WORKSPACE_DELETING`.

`attachment_pin_digest` is always a lowercase SHA-256. It hashes the RFC 8785 bytes of the retained-binary pin array sorted by `attachmentId`, each exact object `{ "attachmentId": id, "byteSize": int, "contentObjectVersion": string, "contentSha256": hash, "state": "ACTIVE" }`. Only descriptors eligible as RETAINED_CLEAN appear; an empty set hashes `[]`. The pinned tuple is immutable for the attempt and must equal its Attachment/descriptor and manifest binary metadata.

All canonical database reads for an attempt use the same snapshot transaction or a durable exported snapshot with equivalent visibility. Page/cursor reads MUST NOT open a newer snapshot.

### 4.2. Exact inclusion rule

A durable workspace-owned record or state event is included if and only if all of the following hold:

1. it belongs directly to the exporting workspace under the authoritative composite workspace foreign keys;
2. its commit is visible at `snapshotWatermark`;
3. it has not been canonically removed in that snapshot, except that its durable tombstone is included when allowed by section 8;
4. its record type appears in the closed allowlist in section 7.

Client, lifecycle and event timestamps never determine snapshot visibility. A created record, revision or state event is included exactly when its commit is visible; a transaction committed after the watermark is excluded even if millisecond rounding makes its trusted timestamp equal to `exportAsOfAt`. Payload timestamps are preserved and domain validators check their own ordering rules, but the exporter never has an implicit per-record lifecycle-field map. `EXPORT_TEMPORAL_INVARIANT_FAILED` is reserved for an impossible snapshot-engine result, such as a cursor returning a commit outside the registered watermark. Shared public closure records are selected by immutable reference from an included workspace record and MUST also be visible at the same watermark.

The exporter includes all visible immutable revisions, superseded revisions and append-only state events, not only records reachable from the current UI. Mutable projection-cache columns are not authoritative; current/active pointers are recomputed from included state events and emitted in `canonical/pointers.json`.

### 4.3. Pointer rule

V1 has only the pointer families in the closed matrix below. A writer MUST NOT invent a pointer name, omit an eligible aggregate, or emit a pointer for an aggregate family not listed here. The exporter derives each pointer from records visible at `snapshotWatermark`, compares it with the source service's as-of projection and fails `EXPORT_POINTER_MISMATCH` on any disagreement; a mutable cache value is never accepted as the derivation basis.

Every `AsOfPointer` has exactly these fields:

```text
pointer_type                    one exact matrix value
workspace_id                    null only for the two SHARED_PUBLIC catalog families
aggregate_key_json              exact object shape from the matrix
aggregate_key_sha256            SHA-256 of RFC 8785 aggregate_key_json
target_record_type              exact matrix value even when the target is null
target_record_key_json          exact target recordKey object; nullable only where the matrix permits
state_value                     exact matrix value or null as specified
basis_record_type               exact matrix value
basis_record_key_json           exact included basis recordKey object
derived_at_export_as_of_at      exactly manifest.exportAsOfAt
```

`aggregate_key_json`, `target_record_key_json` and `basis_record_key_json` are ordinary canonical JSON objects, not escaped JSON strings. Their member sets are exact. A target or basis key MUST equal the referenced record's envelope `recordKey`; the target and basis therefore resolve without an ID-only heuristic. Pointer records sort by `(workspace_id-or-empty, pointer_type, aggregate_key_sha256)`. `workspace_id-or-empty` is the empty byte string for null and otherwise the ID's unsigned UTF-8 bytes.

Within one `(workspace_id, pointer_type)`, two distinct aggregate-key byte sequences yielding the same SHA-256 are a fatal `EXPORT_SCHEMA_VIOLATION`; the implementation MUST NOT choose one or add a tie-breaker.

In the matrix, `id`, `hash` and `ts` are registry type metavariables and `A|B` lists allowed concrete string values; they are not emitted literally. Each archive contains an ordinary JSON object with one concrete value at each position.

| `pointer_type` | Aggregate and exact `aggregate_key_json` | Target and null rule | Exact `state_value` | Resolver and `basis_record_type` |
|---|---|---|---|---|
| `ACTIVE_INSTRUMENT_CATALOG` | One SHARED_PUBLIC aggregate: `{ "productType": "SPOT", "venue": "BINANCE" }` | `InstrumentCatalogPublishEvent`, non-null | null | Greatest visible contiguous family `event_sequence`; basis is that event |
| `ACTIVE_MARKET_CONVERSION_CATALOG` | One SHARED_PUBLIC aggregate: `{ "productType": "SPOT", "venue": "BINANCE" }` | `MarketConversionCatalogPublishEvent`, non-null | null | Greatest visible contiguous family `event_sequence`; basis is that event |
| `CURRENT_WORKSPACE_OWNER_PROFILE` | One for the exporting profile header: `{ "ownerUserId": id }` | `WorkspaceOwnerProfileRevision`, non-null | null | Select greatest visible `revision_no`; revisions are contiguous and `recorded_at` nondecreasing. Basis is the target revision |
| `CURRENT_SETUP_PRESET` | One per visible SetupPreset: `{ "setupId": id }` | `SetupPresetRevision`, non-null | `ACTIVE` or `ARCHIVED` | Replay the contiguous SetupPresetStateEvent stream by greatest visible `event_sequence`: CREATE/REVISE set the referenced revision and ACTIVE; ARCHIVE retains the revision and sets ARCHIVED; REACTIVATE retains its prior revision unless its event carries a revision, then sets ACTIVE. Basis is the greatest-sequence event |
| `CURRENT_TRADE_PLAN` | One per visible TradePlan: `{ "tradePlanId": id }` | `TradePlanRevision`, non-null | `ARMED`, `CONSUMED`, `CANCELLED` or `EXPIRED` | ARM atomically creates header, revision 1 and event sequence 1, so missing revision/ARM is invalid. Target is greatest contiguous revision. Replay the contiguous PlanStateEvent stream: CONSUME/CANCEL/EXPIRE determine their named states; when the greatest event is ARM, state is ARMED iff `exportAsOfAt < TradePlan.expires_at`, otherwise EXPIRED even before lazy EXPIRE materialization. Basis is the greatest event except boundary-derived EXPIRED without EXPIRE, whose basis is TradePlan |
| `ACTIVE_TRADE_EPISODE_PROJECTION` | One per visible TradeEpisode having an active projection at the cutoff: `{ "episodeId": id }`; omit for historical-only/REMOVED header | `TradeEpisodeProjection`, non-null | `OPEN` or `CLOSED` | Select the unique projection whose interval contains the cutoff: `created_at <= exportAsOfAt` and (`superseded_at` is null or `exportAsOfAt < superseded_at`). Exact equality belongs to the new version; intervals may not overlap. Basis is the target projection |
| `CURRENT_EPISODE_METRIC_ELIGIBILITY` | One per TradeEpisode having an `ACTIVE_TRADE_EPISODE_PROJECTION`: `{ "episodeId": id }`; omit when no active projection | `EpisodeMetricEligibilityEvent`; null only with no event for the exact active projection version | `INCLUDED` or `EXCLUDED` | Filter events to `based_on_projection_version = active projection_version`, then select greatest visible aggregate-wide `event_sequence`; no matching event or RESTORE means INCLUDED, EXCLUDE means EXCLUDED. Basis is the target event, or active TradeEpisodeProjection when target is null. Historical-version events never carry forward implicitly |
| `ACTIVE_FEE_CONVERSION` | One per NormalizedFill having conversion history: `{ "fillId": id }` | `FeeConversion`, non-null | `EXACT`, `DERIVED` or `UNAVAILABLE` | Select the unique interval containing the cutoff: `created_at <= exportAsOfAt` and (`superseded_at` is null or `exportAsOfAt < superseded_at`). Exact equality belongs to the new contiguous version; basis is the target conversion |
| `CURRENT_REVIEW_REVISION` | One per visible Review whose episode has an `ACTIVE_TRADE_EPISODE_PROJECTION`: `{ "reviewId": id }`; omit while its episode header is historical-only/REMOVED | `ReviewRevision`, non-null | `COMPLETED` or `RECONFIRM_REQUIRED` | Select greatest `(revision_no, review_revision_id)`; compare its episode projection version with `ACTIVE_TRADE_EPISODE_PROJECTION` exactly as TP-ACC defines Review state. Basis is the target revision. Omission does not remove Review/revision history |
| `CURRENT_UPLOAD_STATE` | One per visible Upload: `{ "uploadId": id }` | `UploadStateEvent`, non-null | `QUARANTINED`, `VALIDATING`, `ACCEPTED`, `REJECTED` or `PURGED` | Greatest `(event_sequence, upload_state_event_id)`; require contiguous sequence and map RECEIVE/START_VALIDATION/ACCEPT/REJECT/PURGE in order to the listed state. Basis is the target event |
| `CURRENT_ATTACHMENT_STATE` | One per visible Attachment: `{ "attachmentId": id }` | `AttachmentStateEvent`, non-null | `ACTIVE`, `DELETING` or `DELETED` | Greatest `(event_sequence, attachment_state_event_id)`; require contiguous sequence and map ACTIVATE/DELETE_REQUEST/DELETE_COMPLETE. Basis is the target event |
| `ACTIVE_CONTEXT_SNAPSHOT` | One per visible exact scope: `{ "algorithmVersion": str, "episodeProjectionVersion": int, "parameterSetId": str, "phase": "ENTRY"\|"EXIT", "timeframe": "1m"\|"5m", "tradeEpisodeId": id }` | `ContextSnapshot`, non-null | `COMPLETE`, `PARTIAL` or `UNRELIABLE` | Within exact `(workspaceId,tradeEpisodeId,episodeProjectionVersion,phase,timeframe,algorithmVersion,parameterSetId)`, validate one contiguous `snapshotRevisionNo` chain and select greatest revision whose `computedAt <= exportAsOfAt`; no timestamp/ID tie-break. Basis is the target snapshot |
| `CURRENT_WEEKLY_COHORT_STATE` | One per visible WeeklyCohort: `{ "weeklyCohortId": id }` | `WeeklyCohortStateEvent`; target is greatest visible sequence, nullable only when none is visible | `SCHEDULED`, `OPEN`, `LOCK_PENDING`, `LOCKED` or `SUPERSEDED` | Validate the contiguous sequence and retain events with `recorded_at <= exportAsOfAt`, then resolve in precedence: valid SUPERSEDE -> SUPERSEDED; else valid LOCK -> LOCKED; else cutoff before start -> SCHEDULED; else cutoff in `[start,end)` -> OPEN; else -> LOCK_PENDING. An OPEN event never overrides bounds. Basis is the target event for LOCKED/SUPERSEDED and the WeeklyCohort header for boundary-derived states or no event |
| `CURRENT_TIMEZONE_CHANGE_SCHEDULE_STATE` | One per visible TimezoneChangeSchedule: `{ "timezoneChangeScheduleId": id }` | `TimezoneChangeScheduleStateEvent`, non-null | `SCHEDULED`, `CANCELLED` or `APPLIED` | Select greatest visible contiguous `event_sequence` and map SCHEDULE/CANCEL/APPLY. Basis is that event |
| `CURRENT_WEEKLY_COHORT_INPUT` | One per WeeklyCohort having input history: `{ "weeklyCohortId": id }` | `WeeklyCohortInputRevision`, non-null | null | If a `CURRENT_WEEKLY_REPORT_REVISION` exists for the cohort, select exactly its input revision; otherwise select the unique input-revision chain leaf. A correction leaf may become current only in the same visible transaction as all affected current report pointers. Basis is the current report revision when present, otherwise the target input revision |
| `CURRENT_WEEKLY_REPORT_REVISION` | One per visible WeeklyReport: `{ "weeklyReportId": id }` | `WeeklyReportRevision`, non-null | `PUBLISHED` | Replay the report aggregate's contiguous WeeklyReportRevisionStateEvent stream by greatest visible `event_sequence`; PUBLISH activates its revision and SUPERSEDE removes the referenced revision. Exactly one published target remains. Basis is the target's PUBLISH event |
| `CURRENT_CONFIRMED_BEHAVIORAL_EXPERIMENT` | One per target WeeklyCohort having its unique experiment aggregate: `{ "targetWeeklyCohortId": id }` | `BehavioralExperimentRevision`; null only when none is currently confirmed | `CONFIRMED` or `NONE` | Replay that aggregate's contiguous BehavioralExperimentStateEvent stream by greatest visible `event_sequence`; CONFIRM activates, and SUPERSEDE/CANCEL removes, the referenced revision. Basis is the greatest-sequence event |
| `CURRENT_AI_CONSENT` | One per feature having ConsentRecord history: `{ "feature": "TRANSCRIPTION"\|"TAXONOMY_SUGGESTION"\|"WEEKLY_SUMMARY" }` | `ConsentRecord`, non-null | `GRANT` or `REVOKE` | Greatest visible contiguous `event_sequence` for that workspace/feature. Basis is the target ConsentRecord |
| `CURRENT_AI_OUTPUT_SUBJECT_STATE` | One per visible AiOutputSubject: `{ "aiOutputSubjectId": id }` | `AiOutputSubjectStateEvent`, non-null | `ACTIVE` or `DELETED` | Greatest visible contiguous `event_sequence`; sequence 1 CREATE maps ACTIVE and optional sequence 2 DELETE maps DELETED. Basis is the target event |
| `CURRENT_PRODUCT_MEASUREMENT_RUN_STATE` | One per visible ProductMeasurementRun: `{ "measurementRunId": id }` | `ProductMeasurementRunStateEvent`, non-null | `OPEN`, `SUCCEEDED` or `ABANDONED` | Require sequence-1 START. A visible sequence-2 SUCCEED/ABANDON selects SUCCEEDED/ABANDONED and is target/basis. With only START, target is START; state is OPEN with START basis iff `exportAsOfAt < deadline_at`, otherwise semantic ABANDONED/TIMEOUT with ProductMeasurementRun basis even before timeout materialization |
| `CURRENT_WORKSPACE_PRODUCT_METRIC_SNAPSHOT` | One per `{ "dimensionSha256": hash, "metricId": id, "windowEndAtExclusive": ts, "windowStartAt": ts }` | `WorkspaceProductMetricSnapshot`, non-null | `PROVISIONAL` or `FINAL` | Follow `supersedes_snapshot_id`; require contiguous `revision_no` and select the unique leaf/greatest revision. Basis is the target snapshot |

The two catalog pointers are emitted only when a visible publish event exists. Other rows are emitted exactly when their aggregate-existence condition in the matrix holds. The target's workspace must equal the pointer workspace, except for the two catalog pointers whose target and catalog closure are SHARED_PUBLIC. Null targets are legal only for the three matrix cases above; their expected target type is still serialized. No pointer target may resolve through a tombstone because pointer families describe live logical selection, while historical domain references may use typed tombstones where section 7.10 permits them.

Every serialized domain `state`, `status` or lifecycle timestamp that the source contract calls derived is materialized at the same `exportAsOfAt` from the same included event stream used for pointers. The exporter never copies a stale cache. The closed equality/coupling rules are:

| Payload projection | Exact same-cutoff derivation and coupling |
|---|---|
| `TradePlan.state`, `consumed_by_episode_id` | A visible header requires revision 1 plus ARM sequence 1 and has no DRAFT branch. Replay terminal/CONSUME events first; an otherwise ARMED plan becomes effective EXPIRED at `exportAsOfAt >= expires_at` even before lazy EXPIRE. It equals `CURRENT_TRADE_PLAN.state_value`. `consumed_by_episode_id` is non-null exactly for CONSUMED, equals the selected CONSUME event field and resolves that same-workspace TradeEpisode; all other plan/event states require null. |
| `Review.state` | Let R be greatest ReviewRevision and P the episode's active projection. COMPLETED iff P exists and `R.episode_projection_version = P.projection_version`; otherwise RECONFIRM_REQUIRED, including historical-only/REMOVED episode identity. When `CURRENT_REVIEW_REVISION` exists, both its target/state must equal R/this result; no-active omission never permits a stale COMPLETED value. `created_at = completed_at =` revision 1 `recorded_at` and both remain unchanged. |
| `Upload.state`, times, error and absence proof | Map greatest contiguous UploadStateEvent RECEIVE/START_VALIDATION/ACCEPT/REJECT/PURGE to QUARANTINED/VALIDATING/ACCEPTED/REJECTED/PURGED and require pointer equality. `created_at = RECEIVE.recorded_at`; `purge_due_at = created_at + 24 hours`, and derived `forced_purge_at = created_at + 20 hours`. `accepted_at = ACCEPT.recorded_at` iff ACCEPT occurred. `terminal_at` is the first ACCEPT or REJECT `recorded_at` and is retained after PURGE; before either it is null. `safe_error_code` is null unless the path contains REJECT, in which case it is the event's safe code and remains after PURGE. A QUARANTINED/VALIDATING stall at forced purge appends REJECT/`RAW_UPLOAD_RETENTION_DEADLINE` before PURGE. Only PURGE has the matching absence-verification ID. |
| `Attachment.state`, `deleted_at`, absence proof and `AttachmentExportDescriptor` lifecycle fields | Map ACTIVATE/DELETE_REQUEST/DELETE_COMPLETE to ACTIVE/DELETING/DELETED and require pointer equality. `created_at = ACTIVATE.recorded_at`; `deleted_at` is null for ACTIVE/DELETING and equals DELETE_COMPLETE `recorded_at` for DELETED. Only DELETE_COMPLETE has the matching exact-content-version absence-verification ID. Descriptor `state_at_cutoff`, availability, path and tombstone/binary presence follow section 8 from that exact result. |
| `WeeklyCohort.state`, `initial_reporting_as_of_at`, `locked_at` | State equals `CURRENT_WEEKLY_COHORT_STATE.state_value` under the cutoff-bound resolver. SCHEDULED/OPEN/LOCK_PENDING/SUPERSEDED require both times null. LOCKED requires `initial_reporting_as_of_at = locked_at =` the valid LOCK event `recorded_at`. |
| `TimezoneChangeSchedule.state` | Map the greatest contiguous schedule event SCHEDULE/CANCEL/APPLY to SCHEDULED/CANCELLED/APPLIED and require pointer equality. |
| `WeeklyReportRevision.status`, `published_at`, `superseded_by_report_revision_id` | Each revision has exactly one PUBLISH; `published_at` equals its event `recorded_at`. It is PUBLISHED with null superseded-by until a later valid SUPERSEDE targets it; then it is SUPERSEDED and superseded-by equals that event's non-null `caused_by_report_revision_id`. Exactly the pointer target is PUBLISHED. |
| `BehavioralExperimentRevision.state`, `confirmed_at` | For each revision map its latest targeted PROPOSE/CONFIRM/SUPERSEDE/CANCEL event to PROPOSED/CONFIRMED/SUPERSEDED/CANCELLED. `confirmed_at` equals CONFIRM `recorded_at` only while state is CONFIRMED and is otherwise null. The aggregate's pointer target is exactly the unique CONFIRMED revision or null for NONE. |
| `AiOutputSubject` lifecycle | `CURRENT_AI_OUTPUT_SUBJECT_STATE.state_value` is ACTIVE for latest CREATE and DELETED for latest DELETE. CREATE is sequence 1 with null receipt and `recorded_at = AiOutputSubject.created_at`; DELETE is optional sequence 2 with a matching deletion receipt and no later event. ACTIVE requires the same-workspace AiOutput bundle; DELETED forbids that bundle and requires the matching typed Tombstone. |
| `ProductMeasurementRun` state | Header and START sequence 1 are atomic. Sequence 2, when visible, is the sole SUCCEED/ABANDON terminal and must mutually reference its terminal ProductAnalyticsEvent; otherwise state is OPEN before `deadline_at` and semantic ABANDONED/TIMEOUT at or after equality. It equals `CURRENT_PRODUCT_MEASUREMENT_RUN_STATE.state_value`; a late timeout event never backdates `recorded_at`. |

The reader recomputes every row above before accepting pointer bytes. A payload/cache value, null, timestamp, lineage ID or descriptor branch inconsistent with replay is `EXPORT_POINTER_MISMATCH`, even when its canonical JSON and manifest checksum are otherwise valid.

### 4.4. Reference-closure algorithm

Starting from every included workspace-owned record, the exporter repeatedly follows canonical foreign keys and immutable reference arrays until no new record is found.

- A workspace-owned reference MUST resolve to the same `workspace_id` and be visible at the cutoff.
- A reference to a catalog, taxonomy, instrument or public market record adds the exact referenced immutable version/row, not the current version.
- Every referenced `MarketBarRevision` adds its selected `MarketBarSourceObservation`, which adds its `MarketDataSourceRequest`, which adds its `MarketDataIngestionBatch`.
- Market bars referenced by `FeeConversion`, even when no ContextSnapshot uses them, receive the same closed provenance chain. Each `market_bar_ids_json[i]` uses only the aligned persisted `market_bar_source_observation_ids_json[i]` and its path's persisted nullable `resolutionRecordKey`; the exporter never reselects among observations or revisions for the same logical bar.
- Every ContextSnapshot includes its exact aligned `inputBarRevisionIds`, `inputBarSourceObservationIds`, nullable `inputBarResolutionIds`, plus sorted `sourceRequestIds` and `sourceIngestionBatchIds` from `TP-MCE`; the archive MUST verify these sets against the closure.
- Every ContextSnapshot also closes `(algorithmVersion,parameterSetId)` to exactly one included immutable ContextAlgorithmRelease with matching release hashes. ContextEpisodeTrigger, ManualContextRecomputeRequest and CONTEXT work-control rows are validated by the source before publication but remain excluded command/control evidence.
- Every non-null bar-resolution reference includes its MarketBarConflict, the complete contiguous MarketBarResolution prefix 1..N through that selected resolution and every candidate MarketBarRevision named by every prefix row. Candidate arrays/content hashes and the shared `market_bar_as_of_v1` cutoff decision verify without a current/latest fallback.
- Every Weekly Lab reference follows `weekly_lab_export_projection_v1`, including old report/input/metric/experiment revisions and exact source episode, Review, ContextSnapshot and taxonomy references.
- Every ProductAnalyticsEvent with a non-null `measurement_run_id` closes to the exact same-workspace ProductMeasurementRun and its complete visible state-event prefix. A terminal run event and sequence-2 state row reference each other; no current-run lookup or omitted timeout-derived state is allowed.
- Every StagedFill closes to its originating ImportRow/ImportBatch/account/instrument, and its optional StagedFillDisposition closes atomically to one ImportResolution plus exactly one admitted NormalizedFill or duplicate target according to outcome.
- Every retained AI bundle closes all AiRunInputReference and AiOutputReference typed keys, source digests and feature cardinality to the exact tenant/public records described in section 7.8; it never substitutes current report, metric, episode, Upload, Attachment or taxonomy records.
- Every AiOutputSubject closes its complete visible CREATE/optional DELETE prefix and current subject pointer; ACTIVE closes to the exact retained bundle, while DELETED closes to the exact receipt-keyed Tombstone and forbids content-bearing bundle rows.

Closure failure is fatal. The exporter MUST NOT drop the referring record, substitute a current revision or emit an untyped dangling ID.

### 4.5. Concurrent edit, replay and binary behavior

- Domain edits, report regeneration, accounting replay or context recomputation committed after `snapshotWatermark` are absent. Historical records and active pointers remain exactly as they were at the cutoff.
- A supersede transaction visible at the cutoff contributes both the new immutable revision and its state event. If only part appears, validation fails.
- Database rows deleted after the cutoff remain readable through the registered snapshot; deletion before the cutoff follows tombstone rules.
- At snapshot registration, each retained attachment is pinned by `(attachment_id, immutable_object_version, byte_size, sha256, state)`. The object is read by exact immutable version, never a mutable key.
- If a binary marked retained at the cutoff changes, cannot be read, has a different size/hash, or disappears, the attempt aborts with `EXPORT_ATTACHMENT_CHANGED`. The job MAY create one new attempt with a new cutoff and a newly authorized object-pin set. It MUST NOT reuse the old cutoff, silently omit the binary or publish a partial archive.
- A second binary race, a workspace entering `deleting`, or inability to renew object pins fails/cancels the job. The client may create a new request after the cause is resolved.

### 4.6. Item-level deletion invalidation

Attachment and AI-output deletion always wins over export retention. Each deletable subject has a durable `SubjectExportGuard(workspace_id, subject_type, subject_id, deletion_generation, deletion_state)`. At snapshot closure, the exporter registers an `ExportArchiveSubjectReference` outside the archive for every content/binary subject, linked to request/job/attempt/archive object version and the observed `deletion_generation`.

The handoff schemas are exact control-plane records, not archive entries:

```text
SubjectExportGuard
  workspace_id
  subject_type                  ATTACHMENT_BINARY | AI_OUTPUT | TRANSCRIPT_DRAFT
  subject_id
  deletion_generation          non-negative integer
  deletion_state               ACTIVE | DELETE_REQUESTED | DELETED
  updated_at

ExportArchiveSubjectReference
  workspace_id
  subject_type                  ATTACHMENT_BINARY | AI_OUTPUT | TRANSCRIPT_DRAFT
  subject_id
  export_request_id
  export_job_id
  export_attempt_id
  archive_object_version       immutable staging/final object version allocated before registration
  observed_deletion_generation
  publication_state            REGISTERED | READY | REVOKED | DELETED
  registered_at
  ready_at                     nullable
  revoked_at                   nullable
  deleted_at                   nullable
```

The guard key is `(workspace_id, subject_type, subject_id)`. The reference key is `(workspace_id, subject_type, subject_id, export_attempt_id)`, and `(workspace_id, export_attempt_id, subject_type, subject_id)` is also unique. All request/job/attempt joins are composite workspace FKs. State timestamps are non-null exactly when their named state has been reached and are append-only audit projections; transitions are REGISTERED -> READY -> REVOKED -> DELETED or REGISTERED -> REVOKED -> DELETED. No record stores content, filename, URL or content hash.

Registration and deletion serialize on that guard:

1. The exporter locks subject guards in `(subject_type, subject_id)` order in a current transaction outside the old MVCC snapshot, requires `deletion_state = ACTIVE`, records the current generation and inserts the reference atomically. If deletion committed first, registration fails and the attempt uses its one permitted new-cutoff restart; a second restart condition fails the job.
2. Deletion locks the same guard, increments `deletion_generation`, commits the source delete plus `SubjectDeletionReceipt`/outbox handoff and invalidates every registered attempt/archive in one transaction/outbox flow. If registration committed first, deletion necessarily observes it; a later exporter derives the archive Tombstone from that receipt.
3. Immediately before `MARK_READY`, the exporter locks the workspace guard first and then all indexed subject guards in the same order, verifies Workspace ACTIVE plus every generation/state against current data rather than the old snapshot, and atomically records the READY publication barrier/event/grant. A subject mismatch uses the one permitted new-cutoff restart or fails when already consumed; workspace deletion cancels. No signed grant can be issued before this barrier.
4. Deletion after the READY barrier observes a READY reference and follows the revoke/delete path below. Signed URL issuance also checks the current guard generations.

- A delete command commits its durable receipt/outbox handoff and invalidates matching non-terminal attempts in the same logical transaction/outbox flow. A SNAPSHOTTING, MATERIALIZING or VALIDATING attempt aborts; if the workspace remains active and the restart budget is unused, the job restarts with a new cutoff that projects the Tombstone. Otherwise it fails without delivery. Deleted content is never published from the old snapshot.
- If a matching archive is already READY, deletion resolves its nonterminal EXPORT_EXPIRY chain, immediately revokes delivery grants/subject guard generation, appends START_EXPIRY with reason `SUBJECT_DELETED` and transitions every attempt reference READY -> REVOKED under that fence. Exact-version delete/absence verification uses its external-operation leases and permits the atomic REVOKED -> DELETED plus EXPIRE plus COMPLETE/`EXPORT_ARCHIVE_EXPIRED` marker transaction. A reference already DELETED is an idempotent no-op for that archive. A new export request is required.
- Subject deletion does not wait for archive cleanup. If object deletion is temporarily unavailable, access remains revoked and a high-priority retry continues until `TP-SEC` retention/deletion requirements pass.
- The guard/index stores only IDs, deletion generation, subject type, archive object version and state; it never stores content. It is retained long enough to prove and complete generated-archive cleanup, then expires under `TP-SEC`.

The same mechanism applies to `ATTACHMENT_BINARY`, `AI_OUTPUT` and `TRANSCRIPT_DRAFT`. V1 never archives raw Upload objects, including raw CSV and raw voice bytes; their later retention purge therefore needs no archive-subject index, while their canonical Upload metadata and Tombstone remain subject to cutoff closure.

## 5. ZIP archive format and fixed layout

### 5.1. Container bytes

The delivered object is one ZIP archive with media type `application/zip` and download filename `tradeproof-export.zip`.

V1 uses this exact container profile:

- all entries use ZIP method `STORE` (method 0); there is no compression and no encryption;
- ZIP64 structures are emitted if and only if an uncompressed size, compressed size, local-header offset, central-directory size or central-directory offset is `>= 0xffffffff`, or an entry-count/disk-number field is `>= 0xffff`; the reserved maximum sentinel value itself therefore requires ZIP64;
- entry names are ASCII, use `/`, are relative, and match the allowlist/patterns below;
- the general-purpose bit flag is exactly `0x0800` in every local and central header (UTF-8 bit 11 set and every other bit, including data-descriptor bit 3, clear);
- local and central timestamps are fixed to DOS `1980-01-01 00:00:00`; semantic times live inside canonical content;
- entries have no comment and no extra field except required ZIP64 fields; the archive has no comment;
- archives are single-disk; every central entry's `version made by` is Unix 3.0 (`0x031e`), `version needed` is `0x000a` for an entry without a ZIP64 extra and `0x002d` for an entry with one, ZIP64 end `version made by/needed` are `0x031e/0x002d`, internal attributes are zero, and external attributes are `0x81a40000` for a regular `0644` file;
- local-header ZIP64 extra field `0x0001` contains only required uncompressed and compressed 64-bit sizes, in that order; the central-header field contains only required uncompressed size, compressed size, local-header offset and disk-start number, in that order; a legacy field is the all-ones sentinel exactly when its ZIP64 value is present, and no `0x0001` field is emitted when none is required;
- if any entry/archive value requires ZIP64, emit exactly one ZIP64 end-of-central-directory record with fixed-size payload 44 and no extensible data, followed by one ZIP64 locator with disk numbers zero and total disks one, then the ordinary end record; otherwise emit neither ZIP64 end structure. Ordinary end-record count/size/offset fields use their exact value below the thresholds and the all-ones sentinel at or above them;
- CRC-32 is the PKZIP/IEEE CRC-32 of exact uncompressed entry bytes (reflected polynomial `0xedb88320`, initial/final XOR `0xffffffff`), and every size field matches those bytes;
- local entry order and central-directory order are identical to section 5.2;
- duplicate entry names, absolute paths, backslashes, drive letters, NUL, `.` segments, `..` segments, symlinks and non-regular entries are forbidden.

Using STORE avoids library-dependent compression output. Complete archive bytes are deterministic only when every semantic input plus all envelope inputs are fixed: workspace ID/timezone, request/job/attempt IDs, `exportAsOfAt`, `generatedAt`, snapshot-engine ID/version, snapshot-watermark hash, complete `domainVersions`, purge-class list and ZIP64 decision. Golden fixtures freeze that complete tuple. A future compression change requires a new archive schema.

### 5.2. Entry order and names

Every archive contains these fixed entries in this exact order, including empty record sets:

| Order | Entry | Logical schema |
|---:|---|---|
| 1 | `manifest.json` | `tradeproof_export_manifest_v1` |
| 2 | `canonical/identity_accounts.json` | `tradeproof_export_identity_accounts_v1` |
| 3 | `canonical/catalogs_taxonomies.json` | `tradeproof_export_catalogs_taxonomies_v1` |
| 4 | `canonical/imports.json` | `tradeproof_export_imports_v1` |
| 5 | `canonical/plans_reviews.json` | `tradeproof_export_plans_reviews_v1` |
| 6 | `canonical/accounting.json` | `tradeproof_export_accounting_v1` |
| 7 | `canonical/context.json` | `tradeproof_export_context_v1` |
| 8 | `canonical/weekly_lab.json` | `weekly_lab_export_projection_v1` |
| 9 | `canonical/ai_consent.json` | `tradeproof_export_ai_consent_v1` |
| 10 | `canonical/attachments.json` | `tradeproof_export_attachments_v1` |
| 11 | `canonical/pointers.json` | `tradeproof_export_pointers_v1` |
| 12 | `canonical/tombstones.json` | `tradeproof_export_tombstones_v1` |
| 13 | `csv/import_batches.csv` | `tradeproof_export_csv_v1` |
| 14 | `csv/fills.csv` | `tradeproof_export_csv_v1` |
| 15 | `csv/episodes.csv` | `tradeproof_export_csv_v1` |
| 16 | `csv/plans.csv` | `tradeproof_export_csv_v1` |
| 17 | `csv/reviews.csv` | `tradeproof_export_csv_v1` |
| 18 | `csv/context_snapshots.csv` | `tradeproof_export_csv_v1` |
| 19 | `csv/metric_snapshots.csv` | `tradeproof_export_csv_v1` |
| 20 | `csv/weekly_reports.csv` | `tradeproof_export_csv_v1` |

Binary entries follow fixed entry 20. Their exact path is `attachments/{attachment_id}.bin`; `attachment_id` MUST match `[A-Za-z0-9_-]{1,128}`. They sort by `attachment_id` using ASCII byte order. The original filename and media type are metadata only and never influence a path or extension.

An empty workspace still has all 20 fixed entries. Each canonical file contains all of its required empty record sets and each CSV contains its one header row plus LF. No attachment directory entry is emitted.

## 6. Canonical JSON and manifest

### 6.1. Encoding and primitive values

Every `.json` entry is exactly one RFC 8785 JSON value encoded as UTF-8 without BOM, trailing whitespace or trailing LF. Additional rules are:

- object member names are unique and sorted by RFC 8785;
- strings contain valid Unicode scalar values and are preserved exactly without NFC, NFD or other normalization;
- invalid UTF-8, unpaired surrogate code points and duplicate JSON member names fail export;
- timestamps are strings in UTC RFC 3339 with exactly millisecond precision, for example `2026-08-27T04:12:03.127Z`, except fields explicitly typed `epoch_ms` in the projection registry;
- `TP-MCE` exchange/source fields typed `epoch_ms` remain exact non-negative JSON integer milliseconds, including `openAt`, `sourceCloseTime`, request times, `fetchedAt` and ContextSnapshot event/cutoff/bar times. They are not transformed to strings, so `contentHash`, `inputHash` and `provenanceHash` reconstruct from the original domain types;
- canonical decimal domain values are JSON strings matching `-?(0|[1-9][0-9]*)(\.[0-9]+)?`, without exponent, leading plus, leading zero, trailing fractional zero or negative zero;
- integer counters within the interoperable exact integer range are JSON numbers; identifiers and values wider than that range are strings;
- booleans are JSON booleans; missing optional values are explicit `null`; NaN and Infinity are forbidden;
- domain-defined arrays retain their authoritative semantic order. Set-like arrays are de-duplicated and sorted by the domain contract, falling back to Unicode code-point order of each RFC 8785 element encoding.

No exporter may normalize user text, labels or filenames before canonical serialization. Safe display metadata is separate from original metadata.

### 6.2. Canonical file envelope

Every fixed canonical file other than the manifest has this exact top-level shape:

```json
{
  "exportAsOfAt": "2026-08-27T04:00:00.000Z",
  "exportSchemaId": "tradeproof_export_v1",
  "logicalSchema": "tradeproof_export_imports_v1",
  "recordSets": [
    {
      "recordType": "ImportBatch",
      "records": []
    }
  ],
  "workspaceId": "ws_opaque"
}
```

`recordSets` contains every record type assigned to that entry by section 7, even if empty, sorted by `recordType`. Every element of `records` is exactly `tradeproof_export_record_envelope_v1` with these seven members and no others:

```text
payload                   exact object from the registry row
recordKey                 exact key object from the registry row
recordSchemaId            exact registry literal
recordType                exact registry literal
sourceContractId          exact registry document ID
sourceContractVersion     1.0.0
workspaceId               tenant opaque ID | null for SHARED_PUBLIC
```

`recordKey`, exact payload members/types/nullability, schema ID, sort tuple and foreign-key rules come only from the normative registry in section 7.10.

The exporter MUST NOT rename payload fields between snake_case and camelCase. It emits the name owned by the registered source projection. Every direct payload workspace field must equal envelope `workspaceId`; a global payload cannot contain a workspace field. Unknown envelope member, payload member, record type or record schema ID fails closed. Records follow their registry sort tuple; byte-equal tuples are forbidden duplicate identities unless the registry declares a final ID tie-breaker.

### 6.3. Manifest schema

`manifest.json` has the following exact field inventory. The notation is structural, not a sample archive instance:

```text
archiveFormat                  object {
                                 compressionMethod = STORE,
                                 container = ZIP,
                                 encryption = NONE,
                                 zip64 = NONE | USED
                               }
entryCountExcludingManifest   integer >= 19
exportAsOfAt                  RFC 3339 UTC milliseconds
exportAttemptId               opaque ID
exportJobId                   opaque ID
exportRequestId               opaque ID
exportSchemaId                tradeproof_export_v1
files                         nonempty array of FileManifest records
generatedAt                   RFC 3339 UTC milliseconds, >= exportAsOfAt
jobContractVersion            tradeproof_export_job_v1
losslessScope                 DURABLE_CANONICAL_STATE_AT_CUTOFF
manifestSchemaId              tradeproof_export_manifest_v1
domainVersions               sorted nonempty array of DomainVersionRegistry records
purgedPayloadClasses          sorted unique array of allowed class IDs
snapshotEngine                object { id, version }
snapshotWatermarkSha256       64 lowercase hex
workspaceId                   exporting workspace opaque ID
workspaceTimezone            as-of IANA timezone ID
```

`snapshotEngine.id` and `.version` are actual non-secret runtime identifiers matching `[A-Za-z0-9._-]{1,64}`; no placeholder is legal. `snapshotWatermarkSha256` hashes the UTF-8 bytes of the attempt's opaque watermark so the manifest binds the snapshot without exposing the database token. Manifest `workspaceId` equals the one Workspace payload and every canonical file envelope; `workspaceTimezone` equals that Workspace's exact as-of IANA timezone. Every file `exportAsOfAt` equals the manifest and attempt. Allowed `purgedPayloadClasses` values are `AI_OUTPUT`, `ATTACHMENT_BINARY`, `RAW_IMPORT_OBJECT`, `RAW_VOICE_OBJECT` and `TRANSCRIPT_DRAFT`, sorted by Unicode code-point order. A class appears if at least one included Tombstone has that subject type; the empty array is valid.

Each `domainVersions` element has exactly `{ "contractId", "includedValues", "slot", "writerBaselineValue" }`. Entries sort by `(contractId, slot)` and `includedValues` is the sorted unique set actually present in canonical records; it is empty when the archive has no applicable record. `writerBaselineValue` is an immutable compatibility marker for this export/manifest schema, not the live domain producer's current default. The exporter never authors a domain record or chooses its version. Changing a baseline literal in this table requires new export and manifest schema identifiers. The exact required entries are:

| contractId | slot | writerBaselineValue |
|---|---|---|
| `TP-ACC` | `FEE_CONVERSION_ALGORITHM` | `fee_conversion_v1` |
| `TP-ACC` | `IMPORT_CONTRACT` | `binance_spot_trade_history_csv_v1` |
| `TP-ACC` | `IMPORT_PREVIEW_SCHEMA` | `import_preview_v1` |
| `TP-ACC` | `IMPORT_ROW_ERROR_DETAIL_SCHEMA` | `import_row_error_detail_v1` |
| `TP-ACC` | `LEDGER_ALGORITHM` | `wac_episode_v1` |
| `TP-ACC` | `MEAN_R_CI_NUMERIC_PROFILE` | `mean_r_ci_95_v1` |
| `TP-ACC` | `METRIC_ALGORITHM` | `metrics_v1` |
| `TP-ACC` | `METRICS_DECIMAL_PROFILE` | `metrics_decimal_v1` |
| `TP-ACC` | `NORMALIZED_FILL_SCHEMA` | `normalized_fill_v1` |
| `TP-ACC` | `NORTH_STAR_METRIC` | `verified_review_week_rate_v1` |
| `TP-ACC` | `PLAN_CHECKLIST_SCHEMA` | `plan_checklist_v1` |
| `TP-ACC` | `PLAN_PROOF_RULE` | `plan_proof_v1` |
| `TP-ACC` | `PROJECTION_ALGORITHM` | `episode_projection_v1` |
| `TP-ACC` | `REVIEW_TAXONOMY_BREACH_TYPE` | `breach_type_v1` |
| `TP-ACC` | `REVIEW_TAXONOMY_EMOTION` | `emotion_v1` |
| `TP-ACC` | `REVIEW_TAXONOMY_EXIT_REASON` | `exit_reason_v1` |
| `TP-ACC` | `SETUP_LABEL_KEY` | `setup_label_key_v1` |
| `TP-ACC` | `SETUP_PRESET_SCHEMA` | `setup_preset_v1` |
| `TP-ACC` | `STAGED_FILL_SCHEMA` | `staged_fill_v1` |
| `TP-LAB` | `BEHAVIORAL_EXPERIMENT_TAXONOMY` | `behavioral_experiment_v1` |
| `TP-LAB` | `EXPORT_PROJECTION` | `weekly_lab_export_projection_v1` |
| `TP-LAB` | `INTERNAL_AGGREGATE_PRODUCT_METRIC_SCHEMA` | `internal_aggregate_product_metric_snapshot_v1` |
| `TP-LAB` | `METRIC_SNAPSHOT_SCHEMA` | `metric_snapshot_v1` |
| `TP-LAB` | `PRODUCT_ANALYTICS_EVENT_SCHEMA` | `product_analytics_event_v1` |
| `TP-LAB` | `PRODUCT_MEASUREMENT_RUN_SCHEMA` | `product_measurement_run_v1` |
| `TP-LAB` | `PRODUCT_METRICS` | `product_metrics_v1` |
| `TP-LAB` | `RENDERER` | `weekly_lab_renderer_v1` |
| `TP-LAB` | `WEEKLY_SCHEMA` | `weekly_lab_v1` |
| `TP-LAB` | `WORKSPACE_PRODUCT_METRIC_SCHEMA` | `workspace_product_metric_snapshot_v1` |
| `TP-MCE` | `CONTEXT_ALGORITHM` | `mce-binance-spot-v1.0.0` |
| `TP-MCE` | `CONTEXT_PARAMETER_SET` | `mce-default-v1` |
| `TP-MCE` | `MARKET_BAR_AS_OF_SELECTOR` | `market_bar_as_of_v1` |
| `TP-SEC` | `AI_ARTIFACT_CONTRACT` | `ai_artifact_v1` |
| `TP-SEC` | `AI_CONFIRMATION_REQUEST_SCHEMA_TAXONOMY` | `taxonomy_suggestion_confirmation_request_v1` |
| `TP-SEC` | `AI_CONSENT_CONTRACT` | `ai_consent_v1` |
| `TP-SEC` | `AI_OUTPUT_SCHEMA_TAXONOMY_SUGGESTION` | `taxonomy_suggestion_v1` |
| `TP-SEC` | `AI_OUTPUT_SCHEMA_TRANSCRIPT_DRAFT` | `transcript_draft_v1` |
| `TP-SEC` | `AI_OUTPUT_SCHEMA_WEEKLY_SUMMARY` | `weekly_summary_v1` |
| `TP-SEC` | `UPLOAD_ATTACHMENT_CONTRACT` | `upload_attachment_v1` |

Historical archives may contain multiple values in one slot. The exporter includes every observed value, the reader validates every record value belongs to that slot's `includedValues`, and no value is rewritten to the writer baseline. For a field declared `ver<C:S>`, a previously unknown nonempty exact value is permitted only when its source record still satisfies the complete v1 projection/schema and closure; a v1 reader preserves that opaque value and validates registry membership without inventing its semantics. A producer change requiring a field, enum, nested shape, hash-basis or closure change requires a new export schema. `writerBaselineValue` is populated even for an empty workspace and remains the exact table literal; it is not implicitly inserted into an otherwise empty `includedValues`. Aliases, case folding and implicit `latest` remain forbidden. `INTERNAL_AGGREGATE_PRODUCT_METRIC_SCHEMA.includedValues` MUST always be empty in a workspace archive; a nonempty value fails `EXPORT_CROSS_TENANT_REFERENCE`.

For each `AI_OUTPUT_SCHEMA_*` slot, `includedValues` contains its one baseline value if and only if at least one retained AiRun for the matching feature, AiOutput of the matching kind or AiOutputSubject of that kind is present; otherwise it is empty. The subject case preserves the exact v1 kind-to-schema mapping after content deletion. A run/output/subject value in the wrong slot, a successful bundle mismatch, or a present value absent from its required slot fails `EXPORT_VERSION_UNSUPPORTED`.

`TP-SEC:AI_ARTIFACT_CONTRACT.includedValues` is exactly `["ai_artifact_v1"]` iff any AiRun, AiRunInputReference, AiOutputSubject, AiOutputSubjectStateEvent, AiOutput, AiOutputReference, TranscriptConfirmation or TaxonomySuggestionConfirmation is present; otherwise it is empty. A payload-free subject or surviving confirmation that outlives deleted output content therefore still declares its authoritative persistence contract even though the active bundle has been replaced by a Tombstone; neither record nor its unsalted integrity hash is anonymous.

`TP-ACC:IMPORT_PREVIEW_SCHEMA.includedValues` is exactly `["import_preview_v1"]` iff any included ImportBatch carries its copied source-preview proof; temporary ImportPreview rows are never required or serialized. `TP-ACC:STAGED_FILL_SCHEMA.includedValues` is exactly `["staged_fill_v1"]` iff any StagedFill is included. `TP-LAB:PRODUCT_MEASUREMENT_RUN_SCHEMA.includedValues` is exactly `["product_measurement_run_v1"]` iff any ProductMeasurementRun or ProductMeasurementRunStateEvent is included. `TP-SEC:AI_CONFIRMATION_REQUEST_SCHEMA_TAXONOMY.includedValues` is exactly `["taxonomy_suggestion_confirmation_request_v1"]` iff any TaxonomySuggestionConfirmation is included. Every named record field must byte-equal its slot value; otherwise the corresponding set is empty. None of these rules authorizes an excluded preview/control/receipt record.

Each `REVIEW_TAXONOMY_*` slot contains the sorted unique `taxonomy_version` values of included ReviewTaxonomyVersion records with its exact corresponding taxonomy type, and every ReviewRevision field of that type must belong to the same set. The three baseline literals remain present only in `writerBaselineValue` when their type has no included version.

`TP-LAB:EXPORT_PROJECTION.includedValues` is always exactly `["weekly_lab_export_projection_v1"]` because fixed entry 8 exists even when all of its record sets are empty. `TP-LAB:INTERNAL_AGGREGATE_PRODUCT_METRIC_SCHEMA.includedValues` is always empty because that cross-workspace record is forbidden. These two closed rules override the general record-field population rule; no other slot may claim a value solely because it is a writer baseline.

`TP-MCE:MARKET_BAR_AS_OF_SELECTOR.includedValues` is exactly `["market_bar_as_of_v1"]` iff any included ContextSnapshot or market-bar FeeConversion consumes a bar selection, or any MarketBarResolution is included; otherwise it is empty. The value names the exact cutoff/candidate/resolution/observation algorithm in sections 4.4 and 7.10 and is never inferred as `latest`.

`TP-ACC:MEAN_R_CI_NUMERIC_PROFILE.includedValues` is exactly `["mean_r_ci_95_v1"]` iff an included MetricSnapshot has `metric_id = mean_r_ci_95`; that snapshot's `metric_formula_version` must equal the slot value. Otherwise it is empty.

`TP-ACC:METRICS_DECIMAL_PROFILE.includedValues` is exactly `["metrics_decimal_v1"]` iff any included MetricSnapshot has `value_type = DECIMAL | INTERVAL`; otherwise it is empty. Every such snapshot also has `metric_algorithm_version = metrics_v1` and must validate under the exact profile below; the profile is never inferred from a runtime decimal library.

`TP-ACC:IMPORT_ROW_ERROR_DETAIL_SCHEMA.includedValues` is exactly `["import_row_error_detail_v1"]` iff any included ImportRow has non-null `error_detail_json`; otherwise it is empty. The identifier names the closed nested object and hash basis in section 7.3 even though it is not repeated as a member inside that object.

`archiveFormat.zip64` is the fixed-width four-ASCII-character string `NONE` or `USED` and reports whether ZIP64 structures are actually present. The writer computes a monotone prospective layout: start with no ZIP64 fields, serialize the complete manifest with `NONE`, calculate every entry/archive value, add every field that reaches a section 5.1 threshold, re-layout with exactly those required extras/sentinels, and repeat until no field is added. Fields are never removed during this calculation, so it terminates after at most the finite number of candidate fields. The fixed point uses `USED` iff its set is nonempty; replacing equal-width `NONE` with `USED` changes manifest content/CRC but cannot move an offset. The writer emits that layout and verifies every actual sentinel/extra/ZIP64 end structure matches the fixed point. This rule handles cascading offsets and has no manifest-length self-reference. `entryCountExcludingManifest` counts fixed entries 2-20 plus binary attachment entries. `files` contains one record for every entry except `manifest.json`:

```json
{
  "logicalSchema": "tradeproof_export_imports_v1",
  "mediaType": "application/json",
  "path": "canonical/imports.json",
  "recordCount": 25,
  "sha256": "<64-lowercase-hex>",
  "uncompressedSizeBytes": 12345
}
```

For canonical files, `recordCount` is the sum of records in all record sets. For CSV it excludes the header. For a binary it is `1` and `logicalSchema = tradeproof_export_attachment_binary_v1`. `files` follows archive entry order excluding the manifest. Media types are exact: `application/json` for canonical JSON, `text/csv; charset=utf-8` for CSV and `application/octet-stream` for binary entries.

SHA-256 is computed over exact uncompressed entry bytes. The manifest MUST NOT list or checksum itself, so there is no recursive checksum. An archive SHA-256 MAY be provided in the authenticated job response and audit evidence; it is deliberately out-of-band and is never inserted into the archive.

## 7. Closed record allowlist

Only the record types below may appear. A newly persisted canonical record type requires an explicit contract revision; it MUST NOT be silently placed in a generic `extra` set.

### 7.1. Identity and account

`canonical/identity_accounts.json` contains, in record-type order:

| Record type | Ownership | Inclusion |
|---|---|---|
| `TradingAccount` | WORKSPACE | All visible records |
| `Workspace` | WORKSPACE | The exporting workspace |
| `WorkspaceOwnerProfile` | WORKSPACE | Stable owner-profile identity header; no authentication secret, token or provider credential |
| `WorkspaceOwnerProfileRevision` | WORKSPACE | Every visible immutable profile revision, including superseded display-name/locale history |

TP-EXP packages TP-ACC's stable `WorkspaceOwnerProfile` header, every visible immutable revision and the `CURRENT_WORKSPACE_OWNER_PROFILE` pointer. Bootstrap guarantees revision 1. A convenience current view, if tooling needs one, derives exactly `{ owner_user_id, workspace_id, display_name, locale, created_at = header.created_at, updated_at = selected_revision.recorded_at }`; it is not an additional archive record. Email, password material, OIDC tokens/claims beyond the ownership ID, magic links, sessions and recovery secrets are excluded. `User`, `UserIdentity`, `IdentityProviderRegistration`, `IdentityProviderRegistrationStateEvent`, encrypted shared-provider grant locators, identity-generation tombstones, `identity_provider_deletion_inventory_v1`, break-glass notices and every IdP unlink/delete/status proof are Restricted authentication/deletion control records and are never archive records or `domainVersions` values. A pinned retry selects the same profile revision and produces the same bytes.

The sole exported Workspace is ACTIVE at request/snapshot publication. Its `lifecycle_state = ACTIVE`, `deletion_guard_generation` is the exact positive generation pinned by the attempt, and `deletion_id`, `deleting_at` and `deleted_at` are all null. Any other value fails `EXPORT_WORKSPACE_DELETING`; a post-cutoff deletion follows section 3.3 and can never be justified by preserving the old Workspace payload alone.

### 7.2. Catalogs and taxonomies

`canonical/catalogs_taxonomies.json` contains:

| Record type | Ownership | Inclusion |
|---|---|---|
| `BehavioralExperimentTaxonomyItem` | SHARED_PUBLIC | Every item in each referenced version |
| `BehavioralExperimentTaxonomyVersion` | SHARED_PUBLIC | Every referenced exact version, including `behavioral_experiment_v1` |
| `Instrument` | SHARED_PUBLIC | Every instrument referenced by an included workspace record |
| `InstrumentCatalogPublishEvent` | SHARED_PUBLIC | Complete sequence prefix 1..N through the cutoff pointer event |
| `InstrumentCatalogVersion` | SHARED_PUBLIC | Complete row set of every referenced or pointer-target version |
| `MarketConversionCatalogPublishEvent` | SHARED_PUBLIC | Complete sequence prefix 1..N through the cutoff pointer event |
| `MarketConversionCatalogVersion` | SHARED_PUBLIC | Complete row set of every referenced or pointer-target version |
| `ReviewTaxonomyItem` | SHARED_PUBLIC | Every ID/label item in each referenced Review taxonomy version |
| `ReviewTaxonomyPublishEvent` | SHARED_PUBLIC | Complete per-taxonomy-type sequence prefix 1..N through the greatest referenced version event |
| `ReviewTaxonomyVersion` | SHARED_PUBLIC | Every referenced `exit_reason_v1`, `breach_type_v1`, `emotion_v1` or later exact version |

Catalog versions include the historical validity windows and immutable metadata defined by `TP-ACC`. Exporting only a current row is forbidden. For each catalog family, N is the `event_sequence` of the exact event selected by its cutoff pointer. The archive includes every publish event 1..N, every version targeted by that prefix and the complete row set for every such version, even when a workspace record directly references only one version. For every included catalog version, the complete row set and its publish event are included atomically: row `published_at = event.recorded_at`; row hashes recompute from TP-ACC's exact RFC 8785 bases; the event hash recomputes over the sorted `{ contentSha256, recordKey }` list and excludes event identity/sequence/time. Family sequences are contiguous and nondecreasing in time. A missing prefix event, target version or row fails `EXPORT_REFERENCE_DANGLING`; unrelated later family events remain excluded.

For each Review taxonomy type referenced by any included ReviewRevision or AI reference, N is the greatest `event_sequence` whose event targets a referenced version. The archive includes that type's complete event prefix 1..N, every version targeted by the prefix and every version's complete nonempty ordered item set. Thus a reference to version 3 necessarily carries events, versions and item sets for versions 1, 2 and 3; continuity is never validated against an omitted prefix. Version hash is the RFC 8785 hash of `{ "items", "taxonomyType", "taxonomyVersion" }`, publish-event hash equals it, `published_at = recorded_at`, and per-type publish sequence is contiguous/nondecreasing. Unreferenced events after N are excluded. BehavioralExperimentTaxonomyVersion similarly closes to its complete seven-row v1 item set and TP-LAB's exact RFC 8785 version hash. Partial sets, reused versions with different bytes, duplicate key/order, regenerated timestamps or a missing prefix fail closed.

### 7.3. Imports

`canonical/imports.json` contains:

| Record type | Inclusion |
|---|---|
| `ImportBatch` | Every original and exact-file alias batch, terminal or non-terminal at the cutoff |
| `ImportResolution` | Every durable resolution visible at the cutoff |
| `ImportRow` | Every durable row metadata/disposition record, including quarantined and alias rows |
| `ReplayConflictPreview` | Every durable preview referenced by a resolution or still part of workspace history |
| `StagedFill` | Every immutable multiplicity-ambiguous candidate, resolved or unresolved |
| `StagedFillDisposition` | The optional immutable resolution fate for each staged candidate |
| `Upload` | Every durable `upload_attachment_v1` metadata aggregate; never raw object bytes or provider location |
| `UploadObjectAbsenceVerification` | Every safe durable raw-object absence proof visible at the cutoff |
| `UploadStateEvent` | Every append-only upload transition through PURGE |

Import records retain source row number, sanitized diagnostic, source hash and canonical references defined by `TP-ACC`. `raw_cells_json`, raw CSV lines and purged source bytes are forbidden.

ImportPreview and ImportPreviewStateEvent are temporary command artifacts and never archive records. Every ImportBatch instead preserves immutable copied proof fields `source_import_preview_id`, `source_preview_schema_version = import_preview_v1`, `source_preview_summary_sha256` and `confirmed_at`, which must match its contract/catalog/file/upload tuple and the creation-time IMPORT control evidence. The copied preview ID is explicitly non-resolving provenance after preview expiry, not a dangling FK or permission to export the temporary row.

`ImportRow.error_code` and `error_detail_json` are both null for `RECONCILED | DUPLICATE` and both non-null for `ACCOUNTING_PENDING | QUARANTINED`. A non-null detail is exactly `import_row_error_detail_v1` with these eight members and no others:

```json
{
  "columnIndex": 4,
  "columnName": "Price",
  "diagnosticSha256": "...",
  "expectedCode": "POSITIVE_DECIMAL",
  "observedCountCapped": null,
  "observedLengthCapped": 32769,
  "ruleCode": "DECIMAL_OVERFLOW",
  "truncated": true
}
```

`ruleCode` equals the row's `error_code`. Its closed mapping to `columnName` and `expectedCode` is:

| ruleCode | columnName | expectedCode |
|---|---|---|
| `COLUMN_COUNT_MISMATCH` | null | `EXACT_COLUMN_COUNT` |
| `INVALID_TIMESTAMP` | `Date(UTC)` | `UTC_TIMESTAMP` |
| `INVALID_DECIMAL`, `DECIMAL_OVERFLOW` | exact offending numeric column | `NONNEGATIVE_DECIMAL` for `Fee`; otherwise `POSITIVE_DECIMAL` |
| `INVALID_SIDE` | `Side` | `BUY_OR_SELL` |
| `UNKNOWN_INSTRUMENT`, `INSTRUMENT_NOT_IMPORT_SUPPORTED`, `INSTRUMENT_VALIDITY_AMBIGUOUS`, `UNSUPPORTED_QUOTE_ASSET` | `Pair` | `KNOWN_INSTRUMENT` |
| `EXECUTED_ASSET_MISMATCH` | `Executed` | `MATCHING_ASSET` |
| `AMOUNT_ASSET_MISMATCH` | `Amount` | `MATCHING_ASSET` |
| `AMOUNT_PRICE_MISMATCH` | `Amount` | `PRICE_AMOUNT_TOLERANCE` |
| `BUY_BASE_FEE_EXCEEDS_EXECUTED` | `Fee` | `BASE_FEE_NOT_EXCEED_EXECUTED` |
| `DUPLICATE_MULTIPLICITY_AMBIGUOUS` | null | `UNIQUE_MULTIPLICITY` |
| `SEQUENCE_AMBIGUOUS` | null | `PARTIAL_ORDER` |
| `SELL_WITHOUT_OPEN_POSITION`, `SELL_EXCEEDS_POSITION` | null | `LONG_ONLY_POSITION` |
| `FEE_CONVERSION_UNAVAILABLE` | `Fee` | `AVAILABLE_FEE_CONVERSION` |
| `HISTORICAL_REPLAY_CONFLICT` | null | `REPLAY_CONFIRMATION` |
| `LEDGER_INVARIANT_FAILED` | null | `LEDGER_INVARIANT` |

No other `ImportRow.error_code` is legal. `columnName` and `columnIndex` are jointly null or non-null. A non-null name is exactly `Date(UTC) | Pair | Side | Price | Executed | Amount | Fee`, and its index is the matching one-based position 1..7. `expectedCode` is always non-null and only a value in the table. The observed fields are non-negative safe magnitudes, never source values: `observedLengthCapped` is null when irrelevant or the exact Unicode-scalar/byte length required by the named rule capped at 32769; `observedCountCapped` is null when irrelevant or the exact row/group/count magnitude capped at 100001. Sentinel 32769 means at least 32769 and sentinel 100001 means at least 100001. `truncated` is true iff either underlying magnitude exceeded its cap, not merely equaled it.

`diagnosticSha256` is lowercase `SHA256(UTF8("tradeproof_import_error_detail_v1\u0000") || RFC8785(object_without_diagnosticSha256))`, where the escape denotes one literal NUL byte. The seven-member hash object retains the exact remaining member names, types and nulls above. Raw cells, rows, filenames, exception messages, user text and hashes of any such content are forbidden in this object. A reader recomputes the hash and rejects any missing, extra, unknown, mismapped, uncapped or privacy-bearing member before staging.

Import-time fill references are immutable. RECONCILED requires only `normalized_fill_id`; DUPLICATE requires only `duplicate_of_fill_id`; QUARANTINED requires all three fill IDs null. An original ACCOUNTING_PENDING/DUPLICATE_MULTIPLICITY_AMBIGUOUS row requires only `staged_fill_id`; a pending proven-new canonical fact requires only `normalized_fill_id`; a proven duplicate pending target requires only `duplicate_of_fill_id`; and an exact-file alias of a multiplicity row has all three null with non-null batch `duplicate_file_of_batch_id`. No other status/error/reference combination is legal. Current multiplicity fate comes only from StagedFillDisposition and never mutates ImportRow or batch counters.

Every StagedFill is the byte-exact immutable parsed candidate for one same-workspace ImportRow, has `staged_fill_v1`, no `dedup_key` and no admission flag. Its optional disposition is unique by staged fill and ImportResolution. ADMITTED_AS_NEW requires one NormalizedFill copied from the candidate plus the resolution-derived dedup key and forbids a duplicate target; DISCARDED_AS_DUPLICATE requires one same-account/instrument/signature NormalizedFill target and forbids a created candidate fill. Missing, double, cross-row or outcome/null mismatches fail closure. NormalizedFill existence itself means admitted/proven-new: it has immutable `created_at` and no `admission_status` or `admitted_at` field.

An Upload becomes canonical only when the RAW_UPLOAD ObjectIngestReservation TRANSFER transaction creates the Upload header, sequence-1 RECEIVE event, exact object lease and forced-purge command together. No header may lack RECEIVE/lease, and no reservation-owned or pre-transfer object/metadata is exported.

Every Upload persists `purge_due_at =` its sequence-1 RECEIVE `recorded_at + 24 hours`; derived `forced_purge_at = purge_due_at - 4 hours = RECEIVE + 20 hours`. Neither deadline extends after acceptance, import, transcription, confirmation or `keep_original`. RECEIVE atomically enqueues the non-exported forced-purge command. Natural deletion may start earlier; otherwise it starts no later than forced purge. A QUARANTINED or VALIDATING Upload at that boundary first appends REJECT with `safe_reason_code = RAW_UPLOAD_RETENTION_DEADLINE`; an ACCEPTED Upload retains ACCEPT state until PURGE but all raw reads are denied. PURGE still occurs only after physical absence verification.

The only Upload transition paths are RECEIVE/QUARANTINED -> START_VALIDATION/VALIDATING or REJECT/REJECTED; VALIDATING -> ACCEPT/ACCEPTED or REJECT/REJECTED; and either ACCEPTED or REJECTED -> PURGE/PURGED. REJECT creates no business record. An event skipped, reordered or appended after PURGE fails stream validation.

A PURGE event has a non-null `object_absence_verification_id` resolving one same-workspace UploadObjectAbsenceVerification for the same upload, with `verified_absent_at <= PURGE.recorded_at <= purge_due_at` in a conforming on-time path; all earlier event types require null. The verification contains no object key, URL, content or provider receipt bytes and proves the exact pinned provider object version/generation absent. Absence verification insertion, lease transition to ABSENCE_VERIFIED, PURGE and the CSV/VOICE SubjectDeletionReceipt are one transaction, so no committed proof-before-PURGE state is legal. A crash before that transaction retries delete/verification; a crash after commit observes all effects and returns them idempotently. If verification misses `purge_due_at`, the Upload remains read-denied and non-PURGED, raises the TP-SEC retention incident and continues real deletion; the exporter never fabricates proof. Raw-object leases, deadlines, deletion attempts/outbox commands and provider locations remain restricted non-exported control-plane data.

ImportBatch reconciliation is field-exact. After file admission, `data_rows = count(ImportRow)` and equals `reconciled_rows + duplicate_rows + accounting_pending_rows + quarantined_rows`; every counter equals its exact row-status count. Let `numerator = reconciled_rows + duplicate_rows` and `denominator = data_rows`. A positive denominator requires `reconciliation_rate = round_scale18_half_even(numerator / denominator)`, serialized as a canonical decimal after one division, trailing-zero stripping and zero normalization; binary floating point is forbidden. Denominator zero requires null rate, status NEEDS_ATTENTION and `EMPTY_FILE`. A pre-admission REJECTED batch has no ImportRow, all four counters zero, and null data_rows/rate.

Terminal status derives from exact integers, never the rounded rate: COMPLETE requires `numerator = denominator` with no pending/quarantine; PARTIAL requires `98 * denominator < 100 * numerator < 100 * denominator` and no blocking projection conflict; NEEDS_ATTENTION requires null rate, `100 * numerator <= 98 * denominator` or a blocking conflict. Cross-products use arbitrary-precision integers. Thus exact 98% is NEEDS_ATTENTION and only a value strictly above 98% and below 100% can be PARTIAL. A rate, counter, row population, status or alias clone that violates these equalities fails `EXPORT_SCHEMA_VIOLATION`; the reader recomputes them from included ImportRows.

`ReplayConflictPreview` nested JSON is closed, canonical domain data rather than an opaque extension. `based_on_active_projection_refs_json` is a sorted unique array of exact TradeEpisodeProjection record keys `{ "episode_id": id, "projection_version": int }`, each of which resolves to an included published projection. `proposed_projection_refs_json` is a sorted unique array by those record-key RFC 8785 bytes. Each member has exactly:

```json
{
  "accountingQuality": "COMPLETE",
  "associatedPlanRecordKey": null,
  "associatedPlanRevisionRecordKey": null,
  "averageCostQuotePerBase": null,
  "closedFillRecordKey": null,
  "feeConversionRecordKeys": [],
  "fillRecordKeys": [{ "fill_id": "..." }],
  "firstFillRecordKey": { "fill_id": "..." },
  "frozenPlanRevisionRecordKey": null,
  "grossRealizedPnlQuote": "0",
  "knownFeeQuote": "0",
  "ledgerAlgorithmVersion": "wac_episode_v1",
  "netRealizedPnlQuote": "0",
  "openCostBasisQuote": "100",
  "planProofReasonCode": "NO_ELIGIBLE_CANDIDATE",
  "planCandidateRecordKeys": [],
  "planProofBasisSha256": "...",
  "planProofRuleVersion": "plan_proof_v1",
  "planProofStatus": "UNMATCHED",
  "positionQtyBase": "1",
  "proposalDigestSha256": "...",
  "projectionAlgorithmVersion": "episode_projection_v1",
  "recordKey": { "episode_id": "...", "projection_version": 2 },
  "state": "OPEN"
}
```

The shown scalar values illustrate types; actual values obey the exact TradeEpisodeProjection enums, decimal-string/null coupling and domain-version slots. `fillRecordKeys` is the exact nonempty unique allocation order; `firstFillRecordKey` equals element 1. CLOSED requires a non-null `closedFillRecordKey` equal to the final close-to-zero fill, while OPEN requires null. `feeConversionRecordKeys` has exact keys `{ "fee_conversion_id": id }`, sorts by the referenced `(fill_id, conversion_version, fee_conversion_id)` and equals every conversion selected by the proposed ledger. Plan keys are exact `{ "trade_plan_id": id }` and `{ "trade_plan_revision_id": id }`; candidates sort by key bytes and plan association/proof/null rules equal TP-ACC. Every fill, conversion, plan and plan-revision key resolves to an included same-workspace record. `planProofBasisSha256` hashes the exact would-be `plan_proof_basis_json`. `proposalDigestSha256` is SHA-256 of the RFC 8785 bytes of the same proposal member with only `proposalDigestSha256` omitted. A proposal `recordKey` is preview-local and MUST NOT be treated as a dangling TradeEpisodeProjection FK before confirmation.

`episode_mapping_json` is an array with exact members `{ "mappingOrdinal": int, "newProposalRecordKeys": [recordKey...], "oldProjectionRecordKeys": [recordKey...], "relation": string }`. Entries sort by unsigned RFC 8785 bytes of first old key or the empty-byte sentinel, then first new key or the empty-byte sentinel, then `relation`; `mappingOrdinal` is contiguous 1..N after sorting. Relation and cardinality are closed: `SAME_ID_CHANGED` is 1/1 with the same episode ID, `SPLIT` is 1/2+, `MERGE` is 2+/1, `REMOVED` is 1/0 and `ADDED` is 0/1. Old arrays partition the based-on refs exactly once and new arrays partition proposal keys exactly once.

`impact_json` has exactly this shape:

```json
{
  "eligibilityImpacts": [{
    "newProjectionRecordKey": {},
    "priorEligibilityEventRecordKeys": [{ "episode_metric_eligibility_event_id": "..." }],
    "requiresDecision": true
  }],
  "planImpacts": [{
    "change": "CHANGED",
    "newPlanRevisionRecordKey": null,
    "newProjectionRecordKey": {},
    "oldPlanRevisionRecordKey": null,
    "oldProjectionRecordKey": {}
  }],
  "reviewImpacts": [{
    "outcome": "RECONFIRM_REQUIRED",
    "reviewRecordKey": { "review_id": "..." },
    "reviewRevisionRecordKey": { "review_revision_id": "..." },
    "sourceProjectionRecordKey": {},
    "targetProjectionRecordKey": {}
  }],
  "sourceFillRecordKeys": [{ "fill_id": "..." }],
  "triggerImportRowRecordKeys": [{ "import_row_id": "..." }]
}
```

Every array sorts by primary record-key RFC 8785 bytes and has no duplicate. Eligibility contains exactly one member per proposal, `requiresDecision` is true, and its prior-event keys sort by referenced `event_sequence`. Plan `change` is `UNCHANGED`, `CHANGED`, `REMOVED` or `ADDED`, with old/new nullability matching TP-ACC. Review `outcome` is `RECONFIRM_REQUIRED` only for the same episode ID with a new version and then requires a proposal target; `HISTORICAL_ONLY` applies to split/merge/removed identity and requires null target. Published projection keys resolve to included TradeEpisodeProjection records; proposal/target keys resolve exactly once inside the preview proposal array. Review, ReviewRevision, EpisodeMetricEligibilityEvent, TradePlanRevision, NormalizedFill and ImportRow keys resolve to included same-workspace records.

`source_input_digest` is lowercase SHA-256 of RFC 8785 exact object `{ "basedOnActiveProjectionRefs": based_on_active_projection_refs_json, "episodeMappings": episode_mapping_json, "impact": impact_json, "proposedProjections": proposed_projection_refs_json }`. `expires_at` is exactly `created_at + 24 hours`. The reader recomputes every proposal digest and `source_input_digest`; raw cells are forbidden.

`ImportResolution.payload_json` is also a closed action-discriminated object:

```text
ACCEPT_AS_NEW  -> {}
MARK_DUPLICATE -> { "targetFillRecordKey": { "fill_id": id } }
SET_SEQUENCE   -> {
  "ambiguousGroupDigestSha256": hash,
  "orderedMembers": [{
    "fillRecordKey": { "fill_id": id },
    "importRowRecordKey": { "import_row_id": id }
  }]
}
CONFIRM_REPLAY -> {
  "eligibilityDecisions": [{
    "action": "EXCLUDE" | "RESTORE",
    "projectionRecordKey": { "episode_id": id, "projection_version": int }
  }],
  "previewSourceInputDigest": hash,
  "replayConflictId": id
}
```

ACCEPT_AS_NEW is valid only for one staged `DUPLICATE_MULTIPLICITY_AMBIGUOUS` row. Its admitted NormalizedFill retains `canonical_signature` and has `dedup_key = lowercase_hex(SHA-256(UTF8("tradeproof_accept_as_new_v1\u0000" + lowercase-canonical-UUID(resolution_id))))`; the shown escape denotes one literal NUL delimiter and there is no BOM, whitespace or trailing NUL. MARK_DUPLICATE's target key resolves another included NormalizedFill with the same workspace, account, instrument and canonical signature; it is not the resolving row, an alias-only source or a duplicate chain.

For SET_SEQUENCE, the ambiguous group is TP-ACC's exact maximal connected component of unresolved staged-fill interval overlaps in one workspace/account/instrument whose allowed topological orders can change episode/accounting output. `orderedMembers` is nonempty, has unique fill and row keys, contains that component exactly, preserves the user's selected order and every `provably_before` edge, and excludes its dependent suffix. Each pair closes both included records and `ImportRow.normalized_fill_id` equals its fill ID. `ambiguousGroupDigestSha256` hashes RFC 8785 exact object below, whose `members` contains the same pairs sorted by unsigned RFC 8785 bytes of `importRowRecordKey`, not user order:

```json
{
  "instrumentRecordKey": { "instrument_id": "..." },
  "members": [{
    "fillRecordKey": { "fill_id": "..." },
    "importRowRecordKey": { "import_row_id": "..." }
  }],
  "schemaId": "import_sequence_ambiguous_group_v1",
  "tradingAccountRecordKey": { "trading_account_id": "..." },
  "workspaceId": "..."
}
```

The account/instrument keys resolve the same workspace records as every member. SET_SEQUENCE outer `import_row_id` is the member row whose key has the smallest RFC 8785 bytes in digest order. ACCEPT_AS_NEW and MARK_DUPLICATE outer `import_row_id` is their sole resolving row. Those three actions require non-null `import_row_id` and null `replay_conflict_id`.

For CONFIRM_REPLAY the reverse null rule holds. Payload conflict ID/digest equal the referenced preview; decisions sort by proposal-key bytes and partition all proposal keys once with no extra or missing key. Confirmation provenance is valid only when TP-ACC's locked revalidation reproduced byte-identical active refs, proposals, mappings, impacts and digest before atomic publish; the exported final projection keys/boundary/plan/accounting summaries and appended eligibility decisions must equal that confirmed preview. Every resolution retry requires byte-equal action, outer references and RFC 8785 payload; a stale, expired, partially closed, duplicate-chain, group/digest/order mismatch or post-hoc rewritten resolution is `EXPORT_SCHEMA_VIOLATION`. Raw CSV/cell values are forbidden in every action.

### 7.4. Plans, setups, reviews and attachments

`canonical/plans_reviews.json` contains:

| Record type | Inclusion |
|---|---|
| `Attachment` | Every durable attachment metadata record, including deleted/tombstoned binary state |
| `AttachmentObjectAbsenceVerification` | Every safe durable retained-object absence proof visible at the cutoff |
| `AttachmentStateEvent` | Every append-only activate/delete transition |
| `PlanEpisodeAssociation` | All append-only late associations |
| `PlanMatchResolution` | All append-only ambiguous-match resolutions |
| `PlanStateEvent` | All append-only plan state events |
| `Review` | All Review aggregates |
| `ReviewRevision` | All revisions, including those for superseded episode projections |
| `ReviewRevisionAttachment` | All immutable joins and their frozen content hashes |
| `SetupPreset` | All workspace setup aggregates referenced by plan history or still durable at the cutoff |
| `SetupPresetRevision` | All immutable revisions, including renamed/archived history |
| `SetupPresetStateEvent` | All append-only setup state events |
| `TradePlan` | All plan aggregates, including terminal plans |
| `TradePlanRevision` | All immutable revisions, not only armed/frozen revisions |

`SetupPreset`, `SetupPresetRevision` and `SetupPresetStateEvent` are the exact `TP-ACC` projections using `setup_preset_v1`, `setup_label_key_v1` and `plan_checklist_v1`. Frozen label/checklist snapshots remain on TradePlanRevision.

V1 persists no plan draft and exports no abandoned client form. ARM atomically creates TradePlan already ARMED, revision 1 and PlanStateEvent sequence 1; `TradePlan.created_at = revision.recorded_at = ARM.recorded_at`, ARM `armed_revision_id` equals revision 1, and every visible header must have that complete triple. TradePlanRevision 1 has null `based_on_revision_id`; revision N>1 points to the same-plan current revision N-1, with contiguous numbering and nondecreasing `recorded_at`. REVISE creates only the next revision, while ARM/CONSUME/CANCEL/EXPIRE are the only state events. PlanCommandReceipt is command idempotency/control evidence and is explicitly non-exported.

Attachment activation is atomic. Before any provider write, TP-SEC creates a restricted SANITIZED_ATTACHMENT ObjectIngestReservation. Every SCREENSHOT ACCEPT transaction transfers exactly one sanitized reservation/object lease and creates one same-source SCREENSHOT Attachment plus its sequence-1 ACTIVATE event; neither record may be absent and no second Attachment may share that source. An accepted VOICE transfers exactly one RETAINED_VOICE reservation/lease and creates Attachment/ACTIVATE only in a `keep_original = true` TranscriptConfirmation transaction; false creates none. CSV never creates an Attachment. Source validity requires an immutable prior same-workspace ACCEPT event, non-null `Upload.accepted_at = ACCEPT.recorded_at`, equal contract and exact kind mapping; it remains valid after the source Upload becomes PURGED and is never a current-ACCEPTED-state FK. Header/event have equal `workspace_id` and `contract_version`, with `Attachment.created_at = ACTIVATE.recorded_at`.

DELETE_COMPLETE alone has a non-null `object_absence_verification_id`, which resolves one same-workspace AttachmentObjectAbsenceVerification for that Attachment and exact `content_object_version`, with `verified_absent_at <= DELETE_COMPLETE.recorded_at`; ACTIVATE and DELETE_REQUEST require null. Absence-proof insertion, lease terminal transition, DELETE_COMPLETE and matching ATTACHMENT_BINARY SubjectDeletionReceipt are one transaction, so no committed proof-before-DELETE_COMPLETE state is legal. Crash before it retries real deletion/verification; crash after commit returns all effects idempotently. No reservation, lease, staging byte, provider key, URL or receipt body is serialized.

For PlanMatchResolution, both association JSON values have exactly `{ "trade_plan_id": id-or-null, "trade_plan_revision_id": id-or-null }`, with the pair both null or both non-null and closing to the same plan/revision. CONFIRM_ASSOCIATION and SELECT_CANDIDATE require both selected scalar IDs; REMOVE_ASSOCIATION requires both null. CONFIRM selected IDs equal the old/current association. SELECT_CANDIDATE selected IDs equal one exact frozen candidate in the based-on projection's plan candidate/basis JSON. The new association equals the action result, old association equals the based-on projection, and a resolution-created projection preserves AMBIGUOUS proof/basis while pointing back to this resolution. Any mismatch fails closure.

`canonical/attachments.json` separately contains one `AttachmentExportDescriptor` for every Attachment:

```text
attachment_id
workspace_id
attachment_kind              SCREENSHOT | RETAINED_VOICE
availability                 RETAINED_CLEAN | DELETE_PENDING | TOMBSTONED
state_at_cutoff
scan_status_at_cutoff
original_filename            nullable
safe_display_filename
media_type
byte_size
content_sha256
content_object_version
archive_path                 attachments/{attachment_id}.bin | null
created_at
deleted_at                   nullable
```

### 7.5. Fills, episodes, accounting and north-star

`canonical/accounting.json` contains:

| Record type | Inclusion |
|---|---|
| `AccountingLedgerEntry` | All entries from every projection version |
| `EpisodeFillAllocation` | All allocations from every projection version |
| `EpisodeMetricEligibilityEvent` | All EXCLUDE/RESTORE history |
| `FeeConversion` | Every conversion version, including unavailable/superseded versions |
| `NormalizedFill` | Every immutable admitted fill; mere staging exists only as `StagedFill` |
| `TradeEpisode` | Every stable episode identity, including identities with only superseded projections |
| `TradeEpisodeProjection` | Every projection version, including superseded/invalid historical versions |
| `VerifiedReviewWeekRateMetricSnapshot` | Every immutable north-star snapshot using `verified_review_week_rate_v1` |

All version fields required by `TP-ACC` remain exact. At minimum, relevant records preserve `normalized_fill_v1`, `episode_projection_v1`, `plan_proof_v1`, `fee_conversion_v1`, `wac_episode_v1`, `metrics_v1`, `verified_review_week_rate_v1` and the import contract `binance_spot_trade_history_csv_v1`.

Every `TradeEpisode` closes to its exact opening `NormalizedFill`. Its ID MUST recompute by the TP-ACC UUIDv5 namespace/framing rule from `(trading_account_id, instrument_id, opening_fill_dedup_key)`, and `opening_fill_id` MUST identify an ADMITTED BUY with the same account, instrument and dedup key. The header and projection version 1 are visible atomically, with equal `created_at`. Replay that keeps the opening fill reuses the immutable header; SPLIT/MERGE/REMOVED handling resolves the exact deterministic header required by TP-ACC and never deletes, aliases or rewrites historical identity.

`TradeEpisodeProjection.state` is only OPEN or CLOSED and remains its immutable business state after supersession. `superseded_at` plus the active-pointer interval represents lifecycle; the exporter never rewrites an old projection to a synthetic SUPERSEDED state. Convenience CSV follows the same rule.

For each TradeEpisodeProjection let Q be its exact `position_qty_base` and B its exact `open_cost_basis_quote`. When `Q > 0`, `average_cost_quote_per_base = round_scale18_half_even(B / Q)` after one division; when `Q = 0`, it is null. The non-null result is canonical signed DECIMAL(38,18), strips trailing zeros, normalizes a half-even zero to `0` and permits no binary float or alternate scale. Overflow fails projection publication. This rounded display/query field never feeds accounting recurrence: a partial SELL computes cost removed from the one exact rational `(pre_fill_B / pre_fill_Q) * fill_q` and rounds only that final cost-removed value; a full close removes exact remaining B. Reusing the persisted rounded average or leaving a non-null average on a closed zero-Q/B projection fails validation.

TradeEpisodeProjection plan-proof JSON is closed. `plan_candidate_ids_json` is a sorted unique array of exact `{ "planRecordKey": { "trade_plan_id": id }, "revisionRecordKey": { "trade_plan_revision_id": id } }`, ordered by unsigned RFC 8785 plan-key bytes then revision-key bytes. `plan_proof_basis_json` has exactly:

```json
{
  "evaluatedAt": "...",
  "evaluatedPlans": [{
    "armEventRecordKey": { "plan_state_event_id": "..." },
    "armRecordedAt": "...",
    "candidate": true,
    "consumeEventRecordKey": null,
    "consumeRecordedAt": null,
    "consumedByEpisodeRecordKey": null,
    "exclusionReason": "NONE",
    "expiresAt": "...",
    "planRecordKey": { "trade_plan_id": "..." },
    "revisionRecordKey": { "trade_plan_revision_id": "..." },
    "revisionRecordedAt": "...",
    "terminalEventRecordKey": null,
    "terminalEventType": null,
    "terminalRecordedAt": null
  }],
  "firstFillInterval": {
    "endExclusive": "...",
    "precision": "MILLISECOND",
    "start": "..."
  },
  "firstFillRecordKey": { "fill_id": "..." },
  "selectedCandidate": null
}
```

`evaluatedAt = plan_proof_resolved_at`. The first-fill key/interval equals the projection's immutable first NormalizedFill and its exact source interval/precision; write `S = start` and `E = endExclusive`. `evaluatedPlans` contains every TradePlan header with `created_at <= evaluatedAt` in the same workspace/account/instrument/LONG scope, sorted by unsigned RFC 8785 plan-key bytes. Each member uses these exact selectors:

1. ARM is the sole ARM event visible at `evaluatedAt`; if none, ARM key/time are both null. An ARM at or after E is retained as boundary evidence.
2. Revision is greatest `(recorded_at, revision_no)` with `recorded_at < E`; if none, it is earliest visible `(recorded_at, revision_no)` with `E <= recorded_at <= evaluatedAt`; if none exists, revision key/time are both null.
3. Terminal is the lowest-`event_sequence` visible CANCEL or EXPIRE. All of terminal key/type/time are null iff none exists; otherwise type and time equal that event. More than one terminal event is invalid source history.
4. Consume is the sole visible CONSUME event. `consumeEventRecordKey`, `consumeRecordedAt` and `consumedByEpisodeRecordKey` are all null or all non-null and then equal that event, its time and exact episode FK. More than one CONSUME is invalid source history.

Every non-null plan, revision, PlanStateEvent, fill and consumed-episode key resolves same-workspace, and every copied timestamp/expiry equals its referenced record. Apply the first matching exclusion exactly:

| Precedence | `exclusionReason` | Condition |
|---:|---|---|
| 1 | `NO_ARM_BEFORE_END` | ARM is null or `armRecordedAt >= E` |
| 2 | `NO_REVISION_BEFORE_END` | No revision has `recorded_at < E`; a boundary-evidence revision may be non-null |
| 3 | `EXPIRED_BEFORE_INTERVAL` | `expiresAt < S` |
| 4 | `TERMINAL_BEFORE_INTERVAL` | Terminal exists and `terminalRecordedAt < S` |
| 5 | `CONSUMED_BY_OTHER_EPISODE` | Consume exists and its episode differs from the projected episode |
| 6 | `NONE` | None above |

`expiresAt` always equals non-null TradePlan.expires_at. `candidate` is true iff reason is NONE, which also requires non-null ARM and revision. `plan_candidate_ids_json` equals the typed plan/revision projection of all and only candidate rows. Every key/time pair obeys all-or-none nullability independently of the exclusion reason. `selectedCandidate` is null or one exact two-member object byte-equal to a candidate-array element; it is non-null only for the original single-candidate VERIFIED or timestamp-AMBIGUOUS auto-match and remains frozen through manual resolution or LATE association.

`plan_proof_reason_code` is one of `VERIFIED_BEFORE_INTERVAL`, `ARM_INSIDE_INTERVAL`, `REVISION_INSIDE_INTERVAL`, `EXPIRY_INSIDE_INTERVAL`, `CANCEL_INSIDE_INTERVAL`, `MULTIPLE_CANDIDATES`, `NO_ELIGIBLE_CANDIDATE` or `USER_ASSOCIATED_AFTER_FILL`. Lazy EXPIRE materialization time is never a semantic proof input, so `EXPIRE_EVENT_INSIDE_INTERVAL` is forbidden. VERIFIED requires associated plan/revision, frozen revision equal associated revision and no late/resolution ID. AMBIGUOUS requires null frozen/late IDs and permits associated links only under the frozen auto/manual resolution rules. LATE requires associated plan/revision and late association but null frozen/resolution ID. UNMATCHED requires all association/frozen/late/resolution IDs null. Unknown/extra nested members, order/duplicate errors, a candidate/selected-candidate mismatch, timestamp mismatch or cross-workspace key fails closure; the reader validates these bases but never reruns plan selection using later records.

Accounting ledger cardinality is exact. For N EpisodeFillAllocation records in one projection, allocation `event_sequence` is the contiguous set 1..N and each allocation/fill has exactly two AccountingLedgerEntry records:

```text
TRADE.entry_sequence = 2 * allocation.event_sequence - 1
FEE.entry_sequence   = 2 * allocation.event_sequence
```

The ledger sequence is exactly 1..2N. `(workspace_id, episode_id, projection_version, entry_sequence)` and `(workspace_id, episode_id, projection_version, fill_id, entry_type)` are unique. `occurred_at = fill.source_time_start`, `created_at = projection.created_at`, and every entry uses `wac_episode_v1`. `ledger_entry_id` is RFC 9562 UUIDv5 with namespace `1fa78c73-95b1-5d92-b0a8-24af63a91c22` and UTF-8 name bytes `tradeproof_ledger_entry_v1\u0000<lowercase-canonical-episode-uuid>\u0000<projection-version-base10>\u0000<entry-sequence-base10>`; the escapes are literal NUL delimiters, integers have no leading zero, and there is no BOM, whitespace or trailing NUL.

Let `q = fill.executed_qty_base`, `A = fill.gross_amount_quote`, `v =` the selected FeeConversion `fee_value_quote`, and for SELL let `c` be the exact cost removed by the allocation. Persisted positive decimals omit the illustrative plus sign. Entry fields are exactly:

| Field | BUY TRADE | SELL TRADE | BUY FEE | SELL FEE |
|---|---|---|---|---|
| `asset` | fill base asset | fill base asset | fill fee asset | fill fee asset |
| `asset_qty_delta` | `q` | `-q` | `-fill.fee_qty` | `-fill.fee_qty` |
| `quote_value_delta` | `-A` | `A` | `-v`, or null if unavailable | `-v`, or null if unavailable |
| `position_qty_delta_base` | `q` | `-q` | `-fill.fee_qty` iff fee asset is base, else `0` | `0` |
| `cost_basis_delta_quote` | `A` | `-c` | `-v` iff fee asset is base, else `0` | `0` |
| `gross_realized_delta_quote` | `0` | `A - c` | `0` | `0` |
| `fee_expense_delta_quote` | `0` | `0` | `v`, or null if unavailable | `v`, or null if unavailable |
| `fee_conversion_id` | null | null | exact selected conversion ID | exact selected conversion ID |

BUY base-fee conversion is FILL_RATE and non-null. SELL base fees do not reduce analytical episode position/cost outside executed quantity. A zero fee still has one FEE entry with zero asset/quote/analytical deltas and the exact zero-fee conversion. Every FEE entry's conversion is same-workspace, belongs to that fill and is the version selected when the projection published; TRADE forbids a conversion ID.

For each allocation, the two ledger entries sum exactly to its position/cost/gross deltas and nullable fee delta; allocation recurrence requires `before + delta = after` for position and cost basis. Across a projection, ending position/basis equal the projection, gross sum equals `gross_realized_pnl_quote`, known non-null fee sum equals `known_fee_quote`, and `net_realized_pnl_quote = gross - all fees` iff every conversion is available, otherwise it is null. Missing, extra, split, reordered or formula/FK/sign/null/UUID mismatch fails `EXPORT_SCHEMA_VIOLATION`.

FeeConversion history is a contiguous per-fill version chain starting at 1. On insert of a new version, old `superseded_at = new.created_at` atomically; active intervals are half-open and non-overlapping. Fee asset/quantity equal the referenced fill and quote asset is USDT. The exact null/value table is:

| Case | `status` | `method` | rate | value | `as_of_at` | arrays/catalog/path |
|---|---|---|---|---|---|---|
| zero fee | EXACT | null | null | `0` | null | all null |
| nonzero quote fee | EXACT | NATIVE_QUOTE | `1` | fee quantity | null | all null |
| nonzero base fee | EXACT | FILL_RATE | TP-ACC rounded fill rate | TP-ACC rounded value | null | all null |
| eligible direct bar | DERIVED | DIRECT_1M_CLOSE | bar close | rounded value | bar end | all exact non-null values below |
| eligible inverse bar | DERIVED | INVERSE_1M_CLOSE | round-half-even reciprocal at 18 decimals | rounded value | bar end | all exact non-null values below |
| no eligible path | UNAVAILABLE | null | null | null | null | all null |

For a market-bar method, both ID arrays have length one and exact shapes `[{ "revisionId": id }]` and `[{ "sourceObservationId": id }]`. `conversion_path_json` has exactly:

```json
{
  "bar": {
    "barEndExclusiveEpochMs": 0,
    "close": "300",
    "openAtEpochMs": 0,
    "recordKey": { "revisionId": "..." },
    "resolutionRecordKey": null,
    "selectedObservationRecordKey": { "sourceObservationId": "..." },
    "timeframe": "1m"
  },
  "catalogPair": {
    "baseAsset": "BNB",
    "catalogVersion": "...",
    "conversionSupported": true,
    "quoteAsset": "USDT",
    "recordKey": {
      "catalog_version": "...",
      "valid_from": "...",
      "venue_symbol": "BNBUSDT"
    },
    "validFrom": "...",
    "validToExclusive": null,
    "venueSymbol": "BNBUSDT"
  },
  "direction": "DIRECT",
  "productType": "SPOT",
  "venue": "BINANCE"
}
```

Direction is DIRECT or INVERSE and matches method. The catalog key/fields equal one included MarketConversionCatalogVersion row, its version equals the top-level field and its validity contains the bar interval. The bar key equals array element 0; selected observation equals observation element 0 and references that bar. `resolutionRecordKey` is null when exactly one revision was visible, otherwise it is exact `{ "marketBarResolutionId": id }`, closes to the included public prefix/candidate set, selects this bar and passes `market_bar_as_of_v1` at `FeeConversion.created_at`. Venue/product/symbol/timeframe/open/close are copied exactly, `barEndExclusiveEpochMs = openAtEpochMs + 60000`, and `as_of_at` is that end as canonical RFC 3339 milliseconds. DIRECT requires catalog pair `(fee_asset, USDT)` and rate equal close. INVERSE requires `(USDT, fee_asset)` and the 18-place round-half-even reciprocal. Both require supported pair, cutoff eligibility and direct-path precedence. The observation's request/batch closure remains mandatory. Unknown/extra path members, non-one array, misalignment, stale resolution, copied-field/rate/value mismatch or current bar/catalog reselection fails closure.

The `verified_review_week_rate_v1` snapshot uses numeric cohort-sequence bounds, never opaque-ID comparison. `cohort_range_start_sequence` and `cohort_range_end_sequence_exclusive` are positive and start < end. Resolve same-workspace/user WeeklyCohort headers created by `reporting_as_of_at` whose sequence is in the half-open range; replay their state at that as-of, exclude SUPERSEDED, and require every retained header LOCKED with its LOCK event visible and `reporting_as_of_at >= cohort_end_at_utc + 72 hours`. `cohort_range_refs_json` is the nonempty array of exact `{ "weekly_cohort_id": id }` for all retained headers sorted by sequence. Its first sequence equals start and end equals greatest retained sequence + 1. Every skipped numeric sequence resolves only to a visible SUPERSEDED header. Retained headers form one predecessor chain with adjacent UTC boundaries. A gap, duplicate, foreign owner, unlocked/not-final cohort or omitted non-superseded header fails closure.

`numerator_weekly_cohort_ids_json` and `denominator_weekly_cohort_ids_json` are filtered arrays of the same typed cohort keys in range order, never scalar IDs. Numerator is exactly drilldown rows with `qualifyingUserWeek = true`; denominator is exactly rows with `eligibleUserWeek = true`; lengths equal their integer totals. With nonzero denominator, `value = ratio18(numerator, denominator)`, integer division rounded once at scale 18 with ROUND_HALF_EVEN then canonical trailing-zero/negative-zero normalization, and `null_reason` is null. Zero denominator requires null value and `null_reason = NO_ELIGIBLE_USER_WEEK`.

`user_week_drilldown_json` sorts by cohort sequence, has one member per range ref and each member has exactly:

```json
{
  "behavioralExperimentRevisionRecordKey": null,
  "candidateEpisodes": [{
    "eligibilityEventRecordKey": null,
    "projectionRecordKey": { "episode_id": "...", "projection_version": 1 },
    "result": "ELIGIBLE",
    "resultReason": "ELIGIBLE"
  }],
  "cohort": {
    "cohortEndAtUtc": "...",
    "cohortEndLocalExclusive": "...",
    "cohortRecordKey": { "weekly_cohort_id": "..." },
    "cohortSequence": 1,
    "cohortStartAtUtc": "...",
    "cohortStartLocal": "...",
    "cohortType": "REGULAR",
    "completionDeadlineAtUtc": "...",
    "endBoundaryResolution": "EXACT",
    "lockEventRecordKey": { "weekly_cohort_state_event_id": "..." },
    "previousCohortRecordKey": null,
    "startBoundaryResolution": "EXACT",
    "timezoneChangeScheduleRecordKey": null,
    "tzdbVersion": "...",
    "workspaceTimezone": "Asia/Ho_Chi_Minh"
  },
  "completionPass": false,
  "coverageComparison": {
    "leftOperand": 0,
    "operator": ">=",
    "rightOperand": 0
  },
  "eligibleEpisodeCount": 0,
  "eligibleEpisodeProjectionRecordKeys": [],
  "eligibleUserWeek": false,
  "excludedEpisodeProjections": [],
  "qualifyingUserWeek": false,
  "userWeekExclusionReasons": ["FEWER_THAN_3_ELIGIBLE_EPISODES"],
  "verifiedCoveragePass": false,
  "verifiedEpisodeCount": 0,
  "verifiedEpisodeProjectionRecordKeys": [],
  "weeklyReportRevisionRecordKey": null,
  "weeklyReviewCompletionRecordKey": null
}
```

The cohort object copies the exact header and selected LOCK event; `completionDeadlineAtUtc = cohortEndAtUtc + 72 hours`, and predecessor/timezone keys equal header FKs. For each row, `candidateEpisodes` is every TradeEpisodeProjection active at `reporting_as_of_at`, CLOSED and with `closed_at` inside the cohort half-open UTC interval, sorted by `(closed_at, episode_id, projection_version)`. Its eligibility key is the greatest visible aggregate `event_sequence` for that exact projection version or null. A pending ReplayConflictPreview never replaces or excludes its based-on active projection.

Candidate `resultReason` is the first matching `TRANSITION_COHORT`, `LEDGER_INVARIANT_FAILED`, `ACCOUNTING_QUALITY_INCOMPLETE`, `NET_PNL_UNAVAILABLE`, `USER_EXCLUDED`, then `ELIGIBLE`; `result` is ELIGIBLE only for the last. Eligible keys are the exact eligible projection-key filter. `excludedEpisodeProjections` is `{ "projectionRecordKey": key, "reasonCode": resultReason }` for excluded rows. Both preserve candidate order and partition once. Verified keys are the eligible subset with VERIFIED plan proof and non-null frozen plan revision. All counts equal array lengths. Coverage is exact `{ "leftOperand": 5 * verifiedEpisodeCount, "operator": ">=", "rightOperand": 4 * eligibleEpisodeCount }` using safe nonnegative integers.

For REGULAR, `eligibleUserWeek = eligibleEpisodeCount >= 3`; exclusion reasons are `[]` or exactly `["FEWER_THAN_3_ELIGIBLE_EPISODES"]`; coverage pass additionally requires the integer comparison. For TRANSITION, eligible/verified arrays and counts are empty/zero, every candidate is excluded first by TRANSITION_COHORT, all week booleans are false and reasons equal `["TRANSITION_COHORT"]`. Select the unique same-cohort WeeklyReviewCompletion visible at the as-of. If absent, completion/report/experiment refs are all null and completionPass false. If present, all three are non-null and equal its immutable FKs; completionPass is `cohortEndAtUtc <= completed_at < completionDeadlineAtUtc`. No completion is valid for TRANSITION. `qualifyingUserWeek` is the conjunction of eligibility, coverage and completion.

`input_event_digest` is SHA-256 of RFC 8785 exact object:

```json
{
  "algorithmVersion": "metrics_v1",
  "cohortRange": {
    "endSequenceExclusive": 2,
    "resolvedCohortRecordKeys": [{ "weekly_cohort_id": "..." }],
    "startSequence": 1
  },
  "denominatorCohortRecordKeys": [],
  "metricId": "verified_review_week_rate",
  "metricVersion": "verified_review_week_rate_v1",
  "numeratorCohortRecordKeys": [],
  "reportingAsOfAt": "...",
  "userId": "...",
  "userWeekDrilldown": [],
  "workspaceId": "..."
}
```

Each array is its exact persisted array. Every nested key resolves an included same-workspace record visible at the as-of; no shared-public escape applies. Snapshot identity is idempotent on workspace/user/version/range/as-of/digest. Any copied-value, sort, range, selection, partition, count, ratio, null, reference or digest mismatch is `EXPORT_SCHEMA_VIOLATION`; the reader validates the stored snapshot rather than recalculating with later events.

### 7.6. Context and shared public provenance

`canonical/context.json` contains:

| Record type | Ownership | Inclusion |
|---|---|---|
| `ContextAlgorithmRelease` | SHARED_PUBLIC | Exact immutable release for every included ContextSnapshot algorithm/parameter tuple |
| `ContextSnapshot` | WORKSPACE | Every visible tenant snapshot, including COMPLETE, PARTIAL, UNRELIABLE and superseded snapshots |
| `MarketBarConflict` | SHARED_PUBLIC | Exact header for every included MarketBarResolution |
| `MarketBarResolution` | SHARED_PUBLIC | Complete contiguous prefix through every resolution referenced by ContextSnapshot or FeeConversion |
| `MarketBarRevision` | SHARED_PUBLIC | Exact reference-closed revisions used by ContextSnapshot or FeeConversion |
| `MarketBarSourceObservation` | SHARED_PUBLIC | Exact selected observation for every consumer-selected bar reference |
| `MarketDataSourceRequest` | SHARED_PUBLIC | Exact request for every included observation |
| `MarketDataIngestionBatch` | SHARED_PUBLIC | Exact batch for every included request |

Only the reference-closed public subset is copied. The exporter does not dump global market tables or unrelated algorithm releases. Shared public records contain no workspace, user, episode, note or tenant-derived identifier. `ContextSnapshot` retains exact `mce-binance-spot-v1.0.0`, `mce-default-v1`, revision scope, quality diagnostics, coverage, aligned input/resolution arrays and hashes. Offline validation MUST resolve and verify algorithm/parameter tuple -> immutable release hashes and logical bar -> optional conflict/resolution prefix -> selected revision/observation -> request -> batch without network access. Release artifact bytes remain in the restricted verification store and are represented by their immutable hashes, not copied into the archive.

### 7.7. Weekly Lab and product metrics

`canonical/weekly_lab.json` is exactly `weekly_lab_export_projection_v1` and contains:

| Record type | Inclusion |
|---|---|
| `BehavioralExperiment` | Every aggregate |
| `BehavioralExperimentRevision` | Every proposed, confirmed, superseded or cancelled revision |
| `BehavioralExperimentStateEvent` | Every append-only state event |
| `ContextAvailabilityDecision` | Every immutable decision referenced by a PENDING, MISSING or NOT_APPLICABLE cohort-input context slot |
| `MetricSnapshot` | Every immutable non-north-star snapshot using `metric_snapshot_v1` |
| `ProductAnalyticsEvent` | Every append-only same-workspace event using `product_analytics_event_v1` |
| `ProductMeasurementRun` | Every immutable same-workspace UX measurement-run header using `product_measurement_run_v1` |
| `ProductMeasurementRunStateEvent` | Complete visible contiguous START and optional terminal prefix for every run |
| `WorkspaceProductMetricSnapshot` | Every immutable same-workspace snapshot/revision using `workspace_product_metric_snapshot_v1` and `product_metrics_v1` |
| `TimezoneChangeSchedule` | Every schedule |
| `TimezoneChangeScheduleStateEvent` | Every append-only schedule state event |
| `WeeklyCohort` | Every regular, transition and superseded header |
| `WeeklyCohortInputRevision` | Every input revision |
| `WeeklyCohortStateEvent` | Every append-only cohort state event |
| `WeeklyReport` | Every stable report aggregate |
| `WeeklyReportRevision` | Every published/superseded revision with exact canonical `section_payload_json`; request-time rendered bytes are excluded |
| `WeeklyReportRevisionStateEvent` | Every append-only state event |
| `WeeklyReviewCompletion` | Every immutable completion event and exact report/experiment references |

It preserves `weekly_lab_v1`, `weekly_lab_renderer_v1`, `metric_snapshot_v1`, `behavioral_experiment_v1`, `weekly_lab_export_projection_v1`, `product_measurement_run_v1`, `product_metrics_v1`, `product_analytics_event_v1` and `workspace_product_metric_snapshot_v1`. Every report revision retains one homogeneous dependency version tuple; the exporter rejects, rather than repairs, a mixed tuple. ProductMeasurementRun headers/state prefixes are canonical tenant history, but PRODUCT_MEASUREMENT_TIMEOUT TenantControlJob/fence/marker detail is Restricted control evidence and never serialized. `InternalAggregateProductMetricSnapshot`, `InternalAggregateCohortRetirement` and their `internal_aggregate_product_metric_snapshot_v1`/`internal_aggregate_cohort_retirement_v1` payloads are service-owned cross-workspace evidence and are forbidden from every workspace archive, even when the owner contributed to an aggregate. Neither literal is added to `domainVersions` because excluded control data cannot advertise archive membership.

### 7.8. AI and consent

`canonical/ai_consent.json` contains these record types when present and empty sets when AI was never enabled:

| Record type | Inclusion |
|---|---|
| `AiOutputSubject` | Every durable payload-free same-workspace output identity visible at cutoff, whether ACTIVE or DELETED |
| `AiOutputSubjectStateEvent` | The complete visible contiguous CREATE and optional DELETE history for every included subject |
| `AiOutput` | Retained canonical output and its validation/fallback status |
| `AiOutputReference` | Exact retained citations to reports, metrics and trade references |
| `AiRun` | Retained provenance fields required by `TP-SEC`; never hidden chain-of-thought |
| `AiRunInputReference` | Every immutable typed input reference and exact payload-fragment digest for a retained run |
| `ConsentRecord` | Every opt-in, opt-out and consent-version event for each AI feature |
| `TaxonomySuggestionConfirmation` | Every immutable explicit user confirmation, including one whose source output was later deleted |
| `TranscriptConfirmation` | Every immutable explicit user confirmation, including one whose source output was later deleted |

AiRunInputReference closes every input to its exact same-workspace tenant record or exact historical shared-public Review taxonomy record. For weekly summary it preserves the exact published WeeklyReportRevision, sorted MetricSnapshot IDs and sorted episode projection grounding used as input; for transcription it preserves the accepted voice Upload and optional retained voice Attachment; for taxonomy suggestion it preserves the selected immutable source text plus exact version and complete item allowlist.

Every successful output atomically creates one `AiOutputSubject` whose ID equals `AiOutput.ai_output_id`, plus its sequence-1 CREATE event. The subject is stable, payload-free canonical identity: `workspace_id`, output kind, copied last-known content hash and creation time are immutable, and it remains after content deletion. `DeleteAiOutput` deletes its content bundle (owning AiRun, every AiRunInputReference, AiOutput and every AiOutputReference), then appends sequence-2 DELETE with the exact receipt. Thus a cutoff before deletion exports subject CREATE plus the complete retained bundle; a cutoff after deletion exports subject CREATE/DELETE plus its typed Tombstone and none of those four bundle types. Confirmation records remain and always reference the subject. Subject/confirmation IDs, public references and unsalted integrity hashes remain Restricted derived personal data under TP-SEC disclosure, retention and workspace-deletion controls; payload-free does not mean anonymous, aggregate or public. Canonical plans, fills, Reviews, metrics, public taxonomy and deterministic reports are not AI copies and remain in their own sets.

### 7.9. Pointers, tombstones and excluded data

`canonical/pointers.json` contains only `AsOfPointer` records from section 4.3.

`canonical/tombstones.json` contains only these `Tombstone` subject types:

```text
ATTACHMENT_BINARY
AI_OUTPUT
RAW_IMPORT_OBJECT
RAW_VOICE_OBJECT
TRANSCRIPT_DRAFT
```

TP-SEC's deletion/retention transaction produces a durable `SubjectDeletionReceipt` handoff with exactly `workspace_id`, `subject_type`, `subject_id`, `completed_at`, nullable `reason_code`, non-null `last_known_sha256`, `source_retention_policy`, `source_record_type`, `source_record_id` and `idempotency_key`. This receipt is control-plane evidence and is not an archive record. `(workspace_id, subject_type, subject_id)` and `(workspace_id, idempotency_key)` are unique. The delete/purge event and receipt are written in one transaction when they share a store; otherwise the delete transaction writes a transactional outbox message, and an export cutoff that sees the delete but not its receipt/tombstone fails `EXPORT_TOMBSTONE_INVALID` and restarts after delivery. It can never become READY with that gap.

The Tombstone projection contains exactly `tombstone_id`, `workspace_id`, `subject_type`, `subject_id`, `purged_or_deleted_at`, `reason_code`, `last_known_sha256` and `source_retention_policy`. It contains no deleted content. One tuple has one stable record forever:

```text
tombstone_id = "tmb_" + lowercase_hex(
  SHA-256(UTF8(RFC8785({
    "subjectId": subject_id,
    "subjectType": subject_type,
    "workspaceId": workspace_id
  })))
)
```

A retry with the same tuple MUST reproduce the same Tombstone and receipt; a different timestamp, hash, policy or non-null reason for an existing tuple is `EXPORT_TOMBSTONE_INVALID`. Receipt-to-Tombstone mapping is closed:

| `subject_type` | Exact `subject_id` and authoritative source | Time/hash mapping | Null-reason fallback | Exact `source_retention_policy` |
|---|---|---|---|---|
| `ATTACHMENT_BINARY` | `Attachment.attachment_id`; `AttachmentStateEvent` DELETE_COMPLETE is `source_record_type/source_record_id` | event `recorded_at`; retained Attachment `content_sha256` | `USER_DELETED` | `TP-SEC:ATTACHMENT_DELETE` |
| `AI_OUTPUT` | `AiOutputSubject.ai_output_subject_id` for TAXONOMY_SUGGESTION or WEEKLY_SUMMARY; its DELETE event names receipt composite key `(workspace_id,AI_OUTPUT,subject_id)` | receipt/DELETE `completed_at`; subject `last_known_content_sha256` copied from AiOutput before removal | `USER_DELETED` | `TP-SEC:AI_OUTPUT_DELETE` |
| `RAW_IMPORT_OBJECT` | `Upload.upload_id` where `upload_kind = CSV`; `UploadStateEvent` PURGE is the source | event `recorded_at`; Upload `source_sha256` | `RETENTION_EXPIRED` | `TP-SEC:RAW_IMPORT_24H` |
| `RAW_VOICE_OBJECT` | `Upload.upload_id` where `upload_kind = VOICE`; `UploadStateEvent` PURGE is the source | event `recorded_at`; Upload `source_sha256` | `RETENTION_EXPIRED` | `TP-SEC:RAW_VOICE_24H` |
| `TRANSCRIPT_DRAFT` | `AiOutputSubject.ai_output_subject_id` where `output_kind = TRANSCRIPT_DRAFT`; its DELETE event names receipt composite key `(workspace_id,TRANSCRIPT_DRAFT,subject_id)` | receipt/DELETE `completed_at`; subject `last_known_content_sha256` copied from AiOutput before removal | `USER_DELETED` | `TP-SEC:TRANSCRIPT_DRAFT_DELETE` |

`purged_or_deleted_at = completed_at`; `reason_code` is the receipt's non-null safe code or the exact fallback above; `last_known_sha256` is the mapped non-null hash. For the three Upload/Attachment state-event rows, the receipt's `source_record_type/source_record_id` must be the named included event and the event workspace/subject must match. For AI rows, receipt `source_record_type = AI_OUTPUT_DELETE`, source ID equals `subject_id`, and the included subject DELETE event repeats the receipt composite subject type/ID; the deleted AiOutput record is forbidden. `RAW_IMPORT_OBJECT` tombstones/hashes do not restore raw cells or make raw CSV lossless.

An UploadStateEvent PURGE or AttachmentStateEvent DELETE_COMPLETE used by this mapping must also carry its non-null verification ID and close to the included same-subject absence proof from section 7.10. A receipt/event/tombstone without that proof, a proof for another object version/generation or a verification later than the event fails `EXPORT_TOMBSTONE_INVALID`; a safe proof never substitutes for deleted bytes.

The archive excludes:

- raw CSV bytes/cells after purge and any unsafe upload/quarantine object;
- passwords, OIDC/session/provider tokens, magic links, API keys, secrets, private keys and signed URLs;
- security, break-glass and internal audit log payloads; export completion audit remains server-side;
- operational logs, alert evidence, trace payloads and internal credentials;
- other workspaces' records and unrelated global public market/catalog rows;
- every `InternalAggregateProductMetricSnapshot`, `InternalAggregateCohortRetirement`, cross-workspace aggregate definition/contribution/member/retirement control and every `internal_aggregate_product_metric_snapshot_v1` or `internal_aggregate_cohort_retirement_v1` payload;
- every `ProductAnalyticsExternalProjection`, `ProductAnalyticsExternalSuppressionReceipt`, processor-key rotation, external-delivery receipt, external-deletion receipt and deletion-inventory record, plus every `product_analytics_external_v1`, `product_analytics_external_suppression_receipt_v1` or `product_analytics_external_deletion_inventory_v1` payload; these are Restricted delivery/privacy control evidence, never workspace canonical data and never a `domainVersions.includedValues` member;
- mutable cache, search index, queue messages, scheduler state, temporary jobs, failed staging files and generated prior export archives; this includes ImportPreview/ImportPreviewStateEvent, PlanCommandReceipt, ContextEpisodeTrigger, ManualContextRecomputeRequest and PRODUCT_MEASUREMENT_TIMEOUT work detail, whose durable canonical effects are represented by the copied batch proof, plan history, ContextSnapshot/release closure and ProductMeasurementRun/state history respectively;
- ExportRequest, ExportJob, ExportAttempt, ExportJobStateEvent and ExportControlFeedNotice control-plane records, which would otherwise make the current archive self-referential; their non-content audit evidence remains server-side;
- every restricted deletion/work-fence/ingest control-plane record, including WorkspaceDeletion, WorkspaceDeletionStateEvent, WorkspaceDeletionTarget, WorkspaceDeletionTargetAttempt, WorkspaceDeletionOutbox, WorkspaceDeletionTombstone, TenantControlJob, TenantWorkItemFence, TenantWorkItemFenceEvent, TenantExternalOperationLease, TenantWorkItemTerminalMarker, JobDrainEvidence, ObjectIngestReservation, ObjectIngestReservationEvent, object leases/deletion attempts, frozen provider/identity inventories, absence/provider proofs and reservation-owned staging objects, plus identity HMAC/key-version/generation evidence; an exportable Workspace is necessarily pre-FENCE ACTIVE, and none is an archive record;
- internal restricted identity/AI registries and controls: User/UserIdentity authentication rows, IdentityProviderRegistration/StateEvent, BreakGlassOwnerNotice, IdP grant/delete/unlink/status evidence, AiConfigurationArtifact/AiConfigurationRelease/AiEvalArtifact/AiConfigurationActivationEvent, AiProcessorRegistration/StateEvent, AiProcessorCopyReference/TerminalEvidence, AiConfirmationCommandIntent/Receipt, AI processor deletion inventory/absence evidence, artifact bytes and storage references; retained AiRun/confirmation records copy only their required non-secret immutable version/hash/registration binding tuple;
- provider hidden reasoning, hidden chain-of-thought and raw processor request/response bodies not defined as retained AiRun/AiOutput.

### 7.10. Normative record projection registry

This registry closes the JSON schema for every allowed `recordType`. The type notation is:

| Token | Exact JSON representation |
|---|---|
| `id` | Nonempty JSON string, 1-128 Unicode scalar values |
| `str` | JSON string with valid Unicode scalar values |
| `ts` | UTC RFC 3339 string with exactly milliseconds |
| `epoch_ms` | Non-negative JSON integer milliseconds, at most `9007199254740991` |
| `int` | JSON integer in `[-9007199254740991, 9007199254740991]` |
| `dec` | Canonical decimal JSON string from section 6.1 |
| `hash` | Exactly 64 lowercase hexadecimal characters |
| `bool` | JSON boolean |
| `json` | Any RFC 8785-compatible JSON value deliberately treated as opaque versioned domain data |
| `json<S>` | RFC 8785-compatible JSON value with the complete closed nested schema and hash basis named by exact identifier `S` in this contract |
| `ver<C:S>` | Nonempty JSON string that MUST appear in `domainVersions` for contract C and slot S |
| `T[]` | JSON array of `T`; ordering is the source-field rule, otherwise RFC 8785 element-byte order |
| `?` suffix | JSON null or the preceding type; without `?`, null and omission are forbidden |
| `enum{A\|B}` | One exact JSON string from the listed set |
| `=value` | The exact literal is required |

The payload field list in each row is exhaustive and ordered for schema documentation; RFC 8785 determines encoded object-member order. Missing required members and any unlisted member are rejected. An opaque `json` field is one declared value, not an extension bag: the reader preserves it byte-semantically and does not promote nested names to record fields. Exact nested reference shapes and enums already specified by the cited `TP-ACC`, `TP-MCE` or `TP-LAB` v1 contract remain mandatory. `ver<C:S>` permits several historical values without aliasing: every value must be listed in the manifest slot, and the current writer uses that slot's baseline for newly created records.

`recordKey` is an object containing exactly the fields named in the **Key** column, copied byte-semantically from payload. The **Sort** tuple is ascending; strings use unsigned UTF-8 byte order, integers numeric order, and null sorts before non-null. `W:X` means a same-workspace composite reference to type X, `P:X` means a required included SHARED_PUBLIC reference, `T:X` permits only the typed Tombstone alternative, and `[]` applies the rule to every element. `sourceContractVersion` is exactly `1.0.0` for every row below.

#### Identity, catalog and taxonomy projections

| RecordType / recordSchemaId / source | Exact payload fields | Key | Sort | FK/reference rules |
|---|---|---|---|---|
| `Workspace` / `tp_exp_workspace_v1` / `TP-SEC` | `workspace_id:id, owner_user_id:id, lifecycle_state:=ACTIVE, deletion_guard_generation:int, deletion_id:id?, timezone:str, created_at:ts, deleting_at:ts?, deleted_at:ts?` | `workspace_id` | `workspace_id` | `owner_user_id -> W:WorkspaceOwnerProfile; generation positive; deletion_id/deleting_at/deleted_at all null for the exportable ACTIVE state` |
| `TradingAccount` / `tp_exp_trading_account_v1` / `TP-ACC` | `trading_account_id:id, workspace_id:id, venue:=BINANCE, product_type:=SPOT, reporting_currency:=USDT, account_label:str, created_at:ts` | `trading_account_id` | `trading_account_id` | `workspace_id -> W:Workspace` |
| `WorkspaceOwnerProfile` / `tp_exp_workspace_owner_profile_v1` / `TP-ACC` | `owner_user_id:id, workspace_id:id, created_at:ts` | `owner_user_id` | `owner_user_id` | `workspace_id -> W:Workspace` |
| `WorkspaceOwnerProfileRevision` / `tp_exp_workspace_owner_profile_revision_v1` / `TP-ACC` | `owner_profile_revision_id:id, owner_user_id:id, workspace_id:id, revision_no:int, display_name:str?, locale:str, recorded_at:ts, idempotency_key:str` | `owner_profile_revision_id` | `owner_user_id,revision_no,owner_profile_revision_id` | `owner_user_id -> W:WorkspaceOwnerProfile; revision positive/contiguous, time nondecreasing` |
| `BehavioralExperimentTaxonomyVersion` / `tp_exp_behavioral_experiment_taxonomy_version_v1` / `TP-LAB` | `taxonomy_version:ver<TP-LAB:BEHAVIORAL_EXPERIMENT_TAXONOMY>, content_sha256:hash, published_at:ts` | `taxonomy_version` | `taxonomy_version` | none |
| `BehavioralExperimentTaxonomyItem` / `tp_exp_behavioral_experiment_taxonomy_item_v1` / `TP-LAB` | `taxonomy_version:ver<TP-LAB:BEHAVIORAL_EXPERIMENT_TAXONOMY>, behavior_id:id, label_vi:str, measurement_metric_id:id, option_order:int` | `taxonomy_version,behavior_id` | `taxonomy_version,option_order,behavior_id` | `taxonomy_version -> P:BehavioralExperimentTaxonomyVersion` |
| `Instrument` / `tp_exp_instrument_v1` / `TP-ACC` | `instrument_id:id, venue:=BINANCE, product_type:=SPOT, venue_symbol:str, base_asset:str, quote_asset:=USDT, created_at:ts` | `instrument_id` | `instrument_id` | none |
| `InstrumentCatalogPublishEvent` / `tp_exp_instrument_catalog_publish_event_v1` / `TP-ACC` | `catalog_publish_event_id:id, event_sequence:int, catalog_version:str, event_type:=PUBLISH, recorded_at:ts, content_sha256:hash` | `catalog_publish_event_id` | `event_sequence,catalog_publish_event_id` | `catalog_version -> complete P:InstrumentCatalogVersion[]; sequence positive/contiguous in INSTRUMENT family; aggregate hash exact TP-ACC basis` |
| `InstrumentCatalogVersion` / `tp_exp_instrument_catalog_version_v1` / `TP-ACC` | `instrument_id:id, catalog_version:str, venue:=BINANCE, product_type:=SPOT, venue_symbol:str, base_asset:str, quote_asset:=USDT, base_precision:int, quote_precision:int, valid_from:ts, valid_to_exclusive:ts?, import_supported:bool, plan_enabled:bool, source:=BINANCE_PUBLIC_SPOT_METADATA, source_retrieved_at:ts, content_sha256:hash, published_at:ts` | `catalog_version,venue_symbol,valid_from` | `catalog_version,venue_symbol,valid_from,instrument_id` | `instrument_id -> P:Instrument` |
| `MarketConversionCatalogPublishEvent` / `tp_exp_market_conversion_catalog_publish_event_v1` / `TP-ACC` | `catalog_publish_event_id:id, event_sequence:int, catalog_version:str, event_type:=PUBLISH, recorded_at:ts, content_sha256:hash` | `catalog_publish_event_id` | `event_sequence,catalog_publish_event_id` | `catalog_version -> complete P:MarketConversionCatalogVersion[]; sequence positive/contiguous in MARKET_CONVERSION family; aggregate hash exact TP-ACC basis` |
| `MarketConversionCatalogVersion` / `tp_exp_market_conversion_catalog_version_v1` / `TP-ACC` | `catalog_version:str, venue_symbol:str, base_asset:str, quote_asset:str, purpose:=FEE_CONVERSION_ONLY, valid_from:ts, valid_to_exclusive:ts?, conversion_supported:bool, source:=BINANCE_PUBLIC_SPOT_METADATA, source_retrieved_at:ts, content_sha256:hash, published_at:ts` | `catalog_version,venue_symbol,valid_from` | `catalog_version,venue_symbol,valid_from` | none |
| `ReviewTaxonomyVersion` / `tp_exp_review_taxonomy_version_v1` / `TP-ACC` | `taxonomy_version:str, taxonomy_type:enum{EXIT_REASON\|BREACH_TYPE\|EMOTION}, content_sha256:hash, published_at:ts` | `taxonomy_version` | `taxonomy_version` | none |
| `ReviewTaxonomyItem` / `tp_exp_review_taxonomy_item_v1` / `TP-ACC` | `taxonomy_version:str, taxonomy_type:enum{EXIT_REASON\|BREACH_TYPE\|EMOTION}, item_id:id, label_vi:str, item_order:int` | `taxonomy_version,item_id` | `taxonomy_version,item_order,item_id` | `taxonomy_version -> P:ReviewTaxonomyVersion` |
| `ReviewTaxonomyPublishEvent` / `tp_exp_review_taxonomy_publish_event_v1` / `TP-ACC` | `taxonomy_publish_event_id:id, taxonomy_type:enum{EXIT_REASON\|BREACH_TYPE\|EMOTION}, event_sequence:int, taxonomy_version:str, recorded_at:ts, content_sha256:hash` | `taxonomy_publish_event_id` | `taxonomy_type,event_sequence,taxonomy_publish_event_id` | `taxonomy_version -> P:ReviewTaxonomyVersion and its complete P:ReviewTaxonomyItem[]; sequence positive/contiguous per taxonomy type; event/version hash equal` |

#### Import, plan, Review and attachment projections

| RecordType / recordSchemaId / source | Exact payload fields | Key | Sort | FK/reference rules |
|---|---|---|---|---|
| `ImportBatch` / `tp_exp_import_batch_v1` / `TP-ACC` | `import_batch_id:id, workspace_id:id, trading_account_id:id, source_upload_id:id, upload_idempotency_key:str, source_import_preview_id:id, source_preview_schema_version:=import_preview_v1, source_preview_summary_sha256:hash, confirmed_at:ts, contract_version:ver<TP-ACC:IMPORT_CONTRACT>, instrument_catalog_version:str, original_filename:str, file_sha256:hash, file_size_bytes:int, uploaded_at:ts, started_at:ts?, finished_at:ts?, status:enum{UPLOADED\|PROCESSING\|COMPLETE\|PARTIAL\|NEEDS_ATTENTION\|REJECTED}, file_error_code:str?, data_rows:int?, reconciled_rows:int, duplicate_rows:int, accounting_pending_rows:int, quarantined_rows:int, reconciliation_rate:dec?, duplicate_file_of_batch_id:id?` | `import_batch_id` | `uploaded_at,import_batch_id` | `trading_account_id -> W:TradingAccount; source_upload_id -> W:Upload with kind CSV and matching hash/size; source_import_preview_id is a typed excluded-command reference validated by the copied schema/hash/confirmation proof, never an archive FK; instrument_catalog_version -> P:InstrumentCatalogVersion[]; duplicate_file_of_batch_id -> W:ImportBatch?; section 7.3 closes times/counters, ratio18 bytes, integer status thresholds and errors` |
| `ImportRow` / `tp_exp_import_row_v1` / `TP-ACC` | `import_row_id:id, workspace_id:id, import_batch_id:id, source_row_number:int, raw_row_sha256:hash, status:enum{RECONCILED\|DUPLICATE\|ACCOUNTING_PENDING\|QUARANTINED}, error_code:str?, error_detail_json:json<import_row_error_detail_v1>?, staged_fill_id:id?, normalized_fill_id:id?, duplicate_of_fill_id:id?, created_at:ts` | `import_row_id` | `import_batch_id,source_row_number,import_row_id` | `import_batch_id -> W:ImportBatch; staged_fill_id -> W:StagedFill?; normalized_fill_id -> W:NormalizedFill?; duplicate_of_fill_id -> W:NormalizedFill?; exact mutually exclusive row-status/reference/error matrix, eight-member diagnostic, mapping, cap, privacy and hash rules are in section 7.3` |
| `ImportResolution` / `tp_exp_import_resolution_v1` / `TP-ACC` | `resolution_id:id, workspace_id:id, import_row_id:id?, replay_conflict_id:id?, action:enum{ACCEPT_AS_NEW\|MARK_DUPLICATE\|SET_SEQUENCE\|CONFIRM_REPLAY}, payload_json:json, reason:str, actor_user_id:id, idempotency_key:str, recorded_at:ts` | `resolution_id` | `recorded_at,resolution_id` | `import_row_id -> W:ImportRow?; replay_conflict_id -> W:ReplayConflictPreview?; action-specific null/payload/closure rules in section 7.3` |
| `ReplayConflictPreview` / `tp_exp_replay_conflict_preview_v1` / `TP-ACC` | `replay_conflict_id:id, workspace_id:id, based_on_active_projection_refs_json:json, proposed_projection_refs_json:json, source_input_digest:hash, episode_mapping_json:json, impact_json:json, created_at:ts, expires_at:ts` | `replay_conflict_id` | `created_at,replay_conflict_id` | `exact nested schemas/digests/partitions from section 7.3; published old/source keys -> W records; embedded proposal keys are preview-local and close inside the proposal array, never as W:TradeEpisodeProjection` |
| `StagedFill` / `tp_exp_staged_fill_v1` / `TP-ACC` | `staged_fill_id:id, workspace_id:id, trading_account_id:id, import_batch_id:id, import_row_id:id, source_row_number:int, import_contract_version:ver<TP-ACC:IMPORT_CONTRACT>, staged_fill_schema_version:=staged_fill_v1, instrument_catalog_version:str, venue:=BINANCE, product_type:=SPOT, instrument_id:id, venue_symbol:str, base_asset:str, quote_asset:=USDT, side:enum{BUY\|SELL}, executed_at:ts, source_timestamp_precision:enum{MILLISECOND\|SECOND}, source_time_start:ts, source_time_end_exclusive:ts, price_quote_per_base:dec, executed_qty_base:dec, gross_amount_quote:dec, fee_qty:dec, fee_asset:str, canonical_signature:hash, occurrence_index:int, created_at:ts` | `staged_fill_id` | `import_batch_id,source_row_number,staged_fill_id` | `trading_account_id -> W:TradingAccount; import_batch_id -> W:ImportBatch; import_row_id -> W:ImportRow; instrument_id -> P:Instrument; row/batch/account/catalog and copied canonical fields must match exactly` |
| `StagedFillDisposition` / `tp_exp_staged_fill_disposition_v1` / `TP-ACC` | `staged_fill_disposition_id:id, workspace_id:id, staged_fill_id:id, resolution_id:id, outcome:enum{ADMITTED_AS_NEW\|DISCARDED_AS_DUPLICATE}, normalized_fill_id:id?, duplicate_of_fill_id:id?, recorded_at:ts` | `staged_fill_disposition_id` | `staged_fill_id,recorded_at,staged_fill_disposition_id` | `staged_fill_id -> W:StagedFill; resolution_id -> W:ImportResolution; ADMITTED_AS_NEW requires normalized_fill_id -> W:NormalizedFill and null duplicate; DISCARDED_AS_DUPLICATE requires duplicate_of_fill_id -> W:NormalizedFill and null normalized; exactly one disposition per staged fill` |
| `Upload` / `tp_exp_upload_v1` / `TP-SEC` | `upload_id:id, workspace_id:id, actor_user_id:id, contract_version:ver<TP-SEC:UPLOAD_ATTACHMENT_CONTRACT>, upload_kind:enum{CSV\|SCREENSHOT\|VOICE}, state:enum{QUARANTINED\|VALIDATING\|ACCEPTED\|REJECTED\|PURGED}, source_sha256:hash, byte_size:int, detected_media_type:str?, created_at:ts, accepted_at:ts?, terminal_at:ts?, purge_due_at:ts, safe_error_code:str?` | `upload_id` | `created_at,upload_id` | `workspace_id -> W:Workspace; purge_due_at = RECEIVE.recorded_at + 24h and never extends; raw object lease/location/bytes forbidden` |
| `UploadObjectAbsenceVerification` / `tp_exp_upload_object_absence_verification_v1` / `TP-SEC` | `object_absence_verification_id:id, workspace_id:id, upload_id:id, provider_object_version:str, lease_generation:int, verified_absent_at:ts, verification_method:enum{PROVIDER_VERSION_LOOKUP\|PROVIDER_INVENTORY}, verification_receipt_sha256:hash` | `object_absence_verification_id` | `upload_id,lease_generation,object_absence_verification_id` | `upload_id -> W:Upload; generation positive; unique workspace/upload/generation; no provider key/URL/content/receipt body` |
| `UploadStateEvent` / `tp_exp_upload_state_event_v1` / `TP-SEC` | `upload_state_event_id:id, workspace_id:id, upload_id:id, contract_version:ver<TP-SEC:UPLOAD_ATTACHMENT_CONTRACT>, event_sequence:int, event_type:enum{RECEIVE\|START_VALIDATION\|ACCEPT\|REJECT\|PURGE}, recorded_at:ts, actor_type:enum{USER\|SYSTEM}, actor_user_id:id?, idempotency_key:str, safe_reason_code:str?, object_absence_verification_id:id?` | `upload_state_event_id` | `upload_id,event_sequence,upload_state_event_id` | `upload_id -> W:Upload with equal contract_version; object_absence_verification_id -> W:UploadObjectAbsenceVerification non-null iff PURGE and matching upload; sequence positive/contiguous` |
| `SetupPreset` / `tp_exp_setup_preset_v1` / `TP-ACC` | `setup_id:id, workspace_id:id, preset_kind:enum{USER_DEFINED\|SYSTEM_OTHER}, created_at:ts` | `setup_id` | `setup_id` | `workspace_id -> W:Workspace` |
| `SetupPresetRevision` / `tp_exp_setup_preset_revision_v1` / `TP-ACC` | `setup_revision_id:id, workspace_id:id, setup_id:id, revision_no:int, schema_version:ver<TP-ACC:SETUP_PRESET_SCHEMA>, label:str, label_key:str, label_normalizer_version:ver<TP-ACC:SETUP_LABEL_KEY>, checklist_schema_version:ver<TP-ACC:PLAN_CHECKLIST_SCHEMA>, checklist_json:json, recorded_at:ts, recorded_by_user_id:id, content_sha256:hash` | `setup_revision_id` | `setup_id,revision_no,setup_revision_id` | `setup_id -> W:SetupPreset` |
| `SetupPresetStateEvent` / `tp_exp_setup_preset_state_event_v1` / `TP-ACC` | `setup_state_event_id:id, workspace_id:id, setup_id:id, event_sequence:int, setup_revision_id:id?, event_type:enum{CREATE\|REVISE\|ARCHIVE\|REACTIVATE}, recorded_at:ts, actor_user_id:id, idempotency_key:str` | `setup_state_event_id` | `setup_id,event_sequence,setup_state_event_id` | `setup_id -> W:SetupPreset; setup_revision_id -> W:SetupPresetRevision?; sequence positive/contiguous per setup` |
| `TradePlan` / `tp_exp_trade_plan_v1` / `TP-ACC` | `trade_plan_id:id, workspace_id:id, trading_account_id:id, instrument_id:id, direction:=LONG, state:enum{ARMED\|CONSUMED\|CANCELLED\|EXPIRED}, created_at:ts, expires_at:ts, consumed_by_episode_id:id?` | `trade_plan_id` | `created_at,trade_plan_id` | `trading_account_id -> W:TradingAccount; instrument_id -> P:Instrument; consumed_by_episode_id -> W:TradeEpisode?; state equals same-cutoff event replay and there is no persisted DRAFT` |
| `TradePlanRevision` / `tp_exp_trade_plan_revision_v1` / `TP-ACC` | `trade_plan_revision_id:id, workspace_id:id, trade_plan_id:id, revision_no:int, based_on_revision_id:id?, recorded_at:ts, recorded_by_user_id:id, instrument_catalog_version:str, setup_id:id, setup_revision_id:id, setup_label_snapshot:str, checklist_schema_version:ver<TP-ACC:PLAN_CHECKLIST_SCHEMA>, thesis:str, entry_zone_low:dec, entry_zone_high:dec, initial_stop_price:dec, planned_risk_quote:dec, planned_risk_asset:=USDT, confidence_score:int, checklist_json:json, content_sha256:hash` | `trade_plan_revision_id` | `trade_plan_id,revision_no,trade_plan_revision_id` | `trade_plan_id -> W:TradePlan; revision 1 has null based_on_revision_id and each later revision points to the immediately prior same-plan W:TradePlanRevision; setup_id -> W:SetupPreset; setup_revision_id -> W:SetupPresetRevision; instrument_catalog_version -> P:InstrumentCatalogVersion[]` |
| `PlanStateEvent` / `tp_exp_plan_state_event_v1` / `TP-ACC` | `plan_state_event_id:id, workspace_id:id, trade_plan_id:id, event_sequence:int, event_type:enum{ARM\|CONSUME\|CANCEL\|EXPIRE}, armed_revision_id:id?, consumed_by_episode_id:id?, recorded_at:ts, actor_type:enum{USER\|SYSTEM}, actor_user_id:id?, idempotency_key:str` | `plan_state_event_id` | `trade_plan_id,event_sequence,plan_state_event_id` | `trade_plan_id -> W:TradePlan; ARM is sequence 1 and armed_revision_id -> revision 1 of the same W:TradePlan while all other events require it null; consumed_by_episode_id -> W:TradeEpisode non-null iff CONSUME, otherwise null; sequence positive/contiguous per plan` |
| `PlanEpisodeAssociation` / `tp_exp_plan_episode_association_v1` / `TP-ACC` | `plan_episode_association_id:id, workspace_id:id, episode_id:id, trade_plan_id:id, trade_plan_revision_id:id, association_type:=LATE, actor_user_id:id, reason:str, recorded_at:ts` | `plan_episode_association_id` | `recorded_at,plan_episode_association_id` | `episode_id -> W:TradeEpisode; trade_plan_id -> W:TradePlan; trade_plan_revision_id -> W:TradePlanRevision` |
| `PlanMatchResolution` / `tp_exp_plan_match_resolution_v1` / `TP-ACC` | `plan_match_resolution_id:id, workspace_id:id, episode_id:id, based_on_projection_version:int, action:enum{CONFIRM_ASSOCIATION\|SELECT_CANDIDATE\|REMOVE_ASSOCIATION}, selected_trade_plan_id:id?, selected_trade_plan_revision_id:id?, old_association_json:json, new_association_json:json, actor_user_id:id, reason:str, idempotency_key:str, plan_proof_rule_version:ver<TP-ACC:PLAN_PROOF_RULE>, recorded_at:ts` | `plan_match_resolution_id` | `recorded_at,plan_match_resolution_id` | `episode_id,based_on_projection_version -> W:TradeEpisodeProjection; selected plan/revision -> W:TradePlan/TradePlanRevision?` |
| `Review` / `tp_exp_review_v1` / `TP-ACC` | `review_id:id, episode_id:id, workspace_id:id, state:enum{COMPLETED\|RECONFIRM_REQUIRED}, created_at:ts, completed_at:ts` | `review_id` | `created_at,review_id` | `episode_id -> W:TradeEpisode` |
| `ReviewRevision` / `tp_exp_review_revision_v1` / `TP-ACC` | `review_revision_id:id, workspace_id:id, review_id:id, revision_no:int, episode_projection_version:int, recorded_at:ts, recorded_by_user_id:id, idempotency_key:str, exit_reason:id, exit_reason_taxonomy_version:str, exit_reason_other_text:str?, rule_breach:bool, breach_taxonomy_version:str, breach_type_ids:id[], breach_other_text:str?, stop_moved_away:bool, risk_exceeded:bool, required_checklist_results_json:json, emotion:id?, emotion_taxonomy_version:str?, lesson:str?, content_sha256:hash` | `review_revision_id` | `review_id,revision_no,review_revision_id` | `review_id -> W:Review; review episode + projection version -> W:TradeEpisodeProjection; (exit_reason_taxonomy_version,EXIT_REASON,exit_reason) -> P:ReviewTaxonomyItem; each (breach_taxonomy_version,BREACH_TYPE,breach_type_ids[]) -> P:ReviewTaxonomyItem; nullable (emotion_taxonomy_version,EMOTION,emotion) -> P:ReviewTaxonomyItem; all matching version records required` |
| `ReviewRevisionAttachment` / `tp_exp_review_revision_attachment_v1` / `TP-ACC` | `review_revision_id:id, workspace_id:id, attachment_id:id, role:=SCREENSHOT, ordinal:=1, attachment_content_sha256:hash, created_at:ts` | `review_revision_id,attachment_id` | `review_revision_id,ordinal,attachment_id` | `review_revision_id -> W:ReviewRevision; attachment_id -> W:Attachment always; Attachment state then requires exact descriptor plus retained binary or T:ATTACHMENT_BINARY` |
| `Attachment` / `tp_exp_attachment_v1` / `TP-SEC` | `attachment_id:id, workspace_id:id, source_upload_id:id, contract_version:ver<TP-SEC:UPLOAD_ATTACHMENT_CONTRACT>, attachment_kind:enum{SCREENSHOT\|RETAINED_VOICE}, state:enum{ACTIVE\|DELETING\|DELETED}, scan_status:=PASSED, content_object_version:str, content_sha256:hash, byte_size:int, media_type:str, original_filename:str?, safe_display_filename:str, created_at:ts, deleted_at:ts?` | `attachment_id` | `created_at,attachment_id` | `source_upload_id -> unique same-workspace W:Upload with equal contract and immutable prior ACCEPT/accepted_at proof, even if current Upload is PURGED; SCREENSHOT iff source kind SCREENSHOT, RETAINED_VOICE iff VOICE+keep_original confirmation; exact atomic sequence-1 ACTIVATE required; deleted binary -> T:ATTACHMENT_BINARY` |
| `AttachmentObjectAbsenceVerification` / `tp_exp_attachment_object_absence_verification_v1` / `TP-SEC` | `object_absence_verification_id:id, workspace_id:id, attachment_id:id, content_object_version:str, lease_generation:int, verified_absent_at:ts, verification_method:enum{PROVIDER_VERSION_LOOKUP\|PROVIDER_INVENTORY}, verification_receipt_sha256:hash` | `object_absence_verification_id` | `attachment_id,lease_generation,object_absence_verification_id` | `attachment_id -> W:Attachment with equal content_object_version; generation positive; unique workspace/attachment/generation; no provider key/URL/content/receipt body` |
| `AttachmentStateEvent` / `tp_exp_attachment_state_event_v1` / `TP-SEC` | `attachment_state_event_id:id, workspace_id:id, attachment_id:id, contract_version:ver<TP-SEC:UPLOAD_ATTACHMENT_CONTRACT>, event_sequence:int, event_type:enum{ACTIVATE\|DELETE_REQUEST\|DELETE_COMPLETE}, recorded_at:ts, actor_type:enum{USER\|SYSTEM}, actor_user_id:id?, idempotency_key:str, safe_reason_code:str?, object_absence_verification_id:id?` | `attachment_state_event_id` | `attachment_id,event_sequence,attachment_state_event_id` | `attachment_id -> W:Attachment with equal contract_version; object_absence_verification_id -> W:AttachmentObjectAbsenceVerification non-null iff DELETE_COMPLETE and matching attachment/content version; sequence positive/contiguous` |
| `AttachmentExportDescriptor` / `tp_exp_attachment_export_descriptor_v1` / `TP-EXP` | `attachment_id:id, workspace_id:id, attachment_kind:enum{SCREENSHOT\|RETAINED_VOICE}, availability:enum{RETAINED_CLEAN\|DELETE_PENDING\|TOMBSTONED}, state_at_cutoff:enum{ACTIVE\|DELETING\|DELETED}, scan_status_at_cutoff:=PASSED, original_filename:str?, safe_display_filename:str, media_type:str, byte_size:int, content_sha256:hash, content_object_version:str, archive_path:str?, created_at:ts, deleted_at:ts?` | `attachment_id` | `attachment_id` | `attachment_id -> W:Attachment; ACTIVE/RETAINED_CLEAN has path+binary; DELETING/DELETE_PENDING has null path/no tombstone; DELETED/TOMBSTONED has null path and T:ATTACHMENT_BINARY` |

Review taxonomy closure is field-exact. `exit_reason` resolves an item whose version equals `exit_reason_taxonomy_version` and whose `taxonomy_type = EXIT_REASON`; every unique `breach_type_ids` element resolves under `breach_taxonomy_version` with `BREACH_TYPE`; `emotion` and `emotion_taxonomy_version` are both null or both non-null and, when present, resolve with `EMOTION`. The matching `ReviewTaxonomyVersion` and `ReviewTaxonomyItem` records are both included. An ID found only in another version/type, an active-version substitution, a missing item, duplicate breach ID or broken null coupling fails `EXPORT_REFERENCE_DANGLING` or `EXPORT_SCHEMA_VIOLATION`; the exporter never repairs it.

#### Accounting projections

| RecordType / recordSchemaId / source | Exact payload fields | Key | Sort | FK/reference rules |
|---|---|---|---|---|
| `TradeEpisode` / `tp_exp_trade_episode_v1` / `TP-ACC` | `episode_id:id, workspace_id:id, trading_account_id:id, instrument_id:id, opening_fill_id:id, opening_fill_dedup_key:hash, created_at:ts` | `episode_id` | `episode_id` | `trading_account_id -> W:TradingAccount; instrument_id -> P:Instrument; opening_fill_id -> W:NormalizedFill with matching account/instrument/dedup key, BUY and ADMITTED; deterministic UUIDv5 and version-1 atomicity rules above` |
| `NormalizedFill` / `tp_exp_normalized_fill_v1` / `TP-ACC` | `fill_id:id, workspace_id:id, trading_account_id:id, import_batch_id:id, import_row_id:id, source_row_number:int, import_contract_version:ver<TP-ACC:IMPORT_CONTRACT>, fill_schema_version:ver<TP-ACC:NORMALIZED_FILL_SCHEMA>, instrument_catalog_version:str, venue:=BINANCE, product_type:=SPOT, instrument_id:id, venue_symbol:str, base_asset:str, quote_asset:=USDT, side:enum{BUY\|SELL}, executed_at:ts, source_timestamp_precision:enum{MILLISECOND\|SECOND}, source_time_start:ts, source_time_end_exclusive:ts, price_quote_per_base:dec, executed_qty_base:dec, gross_amount_quote:dec, fee_qty:dec, fee_asset:str, canonical_signature:hash, occurrence_index:int, dedup_key:hash, created_at:ts` | `fill_id` | `source_time_start,dedup_key,fill_id` | `trading_account_id -> W:TradingAccount; import_batch_id -> W:ImportBatch; import_row_id -> W:ImportRow; instrument_id -> P:Instrument; catalog version -> P:InstrumentCatalogVersion[]; existence is immutable admission and admission_status/admitted_at fields are forbidden` |
| `TradeEpisodeProjection` / `tp_exp_trade_episode_projection_v1` / `TP-ACC` | `episode_id:id, projection_version:int, projection_algorithm_version:ver<TP-ACC:PROJECTION_ALGORITHM>, ledger_algorithm_version:ver<TP-ACC:LEDGER_ALGORITHM>, workspace_id:id, trading_account_id:id, instrument_id:id, quote_asset:=USDT, state:enum{OPEN\|CLOSED}, first_fill_id:id, first_fill_at:ts, first_fill_time_end_exclusive:ts, first_fill_timestamp_precision:enum{MILLISECOND\|SECOND}, closed_fill_id:id?, closed_at:ts?, closed_time_end_exclusive:ts?, closed_timestamp_precision:enum{MILLISECOND\|SECOND}?, associated_plan_id:id?, associated_plan_revision_id:id?, frozen_plan_revision_id:id?, plan_proof_status:enum{VERIFIED\|AMBIGUOUS\|LATE\|UNMATCHED}, plan_proof_reason_code:enum{VERIFIED_BEFORE_INTERVAL\|ARM_INSIDE_INTERVAL\|REVISION_INSIDE_INTERVAL\|EXPIRY_INSIDE_INTERVAL\|CANCEL_INSIDE_INTERVAL\|MULTIPLE_CANDIDATES\|NO_ELIGIBLE_CANDIDATE\|USER_ASSOCIATED_AFTER_FILL}, plan_proof_rule_version:ver<TP-ACC:PLAN_PROOF_RULE>, plan_candidate_ids_json:json, plan_proof_basis_json:json, plan_proof_resolved_at:ts, late_association_id:id?, plan_match_resolution_id:id?, position_qty_base:dec, open_cost_basis_quote:dec, average_cost_quote_per_base:dec?, gross_realized_pnl_quote:dec, known_fee_quote:dec, net_realized_pnl_quote:dec?, accounting_quality:enum{COMPLETE\|FEE_CONVERSION_MISSING\|SEQUENCE_PENDING\|REPLAY_PENDING\|INVALID}, created_at:ts, superseded_at:ts?` | `episode_id,projection_version` | `episode_id,projection_version` | `episode_id -> W:TradeEpisode; fill IDs -> W:NormalizedFill; closed-field coupling; average-cost round18/null and no rounded recurrence reuse in section 7.5; plan/revision/association/resolution and nested proof keys -> exact W records; active interval is created_at <= T < superseded_at, null infinity; history retains OPEN/CLOSED` |
| `EpisodeFillAllocation` / `tp_exp_episode_fill_allocation_v1` / `TP-ACC` | `episode_id:id, workspace_id:id, projection_version:int, fill_id:id, event_sequence:int, position_qty_before:dec, position_qty_delta:dec, position_qty_after:dec, cost_basis_before:dec, cost_basis_delta:dec, cost_basis_after:dec, gross_realized_delta_quote:dec, fee_expense_delta_quote:dec?` | `episode_id,projection_version,fill_id` | `episode_id,projection_version,event_sequence,fill_id` | `episode_id,projection_version -> W:TradeEpisodeProjection; fill_id -> W:NormalizedFill` |
| `AccountingLedgerEntry` / `tp_exp_accounting_ledger_entry_v1` / `TP-ACC` | `ledger_entry_id:id, workspace_id:id, episode_id:id, projection_version:int, fill_id:id, entry_sequence:int, entry_type:enum{TRADE\|FEE}, occurred_at:ts, asset:str, asset_qty_delta:dec, quote_asset:=USDT, quote_value_delta:dec?, position_qty_delta_base:dec, cost_basis_delta_quote:dec, gross_realized_delta_quote:dec, fee_expense_delta_quote:dec?, fee_conversion_id:id?, algorithm_version:ver<TP-ACC:LEDGER_ALGORITHM>, created_at:ts` | `ledger_entry_id` | `episode_id,projection_version,entry_sequence,ledger_entry_id` | `episode projection/allocation -> W records; fill_id -> W:NormalizedFill; FEE fee_conversion_id -> exact selected W:FeeConversion, TRADE requires null; two-entry/sign/UUIDv5/sum rules in section 7.5` |
| `FeeConversion` / `tp_exp_fee_conversion_v1` / `TP-ACC` | `fee_conversion_id:id, workspace_id:id, fill_id:id, conversion_version:int, fee_asset:str, quote_asset:=USDT, fee_qty:dec, status:enum{EXACT\|DERIVED\|UNAVAILABLE}, method:enum{NATIVE_QUOTE\|FILL_RATE\|DIRECT_1M_CLOSE\|INVERSE_1M_CLOSE}?, rate_quote_per_fee_asset:dec?, fee_value_quote:dec?, as_of_at:ts?, market_bar_ids_json:json?, market_bar_source_observation_ids_json:json?, market_conversion_catalog_version:str?, conversion_path_json:json?, algorithm_version:ver<TP-ACC:FEE_CONVERSION_ALGORITHM>, created_at:ts, superseded_at:ts?` | `fee_conversion_id` | `fill_id,conversion_version,fee_conversion_id` | `fill_id -> W:NormalizedFill; exact null/version/path rules in section 7.5; aligned typed bar/observation keys -> P:MarketBarRevision/MarketBarSourceObservation then request/batch; nullable path resolution key -> P:MarketBarResolution; path catalog key -> P:MarketConversionCatalogVersion` |
| `EpisodeMetricEligibilityEvent` / `tp_exp_episode_metric_eligibility_event_v1` / `TP-ACC` | `episode_metric_eligibility_event_id:id, workspace_id:id, episode_id:id, event_sequence:int, based_on_projection_version:int, action:enum{EXCLUDE\|RESTORE}, reason:str, actor_user_id:id, idempotency_key:str, recorded_at:ts` | `episode_metric_eligibility_event_id` | `episode_id,event_sequence,episode_metric_eligibility_event_id` | `episode/projection -> W:TradeEpisodeProjection; sequence positive/contiguous per episode across projection versions` |
| `VerifiedReviewWeekRateMetricSnapshot` / `tp_exp_verified_review_week_rate_metric_snapshot_v1` / `TP-ACC` | `metric_snapshot_id:id, metric_id:=verified_review_week_rate, metric_version:ver<TP-ACC:NORTH_STAR_METRIC>, algorithm_version:ver<TP-ACC:METRIC_ALGORITHM>, workspace_id:id, user_id:id, reporting_as_of_at:ts, cohort_range_start_sequence:int, cohort_range_end_sequence_exclusive:int, cohort_range_refs_json:json, numerator:int, denominator:int, value:dec?, null_reason:str?, numerator_weekly_cohort_ids_json:json, denominator_weekly_cohort_ids_json:json, user_week_drilldown_json:json, input_event_digest:hash, created_at:ts` | `metric_snapshot_id` | `reporting_as_of_at,metric_snapshot_id` | `exact range/drilldown/digest schema in section 7.5; every embedded cohort/lock/episode/eligibility/completion/report/experiment key -> corresponding same-workspace W record` |

#### Context and public provenance projections

The `epoch_ms` declarations below override the general timestamp string rule and preserve the exact `TP-MCE` hash basis.

| RecordType / recordSchemaId / source | Exact payload fields | Key | Sort | FK/reference rules |
|---|---|---|---|---|
| `MarketDataIngestionBatch` / `tp_exp_market_data_ingestion_batch_v1` / `TP-MCE` | `ingestionBatchId:id, sourceVenue:=BINANCE, productType:=SPOT, sourceBaseUrl:str, fetcherVersion:str, startedAt:epoch_ms, completedAt:epoch_ms?, status:enum{RUNNING\|COMPLETE\|PARTIAL\|FAILED}` | `ingestionBatchId` | `startedAt,ingestionBatchId` | `sourceBaseUrl is exact pinned official HTTPS origin; no tenant field` |
| `MarketDataSourceRequest` / `tp_exp_market_data_source_request_v1` / `TP-MCE` | `sourceRequestId:id, ingestionBatchId:id, retryAttempt:int, sourceBaseUrl:str, httpMethod:=GET, path:=/api/v3/klines, symbol:str, timeframe:enum{1m\|5m}, timeZone:=0, startTime:epoch_ms, endTime:epoch_ms, limit:=1000, requestedAt:epoch_ms, fetchedAt:epoch_ms?, httpStatus:int?, responseSha256:hash?, responseRowCount:int?, requestMetadataHash:hash` | `sourceRequestId` | `requestedAt,sourceRequestId` | `ingestionBatchId -> P:MarketDataIngestionBatch; sourceBaseUrl byte-equals batch; exact request metadata hash` |
| `MarketBarRevision` / `tp_exp_market_bar_revision_v1` / `TP-MCE` | `revisionId:id, venue:=BINANCE, productType:=SPOT, symbol:str, timeframe:enum{1m\|5m}, openAt:epoch_ms, open:dec, high:dec, low:dec, close:dec, baseVolume:dec, sourceCloseTime:epoch_ms, quoteVolume:dec, tradeCount:int, takerBuyBaseVolume:dec, takerBuyQuoteVolume:dec, contentHash:hash` | `revisionId` | `symbol,timeframe,openAt,revisionId` | none |
| `MarketBarConflict` / `tp_exp_market_bar_conflict_v1` / `TP-MCE` | `marketBarConflictId:id, venue:=BINANCE, productType:=SPOT, symbol:str, timeframe:enum{1m\|5m}, openAt:epoch_ms, createdAt:epoch_ms` | `marketBarConflictId` | `symbol,timeframe,openAt,marketBarConflictId` | exact unique logical bar key; public header has no tenant field |
| `MarketBarResolution` / `tp_exp_market_bar_resolution_v1` / `TP-MCE` | `marketBarResolutionId:id, marketBarConflictId:id, resolutionSequence:int, candidateRevisionIdsJson:json, selectedRevisionId:id, reasonCode:=VERIFIED_SOURCE_SELECTION, actorType:=OPERATOR, idempotencyKey:str, recordedAt:epoch_ms, contentSha256:hash` | `marketBarResolutionId` | `marketBarConflictId,resolutionSequence,marketBarResolutionId` | `conflict -> P:MarketBarConflict; every candidate/selected ID -> P:MarketBarRevision; exact prefix/order/hash/selector rules below` |
| `MarketBarSourceObservation` / `tp_exp_market_bar_source_observation_v1` / `TP-MCE` | `sourceObservationId:id, sourceRequestId:id, marketBarRevisionId:id, responseRowIndex:int, observationSequence:int` | `sourceObservationId` | `marketBarRevisionId,observationSequence,sourceObservationId` | `sourceRequestId -> P:MarketDataSourceRequest; marketBarRevisionId -> P:MarketBarRevision; sequence positive/contiguous per revision; (request,row) unique` |
| `ContextAlgorithmRelease` / `tp_exp_context_algorithm_release_v1` / `TP-MCE` | `contextAlgorithmReleaseId:id, algorithmVersion:str, parameterSetId:str, calculationContractVersion:str, calculationContractSha256:hash, implementationArtifactSha256:hash, parameterPayloadSha256:hash, releasedAt:epoch_ms, releasedBySystemPrincipalId:id, releaseSha256:hash` | `contextAlgorithmReleaseId` | `algorithmVersion,parameterSetId,contextAlgorithmReleaseId` | shared public immutable release; unique `(algorithmVersion,parameterSetId)`; `releaseSha256` is SHA-256 of RFC 8785 bytes of exactly `{ "algorithmVersion":str, "calculationContractSha256":hash, "calculationContractVersion":str, "implementationArtifactSha256":hash, "parameterPayloadSha256":hash, "parameterSetId":str }`; releasedBySystemPrincipalId is a typed approved-system-principal reference, not a tenant/user FK |
| `ContextSnapshot` / `tp_exp_context_snapshot_v1` / `TP-MCE` | `id:id, workspaceId:id, tradeEpisodeId:id, episodeProjectionVersion:int, snapshotRevisionNo:int, phase:enum{ENTRY\|EXIT}, eventFillId:id, eventSequence:int, eventAt:epoch_ms, eventTimeEndExclusive:epoch_ms, eventTimestampPrecision:enum{MILLISECOND\|SECOND}, referencePrice:dec, venue:=BINANCE, productType:=SPOT, symbol:str, timeframe:enum{1m\|5m}, timezone:=UTC, asOfAt:epoch_ms, cutoffAt:epoch_ms, targetBarOpenAt:epoch_ms, hourOfWeek:int, sessionStartAt:epoch_ms, rvol:dec?, effortPercentile:dec?, volumeRobustZ:dec?, volumeAnomalyCode:enum{UNUSUALLY_HIGH_VOLUME\|UNUSUALLY_LOW_VOLUME}?, normalizedTrueRange:dec?, responsePercentile:dec?, rangeRobustZ:dec?, rangeAnomalyCode:enum{UNUSUALLY_HIGH_RANGE\|UNUSUALLY_LOW_RANGE}?, sessionVwap:dec?, vwapDistanceBps:dec?, effortResponseCode:enum{E_HIGH_R_HIGH\|E_HIGH_R_LOW\|E_LOW_R_HIGH\|E_LOW_R_LOW}?, efficiencyRatio20:dec?, realizedVol20:dec?, realizedVolPercentile:dec?, regimeCode:enum{TREND_HIGH_VOL\|TREND_LOW_VOL\|RANGE_HIGH_VOL\|RANGE_LOW_VOL}?, quality:enum{COMPLETE\|PARTIAL\|UNRELIABLE}, qualityReasons:enum{BASELINE_COVERAGE_INSUFFICIENT\|BASELINE_COVERAGE_PARTIAL\|BASELINE_DISTINCT_WEEKS_INSUFFICIENT\|BASELINE_DISTINCT_WEEKS_PARTIAL\|CORE_GAP\|INPUT_HASH_MISMATCH\|INVALID_TARGET_OR_CORE_BAR\|MISSING_TARGET_BAR\|PAGINATION_STALLED\|PROVENANCE_HASH_MISMATCH\|REQUIRED_METRIC_UNAVAILABLE\|SESSION_COVERAGE_INSUFFICIENT\|SESSION_COVERAGE_PARTIAL\|SESSION_HAS_NO_CLOSED_BAR\|SOURCE_INGESTION_BATCH_INVALID\|SOURCE_MISMATCH\|SOURCE_OBSERVATION_MISSING\|SOURCE_REQUEST_INVALID\|SOURCE_RESPONSE_INVALID\|SOURCE_REVISION_CONFLICT}[], missingIntervals:json[], coreCoverage:dec, sessionCoverage:dec?, baselineCoverage:dec, baselineDistinctWeeks:int, aggregationEligible:bool, algorithmVersion:ver<TP-MCE:CONTEXT_ALGORITHM>, parameterSetId:ver<TP-MCE:CONTEXT_PARAMETER_SET>, inputBarRevisionIds:id[], inputBarSourceObservationIds:id[], inputBarResolutionIds:id?[], sourceRequestIds:id[], sourceIngestionBatchIds:id[], inputHash:hash, provenanceHash:hash, computedAt:epoch_ms, supersedesSnapshotId:id?, recomputeReason:enum{SOURCE_GAP_FILLED\|SOURCE_REVISION_RESOLVED\|EPISODE_PROJECTION_REPLAYED\|ALGORITHM_UPGRADE\|MANUAL_RETRY}?` | `id` | `tradeEpisodeId,episodeProjectionVersion,phase,timeframe,algorithmVersion,parameterSetId,snapshotRevisionNo,id` | `workspace/episode projection -> W:TradeEpisodeProjection; eventFillId -> W:NormalizedFill; (algorithmVersion,parameterSetId) -> exact P:ContextAlgorithmRelease; aligned bars/observations/resolutions and request/batch sets -> P corresponding types; exact revision/quality/interval/hash rules below; supersedesSnapshotId -> exact prior W:ContextSnapshot?` |

`MarketBarResolution.candidateRevisionIdsJson` is a nonempty sorted unique array of exact `{ "revisionId": id }`, ordered by `(MarketBarRevision.contentHash,revisionId)`; all candidates share the conflict's exact venue/product/symbol/timeframe/openAt and `selectedRevisionId` occurs exactly once. `resolutionSequence` starts at 1 and is contiguous per conflict, `recordedAt` is nondecreasing, and the archive includes the full prefix through every referenced resolution. `contentSha256` is SHA-256 of RFC 8785 bytes of exactly `{ "candidateRevisionRecordKeys": candidateRevisionIdsJson, "conflictRecordKey": { "marketBarConflictId": id }, "reasonCode": "VERIFIED_SOURCE_SELECTION", "recordedAt": epoch_ms, "resolutionSequence": int, "selectedRevisionRecordKey": { "revisionId": id } }`; resolution ID/hash/actor/idempotency are outside. A duplicate sequence/idempotency payload or a changed hash fails closed.

The reader replays `market_bar_as_of_v1` at each consumer cutoff (`ContextSnapshot.computedAt` or `FeeConversion.created_at`). A revision is visible only through an eligible observation whose request has `fetchedAt <= cutoff`, HTTP 2xx plus response hash, and a COMPLETE/PARTIAL terminal batch. One visible distinct revision requires null resolution. Multiple require the greatest visible resolution sequence whose candidate array byte-equals the entire visible revision set; otherwise the selection is unresolved. The selected observation is the unique lowest visible eligible `observationSequence` for the selected revision. A current/latest revision, observation or resolution substitution is forbidden.

Every ContextSnapshot's exact `(algorithmVersion,parameterSetId)` resolves one included ContextAlgorithmRelease. The reader recomputes `releaseSha256`, tuple uniqueness and snapshot equality from the exported row. Calculation contract, implementation artifact and parameter payload bytes remain deployment artifacts outside the workspace archive; their hashes are preserved but the reader MUST NOT claim it reverified absent artifact bytes. ContextEpisodeTrigger and ManualContextRecomputeRequest remain non-exported tenant command/control receipts and are not substitutes for this release closure.

For every `ContextSnapshot`, `inputBarRevisionIds`, `inputBarSourceObservationIds` and `inputBarResolutionIds` have equal length and index alignment. Revision/observation IDs are non-null; resolution is null exactly for the one-visible-revision branch and otherwise resolves a prefix row selecting that index's revision. Entries sort by referenced `(timeframe,openAt,revisionId)` and have no duplicate revision. `sourceRequestIds` and `sourceIngestionBatchIds` are the exact sorted unique closure of selected observations. Every set is validated at `computedAt` before recomputing `inputHash` and `provenanceHash` from TP-MCE's exact RFC 8785 bases.

Context revision scope is exactly `(workspaceId,tradeEpisodeId,episodeProjectionVersion,phase,timeframe,algorithmVersion,parameterSetId)`. `snapshotRevisionNo` starts at 1, is contiguous and unique per scope. Revision 1 has null supersedes ID. A same-scope revision N>1 points exactly to N-1 and uses SOURCE_GAP_FILLED, SOURCE_REVISION_RESOLVED or MANUAL_RETRY. A changed episode projection or dependency tuple starts revision 1/null supersedes with EPISODE_PROJECTION_REPLAYED or ALGORITHM_UPGRADE. `computedAt` is nondecreasing by revision and there is exactly one leaf. At cutoff T, greatest revision with `computedAt <= T` wins even when IDs/times reverse-sort; a gap, two leaves, wrong predecessor or cross-scope supersede fails.

`qualityReasons` is unique ASCII-code sorted. COMPLETE requires `[]` and `aggregationEligible=true`. PARTIAL requires `aggregationEligible=false` and a nonempty subset of BASELINE_COVERAGE_PARTIAL, BASELINE_DISTINCT_WEEKS_PARTIAL, SESSION_COVERAGE_PARTIAL and SESSION_HAS_NO_CLOSED_BAR. UNRELIABLE requires `aggregationEligible=false`, at least one reason, and every derived metric/anomaly/regime value null. An insufficient threshold uses its INSUFFICIENT code, never PARTIAL; a source conflict uses SOURCE_REVISION_CONFLICT.

Each `missingIntervals` member has exactly `{ "endExclusive": epoch_ms, "reasonCode": reason, "scope": scope, "start": epoch_ms }`. Scope is CORE/SESSION/BASELINE; reason is REVISION_CONFLICT/INVALID_SOURCE_BAR/SOURCE_REQUEST_FAILED/PAGINATION_STALLED/NO_SOURCE_BAR. Bounds are safe aligned milliseconds with start < endExclusive. Each unusable expected slot takes the first applicable reason in that listed precedence, adjacent same-scope/reason slots coalesce, and rows sort by scope order CORE,SESSION,BASELINE then start/end/reason; intervals cannot overlap within a scope. COMPLETE requires `[]`; PARTIAL/UNRELIABLE may use `[]` only for a no-slot/hash/provenance diagnostic. Core/session/baseline coverage and distinct-week counts must recompute exactly from these intervals and selected inputs.

#### Weekly Lab projections

| RecordType / recordSchemaId / source | Exact payload fields | Key | Sort | FK/reference rules |
|---|---|---|---|---|
| `WeeklyCohort` / `tp_exp_weekly_cohort_v1` / `TP-LAB` | `weekly_cohort_id:id, workspace_id:id, user_id:id, cohort_sequence:int, cohort_key_sha256:hash, cohort_type:enum{REGULAR\|TRANSITION}, state:enum{SCHEDULED\|OPEN\|LOCK_PENDING\|LOCKED\|SUPERSEDED}, workspace_timezone:str, tzdb_version:str, cohort_start_local:str, cohort_end_local_exclusive:str, start_boundary_resolution:enum{EXACT\|AMBIGUOUS_EARLIER\|GAP_FORWARD}, end_boundary_resolution:enum{EXACT\|AMBIGUOUS_EARLIER\|GAP_FORWARD}, cohort_start_at_utc:ts, cohort_end_at_utc:ts, regular_week_start_local_date:str?, previous_weekly_cohort_id:id?, timezone_change_schedule_id:id?, north_star_eligible_cohort:bool, completion_eligible_cohort:bool, initial_reporting_as_of_at:ts?, locked_at:ts?, created_at:ts` | `weekly_cohort_id` | `cohort_sequence,weekly_cohort_id` | `workspace -> W:Workspace; previous cohort -> W:WeeklyCohort?; timezone schedule -> W:TimezoneChangeSchedule?; state recomputes at cutoff by section 4.3, LOCK_PENDING has null lock fields` |
| `WeeklyCohortStateEvent` / `tp_exp_weekly_cohort_state_event_v1` / `TP-LAB` | `weekly_cohort_state_event_id:id, workspace_id:id, weekly_cohort_id:id, event_sequence:int, event_type:enum{SCHEDULE\|OPEN\|LOCK\|SUPERSEDE}, recorded_at:ts, actor_type:enum{SYSTEM\|USER}, actor_user_id:id?, idempotency_key:str, reason_code:str` | `weekly_cohort_state_event_id` | `weekly_cohort_id,event_sequence,weekly_cohort_state_event_id` | `(workspace_id,weekly_cohort_id) -> W:WeeklyCohort; sequence positive/contiguous per cohort` |
| `TimezoneChangeSchedule` / `tp_exp_timezone_change_schedule_v1` / `TP-LAB` | `timezone_change_schedule_id:id, workspace_id:id, user_id:id, old_timezone:str, old_tzdb_version:str, new_timezone:str, new_tzdb_version:str, requested_at:ts, effective_at:ts, new_regular_start_local:str, new_regular_start_at_utc:ts, state:enum{SCHEDULED\|CANCELLED\|APPLIED}, actor_user_id:id, idempotency_key:str, created_at:ts` | `timezone_change_schedule_id` | `requested_at,timezone_change_schedule_id` | `workspace_id -> W:Workspace` |
| `TimezoneChangeScheduleStateEvent` / `tp_exp_timezone_change_schedule_state_event_v1` / `TP-LAB` | `timezone_change_schedule_state_event_id:id, workspace_id:id, timezone_change_schedule_id:id, event_sequence:int, event_type:enum{SCHEDULE\|CANCEL\|APPLY}, recorded_at:ts, actor_user_id:id?, idempotency_key:str, reason_code:str` | `timezone_change_schedule_state_event_id` | `timezone_change_schedule_id,event_sequence,timezone_change_schedule_state_event_id` | `timezone_change_schedule_id -> W:TimezoneChangeSchedule; sequence positive/contiguous per schedule` |
| `WeeklyCohortInputRevision` / `tp_exp_weekly_cohort_input_revision_v1` / `TP-LAB` | `weekly_cohort_input_revision_id:id, weekly_cohort_id:id, workspace_id:id, revision_no:int, weekly_lab_schema_version:ver<TP-LAB:WEEKLY_SCHEMA>, reason:enum{INITIAL_LOCK\|REVIEW_CORRECTION\|ACCOUNTING_CORRECTION\|CONTEXT_RECOVERY\|DATA_BACKFILL}, reporting_as_of_at:ts, cohort_locked_at:ts, revision_locked_at:ts, supersedes_input_revision_id:id?, correction_group_id:id?, episode_projection_refs_json:json, review_revision_refs_json:json, context_ref_matrix_json:json, referenced_taxonomy_versions_json:json, input_digest_sha256:hash, created_at:ts` | `weekly_cohort_input_revision_id` | `weekly_cohort_id,revision_no,weekly_cohort_input_revision_id` | `cohort/supersedes -> W records; embedded episode/review/context refs -> exact W records; taxonomy refs -> exact P versions/items; lock, selection, array, digest and correction rules below` |
| `ContextAvailabilityDecision` / `tp_exp_context_availability_decision_v1` / `TP-LAB` | `context_availability_decision_id:id, workspace_id:id, weekly_cohort_input_revision_id:id, episode_id:id, episode_projection_version:int, phase:enum{ENTRY\|EXIT}, timeframe:enum{1m\|5m}, context_visibility_cutoff_at:ts, status:enum{PENDING\|MISSING\|NOT_APPLICABLE}, reason_code:enum{JOB_PENDING\|SOURCE_FAILED\|SOURCE_UNAVAILABLE\|EXACT_SNAPSHOT_NOT_FOUND\|VERSION_MISMATCH\|PROJECTION_NOT_CONTEXT_READY}, observed_at:ts, content_sha256:hash` | `context_availability_decision_id` | `weekly_cohort_input_revision_id,episode_id,episode_projection_version,phase,timeframe,context_availability_decision_id` | `input revision -> W:WeeklyCohortInputRevision; episode/projection -> W:TradeEpisodeProjection; exact one-to-one slot, cutoff/status/reason/time/hash coupling below` |
| `MetricSnapshot` / `tp_exp_metric_snapshot_v1` / `TP-LAB` | `metric_snapshot_id:id, workspace_id:id, weekly_cohort_id:id, weekly_cohort_input_revision_id:id, metric_snapshot_schema_version:ver<TP-LAB:METRIC_SNAPSHOT_SCHEMA>, weekly_lab_schema_version:ver<TP-LAB:WEEKLY_SCHEMA>, metric_id:id, metric_formula_version:str, metric_algorithm_version:ver<TP-ACC:METRIC_ALGORITHM>, eligibility_policy_id:str, dependency_version_tuple_hash:hash, reporting_start_local:str, reporting_end_local_exclusive:str, workspace_timezone:str, tzdb_version:str, reporting_start_at_utc:ts, reporting_end_at_utc:ts, reporting_as_of_at:ts, dimension_json:json, phase:enum{ENTRY\|EXIT}?, timeframe:enum{1m\|5m}?, value_type:enum{DECIMAL\|INTEGER\|DURATION_MS\|INTERVAL\|OBJECT}, value_decimal:dec?, value_integer:int?, value_duration_ms:int?, value_interval_json:json?, value_object_json:json?, unit:str, numerator_decimal:dec?, denominator_decimal:dec?, null_reason:str?, display_state:enum{NORMAL\|POSITIVE_INFINITY\|UNDEFINED\|UNAVAILABLE}, computation_status:enum{COMPLETE\|UNAVAILABLE}, candidate_episode_count:int, eligible_episode_count:int, excluded_episode_count:int, candidate_episode_refs_json:json, included_episode_refs_json:json, excluded_episode_refs_json:json, exclusion_reason_counts_json:json, source_review_revision_ids_json:json, source_context_snapshot_ids_json:json, evidence_label:enum{INSUFFICIENT\|EXPLORATORY\|ESTIMATED}, input_digest_sha256:hash, computed_at:ts, supersedes_metric_snapshot_id:id?` | `metric_snapshot_id` | `weekly_cohort_id,metric_id,phase,timeframe,metric_snapshot_id` | `cohort/input/supersedes -> W records; exhaustive metric/formula/policy/dimension/type/unit/null matrix, metrics_decimal_v1 arithmetic, population partition, typed sources, value/counter coupling and digest rules below` |
| `WeeklyReport` / `tp_exp_weekly_report_v1` / `TP-LAB` | `weekly_report_id:id, workspace_id:id, user_id:id, weekly_cohort_id:id, created_at:ts` | `weekly_report_id` | `weekly_cohort_id,weekly_report_id` | `weekly_cohort_id -> W:WeeklyCohort` |
| `WeeklyReportRevision` / `tp_exp_weekly_report_revision_v1` / `TP-LAB` | `weekly_report_revision_id:id, weekly_report_id:id, workspace_id:id, weekly_cohort_id:id, weekly_cohort_input_revision_id:id, revision_no:int, status:enum{PUBLISHED\|SUPERSEDED}, weekly_lab_schema_version:ver<TP-LAB:WEEKLY_SCHEMA>, renderer_id:ver<TP-LAB:RENDERER>, locale:=vi-VN, dependency_version_tuple_json:json, dependency_version_tuple_hash:hash, reporting_as_of_at:ts, cohort_type:enum{REGULAR\|TRANSITION}, context_section_status:enum{AVAILABLE\|PARTIAL_COVERAGE\|UNAVAILABLE\|EMPTY}, metric_snapshot_ids_json:json, section_payload_json:json, input_digest_sha256:hash, content_sha256:hash, supersedes_report_revision_id:id?, superseded_by_report_revision_id:id?, recompute_reason:enum{INITIAL\|REVIEW_CORRECTION\|ACCOUNTING_CORRECTION\|CONTEXT_RECOVERY\|DATA_BACKFILL\|VERSION_CHANGE}, published_at:ts` | `weekly_report_revision_id` | `weekly_report_id,revision_no,weekly_report_revision_id` | `report/cohort/input/supersedes -> W records; exact section grammar and metric union below; request-time renderer bytes are forbidden` |
| `WeeklyReportRevisionStateEvent` / `tp_exp_weekly_report_revision_state_event_v1` / `TP-LAB` | `weekly_report_revision_state_event_id:id, workspace_id:id, weekly_report_id:id, weekly_report_revision_id:id, event_sequence:int, event_type:enum{PUBLISH\|SUPERSEDE}, caused_by_report_revision_id:id?, recorded_at:ts` | `weekly_report_revision_state_event_id` | `weekly_report_id,event_sequence,weekly_report_revision_state_event_id` | `(workspace_id,weekly_report_id) -> W:WeeklyReport; (workspace_id,revision/caused-by) -> W:WeeklyReportRevision; sequence positive/contiguous per report` |
| `BehavioralExperiment` / `tp_exp_behavioral_experiment_v1` / `TP-LAB` | `behavioral_experiment_id:id, workspace_id:id, user_id:id, target_weekly_cohort_id:id, created_at:ts` | `behavioral_experiment_id` | `target_weekly_cohort_id,behavioral_experiment_id` | `target cohort -> W:WeeklyCohort` |
| `BehavioralExperimentRevision` / `tp_exp_behavioral_experiment_revision_v1` / `TP-LAB` | `behavioral_experiment_revision_id:id, behavioral_experiment_id:id, workspace_id:id, user_id:id, revision_no:int, source_weekly_report_revision_id:id, source_weekly_cohort_id:id, target_weekly_cohort_id:id, taxonomy_version:ver<TP-LAB:BEHAVIORAL_EXPERIMENT_TAXONOMY>, behavior_id:id, measurement_metric_id:id, other_behavior_text:str?, other_success_check_text:str?, state:enum{PROPOSED\|CONFIRMED\|SUPERSEDED\|CANCELLED}, recorded_at:ts, confirmed_at:ts?, actor_user_id:id, idempotency_key:str, supersedes_experiment_revision_id:id?, content_sha256:hash` | `behavioral_experiment_revision_id` | `behavioral_experiment_id,revision_no,behavioral_experiment_revision_id` | `experiment/report/cohorts/supersedes -> W records; taxonomy/item -> P records` |
| `BehavioralExperimentStateEvent` / `tp_exp_behavioral_experiment_state_event_v1` / `TP-LAB` | `behavioral_experiment_state_event_id:id, workspace_id:id, behavioral_experiment_id:id, behavioral_experiment_revision_id:id, event_sequence:int, event_type:enum{PROPOSE\|CONFIRM\|SUPERSEDE\|CANCEL}, caused_by_experiment_revision_id:id?, recorded_at:ts, actor_user_id:id, idempotency_key:str` | `behavioral_experiment_state_event_id` | `behavioral_experiment_id,event_sequence,behavioral_experiment_state_event_id` | `(workspace_id,behavioral_experiment_id) -> W:BehavioralExperiment; (workspace_id,revision/caused-by) -> W:BehavioralExperimentRevision; sequence positive/contiguous per experiment` |
| `WeeklyReviewCompletion` / `tp_exp_weekly_review_completion_v1` / `TP-ACC` | `weekly_review_completion_id:id, workspace_id:id, user_id:id, weekly_cohort_id:id, cohort_type:enum{REGULAR\|TRANSITION}, weekly_report_revision_id:id, behavioral_experiment_revision_id:id, cohort_start_local:str, cohort_end_local:str, workspace_timezone:str, tzdb_version:str, cohort_start_at_utc:ts, cohort_end_at_utc:ts, completed_at:ts, actor_user_id:id, idempotency_key:str, recorded_at:ts` | `weekly_review_completion_id` | `weekly_cohort_id,recorded_at,weekly_review_completion_id` | `cohort/report/experiment -> corresponding W records` |
| `ProductMeasurementRun` / `tp_exp_product_measurement_run_v1` / `TP-LAB` | `measurement_run_id:id, workspace_id:id, actor_user_id:id, run_schema_version:=product_measurement_run_v1, study_id:id, feature:enum{ONBOARDING\|QUICK_PLAN\|QUICK_REVIEW\|FIRST_INSIGHT}, run_mode:enum{PRACTICE\|MEASURED}, practice_index:int?, started_at:ts, deadline_at:ts, start_idempotency_key:str` | `measurement_run_id` | `started_at,measurement_run_id` | `workspace/actor -> W records; deadline_at = started_at + 30 minutes; PRACTICE requires practice_index in 1..3 and MEASURED requires null; study/feature/run-mode/index must match every linked ProductAnalyticsEvent` |
| `ProductMeasurementRunStateEvent` / `tp_exp_product_measurement_run_state_event_v1` / `TP-LAB` | `measurement_run_state_event_id:id, workspace_id:id, measurement_run_id:id, run_schema_version:=product_measurement_run_v1, event_sequence:int, event_type:enum{START\|SUCCEED\|ABANDON}, terminal_product_analytics_event_id:id?, abandonment_reason_code:enum{USER_CANCELLED\|NEGATIVE_DURATION\|ZERO_DURATION\|BACKGROUND_INTERRUPTED\|MISSING_TERMINAL_EVENT\|DURATION_OVER_30_MINUTES\|TIMEOUT}?, actor_type:enum{USER\|SYSTEM}, actor_user_id:id?, recorded_at:ts, idempotency_key:str` | `measurement_run_state_event_id` | `measurement_run_id,event_sequence,measurement_run_state_event_id` | `measurement_run_id -> W:ProductMeasurementRun; terminal_product_analytics_event_id -> W:ProductAnalyticsEvent iff sequence 2; exact two-event maximum, actor/reason/idempotency/time matrix and mutual terminal-event closure below` |
| `ProductAnalyticsEvent` / `tp_exp_product_analytics_event_v1` / `TP-LAB` | `product_analytics_event_id:id, workspace_id:id, actor_user_id:id?, event_schema_version:ver<TP-LAB:PRODUCT_ANALYTICS_EVENT_SCHEMA>, event_type:enum{onboarding_completed\|plan_armed\|plan_proof_resolved\|import_previewed\|import_completed\|episode_closed\|review_completed\|file_selected\|insight_rendered\|measurement_abandoned\|weekly_lab_opened\|weekly_review_completed\|export_completed\|account_deletion_requested}, source_record_type:enum{Workspace\|TradePlanRevision\|TradeEpisodeProjection\|Upload\|ImportBatch\|ReviewRevision\|MetricSnapshot\|ContextSnapshot\|WeeklyReportRevision\|WeeklyReviewCompletion}?, source_record_key_json:json?, measurement_run_id:id?, study_id:id?, run_mode:enum{PRACTICE\|MEASURED}?, practice_index:int?, payload_json:json, occurred_at:ts, idempotency_key:str, created_at:ts` | `product_analytics_event_id` | `occurred_at,product_analytics_event_id` | exact TP-LAB producer/source/measurement matrix; non-null type/key -> exact matching same-workspace envelope recordKey; measurement tuple is all-null or measurement_run_id -> same-workspace W:ProductMeasurementRun with copied study/run-mode/practice values; only the run's terminal ProductAnalyticsEvent is mutually referenced by sequence 2, while nonterminal journey events are not; `export_completed` has null source and measurement tuple, and no ExportJob key; exact per-event payload allowlist |
| `WorkspaceProductMetricSnapshot` / `tp_exp_workspace_product_metric_snapshot_v1` / `TP-LAB` | `workspace_product_metric_snapshot_id:id, workspace_id:id, schema_version:ver<TP-LAB:WORKSPACE_PRODUCT_METRIC_SCHEMA>, metric_dictionary_version:ver<TP-LAB:PRODUCT_METRICS>, metric_id:id, revision_no:int, status:enum{PROVISIONAL\|FINAL}, window_start_at:ts, window_end_at_exclusive:ts, evaluation_as_of_at:ts, dimension_json:json, dimension_sha256:hash, value_type:enum{DECIMAL\|INTEGER\|DURATION_MS\|OBJECT}, value_decimal:dec?, value_integer:int?, value_duration_ms:int?, value_object_json:json?, numerator_integer:int?, denominator_integer:int?, null_reason:str?, included_source_refs_json:json, excluded_source_refs_json:json, exclusion_reason_counts_json:json, input_event_digest_sha256:hash, supersedes_snapshot_id:id?, created_at:ts` | `workspace_product_metric_snapshot_id,metric_id,window_start_at,window_end_at_exclusive,dimension_sha256,revision_no` | `metric_id,window_start_at,window_end_at_exclusive,dimension_sha256,revision_no,workspace_product_metric_snapshot_id` | closed OVERALL/STUDY metric/source matrix and dimension hash below; included refs are exact `{sourceRecordKey,sourceType}`; excluded refs add `reasonCode`; keys equal same-workspace envelope `recordKey`; exact input digest below; `supersedes_snapshot_id -> W:WorkspaceProductMetricSnapshot?` |

`WeeklyCohortInputRevision` nested JSON is closed, not an opaque `json` extension point. `episode_projection_refs_json` has one unique object per selected CLOSED projection, sorted by `(closedAt, episode_id, projection_version)`, with exactly `accountingQuality`, `closedAt`, `ledgerAlgorithmVersion`, `planProofRuleVersion`, `projectionAlgorithmVersion` and `recordKey:{episode_id,projection_version}`. Every copied scalar equals the same-workspace projection active at `reporting_as_of_at`; a later version, an OPEN projection or a scalar-only key fails `EXPORT_SCHEMA_VIOLATION`.

`review_revision_refs_json` has exactly one object per episode entry in the same order and exactly these members: `projectionRecordKey`, nullable `reviewRecordKey:{review_id}`, nullable `reviewRevisionRecordKey:{review_revision_id}`, nullable `revisionNo`, `selectionStatus` and nullable `staleReviewRevisionRecordKey:{review_revision_id}`. `selectionStatus` is `COMPLETED | MISSING | RECONFIRM_REQUIRED`. COMPLETED requires both selected keys and revision number, null stale key and an exact same-projection completed ReviewRevision. MISSING requires all four nullable members null. RECONFIRM_REQUIRED requires only the stale key, which resolves to the unique greatest visible completed prior-projection revision by `(recorded_at,revision_no)`. The selected or stale key closes to the same workspace/episode; no completed current-projection Review may be represented as MISSING.

`context_ref_matrix_json` has exactly one object per episode entry in the same order: `{ "projectionRecordKey": key, "slots": [...] }`. Each `slots` array has exactly four objects in literal order `(ENTRY,5m)`, `(ENTRY,1m)`, `(EXIT,5m)`, `(EXIT,1m)` and each object has exactly `availabilityDecisionRecordKey`, `contextSnapshotRecordKey`, `phase`, `quality`, `reasonCode`, `status`, `timeframe`. Coupling is exact: AVAILABLE has a non-null `{ "id": id }`, null decision key, quality COMPLETE/PARTIAL/UNRELIABLE and reason respectively null/QUALITY_PARTIAL/QUALITY_UNRELIABLE; PENDING has null snapshot/quality, non-null `{ "context_availability_decision_id": id }` and JOB_PENDING; MISSING has null snapshot/quality, non-null decision and SOURCE_FAILED/SOURCE_UNAVAILABLE/EXACT_SNAPSHOT_NOT_FOUND/VERSION_MISMATCH; NOT_APPLICABLE has null snapshot/quality, non-null decision and PROJECTION_NOT_CONTEXT_READY. Every AVAILABLE snapshot resolves in this workspace and matches the projection, phase, timeframe, algorithm/parameter tuple and exact phase event identity. Only COMPLETE is aggregation eligible.

There is exactly one `ContextAvailabilityDecision` for each non-AVAILABLE slot and none for AVAILABLE. Its workspace/input/projection, phase, timeframe, `context_visibility_cutoff_at`, status and reason equal that slot; `observed_at = WeeklyCohortInputRevision.revision_locked_at`. The cutoff is `reporting_as_of_at` for INITIAL_LOCK, REVIEW_CORRECTION, ACCOUNTING_CORRECTION and DATA_BACKFILL, and the new `revision_locked_at` for CONTEXT_RECOVERY. NOT_APPLICABLE is allowed only for the durable accounting-not-ready predicate; MISSING only for a terminal source outcome or no exact/version-compatible snapshot at cutoff; otherwise the branch is PENDING. Internal queue/job IDs are forbidden. `content_sha256` is SHA-256 of RFC 8785 bytes of exactly `{ "contextVisibilityCutoffAt": ts, "episodeProjectionRecordKey": { "episode_id": id, "projection_version": int }, "phase": phase, "reasonCode": reason, "status": status, "timeframe": timeframe, "weeklyCohortInputRevisionRecordKey": { "weekly_cohort_input_revision_id": id }, "workspaceId": id }`; decision ID, observed time and hash are outside the basis.

For an AVAILABLE input slot, the exporter uses that same context visibility cutoff and exact expected `(workspace,episode,projection,phase,timeframe,algorithmVersion,parameterSetId)` scope, validates its complete ContextSnapshot revision chain, and selects the greatest contiguous `snapshotRevisionNo` with `computedAt <= context_visibility_cutoff`. CONTEXT_RECOVERY may therefore select a later context revision while its business `reporting_as_of_at`, episode array and Review array stay unchanged. Choosing a current/later-at-cutoff snapshot, an ID tie-break, a different dependency tuple, a revision gap or a second leaf fails `EXPORT_POINTER_MISMATCH`.

`referenced_taxonomy_versions_json` is the sorted unique exact set consumed by selected COMPLETED Reviews plus exactly one behavioral v1 entry. A Review entry is exactly `{ "recordKey": { "taxonomy_version": version }, "recordType": "REVIEW_TAXONOMY_VERSION", "taxonomyType": type }`, where type is EXIT_REASON/BREACH_TYPE/EMOTION. The behavioral entry is exactly `{ "recordKey": { "taxonomy_version": "behavioral_experiment_v1" }, "recordType": "BEHAVIORAL_EXPERIMENT_TAXONOMY_VERSION", "taxonomyType": "BEHAVIORAL_EXPERIMENT" }`. Sort by `(recordType,taxonomyType,taxonomy_version)` in Unicode code-point order; each key closes to the matching public version and complete item set; unused extras and current-version substitution fail.

Input-revision time coupling is exact. `cohort_locked_at` always copies the cohort LOCK event. INITIAL_LOCK requires `reporting_as_of_at = cohort_locked_at = revision_locked_at = created_at`. CONTEXT_RECOVERY requires a later `revision_locked_at = created_at`, preserves its superseded revision's `reporting_as_of_at`, episode array and Review array byte-for-byte, and changes only the context matrix and resulting digest. REVIEW_CORRECTION, ACCOUNTING_CORRECTION and DATA_BACKFILL require `reporting_as_of_at = revision_locked_at = created_at > superseded.reporting_as_of_at` and rerun all selectors while preserving `cohort_locked_at`. Revisions start at 1 and are contiguous; revision 1 has null supersedes ID. `input_digest_sha256` is SHA-256 of RFC 8785 bytes of exactly:

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

For `MetricSnapshot`, `dimension_json` is exactly one of `{ "dimensionType": "OVERALL" }`, `{ "dimensionType": "SETUP", "setupId": id-or-"UNKNOWN" }`, `{ "dimensionType": "RULE_BREACH", "ruleBreach": bool }`, `{ "breachTaxonomyVersion": version, "breachTypeId": id, "dimensionType": "BREACH_TYPE" }`, `{ "dimensionType": "CONTEXT_REGIME", "regimeCode": code }` or `{ "dimensionType": "CONTEXT_COVERAGE" }`. Only CONTEXT_REGIME/CONTEXT_COVERAGE require non-null phase/timeframe; all other dimensions require both null. BREACH_TYPE is legal only for integer `breach_episode_count`, and its exact version/item closes to an included public ReviewTaxonomyVersion/Item. Candidate and included arrays contain only exact `{ "episode_id": id, "projection_version": int }` keys. Excluded entries contain exactly `{ "episodeRecordKey": key, "primaryReason": reason, "reasonCodes": [reason...] }`; reasons are nonempty, unique and Unicode-code-point sorted, and `primaryReason = reasonCodes[0]`. Candidate keys are the exact dimension-filtered cohort-input episode array and preserve its order; included and excluded preserve that order, are disjoint and partition each candidate exactly once.

The closed exclusion-reason enum is `ACCOUNTING_INCOMPLETE | CONTEXT_MISSING | CONTEXT_NOT_APPLICABLE | CONTEXT_PARTIAL | CONTEXT_PENDING | CONTEXT_UNKNOWN | CONTEXT_UNRELIABLE | CONTEXT_VERSION_MISMATCH | ELIGIBILITY_VERSION_UNRESOLVED | FEE_CONVERSION_MISSING | LEDGER_INVARIANT_FAILED | PLAN_PROOF_NOT_VERIFIED | PLANNED_RISK_UNAVAILABLE | REVIEW_MISSING | REVIEW_RECONFIRM_REQUIRED | USER_EXCLUDED`. `exclusion_reason_counts_json` is sorted by `reasonCode`, has exactly `{ "count": positive-int, "reasonCode": reason }`, one row per distinct primary reason and no zero row. Counts obey candidate = array length = eligible + excluded; eligible/excluded equal their array lengths; reason counts sum to excluded.

`source_review_revision_ids_json` is a sorted unique array of exact `{ "review_revision_id": id }`; `source_context_snapshot_ids_json` is a sorted unique array of exact `{ "id": id }`. Sort both by unsigned RFC 8785 key bytes. Each is exactly the set actually read: Review keys occur as COMPLETED in the input; Context keys occur in AVAILABLE slots, and a context performance metric consumes only COMPLETE slots matching its phase/timeframe/dependency tuple. Empty/non-applicable source families use `[]`; scalar IDs, unused extras, missing keys and cross-workspace keys fail.

The following MetricSnapshot contract matrix is exhaustive. Each row fixes `metric_formula_version`, legal dimension and exact `eligibility_policy_id`, value type/unit and mathematical-null reason; no unlisted pair is valid.

| metric_id | metric_formula_version | Dimension -> eligibility_policy_id | Type / unit | Mathematical null |
|---|---|---|---|---|
| `accounting_completeness_rate` | `accounting_completeness_rate_v1` | OVERALL -> `closed_base_eligible_v1` | DECIMAL / `RATIO` | `NO_ELIGIBLE_EPISODE` |
| `planned_trade_rate` | `planned_trade_rate_v1` | OVERALL -> `net_eligible_v1` | DECIMAL / `RATIO` | `NO_ELIGIBLE_EPISODE` |
| `review_coverage_rate` | `review_coverage_rate_v1` | OVERALL -> `net_eligible_v1` | DECIMAL / `RATIO` | `NO_ELIGIBLE_EPISODE` |
| `mean_expectancy_r` | `mean_expectancy_r_v1` | OVERALL/SETUP -> `r_eligible_v1`; RULE_BREACH -> `planned_reviewed_r_eligible_v1`; CONTEXT_REGIME -> `context_regime_r_eligible_v1` | DECIMAL / `R` | `NO_ELIGIBLE_EPISODE` |
| `median_expectancy_r` | `median_expectancy_r_v1` | OVERALL/SETUP -> `r_eligible_v1`; RULE_BREACH -> `planned_reviewed_r_eligible_v1`; CONTEXT_REGIME -> `context_regime_r_eligible_v1` | DECIMAL / `R` | `NO_ELIGIBLE_EPISODE` |
| `mean_r_ci_95` | `mean_r_ci_95_v1` | SETUP -> `r_eligible_v1` | INTERVAL / `R` | `INSUFFICIENT_SAMPLE` for N < 2 |
| `win_rate` | `win_rate_v1` | SETUP -> `net_eligible_v1`; RULE_BREACH -> `planned_reviewed_net_eligible_v1`; CONTEXT_REGIME -> `context_regime_net_eligible_v1` | DECIMAL / `RATIO` | `NO_ELIGIBLE_EPISODE` |
| `plan_adherence_rate` | `plan_adherence_rate_v1` | OVERALL -> `planned_reviewed_net_eligible_v1` | DECIMAL / `RATIO` | `NO_ELIGIBLE_EPISODE` |
| `rule_breach_rate` | `rule_breach_rate_v1` | OVERALL -> `planned_reviewed_net_eligible_v1` | DECIMAL / `RATIO` | `NO_ELIGIBLE_EPISODE` |
| `stop_moved_away_rate` | `stop_moved_away_rate_v1` | OVERALL -> `planned_reviewed_net_eligible_v1` | DECIMAL / `RATIO` | `NO_ELIGIBLE_EPISODE` |
| `risk_exceeded_rate` | `risk_exceeded_rate_v1` | OVERALL -> `planned_reviewed_net_eligible_v1` | DECIMAL / `RATIO` | `NO_ELIGIBLE_EPISODE` |
| `fee_drag_pct_of_gross_profit` | `fee_drag_pct_of_gross_profit_v1` | OVERALL -> `net_eligible_v1` | DECIMAL / `PERCENT` | `NO_GROSS_PROFIT` |
| `fee_pct_of_gross_turnover` | `fee_pct_of_gross_turnover_v1` | OVERALL -> `net_eligible_v1` | DECIMAL / `PERCENT` | `NO_GROSS_TURNOVER` |
| `breach_episode_count` | `breach_episode_count_v1` | BREACH_TYPE -> `planned_reviewed_net_eligible_v1` | INTEGER / `EPISODE_COUNT` | none; zero rows are absent |
| `context_coverage_counts` | `context_coverage_counts_v1` | CONTEXT_COVERAGE -> `context_coverage_all_candidates_v1` | OBJECT / `EPISODE_COUNT` | none |
| `required_checklist_completion_rate` | `required_checklist_completion_rate_v1` | OVERALL -> `planned_reviewed_net_eligible_v1` | DECIMAL / `RATIO` | `NO_ELIGIBLE_EPISODE` |
| `stop_kept_rate` | `stop_kept_rate_v1` | OVERALL -> `planned_reviewed_net_eligible_v1` | DECIMAL / `RATIO` | `NO_ELIGIBLE_EPISODE` |
| `risk_within_plan_rate` | `risk_within_plan_rate_v1` | OVERALL -> `planned_reviewed_net_eligible_v1` | DECIMAL / `RATIO` | `NO_ELIGIBLE_EPISODE` |
| `episode_review_within_24h_rate` | `episode_review_within_24h_rate_v1` | OVERALL -> `net_eligible_v1` | DECIMAL / `RATIO` | `NO_ELIGIBLE_EPISODE` |

The policy predicates are also closed. `closed_base_eligible_v1` applies the active CLOSED/as-of/cohort rule and latest exact-projection eligibility, adding only USER_EXCLUDED or ELIGIBILITY_VERSION_UNRESOLVED. `net_eligible_v1` adds COMPLETE accounting, non-null net and ledger invariants, with ACCOUNTING_INCOMPLETE, FEE_CONVERSION_MISSING or LEDGER_INVARIANT_FAILED. `r_eligible_v1` adds VERIFIED frozen plan and positive available planned risk, with PLAN_PROOF_NOT_VERIFIED or PLANNED_RISK_UNAVAILABLE. `planned_reviewed_net_eligible_v1` adds VERIFIED plan and an exact-projection COMPLETED Review, with PLAN_PROOF_NOT_VERIFIED, REVIEW_MISSING or REVIEW_RECONFIRM_REQUIRED; `planned_reviewed_r_eligible_v1` additionally requires positive available risk. `context_regime_net_eligible_v1` adds exact AVAILABLE/COMPLETE aggregation-eligible ContextSnapshot matching phase/timeframe/version/regime and may add only the closed CONTEXT_* reasons; `context_regime_r_eligible_v1` additionally requires the verified plan/risk predicates. `context_coverage_all_candidates_v1` includes every underlying-family candidate exactly once and has no exclusion. Dimension filtering happens before these policies; out-of-dimension episodes are not fabricated as exclusions.

Every `metrics_v1` DECIMAL or INTERVAL calculation uses `metrics_decimal_v1`. Ledger/quote inputs are exact persisted scale-18 decimals, counts are exact integers, subsequent arithmetic is rational and binary floating point is forbidden. The one intentional per-episode division boundary is:

```text
r_multiple18 = round_scale18_half_even(
    episode_net_pnl_quote / planned_initial_risk_quote)
```

It exists only for VERIFIED proof with positive exact risk. It is the exact `r_multiple` exposed by drill-down/counterexample payloads and the only input to mean/median/CI; no consumer recomputes an unrounded quotient. Every other division rounds once, only at the final MetricSnapshot value boundary, to scale 18 ROUND_HALF_EVEN; trailing zeros strip and negative zero becomes `0`. Before that final round, definitions are exact:

```text
mean_expectancy_r   = sum(r_multiple18) / N
median_expectancy_r = middle r_multiple18, or
                       (middle_left + middle_right) / 2 for even N
count_rate          = numerator_count / denominator_count
payoff_ratio        = (sum_win_quote * loss_count) /
                      (abs(sum_loss_quote) * win_count)
profit_factor       = sum_win_quote / abs(sum_loss_quote)
fee_drag_pct        = (sum_fee_quote * 100) / sum_positive_gross_quote
fee_turnover_pct    = (sum_fee_quote * 100) / sum_gross_turnover_quote
```

Complement behavior rates use exact counts `(denominator_count - source_numerator_count) / denominator_count`, never `1 - rounded_rate`. Sums accumulate exact scale-18 operands; mean/median never combine rounded aggregate means. Win/loss/breakeven uses exact episode net PnL before division. Count rates lie in `[0,1]` with unit RATIO; fee formulas already multiply by 100 and use PERCENT.

Numerator/denominator persistence is exact: count rates copy integer counts as canonical decimal strings; payoff copies `sum_win_quote * loss_count` and `abs(sum_loss_quote) * win_count`; profit factor copies positive/negative absolute sums; fee percentages copy `sum_fee_quote * 100` and the exact formula denominator. Mean, median, CI, INTEGER and OBJECT metrics require both fields null. The final value must recompute from a non-null pair. Final DECIMAL values and each INTERVAL bound fit signed DECIMAL(38,18), with at most 20 integer digits after rounding; intermediate arithmetic is unbounded. Final overflow fails `METRIC_DECIMAL_OVERFLOW` and publishes no snapshot/report. A null/zero-denominator branch skips division and retains its exact reason/display state.

Metric value/null invariants, formula-specific `null_reason`, numerator/denominator rules and evidence thresholds are exactly `metric_snapshot_v1` in TP-LAB. A value requires `computation_status = COMPLETE`, `display_state = NORMAL`, exactly one non-null typed field matching `value_type` and null reason. A mathematically null result requires COMPLETE, UNDEFINED, all five typed fields null and the matrix's authoritative reason; it is never relabeled as upstream unavailable. POSITIVE_INFINITY is legal only for TP-ACC profit_factor with positive gross profit/no loss, remains COMPLETE with all value fields null and `NO_LOSSES`, and is forbidden for every v1 report metric in the matrix. Required upstream non-core unavailability requires `computation_status = UNAVAILABLE`, `display_state = UNAVAILABLE`, every typed field null and its stable source reason. INTERVAL uses only `value_interval_json` with exactly `{ "lowerDecimal": dec, "upperDecimal": dec }`, `lowerDecimal <= upperDecimal`, and `unit = R` for `mean_r_ci_95`; OBJECT uses only `value_object_json`; each scalar type uses only its matching scalar field. Zero is never substituted for null and numeric infinity is never serialized. Every snapshot derives evidence from its own eligible count with no exception: INSUFFICIENT for N < 2, EXPLORATORY for 2 <= N < 30 and ESTIMATED for N >= 30.

The CONTEXT_COVERAGE snapshot is further closed: `metric_id = context_coverage_counts`, `metric_formula_version = context_coverage_counts_v1`, `eligibility_policy_id = context_coverage_all_candidates_v1`, matching CONTEXT_COVERAGE dimension/phase/timeframe, OBJECT/EPISODE_COUNT, COMPLETE/NORMAL, null numerator/denominator/reason, identical candidate and included arrays, and empty excluded/reason-count arrays. Its `value_object_json` has exactly nonnegative safe-integer members `completeCount`, `missingCount`, `notApplicableCount`, `partialCount`, `pendingCount`, `totalCount`, `unknownCount`, `unreliableCount`, `versionMismatchCount`. Each candidate maps exactly once: AVAILABLE COMPLETE known regime -> complete; AVAILABLE COMPLETE missing/UNKNOWN regime -> unknown; AVAILABLE PARTIAL -> partial; AVAILABLE UNRELIABLE -> unreliable; PENDING -> pending; NOT_APPLICABLE -> notApplicable; MISSING VERSION_MISMATCH -> versionMismatch; every other MISSING -> missing. The eight category counts sum to `totalCount = candidate_episode_count`; panel state and coverage exclusions derive only from these counts.

For `metric_id = mean_r_ci_95`, `metric_formula_version` is exactly `mean_r_ci_95_v1`, `value_type = INTERVAL` and `unit = R`. N is `eligible_episode_count`. N < 2 requires all five typed values null and `null_reason = INSUFFICIENT_SAMPLE`; no square root/critical lookup runs. N >= 2 requires COMPLETE with null reason and exact `{ "lowerDecimal": lower, "upperDecimal": upper }` from TP-ACC's rational sample-variance profile: no binary floating point, population variance, early rounding, alternate critical table or clamping is permitted. `sqrt36_half_even`, the frozen `critical95(df)` table (`df >= 30` uses exact `1.959963984540054`) and final scale-18 half-even/canonical stripping are the authoritative `mean_r_ci_95_v1` basis.

The reader recomputes `MetricSnapshot.input_digest_sha256` as SHA-256 of RFC 8785 bytes of exactly the object below. Every nullable member is present; the copied arrays and values obey all preceding rules. Only `metric_snapshot_id`, `computed_at`, `supersedes_metric_snapshot_id` and the digest field itself are outside this basis.

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

`WeeklyReportRevision.section_payload_json` is the only canonical report data model and is closed by `weekly_lab_v1`. It contains exactly root members `cohortId`, `cohortInputRevisionId`, `reportingAsOfAt`, `schemaVersion` and `sections`; the first four equal their owning revision/cohort input. `sections` has exactly seven entries in `order` 1..7 and literal `sectionId` order OVERVIEW, SETUP, ADHERENCE, COST, CONTEXT, COUNTEREXAMPLES, EXPERIMENT. Each section has only the source-owned members for that section. A metric cell is exactly `{ "cellId": id, "metricSnapshotId": id }`; no display value is duplicated.

The exact cell sequences are: OVERVIEW `accounting_completeness_rate, planned_trade_rate, review_coverage_rate, mean_expectancy_r, median_expectancy_r`; each SETUP row `mean_expectancy_r, median_expectancy_r, mean_r_ci_95, win_rate`; ADHERENCE overall `review_coverage_rate, plan_adherence_rate, rule_breach_rate, stop_moved_away_rate, risk_exceeded_rate`; each ADHERENCE outcome row `mean_expectancy_r, median_expectancy_r, win_rate`; COST `fee_drag_pct_of_gross_profit, fee_pct_of_gross_turnover, accounting_completeness_rate`; each CONTEXT regime row `mean_expectancy_r, median_expectancy_r, win_rate`. Every cell resolves a same-input MetricSnapshot with the exact metric, dimension and phase/timeframe for its location.

Section cardinality/state is exact:

- OVERVIEW has five cells and `cohortSummary` with exactly candidate count, cohort local/UTC bounds/type and sorted nonzero `{ "count", "reasonCode" }` exclusions; AVAILABLE iff candidates exist, else EMPTY.
- SETUP is EMPTY with `rows=[]` iff no candidate; otherwise AVAILABLE with one row per observed stable setup dimension including UNKNOWN. Each row has exact `dimensionKey`, `displayLabel`, contiguous `rowOrder`, four cells and `labelSnapshots` of exact `{ "label", "setupRevisionId" }` objects in TP-LAB order.
- ADHERENCE is AVAILABLE iff candidates exist, else EMPTY; it always has five overall cells and exactly two breach outcome rows in `false,true` order with contiguous rowOrder and three cells. Breach-type rows contain exactly positive-count types sorted by `(breachTaxonomyVersion,breachTypeId)`, and each count snapshot is INTEGER with the matching dimension and exact count.
- COST has all three cells and is AVAILABLE iff candidates exist, else EMPTY; null remains in the snapshot.
- CONTEXT has exactly four panels in literal ENTRY/5m, ENTRY/1m, EXIT/5m, EXIT/1m order; only the first has `isPrimary=true`. Every panel has one non-null CONTEXT_COVERAGE snapshot plus exactly four regime rows in TREND_HIGH_VOL, TREND_LOW_VOL, RANGE_HIGH_VOL, RANGE_LOW_VOL order, each with three cells. Its section state is EMPTY iff all panels EMPTY; UNAVAILABLE iff candidates exist and no panel has eligible context; PARTIAL_COVERAGE iff any panel is PARTIAL_COVERAGE or available and unavailable panels coexist; otherwise AVAILABLE. `context_section_status` equals it.
- COUNTEREXAMPLES AVAILABLE has 1..3 exact items and null reason; EMPTY has no items and `NO_OPPOSITE_SIGN_EXCEPTION`. Items have exactly `candidateRank`, `episodeRef:{episodeId,projectionVersion}`, `evidenceLabel`, `observationKey`, `rMultiple`, `sourceMetricSnapshotId`; ranks are contiguous, episode keys unique and every key closes to the source snapshot's included set.
- EXPERIMENT for REGULAR is ACTION_REQUIRED with the next REGULAR cohort target, `behavioral_experiment_v1` and all seven public taxonomy options in `optionOrder`; every option has exactly `baselineMetricSnapshotId`, `behaviorId`, `label`, `measurementMetricId`, `optionOrder`. The baseline ID is non-null for options 1..5 and resolves the same-input metric named by `measurementMetricId`; it is null for option 6 because success is observed only by the later WeeklyReviewCompletion, and null for option 7 OTHER because `USER_SELF_CHECK` is the exact non-metric sentinel. TRANSITION is NOT_APPLICABLE with null target, the same taxonomy version and `options=[]`.

`metric_snapshot_ids_json` is the lowercase-canonical-ID-byte sorted unique union of every cell, breach-count, context-coverage, counterexample-source and non-null experiment-baseline snapshot ID. A missing/extra cell, wrong state/cardinality/order, dangling snapshot, summary mismatch or union mismatch is `EXPORT_SCHEMA_VIOLATION`. The reader recomputes `content_sha256` over TP-LAB's exact RFC 8785 object containing every persisted immutable revision field, including `section_payload_json`, and excluding only derived `status`, reverse superseded link, events, `published_at` and the hash field. `rendered_report_json` and all cached/request-time `weekly_lab_renderer_v1` bytes are forbidden archive data; `renderer_id` remains the pinned renderer contract value in the canonical content hash.

#### AI, consent, pointer and tombstone projections

| RecordType / recordSchemaId / source | Exact payload fields | Key | Sort | FK/reference rules |
|---|---|---|---|---|
| `ConsentRecord` / `tp_exp_consent_record_v1` / `TP-SEC` | `consent_record_id:id, workspace_id:id, actor_user_id:id, consent_contract_version:ver<TP-SEC:AI_CONSENT_CONTRACT>, feature:enum{TRANSCRIPTION\|TAXONOMY_SUGGESTION\|WEEKLY_SUMMARY}, event_sequence:int, decision:enum{GRANT\|REVOKE}, disclosure_version:str, policy_version:str, disclosure_sha256:hash, recorded_at:ts, idempotency_key:str` | `consent_record_id` | `feature,event_sequence,consent_record_id` | `workspace_id -> W:Workspace; sequence positive/contiguous per workspace+feature` |
| `AiRun` / `tp_exp_ai_run_v1` / `TP-SEC` | `ai_run_id:id, workspace_id:id, ai_artifact_contract_version:ver<TP-SEC:AI_ARTIFACT_CONTRACT>, consent_record_id:id, feature:enum{TRANSCRIPTION\|TAXONOMY_SUGGESTION\|WEEKLY_SUMMARY}, status:enum{QUEUED\|RUNNING\|SUCCEEDED\|FAILED\|REJECTED\|CANCELLED_CONSENT_REVOKED}, configuration_release_version:str, configuration_release_sha256:hash, configuration_activation_sequence:int, eval_corpus_version:str, eval_result_sha256:hash, model_provider:str, model_version_identifier:str, processor_registration_id:id, provider_configuration_version:str, provider_configuration_sha256:hash, prompt_template_version:str, prompt_template_sha256:hash, policy_version:str, policy_sha256:hash, input_schema_version:str, input_schema_sha256:hash, output_schema_version:enum{transcript_draft_v1\|taxonomy_suggestion_v1\|weekly_summary_v1}, output_schema_sha256:hash, output_validator_version:str, output_validator_sha256:hash, output_renderer_version:str, output_renderer_sha256:hash, canonical_input_sha256:hash, request_options_json:json, weekly_report_revision_id:id?, metric_snapshot_ids_json:json, recorded_at:ts, completed_at:ts?, validation_result:enum{NOT_EVALUATED\|PASSED\|REJECTED_SCHEMA\|REJECTED_GROUNDING\|REJECTED_POLICY}, fallback_reason:enum{INVALID_SCHEMA\|GROUNDING_FAILED\|POLICY_FAILED\|PROCESSOR_TIMEOUT\|PROCESSOR_ERROR\|RETRY_EXHAUSTED\|CONSENT_REVOKED}?, ai_output_id:id?, idempotency_key:str` | `ai_run_id` | `recorded_at,ai_run_id` | `consent_record_id -> same-feature W:ConsentRecord GRANT; complete input set -> W:AiRunInputReference[] whose typed keys close to W/P records; copied configuration/eval/processor-registration binding is immutable audit data and processor_registration_id is a typed excluded-control reference, not an archive FK; WEEKLY_SUMMARY report/metric IDs -> exact W records, other features null/empty; SUCCEEDED output -> W:AiOutput; state/null/fallback/options/schema mapping is exact ai_artifact_v1; deleted bundle has no AiRun` |
| `AiRunInputReference` / `tp_exp_ai_run_input_reference_v1` / `TP-SEC` | `ai_run_id:id, workspace_id:id, ordinal:int, input_role:enum{VOICE_UPLOAD_PROVENANCE\|VOICE_RETAINED_ATTACHMENT\|TAXONOMY_SOURCE_TEXT\|TAXONOMY_VERSION_ALLOWLIST\|TAXONOMY_ITEM_ALLOWLIST\|WEEKLY_REPORT_PAYLOAD\|WEEKLY_METRIC_PAYLOAD\|WEEKLY_EPISODE_GROUNDING}, reference_type:enum{UPLOAD\|ATTACHMENT\|TRADE_PLAN_REVISION\|REVIEW_REVISION\|WEEKLY_REPORT_REVISION\|METRIC_SNAPSHOT\|TRADE_EPISODE_PROJECTION\|REVIEW_TAXONOMY_VERSION\|REVIEW_TAXONOMY_ITEM}, reference_record_key_json:json, reference_record_schema_id:enum{tp_exp_upload_v1\|tp_exp_attachment_v1\|tp_exp_trade_plan_revision_v1\|tp_exp_review_revision_v1\|tp_exp_weekly_report_revision_v1\|tp_exp_metric_snapshot_v1\|tp_exp_trade_episode_projection_v1\|tp_exp_review_taxonomy_version_v1\|tp_exp_review_taxonomy_item_v1}, reference_digest_schema_id:enum{upload_source_bytes_sha256_v1\|attachment_content_bytes_sha256_v1\|trade_plan_revision_content_sha256_v1\|review_revision_content_sha256_v1\|weekly_report_revision_content_sha256_v1\|metric_snapshot_input_digest_sha256_v1\|trade_episode_projection_payload_sha256_v1\|review_taxonomy_version_content_sha256_v1\|review_taxonomy_item_payload_sha256_v1}, reference_digest_sha256:hash, processor_payload_included:bool, payload_fragment_schema_id:enum{voice_upload_bytes_v1\|retained_voice_attachment_bytes_v1\|taxonomy_selected_text_utf8_v1\|taxonomy_version_allowlist_fragment_v1\|taxonomy_item_allowlist_fragment_v1\|weekly_report_summary_fragment_v1\|weekly_metric_summary_fragment_v1\|weekly_episode_grounding_fragment_v1}?, payload_fragment_sha256:hash?, field_selector:enum{TRADE_PLAN_THESIS\|REVIEW_LESSON}?, selection_start_scalar_index:int?, selection_end_scalar_index_exclusive:int?, selected_text_sha256:hash?` | `ai_run_id,ordinal` | `ai_run_id,ordinal` | `ai_run_id -> W:AiRun; typed exact key -> W:Upload/Attachment/TradePlanRevision/ReviewRevision/WeeklyReportRevision/MetricSnapshot/TradeEpisodeProjection or P:ReviewTaxonomyVersion/ReviewTaxonomyItem; exact role/schema/digest/fragment matrix, cardinality and null rules below; deleted bundle has no input reference` |
| `AiOutputSubject` / `tp_exp_ai_output_subject_v1` / `TP-SEC` | `ai_output_subject_id:id, workspace_id:id, output_kind:enum{TRANSCRIPT_DRAFT\|TAXONOMY_SUGGESTION\|WEEKLY_SUMMARY}, last_known_content_sha256:hash, created_at:ts` | `ai_output_subject_id` | `created_at,ai_output_subject_id` | `workspace_id -> W:Workspace; ID equals the created AiOutput ID; complete state history -> W:AiOutputSubjectStateEvent[]; ACTIVE closes to W:AiOutput and DELETED closes to matching T:AI_OUTPUT or T:TRANSCRIPT_DRAFT; exact lifecycle below` |
| `AiOutputSubjectStateEvent` / `tp_exp_ai_output_subject_state_event_v1` / `TP-SEC` | `ai_output_subject_state_event_id:id, workspace_id:id, ai_output_subject_id:id, event_sequence:int, event_type:enum{CREATE\|DELETE}, receipt_subject_type:enum{AI_OUTPUT\|TRANSCRIPT_DRAFT}?, receipt_subject_id:id?, recorded_at:ts, idempotency_key:str` | `ai_output_subject_state_event_id` | `ai_output_subject_id,event_sequence,ai_output_subject_state_event_id` | `ai_output_subject_id -> W:AiOutputSubject; sequence positive/contiguous; non-null receipt pair -> exact TP-SEC SubjectDeletionReceipt composite identity represented by matching Tombstone; exact lifecycle below` |
| `AiOutput` / `tp_exp_ai_output_v1` / `TP-SEC` | `ai_output_id:id, workspace_id:id, ai_artifact_contract_version:ver<TP-SEC:AI_ARTIFACT_CONTRACT>, ai_run_id:id, output_kind:enum{TRANSCRIPT_DRAFT\|TAXONOMY_SUGGESTION\|WEEKLY_SUMMARY}, content_media_type:enum{TEXT_PLAIN\|APPLICATION_JSON}, content_utf8:str, content_sha256:hash, output_schema_version:enum{transcript_draft_v1\|taxonomy_suggestion_v1\|weekly_summary_v1}, validation_result:=PASSED, created_at:ts` | `ai_output_id` | `created_at,ai_output_id` | `ai_run_id -> W:AiRun SUCCEEDED; ai_output_id -> W:AiOutputSubject ACTIVE; exact feature/kind/schema/media mapping; subject ID/kind/hash/time equal output fields; content hash over UTF-8; output schema value appears in its matching TP-SEC manifest slot; deleted bundle has no AiOutput` |
| `AiOutputReference` / `tp_exp_ai_output_reference_v1` / `TP-SEC` | `ai_output_id:id, workspace_id:id, reference_type:enum{WEEKLY_REPORT_REVISION\|METRIC_SNAPSHOT\|TRADE_EPISODE_PROJECTION\|REVIEW_TAXONOMY_VERSION\|REVIEW_TAXONOMY_ITEM}, reference_record_key_json:json, reference_role:enum{REPORT_SOURCE\|CLAIM_METRIC\|CLAIM_EPISODE\|TAXONOMY_VERSION\|TAXONOMY_ITEM}, output_item_ordinal:int?, role_ordinal:int, ordinal:int` | `ai_output_id,ordinal` | `ai_output_id,ordinal` | `ai_output_id -> W:AiOutput; typed exact key -> W:WeeklyReportRevision/MetricSnapshot/TradeEpisodeProjection or P:ReviewTaxonomyVersion/ReviewTaxonomyItem; key must match one AiRunInputReference; role/type/item/global ordinal mapping below; deletion removes entire bundle` |
| `TaxonomySuggestionConfirmation` / `tp_exp_taxonomy_suggestion_confirmation_v1` / `TP-SEC` | `taxonomy_suggestion_confirmation_id:id, workspace_id:id, ai_output_id:id, review_id:id, based_on_review_revision_id:id, taxonomy_type:enum{EXIT_REASON\|BREACH_TYPE\|EMOTION}, selected_suggestions_json:json, confirmed_taxonomy_item_ids_json:id[], confirmation_request_schema_version:=taxonomy_suggestion_confirmation_request_v1, confirmation_request_sha256:hash, full_replacement_review_payload_sha256:hash, result_review_revision_id:id, result_review_revision_content_sha256:hash, source_output_content_sha256:hash, actor_user_id:id, idempotency_key:str, recorded_at:ts, content_sha256:hash` | `taxonomy_suggestion_confirmation_id` | `recorded_at,taxonomy_suggestion_confirmation_id` | `ai_output_id -> W:AiOutputSubject TAXONOMY_SUGGESTION; active subject additionally closes to W:AiOutput, deleted subject to T:AI_OUTPUT; review/base/result -> same W:Review/ReviewRevision and result content hash must equal that revision; selected triples/items -> exact P:ReviewTaxonomyItem set frozen by source/result; request/full-replacement/result/source/content hashes use exact TP-SEC bases below` |
| `TranscriptConfirmation` / `tp_exp_transcript_confirmation_v1` / `TP-SEC` | `transcript_confirmation_id:id, workspace_id:id, ai_output_id:id, source_upload_id:id, target_record_type:enum{TRADE_PLAN\|REVIEW}, target_record_id:id, based_on_revision_record_key_json:json, target_field:enum{THESIS\|LESSON}, result_revision_record_key_json:json, source_output_content_sha256:hash, confirmed_text_sha256:hash, keep_original:bool, retained_attachment_id:id?, actor_user_id:id, idempotency_key:str, recorded_at:ts, content_sha256:hash` | `transcript_confirmation_id` | `recorded_at,transcript_confirmation_id` | `ai_output_id -> W:AiOutputSubject TRANSCRIPT_DRAFT; active subject additionally closes to W:AiOutput, deleted subject to T:TRANSCRIPT_DRAFT; source_upload_id -> W:Upload VOICE; target/base/result -> typed same-workspace plan or Review records; retained_attachment_id -> W:Attachment?; exact confirmation/hash rules below` |
| `AsOfPointer` / `tp_exp_as_of_pointer_v1` / `TP-EXP` | `pointer_type:enum{ACTIVE_INSTRUMENT_CATALOG\|ACTIVE_MARKET_CONVERSION_CATALOG\|CURRENT_WORKSPACE_OWNER_PROFILE\|CURRENT_SETUP_PRESET\|CURRENT_TRADE_PLAN\|ACTIVE_TRADE_EPISODE_PROJECTION\|CURRENT_EPISODE_METRIC_ELIGIBILITY\|ACTIVE_FEE_CONVERSION\|CURRENT_REVIEW_REVISION\|CURRENT_UPLOAD_STATE\|CURRENT_ATTACHMENT_STATE\|ACTIVE_CONTEXT_SNAPSHOT\|CURRENT_WEEKLY_COHORT_STATE\|CURRENT_TIMEZONE_CHANGE_SCHEDULE_STATE\|CURRENT_WEEKLY_COHORT_INPUT\|CURRENT_WEEKLY_REPORT_REVISION\|CURRENT_CONFIRMED_BEHAVIORAL_EXPERIMENT\|CURRENT_AI_CONSENT\|CURRENT_AI_OUTPUT_SUBJECT_STATE\|CURRENT_PRODUCT_MEASUREMENT_RUN_STATE\|CURRENT_WORKSPACE_PRODUCT_METRIC_SNAPSHOT}, workspace_id:id?, aggregate_key_json:json, aggregate_key_sha256:hash, target_record_type:enum{InstrumentCatalogPublishEvent\|MarketConversionCatalogPublishEvent\|WorkspaceOwnerProfileRevision\|SetupPresetRevision\|TradePlanRevision\|TradeEpisodeProjection\|EpisodeMetricEligibilityEvent\|FeeConversion\|ReviewRevision\|UploadStateEvent\|AttachmentStateEvent\|ContextSnapshot\|WeeklyCohortStateEvent\|TimezoneChangeScheduleStateEvent\|WeeklyCohortInputRevision\|WeeklyReportRevision\|BehavioralExperimentRevision\|ConsentRecord\|AiOutputSubjectStateEvent\|ProductMeasurementRunStateEvent\|WorkspaceProductMetricSnapshot}, target_record_key_json:json?, state_value:str?, basis_record_type:str, basis_record_key_json:json, derived_at_export_as_of_at:ts` | `workspace_id,pointer_type,aggregate_key_sha256` | `workspace_id,pointer_type,aggregate_key_sha256` | exact family, keys, nullability, states, basis and resolver from section 4.3; target/basis -> matching W/P included record; no tombstone target |
| `Tombstone` / `tp_exp_tombstone_v1` / `TP-EXP` | `tombstone_id:id, workspace_id:id, subject_type:enum{ATTACHMENT_BINARY\|AI_OUTPUT\|RAW_IMPORT_OBJECT\|RAW_VOICE_OBJECT\|TRANSCRIPT_DRAFT}, subject_id:id, purged_or_deleted_at:ts, reason_code:str, last_known_sha256:hash, source_retention_policy:enum{TP-SEC:ATTACHMENT_DELETE\|TP-SEC:AI_OUTPUT_DELETE\|TP-SEC:RAW_IMPORT_24H\|TP-SEC:RAW_VOICE_24H\|TP-SEC:TRANSCRIPT_DRAFT_DELETE}` | `tombstone_id` | `purged_or_deleted_at,tombstone_id` | deterministic ID and receipt mapping from section 7.9; same workspace; retained Upload/Attachment metadata and source event resolve where required, but deleted bytes/AiOutput do not |

`AiRun` state/null validation is closed. QUEUED/RUNNING have null `completed_at`, `fallback_reason` and `ai_output_id` with NOT_EVALUATED. SUCCEEDED has non-null completion/output, PASSED and null fallback. REJECTED uses respectively REJECTED_SCHEMA/INVALID_SCHEMA, REJECTED_GROUNDING/GROUNDING_FAILED or REJECTED_POLICY/POLICY_FAILED and has no output. FAILED uses NOT_EVALUATED and one of PROCESSOR_TIMEOUT, PROCESSOR_ERROR or RETRY_EXHAUSTED. CANCELLED_CONSENT_REVOKED uses NOT_EVALUATED/CONSENT_REVOKED and no output. For WEEKLY_SUMMARY, report is non-null and `metric_snapshot_ids_json` is the lowercase-ID-byte sorted unique union of exact report payload locations `/sections/0/cells/*/metricSnapshotId` and `/sections/3/cells/*/metricSnapshotId`, limited to `accounting_completeness_rate`, `planned_trade_rate`, `review_coverage_rate`, `mean_expectancy_r`, `median_expectancy_r`, `fee_drag_pct_of_gross_profit`, `fee_pct_of_gross_turnover`. Every snapshot has exact OVERALL dimension, null phase/timeframe and same input; a duplicate report location contributes one ID while preserving both pointer bindings. Setup, breach, context, counterexample, experiment or same-named dimensional snapshots fail. For the other features report is null and metric array empty. `request_options_json` is exactly `{ "languageHint": <BCP-47-or-null> }`, `{ "maxSuggestions": <integer-1-through-5> }` or `{ "locale": <exact-report-locale> }` for TRANSCRIPTION, TAXONOMY_SUGGESTION or WEEKLY_SUMMARY respectively; extra members fail closed.

`AiOutputSubject` lifecycle validation is exact. A successful output transaction inserts the subject, AiOutput and sequence-1 CREATE event atomically. Subject ID equals output ID; kind, hash and creation time equal the corresponding output fields. CREATE has both receipt fields null, `recorded_at = created_at`, and its idempotency key is unique per workspace. While CREATE is latest, the subject pointer is ACTIVE and exactly one matching AiOutput exists. Delete appends only sequence 2; its receipt fields are jointly non-null, `receipt_subject_id = ai_output_subject_id`, and `receipt_subject_type` is TRANSCRIPT_DRAFT for that kind or AI_OUTPUT otherwise. That pair is the exact TP-SEC SubjectDeletionReceipt composite identity `(workspace_id,subject_type,subject_id)`, and DELETE `recorded_at = receipt.completed_at`. The corresponding Tombstone exposes the same composite identity/hash/time/policy; `last_known_sha256 = subject.last_known_content_sha256`. DELETE is latest forever, pointer state is DELETED, the content bundle is absent and exactly one matching Tombstone is present. Missing/duplicate/gapped events, a second CREATE/DELETE, time regression, pointer mismatch, hash/kind mismatch or active/deleted closure mismatch fails `EXPORT_SCHEMA_VIOLATION` or `EXPORT_TOMBSTONE_INVALID` as applicable.

The copied configuration tuple is required even for failed/rejected runs. `configuration_activation_sequence` is positive; every version string is nonempty and not `latest`; every corresponding SHA-256 is non-null. `output_schema_version` maps TRANSCRIPTION/TAXONOMY_SUGGESTION/WEEKLY_SUMMARY to `transcript_draft_v1`/`taxonomy_suggestion_v1`/`weekly_summary_v1`, and a successful AiOutput repeats it exactly. These fields are immutable audit values copied from the active passing release at enqueue. `AiConfigurationArtifact`, `AiConfigurationRelease`, `AiEvalArtifact`, `AiConfigurationActivationEvent`, `AiProcessorRegistration`, `AiProcessorRegistrationStateEvent`, `AiProcessorCopyReference`, `AiProcessorCopyTerminalEvidence`, `AiConfirmationCommandReceipt`, AI processor deletion inventory/absence evidence, their bytes and restricted storage references are internal TP-SEC control-plane data, are not exported and are not dangling archive FKs. `AiRun.processor_registration_id` is preserved only as the typed immutable binding used at enqueue; an isolated reader validates its ID format and copied tuple coupling but does not fabricate or require the excluded registry row.

Every append-only domain state stream in the registry uses its persisted positive aggregate `event_sequence`: sequence starts at 1, is contiguous, and `recorded_at` is nondecreasing by sequence. Pointer replay first applies cutoff visibility, then uses sequence; timestamp and opaque event ID never choose semantic state. A gap, duplicate, aggregate-ID mismatch or decreasing time is `EXPORT_SCHEMA_VIOLATION`.

AiRunInputReference ordinals are contiguous from 1. Typed keys are exactly: Upload `{ "upload_id": id }`; Attachment `{ "attachment_id": id }`; TradePlanRevision `{ "trade_plan_revision_id": id }`; ReviewRevision `{ "review_revision_id": id }`; WeeklyReportRevision `{ "weekly_report_revision_id": id }`; MetricSnapshot `{ "metric_snapshot_id": id }`; TradeEpisodeProjection `{ "episode_id": id, "projection_version": int }`; ReviewTaxonomyVersion `{ "taxonomy_version": str }`; ReviewTaxonomyItem `{ "taxonomy_version": str, "item_id": id }`. Scalar keys and unknown/missing/extra members fail. Role/type and hash basis are closed:

| Input role / reference type | Exact record schema | Exact digest schema | Fragment schema when included |
|---|---|---|---|
| `VOICE_UPLOAD_PROVENANCE / UPLOAD` | `tp_exp_upload_v1` | `upload_source_bytes_sha256_v1` | `voice_upload_bytes_v1` |
| `VOICE_RETAINED_ATTACHMENT / ATTACHMENT` | `tp_exp_attachment_v1` | `attachment_content_bytes_sha256_v1` | `retained_voice_attachment_bytes_v1` |
| `TAXONOMY_SOURCE_TEXT / TRADE_PLAN_REVISION` | `tp_exp_trade_plan_revision_v1` | `trade_plan_revision_content_sha256_v1` | `taxonomy_selected_text_utf8_v1` |
| `TAXONOMY_SOURCE_TEXT / REVIEW_REVISION` | `tp_exp_review_revision_v1` | `review_revision_content_sha256_v1` | `taxonomy_selected_text_utf8_v1` |
| `TAXONOMY_VERSION_ALLOWLIST / REVIEW_TAXONOMY_VERSION` | `tp_exp_review_taxonomy_version_v1` | `review_taxonomy_version_content_sha256_v1` | `taxonomy_version_allowlist_fragment_v1` |
| `TAXONOMY_ITEM_ALLOWLIST / REVIEW_TAXONOMY_ITEM` | `tp_exp_review_taxonomy_item_v1` | `review_taxonomy_item_payload_sha256_v1` | `taxonomy_item_allowlist_fragment_v1` |
| `WEEKLY_REPORT_PAYLOAD / WEEKLY_REPORT_REVISION` | `tp_exp_weekly_report_revision_v1` | `weekly_report_revision_content_sha256_v1` | `weekly_report_summary_fragment_v1` |
| `WEEKLY_METRIC_PAYLOAD / METRIC_SNAPSHOT` | `tp_exp_metric_snapshot_v1` | `metric_snapshot_input_digest_sha256_v1` | `weekly_metric_summary_fragment_v1` |
| `WEEKLY_EPISODE_GROUNDING / TRADE_EPISODE_PROJECTION` | `tp_exp_trade_episode_projection_v1` | `trade_episode_projection_payload_sha256_v1` | `weekly_episode_grounding_fragment_v1` |

Upload/Attachment digests are their source/content byte hash; TradePlanRevision, ReviewRevision, WeeklyReportRevision and ReviewTaxonomyVersion copy owner `content_sha256`; MetricSnapshot copies `input_digest_sha256`; ReviewTaxonomyItem and TradeEpisodeProjection hash RFC 8785 exact payload bytes under the named record schema. A reader retains the named old basis forever and never reinterprets it with a newer projection. For audio, the fragment hashes exact raw bytes and equals the corresponding retained source byte hash even if only durable metadata/tombstone remains. For selected text, it hashes the exact UTF-8 substring. JSON fragment shapes are exactly:

- `taxonomy_version_allowlist_fragment_v1`: `{ "taxonomyType": type, "taxonomyVersion": version }`.
- `taxonomy_item_allowlist_fragment_v1`: `{ "itemId": id, "itemOrder": int, "labelVi": label, "taxonomyType": type, "taxonomyVersion": version }`.
- `weekly_report_summary_fragment_v1`: `{ "contentSha256": hash, "locale": locale, "reportMetricBindings": [{ "metricSnapshotRecordKey": key, "payloadPointers": [RFC6901-string...] }], "reportRecordKey": key, "reportingAsOfAt": ts, "weeklyLabSchemaVersion": version }`. Bindings equal the exact AiRun metric allowlist, sort by MetricSnapshot ID, and each pointer array contains every matching overview/cost location in report payload order and no other pointer.
- `weekly_metric_summary_fragment_v1`: `{ "candidateEpisodeCount": int, "computationStatus": status, "dimension": { "dimensionType": "OVERALL" }, "displayState": state, "eligibleEpisodeCount": int, "evidenceLabel": label, "excludedEpisodeCount": int, "metricId": id, "metricSnapshotRecordKey": key, "nullReason": str-or-null, "phase": null, "reportPayloadPointers": [RFC6901-string...], "timeframe": null, "unit": str, "value": { "valueDecimal": dec-or-null, "valueDurationMs": int-or-null, "valueInteger": int-or-null, "valueInterval": { "lowerDecimal": dec, "upperDecimal": dec }-or-null, "valueObject": object-or-null, "valueType": type } }`. Pointers byte-equal the report binding for this metric.
- `weekly_episode_grounding_fragment_v1`: `{ "episodeProjectionRecordKey": { "episode_id": id, "projection_version": int } }`.

No fragment member may be missing or extra. Fragment computation status is COMPLETE/UNAVAILABLE, display is NORMAL/POSITIVE_INFINITY/UNDEFINED/UNAVAILABLE, evidence is INSUFFICIENT/EXPLORATORY/ESTIMATED, and typed-value/null/INTERVAL/object rules equal the pinned MetricSnapshot. An unknown display such as AVAILABLE, a non-OVERALL dimension, non-null phase/timeframe, missing/extra pointer or pointer that resolves outside the pinned report is invalid. `processor_payload_included = true` iff both fragment schema and hash are non-null; false requires both null. Selection fields are all null except TAXONOMY_SOURCE_TEXT, whose exact source-field selector, Unicode-scalar bounds and selected-text hash follow TP-SEC. `canonical_input_sha256` is SHA-256 of RFC 8785 bytes of exactly this object; references are in ordinal order and every nullable member remains present as null:

```json
{
  "feature": "<AiRun.feature>",
  "inputSchemaVersion": "<AiRun.input_schema_version>",
  "references": [{
    "fieldSelector": null,
    "inputRole": "...",
    "ordinal": 1,
    "payloadFragmentSha256": null,
    "payloadFragmentSchemaId": null,
    "processorPayloadIncluded": false,
    "referenceDigestSha256": "...",
    "referenceDigestSchemaId": "...",
    "referenceRecordKey": {},
    "referenceRecordSchemaId": "...",
    "referenceType": "...",
    "selectedTextSha256": null,
    "selectionEndScalarIndexExclusive": null,
    "selectionStartScalarIndex": null
  }],
  "requestOptions": {}
}
```

Input cardinality is exact: TRANSCRIPTION has ordinal 1 VOICE Upload provenance with a prior same-workspace ACCEPT event/accepted time at enqueue, even if its current cutoff state is later PURGED, and either raw payload on that row or ordinal 2 ACTIVE/PASSED RETAINED_VOICE Attachment with the same source upload and the only included audio payload. TAXONOMY_SUGGESTION has source text at 1, one exact Review taxonomy version at 2 and every item of that version at 3..N in `(item_order,item_id)` order. WEEKLY_SUMMARY has the exact published report at 1, then each exact overview/cost allowlist MetricSnapshot in AiRun ID order, then sorted unique referenced episode projection keys; all are payload-included. Report bindings, each metric's pointer array, dimension/phase/timeframe and IDs match three ways. Episode keys are reachable from exact report/metric inputs and equal the outbound opaque grounding allowlist. Zero/current lookup, dimensional/extra metric, pointer mismatch or unreachable episode fails. Raw/retained bytes need not outlive the run, but the referenced Upload/Attachment metadata, deletion tombstone when applicable and recorded digest MUST close at the export cutoff.

AiOutput mapping is exact: TRANSCRIPTION -> TRANSCRIPT_DRAFT/`transcript_draft_v1`/TEXT_PLAIN; TAXONOMY_SUGGESTION -> TAXONOMY_SUGGESTION/`taxonomy_suggestion_v1`/APPLICATION_JSON; WEEKLY_SUMMARY -> WEEKLY_SUMMARY/`weekly_summary_v1`/APPLICATION_JSON. `content_sha256 = SHA-256(UTF8(content_utf8))`. APPLICATION_JSON content is itself exact RFC 8785 text, not merely semantically equivalent JSON. Taxonomy JSON is exactly `{ "suggestions": [{ "ordinal": 1, "taxonomyId": "...", "taxonomyVersion": "..." }] }`, with 0-5 items, contiguous ordinals, unique IDs and only input-allowlisted taxonomy IDs. Every AiOutput closes to its ACTIVE same-workspace AiOutputSubject with exact ID/kind/hash/created-time equality.

The reader parses `weekly_summary_v1` as TP-SEC's closed structured claim object and rejects plain text. The root has exactly `claims`, `headline`, `reportRecordKey`, `schemaVersion`; every claim has exactly `claimKind`, `commentary`, `dimension`, `episodeProjectionRecordKeys`, `metricId`, `metricSnapshotRecordKey`, `metricValue`, `ordinal`, `phase`, `quality`, `reportPayloadPointers`, `sampleSize`, `timeframe`. There are 0-5 contiguous claims and unique metric keys. Each claim copies an input metric's exact OVERALL dimension, null phase/timeframe, report pointers, metric ID, eligible-count sample, unit, five typed values plus valueType, and quality object `{ computationStatus, displayState, evidenceLabel, nullReason }`. `metricValue` always has exactly seven members: `unit`, `valueDecimal`, `valueDurationMs`, `valueInteger`, `valueInterval`, `valueObject`, `valueType`; INTERVAL is the exact ordered two-decimal object. DATA_LIMITATION requires all value fields null; METRIC_OBSERVATION and COUNTEREXAMPLE require source COMPLETE with one non-null typed value. Unknown display values, including AVAILABLE, fail.

Claim episode keys are sorted unique by `(episode_id,projection_version)`, at most three, occur in source metric evidence and have matching WEEKLY_EPISODE_GROUNDING input; COUNTEREXAMPLE requires at least one and DATA_LIMITATION requires none. Headline is trimmed single-line 1-120 Unicode scalars and commentary 1-500; both reject controls, markup/HTML, URL, opaque record ID, Unicode decimal digit and numeric/currency tokens `%`, per-mille, `$`, EUR/GBP/JPY/VND symbols, `USDT`, `USD`, `BTC`. Report key equals the pinned report. Output references prove the mapping: global ordinals are contiguous; ordinal 1 is the report key with REPORT_SOURCE/null item/role ordinal 1; each claim has one CLAIM_METRIC at role ordinal 1 and 0-3 CLAIM_EPISODE rows in episode-array order, globally ordered by claim, metric before episode, then role ordinal. `(workspace_id, ai_output_id, output_item_ordinal, reference_role, role_ordinal)` is unique with null normalized to one sentinel.

Transcript has no output reference. Taxonomy suggestion has TAXONOMY_VERSION/null item/role ordinal 1 first, then one TAXONOMY_ITEM per suggestion with matching item ordinal and role ordinal 1. Every output typed key must equal a matching AiRunInputReference key. Missing, duplicate, extra, wrong-role, wrong-item, wrong-order, wrong-type, current-source-substituted or digest/value-mismatched references fail closed.

`TranscriptConfirmation` is a full immutable user-action projection. `target_record_type = TRADE_PLAN` requires `target_field = THESIS`, `target_record_id` resolving a TradePlan and both revision keys having exact `{ "trade_plan_revision_id": id }` shape. `REVIEW` requires LESSON, a Review target and exact `{ "review_revision_id": id }` keys. The base is the current revision at command entry; the result is exactly its next full-replacement revision, created atomically with the confirmation, with only the target text changed under TP-ACC validation. `confirmed_text_sha256 = SHA256(UTF8(exact persisted result thesis-or-lesson))`; that text is trimmed nonempty plain UTF-8 of at most 2,000 Unicode scalars. `source_output_content_sha256` equals the referenced subject's `last_known_content_sha256`, plus active AiOutput content hash while ACTIVE or Tombstone `last_known_sha256` while DELETED. The source Upload is the run's sole accepted VOICE provenance at insertion. `keep_original = true` requires one non-null ACTIVE/PASSED RETAINED_VOICE Attachment whose `source_upload_id` matches and which was created atomically before confirmation; false requires null. Neither branch changes `Upload.purge_due_at`.

`TaxonomySuggestionConfirmation.selected_suggestions_json` is a nonempty array sorted by ordinal with exact item `{ "ordinal": positive-int, "taxonomyId": id, "taxonomyVersion": version }`; ordinals are unique in 1..5. While the subject is ACTIVE, every triple byte-matches one canonical output suggestion. `confirmed_taxonomy_item_ids_json` contains exactly the same IDs sorted by frozen `(item_order,item_id)`, with no extra/manual ID; all triples use the one version/type represented by the result. EXIT_REASON and EMOTION require exactly one triple/item; BREACH_TYPE requires 1..5. The base is the current COMPLETED ReviewRevision at command entry and the result is exactly its atomic next full-replacement revision: the corresponding one-value exit/emotion field or complete breach array equals the confirmed IDs and every dependent OTHER/checklist invariant remains valid. Review/base/result workspace and aggregate must match, and each triple/item closes to its exact public ReviewTaxonomyVersion and ReviewTaxonomyItem.

The reader reconstructs `review_full_replacement_request_v1` from the result revision and its ordered ReviewRevisionAttachment summaries as exactly `{ "attachments":[{ "attachment_content_sha256":hash, "attachment_id":id, "ordinal":int, "role":"SCREENSHOT" }], "breach_other_text":str-or-null, "breach_taxonomy_version":str, "breach_type_ids":[id...], "emotion":id-or-null, "emotion_taxonomy_version":str-or-null, "episode_projection_version":int, "exit_reason":id, "exit_reason_other_text":str-or-null, "exit_reason_taxonomy_version":str, "lesson":str-or-null, "required_checklist_results_json":object, "risk_exceeded":bool, "rule_breach":bool, "stop_moved_away":bool }`. Nullable members are explicit null; attachments are empty or the one TP-ACC-ordered screenshot summary, taxonomy ID arrays retain their frozen order, and no server ID/time/revision number is added. `full_replacement_review_payload_sha256` is lowercase SHA-256 of these RFC 8785 bytes. Unknown/missing fields, a different attachment summary or a result-revision mismatch fails closed.

`confirmation_request_sha256` is lowercase SHA-256 of RFC 8785 bytes of exactly `{ "aiOutputId": id, "basedOnReviewRevisionId": id, "fullReplacementReviewPayloadSha256": hash, "reviewId": id, "selectedSuggestions": selected_suggestions_json, "taxonomyType": type }`. `result_review_revision_content_sha256` byte-equals the referenced result ReviewRevision `content_sha256`; `source_output_content_sha256` byte-equals the AiOutputSubject last-known hash and, while ACTIVE, the AiOutput content hash. `content_sha256` is lowercase SHA-256 of RFC 8785 bytes of exactly `{ "actorUserId": id, "aiOutputId": id, "basedOnReviewRevisionId": id, "confirmationRequestSchemaVersion": "taxonomy_suggestion_confirmation_request_v1", "confirmationRequestSha256": hash, "confirmedTaxonomyItemIds": confirmed_taxonomy_item_ids_json, "fullReplacementReviewPayloadSha256": hash, "idempotencyKey": str, "recordedAt": ts, "resultReviewRevisionContentSha256": hash, "resultReviewRevisionId": id, "reviewId": id, "selectedSuggestions": selected_suggestions_json, "sourceOutputContentSha256": hash, "taxonomySuggestionConfirmationId": id, "taxonomyType": type, "workspaceId": id }`. The full-replacement payload bytes are command input and are not separately serialized; their hash is immutable evidence. `AiConfirmationCommandIntent` and `AiConfirmationCommandReceipt` are Restricted preparation/idempotency controls and are never exported.

For each confirmation table, `(workspace_id,ai_output_id)` is unique, and `(workspace_id,idempotency_key)` is unique across both commands. A retry must be byte-equal. Every confirmation references exactly one same-workspace AiOutputSubject. ACTIVE subject closure requires the exact AiOutput and revalidates suggestion mapping or transcript source/input; DELETED subject closure requires the exact typed Tombstone and validates only the surviving confirmation/result/public-item evidence. It MUST NOT claim to reconstruct or revalidate deleted output content from a hash. Stale bases, wrong subject kind/state, wrong surviving shape/type/version/item, cross-workspace keys, mismatched hashes and non-atomic result/attachment creation fail `EXPORT_SCHEMA_VIOLATION`.

Confirmation `content_sha256` is lowercase SHA-256 of RFC 8785 bytes of exactly one of these objects; timestamps use canonical RFC 3339 milliseconds, nullable attachment is explicit null and arrays preserve their declared order:

```json
{
  "actorUserId": "...",
  "aiOutputId": "...",
  "basedOnRevisionRecordKey": {},
  "confirmedTextSha256": "...",
  "idempotencyKey": "...",
  "keepOriginal": false,
  "recordedAt": "...",
  "resultRevisionRecordKey": {},
  "retainedAttachmentId": null,
  "sourceOutputContentSha256": "...",
  "sourceUploadId": "...",
  "targetField": "THESIS",
  "targetRecordId": "...",
  "targetRecordType": "TRADE_PLAN",
  "transcriptConfirmationId": "...",
  "workspaceId": "..."
}
```

```json
{
  "actorUserId": "...",
  "aiOutputId": "...",
  "basedOnReviewRevisionId": "...",
  "confirmedTaxonomyItemIds": [],
  "idempotencyKey": "...",
  "recordedAt": "...",
  "resultReviewRevisionId": "...",
  "reviewId": "...",
  "selectedSuggestions": [],
  "sourceOutputContentSha256": "...",
  "taxonomySuggestionConfirmationId": "...",
  "taxonomyType": "BREACH_TYPE",
  "workspaceId": "..."
}
```

No hash-basis field is omitted or added. The reader recomputes both hashes even in the DELETED branch; a mismatch fails before reference staging.

For `ProductAnalyticsEvent`, `payload_json` and the producer/source/measurement combination are closed by `product_analytics_event_v1` in current `TP-LAB`; `json` is not permission to add members. Type and source key are both null or both non-null. A non-null `source_record_key_json` is an ordinary canonical object exactly equal to the referenced envelope `recordKey`: Workspace `{ "workspace_id": id }`; TradePlanRevision `{ "trade_plan_revision_id": id }`; TradeEpisodeProjection `{ "episode_id": id, "projection_version": int }`; Upload `{ "upload_id": id }`; ImportBatch `{ "import_batch_id": id }`; ReviewRevision `{ "review_revision_id": id }`; MetricSnapshot `{ "metric_snapshot_id": id }`; ContextSnapshot `{ "id": id }`; WeeklyReportRevision `{ "weekly_report_revision_id": id }`; WeeklyReviewCompletion `{ "weekly_review_completion_id": id }`. Unknown, missing or extra key members fail closed, and domain-derived event idempotency uses exact RFC 8785 source-key bytes.

`ProductMeasurementRunStateEvent` has exactly sequence 1 START and at most one sequence 2 terminal. START is USER-authored, has null terminal event/reason, `recorded_at = started_at`, and idempotency key `measurement-run:<measurement_run_id>:start`. Sequence 2 uses `measurement-run:<measurement_run_id>:terminal`; SUCCEED has a non-null terminal event, null reason and USER actor, while ABANDON has a non-null terminal event and one closed reason. TIMEOUT uniquely requires SYSTEM/null actor and `recorded_at >= deadline_at`; every other abandonment reason requires USER/owner actor. The terminal ProductAnalyticsEvent and state event mutually reference one another, share workspace and copied measurement tuple, and their timestamps obey the TP-LAB producer matrix. Before the deadline an absent sequence 2 means OPEN; at or after `exportAsOfAt == deadline_at` it means semantic ABANDONED/TIMEOUT even when the scheduler has not persisted sequence 2. The excluded PRODUCT_MEASUREMENT_TIMEOUT work item may be validated by the live writer but is never required by the archive reader.

Run ordering is also canonical and reader-validated. There is at most one OPEN run per `(workspace_id,study_id,feature)`. PRACTICE requires a unique contiguous index 1..3 whose predecessors are already terminal; ONBOARDING forbids PRACTICE. MEASURED has null index, is unique per scope, starts only after all existing practice is terminal and forbids later practice; QUICK_PLAN requires exactly terminal practices 1, 2 and 3 first. SUCCEED must commit before `deadline_at`. A TIMEOUT ProductAnalyticsEvent has `occurred_at = deadline_at`, while its `created_at` and state-event `recorded_at` equal the later materialization commit. Every retained run eventually has one terminal sequence 2; a deletion-fence winner removes the entire run bundle and therefore cannot leave an exported permanent OPEN row at or beyond deadline. A FINAL WorkspaceProductMetricSnapshot cannot depend on an applicable unresolved timeout semantic state.

`export_completed` always has null source fields and null measurement fields. Its opaque `idempotency_key` is produced server-side from `(workspace_id, export_job_id, READY event_sequence)`, but neither ExportJob ID nor READY sequence is copied into `source_record_key_json`, `payload_json` or any other exported field. `file_selected` and `measurement_abandoned` are the only other source-null types; their full instrumented run tuple is required. A reader rejects every other source-null or partial-measurement combination.

For `WorkspaceProductMetricSnapshot`, `dimension_json` is exactly `{ "dimensionType": "OVERALL" }` or `{ "dimensionType": "STUDY", "studyId": "<canonical-lowercase-RFC9562-UUID>" }`; unknown, missing or extra members fail. `quick_plan_duration_ms`, `quick_review_duration_ms` and `time_to_first_insight_ms` require STUDY, and every selected ProductAnalyticsEvent has the same non-null `study_id = studyId`. The remaining product metric IDs `verified_pre_fill_plan_coverage`, `weekly_review_completion_rate`, `import_reconciliation_coverage`, `net_metric_episode_exclusion_rate`, `weekly_active_retained_users_w4`, `weekly_active_retained_users_w8` and `episode_count_change_after_adoption` require OVERALL; an instrumented event with non-null study ID cannot substitute for their domain population. Cross-study workspace snapshots and arbitrary filter, label, cohort or member dimensions are forbidden. `dimension_sha256` is lowercase SHA-256 of exact RFC 8785 `dimension_json` bytes and is part of the immutable contiguous revision identity `(workspace_id, metric_id, window_start_at, window_end_at_exclusive, dimension_sha256, revision_no)`.

For `WorkspaceProductMetricSnapshot`, included items are exactly `{ "sourceRecordKey": {}, "sourceType": recordType }`. Excluded items are exactly `{ "reasonCode": str, "sourceRecordKey": {}, "sourceType": recordType }`. Every key is the referenced envelope's exact recordKey, including all composite identity members. Included items sort by `(sourceType, RFC8785(sourceRecordKey) unsigned UTF-8 bytes)`; excluded items add `reasonCode` as the final Unicode-code-point tie-breaker. The arrays have no duplicate `(sourceType, canonical-key-bytes)`, are disjoint, partition the selected as-of population and resolve only to immutable/version-specific records in the same workspace. `exclusion_reason_counts_json` is the exact count by `reasonCode`, with object keys in RFC 8785 order. `input_event_digest_sha256` is SHA-256 of the RFC 8785 bytes of exactly:

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

`dimension` is the exact `dimension_json`; the three source/count values are the exact persisted JSON values after the rules above. Revision, status, typed result values, supersedes ID and `created_at` are not in the digest. `InternalAggregateProductMetricSnapshot`, `InternalAggregateCohortRetirement`, `contribution_digest_key_version` and all aggregate definition/contribution/member/retirement evidence remain non-exportable.

The `WorkspaceProductMetricSnapshot` output matrix is also closed; the registry's nullable fields are not independent options. `value_type` is fixed by metric in every status. PROVISIONAL requires all four typed value fields plus `numerator_integer` and `denominator_integer` null, with exact `null_reason = EVALUATION_NOT_FINAL`; it never exposes a partial KPI value. FINAL obeys the following exhaustive matrix:

| Metric IDs | Exact `value_type` | FINAL value/count branch | Sole whole-value-null branch |
|---|---|---|---|
| `verified_pre_fill_plan_coverage` | `DECIMAL` | Nonnegative eligible-episode numerator/denominator, numerator <= denominator; denominator > 0 requires only `value_decimal = round18(numerator/denominator)` non-null and null reason | numerator = denominator = 0, all values null, `NO_ELIGIBLE_EPISODE` |
| `weekly_review_completion_rate` | `DECIMAL` | Nonnegative eligible-REGULAR-user-week numerator/denominator, numerator <= denominator; denominator > 0 requires only exact ratio value and null reason | numerator = denominator = 0, all values null, `NO_ELIGIBLE_USER_WEEK` |
| `import_reconciliation_coverage` | `DECIMAL` | Numerator is exact RECONCILED+DUPLICATE row sum; denominator is admitted nonblank-row sum; nonnegative/numerator <= denominator; denominator > 0 requires only exact ratio value and null reason | numerator = denominator = 0, all values null, `NO_ADMITTED_IMPORT_ROW` |
| `net_metric_episode_exclusion_rate` | `DECIMAL` | Numerator is selected closed projections that are not net-eligible; denominator is all selected closed projections; nonnegative/numerator <= denominator; denominator > 0 requires only exact ratio value and null reason | numerator = denominator = 0, all values null, `NO_CLOSED_EPISODE` |
| `quick_plan_duration_ms`, `quick_review_duration_ms`, `time_to_first_insight_ms` | `DURATION_MS` | One valid MEASURED run requires positive `value_duration_ms`; every other value/count and null reason is null | All value/count fields null; `MEASUREMENT_ABANDONED` iff its terminal event exists, otherwise `NO_MEASURED_RUN` |
| `weekly_active_retained_users_w4`, `weekly_active_retained_users_w8` | `INTEGER` | After the exact interval, only `value_integer` is non-null and is exactly 0 or 1; null reason is null | No onboarding source: all value/count fields null, `NO_ONBOARDING_EVENT` |
| `episode_count_change_after_adoption` | `OBJECT` | After the full post window, only `value_object_json` is non-null and is exactly `{ "definedRatio": decimal-or-null, "postCount": nonnegative-int, "preCount": nonnegative-int }`; preCount > 0 requires `definedRatio = round18((postCount-preCount)/preCount)` and null top reason; preCount = 0 requires null definedRatio and top `PRE_PERIOD_ZERO` | Before any adoption source: all value/count fields null, `NO_ADOPTION_EVENT` |

Ratio count fields remain populated in the FINAL denominator-zero branches; they distinguish a real zero population from absent data. `round18` divides arbitrary-precision integers/rationals, rounds once to scale 18 using ROUND_HALF_EVEN, normalizes negative zero and strips trailing zeros; mean-of-ratios and binary floating point are forbidden. `PRE_PERIOD_ZERO` is the only case where a non-null typed value and non-null top-level reason coexist because the two counts are defined while the nested ratio is not. Every other non-null typed value requires null reason, and every whole-value null requires exactly its matrix reason. Unknown metric/type/reason combinations, an extra typed field, non-null scalar count on a non-ratio, partial PROVISIONAL output or arithmetic overflow is `EXPORT_SCHEMA_VIOLATION`.

Every record type named in sections 7.1-7.9 MUST have exactly the registry row above, and no registry row authorizes placement in a different fixed entry. Schema files generated from this table are build artifacts; this document remains normative. CI compares generated schemas against a committed digest and runs unknown-field, nullability, composite-key, sort and cross-workspace mutations for every row.

## 8. Attachment binary contract

An attachment binary is exported if and only if its as-of descriptor has `availability = RETAINED_CLEAN`, `state_at_cutoff = ACTIVE`, `scan_status_at_cutoff = PASSED`, and `TP-SEC` classifies it as still retained. The descriptor mapping is exact: ACTIVE -> RETAINED_CLEAN with non-null path and binary; DELETING -> DELETE_PENDING with null path and no Tombstone yet; DELETED -> TOMBSTONED with null path and its required Tombstone. In v1 this permits cleaned/re-encoded screenshots and user-retained voice objects; quarantined, pending, rejected, infected, deleting or deleted objects are never included as bytes.

For every retained binary:

1. exact bytes are read by immutable object version;
2. `sha256` and byte size are checked against AttachmentExportDescriptor;
3. its archive path is exactly `attachments/{attachment_id}.bin`;
4. the manifest repeats exact byte size and SHA-256;
5. the byte entry is marked `application/octet-stream` in the manifest; the safe original media type stays metadata;
6. no byte is parsed, executed, transformed, recompressed or renamed during export.

`original_filename` is retained only when it is durable authorized user metadata. To derive `safe_display_filename`, replace `/`, `\\`, U+0000..U+001F, U+007F..U+009F and bidi controls U+061C, U+200E, U+200F, U+202A..U+202E and U+2066..U+2069 with `_`; then truncate to 255 Unicode scalar values without normalization. Empty output becomes `attachment`. This value is never used as a filesystem path. The exporter does not generate HTML, SVG, formula or executable previews.

If a historical ReviewRevisionAttachment points to a deleted binary, the Attachment metadata, immutable join/content hash and `ATTACHMENT_BINARY` tombstone remain; `archive_path` is null. If it points to a retained binary, omission is fatal.

An Attachment record and its one AttachmentExportDescriptor are mandatory for every ReviewRevisionAttachment, regardless of binary availability. A Tombstone substitutes only for deleted bytes, never for either metadata record. Removing the Attachment or descriptor, or allowing its ID/hash/workspace to disagree with the join, is `EXPORT_REFERENCE_DANGLING` and prevents READY.

## 9. Convenience CSV profile

CSV is non-lossless and cannot replace canonical JSON. Every CSV uses UTF-8 without BOM, RFC 4180 quoting, comma delimiter and LF record terminator. The final record ends with LF. Headers and columns are fixed in the order below; null is empty, booleans are lowercase `true`/`false`, timestamps and decimals use section 6.1, and array/object values are omitted rather than embedded.

| Entry | Exact columns in order | Record order |
|---|---|---|
| `import_batches.csv` | `import_batch_id,source_upload_id,uploaded_at,status,contract_version,instrument_catalog_version,file_sha256,file_size_bytes,data_rows,reconciled_rows,duplicate_rows,accounting_pending_rows,quarantined_rows,reconciliation_rate,file_error_code,duplicate_file_of_batch_id` | `uploaded_at,import_batch_id` |
| `fills.csv` | `fill_id,import_batch_id,source_row_number,executed_at,source_timestamp_precision,venue_symbol,side,price_quote_per_base,executed_qty_base,gross_amount_quote,fee_qty,fee_asset,created_at,fill_schema_version` | `source_time_start,fill_id` |
| `episodes.csv` | `episode_id,projection_version,state,venue_symbol,first_fill_at,closed_at,plan_proof_status,accounting_quality,position_qty_base,gross_realized_pnl_quote,known_fee_quote,net_realized_pnl_quote,projection_algorithm_version,ledger_algorithm_version` | `first_fill_at,episode_id,projection_version` |
| `plans.csv` | `trade_plan_id,trade_plan_revision_id,revision_no,state,venue_symbol,recorded_at,expires_at,setup_id,setup_label_snapshot,entry_zone_low,entry_zone_high,initial_stop_price,planned_risk_quote,planned_risk_asset,confidence_score` | `recorded_at,trade_plan_id,revision_no` |
| `reviews.csv` | `review_id,review_revision_id,revision_no,episode_id,episode_projection_version,state,completed_at,recorded_at,exit_reason,rule_breach,stop_moved_away,risk_exceeded,emotion,has_screenshot` | `completed_at,review_id,revision_no` |
| `context_snapshots.csv` | `id,tradeEpisodeId,episodeProjectionVersion,phase,timeframe,eventAt,asOfAt,quality,aggregationEligible,regimeCode,rvol,normalizedTrueRange,vwapDistanceBps,coreCoverage,sessionCoverage,baselineCoverage,algorithmVersion,parameterSetId` | `eventAt,tradeEpisodeId,episodeProjectionVersion,phase,timeframe,id` |
| `metric_snapshots.csv` | `metric_snapshot_id,weekly_cohort_id,metric_id,metric_formula_version,metric_algorithm_version,phase,timeframe,value_type,value,unit,eligible_episode_count,excluded_episode_count,evidence_label,null_reason,reporting_as_of_at` | `weekly_cohort_id,metric_id,phase-or-empty,timeframe-or-empty,metric_snapshot_id` |
| `weekly_reports.csv` | `weekly_report_id,weekly_report_revision_id,weekly_cohort_id,revision_no,status,cohort_type,reporting_as_of_at,context_section_status,weekly_lab_schema_version,renderer_id,content_sha256,published_at` | `weekly_cohort_id,weekly_report_id,revision_no` |

`venue_symbol` in episode/plan CSV is resolved from the exact referenced instrument/catalog record; it is convenience display data and does not alter JSON. `value` in metric CSV contains the single typed scalar display value only; OBJECT and INTERVAL values are empty and remain available in JSON.

CSV row selection and mapping are closed; there is no UI filtering, current-only selection or deduplication. Unless the table below names a derived/joined field, each CSV cell is the same-named canonical payload field converted by the scalar rules above. Each source record produces exactly one row and no other record may produce a row:

| Entry | Exact canonical row source | Exact joins and derived fields |
|---|---|---|
| `import_batches.csv` | Every `ImportBatch` | Direct fields only; `contract_version = ImportBatch.contract_version` |
| `fills.csv` | Every `NormalizedFill` | Direct fields only; row order uses the non-column source field `source_time_start` |
| `episodes.csv` | Every `TradeEpisodeProjection`, including superseded history | `venue_symbol` is exact referenced `Instrument.venue_symbol`; every other cell is from the projection |
| `plans.csv` | Every `TradePlanRevision` | Join its exact `TradePlan` for `state` and `expires_at`, and that plan's exact `Instrument` for `venue_symbol`; all other cells are from the revision |
| `reviews.csv` | Every `ReviewRevision` | Join exact `Review` for `review_id`, `episode_id`, `state` and `completed_at`; `has_screenshot` is `true` iff the revision has its one allowed `ReviewRevisionAttachment`, otherwise `false`; other cells are from the revision |
| `context_snapshots.csv` | Every `ContextSnapshot` | Direct fields only |
| `metric_snapshots.csv` | Every non-north-star `MetricSnapshot` | Direct fields except `value`: DECIMAL uses `value_decimal`, INTEGER uses base-10 `value_integer`, DURATION_MS uses base-10 `value_duration_ms`, and OBJECT or unavailable/non-scalar value is empty |
| `weekly_reports.csv` | Every `WeeklyReportRevision`, including SUPERSEDED history | Direct revision fields only |

The exporter and round-trip reader regenerate all eight files solely from staged canonical JSON using this table, the fixed headers/order and section 9.1 escaping, then require exact byte equality and exact CSV manifest `recordCount`. An omitted, duplicate, foreign, current-only or fabricated row, or any altered cell, is invalid even when RFC 4180 syntax and manifest checksums are internally consistent.

### 9.1. Formula injection defense

Every textual CSV cell is inspected before RFC 4180 quoting. Let `probe` be the cell after removing leading code points in the Unicode 15.1 `White_Space` property or Unicode control ranges U+0000..U+001F and U+007F..U+009F. If `probe` begins with `=`, `+`, `-` or `@`, or if the original cell begins with any of those control code points, prefix the exported cell with a single apostrophe U+0027. Leading whitespace/control characters are retained after the apostrophe so the value is visible but inert. Apply this rule to identifiers and user text as well as labels and filenames.

Validated numeric/boolean fields are emitted from their typed canonical value and are not passed through text interpolation. CR, LF, comma and quote inside text are handled by RFC 4180 quoting after injection escaping. CSV readers MUST treat all convenience files as untrusted data and MUST NOT execute formulas, links, macros or external references.

## 10. Referential integrity and allowed non-record references

Every canonical foreign key/reference must resolve to an included record, an allowed Tombstone, or one of these explicitly typed non-record public references:

| Reference type | Allowed value |
|---|---|
| `IANA_TIMEZONE_ID` | Stored timezone string, interpreted with the exported TZDB version |
| `TZDB_RELEASE_ID` | Stored immutable TZDB release identifier |
| `BINANCE_SOURCE_ORIGIN` | Sanitized `sourceBaseUrl` retained in MarketDataSourceRequest |
| `TEMPORARY_IMPORT_PREVIEW_PROOF_ID` | `ImportBatch.source_import_preview_id`; accepted only with exact copied `import_preview_v1` schema, summary hash and confirmation time, never resolved to an archive record |
| `RESTRICTED_AI_PROCESSOR_REGISTRATION_ID` | `AiRun.processor_registration_id`; immutable enqueue-time binding to an excluded TP-SEC processor registry, never usable to fetch provider configuration from the archive |
| `APPROVED_SYSTEM_PRINCIPAL_ID` | `ContextAlgorithmRelease.releasedBySystemPrincipalId`; immutable non-tenant release-audit identity, never a workspace owner/user reference |

These are closed scalar standards/source/control identifiers, not substitutes for domain records. Only the exact named fields may use their row; no episode, fill, plan, Review, report, metric, attachment, AI-content or workspace foreign key may use this escape hatch.

V1 has exactly one owner user per Workspace. Consequently every non-null `owner_user_id`, `user_id`, `actor_user_id` or `recorded_by_user_id` in any WORKSPACE envelope, including a declared nested reference, MUST equal the included `Workspace.owner_user_id` and resolve to the same `WorkspaceOwnerProfile`. For a record with `actor_type`, SYSTEM requires null `actor_user_id` and USER requires that owner ID. A nullable actor without `actor_type` is null only for the source contract's SYSTEM case; if non-null it still equals the owner. Any different or unresolved identity fails `EXPORT_CROSS_TENANT_REFERENCE`; a future multi-user model requires a new export schema rather than weakening this invariant.

Before packaging, the validator MUST check:

- same-workspace composite ownership for every tenant reference;
- no workspace field on shared public market provenance and no other-tenant identifier embedded within it;
- exact Review taxonomy version/type/item membership and AI typed key/cardinality/digest closure;
- revision chains are contiguous where the source contract requires it;
- every as-of pointer derives from its included basis record/event under the closed matrix;
- content, input and provenance hashes recompute;
- every Attachment has one descriptor, all retained descriptors have one binary, all binary entries have one descriptor, and a deleted binary Tombstone never replaces metadata;
- all manifest counts/sizes/hashes match exact entry bytes;
- the closed allowlist, fixed layout and record ordering are exact.

## 11. Stable errors and retry behavior

| Error code | Retry | Required behavior |
|---|---|---|
| `EXPORT_AUTH_REQUIRED` | After re-auth | No request/signed URL issued |
| `EXPORT_FORBIDDEN` | No | Generic denial; no target existence disclosure |
| `EXPORT_RATE_LIMITED` | After retry-after | No snapshot/archive side effect |
| `EXPORT_IDEMPOTENCY_CONFLICT` | New key | Existing request unchanged |
| `EXPORT_WORKSPACE_DELETING` | No while deleting | Cancel/fail; no delivery |
| `EXPORT_SNAPSHOT_UNAVAILABLE` | Bounded internal retry | No mixed-page snapshots |
| `EXPORT_TEMPORAL_INVARIANT_FAILED` | After repair | No archive |
| `EXPORT_REFERENCE_DANGLING` | After repair | No record omission/substitution |
| `EXPORT_CROSS_TENANT_REFERENCE` | No; security incident path | No archive or leaked identifier |
| `EXPORT_POINTER_MISMATCH` | After repair | No archive |
| `EXPORT_TOMBSTONE_INVALID` | After receipt/outbox repair | No missing, conflicting or fabricated deletion state |
| `EXPORT_VERSION_UNSUPPORTED` | After deployment/migration | No silent downgrade |
| `EXPORT_SCHEMA_VIOLATION` | After repair | No archive |
| `EXPORT_ATTACHMENT_CHANGED` | One automatic new-cutoff attempt | Old attempt aborted; never partial |
| `EXPORT_SUBJECT_CHANGED` | One automatic new-cutoff attempt | Deleted subject invalidates old attempt; second race fails without delivery |
| `EXPORT_ATTACHMENT_UNAVAILABLE` | After repair/new request | No archive |
| `EXPORT_ARCHIVE_EXPIRED_BEFORE_READY` | New request | Candidate is revoked and verified absent under its expiry fence; no READY or in-place lifetime refresh |
| `EXPORT_CHECKSUM_MISMATCH` | One rebuild from same pinned snapshot if valid | Never READY until exact validation passes |
| `EXPORT_ARCHIVE_UNSAFE` | No | Reject zip-slip/duplicate/symlink/unsupported feature |
| `EXPORT_PROFILE_SLA_EXCEEDED` | No automatic waiver | Release evidence fails |
| `EXPORT_OVERSIZE_ACCEPTED` | Informational; job continues | Lossless OVERSIZE lane and notifications from section 15.2 |
| `EXPORT_STANDARD_SLA_MISSED` | Job continues; operations intervene | Owner notified; no truncation or false READY |
| `EXPORT_INTERNAL_FAILED` | Bounded retry/new request | Sanitized response only |
| `ROUND_TRIP_TARGET_NOT_EMPTY` | New empty namespace | No record staged or ID remapped |
| `ROUND_TRIP_ID_COLLISION` | After fixture/archive repair | No reference materialization |
| `ROUND_TRIP_UNSUPPORTED_SCHEMA` | Compatible reader required | No field/record ignored or partial import |

Retries never change canonical data under an existing cutoff. The only new-cutoff retry is the one explicitly recorded RESTART_ATTEMPT per job for `ATTACHMENT_CHANGED`, `SUBJECT_DELETED` or `TOMBSTONE_PENDING`; it creates attempt 2 and never mutates attempt 1. An attempt cannot become SUCCEEDED after ABORTED/FAILED, and a second restart condition fails without partial delivery.

## 12. Observability without content

The service emits metrics by environment and coarse outcome only:

- request/job counts by state and stable error code;
- queue, snapshot, materialization, validation and total duration histograms;
- archive byte/count buckets and attachment-byte buckets;
- canonical record-count buckets by record type;
- automatic restart count by closed non-content reason code;
- closure expansion count and depth;
- checksum/round-trip/profile pass/fail counters;
- signed URL issue/download outcome and archive-expiry lag under `TP-SEC`.

Operational telemetry and logs MAY contain request/job/attempt ID, pseudonymous workspace ID, correlation ID, timestamps, state, duration, byte/count bucket and stable error code. They MUST NOT contain filenames, notes, CSV cells, trade values, P&L, symbols, attachment bytes, JSON/CSV fragments, AI content, consent content, signed URLs, hashes that can be used as content identifiers, or another tenant's ID. Export request, READY, download and expiry/deletion outcomes are audited under `TP-SEC` without archive content.

## 13. Round-trip conformance reader

### 13.1. Purpose and target

`tradeproof_export_round_trip_v1` is a release/test reader that imports into a newly created, isolated, empty validation namespace with no network access and no production credentials. It is not exposed to product users and MUST NOT write into a production workspace.

The namespace stores canonical export records and object bytes separately from live service tables so validation cannot trigger email, AI, market fetch, scheduler, webhooks or domain commands.

### 13.2. Validation order

The reader performs these steps in order and stops at the first failure:

1. Enforce archive byte/entry limits, ZIP profile, fixed paths/order, no encryption, no symlink, no duplicate and no zip-slip.
2. Read and schema-validate `manifest.json`; require `tradeproof_export_v1` and `tradeproof_export_manifest_v1`.
3. Match the complete actual entry list to `files`; reject missing and undeclared entries.
4. Stream each entry while checking CRC-32, exact uncompressed size and SHA-256; enforce a bounded compression ratio even though v1 requires STORE.
5. Decode UTF-8 strictly, reject BOM/duplicate JSON keys/invalid Unicode, parse canonical JSON and require reserialization byte equality.
6. Validate fixed envelope/logical schema, record types, primitive formats, sort order, exact version identifiers and CSV profile.
7. Stage all IDs in the empty namespace; detect duplicate primary IDs and identity collisions before creating references.
8. Validate same-workspace ownership, reference closure, revision/event chains, pointers, object-absence proofs, tombstones, attachment descriptors, AiOutputSubject lifecycle/receipt coupling, AI confirmations and domain hashes.
9. Recompute ContextSnapshot input/provenance verification offline and verify WeeklyReport/MetricSnapshot stored hashes without recalculating authoritative business values differently.
10. Regenerate all eight convenience CSV files from the staged canonical record sets and require exact byte equality and manifest row counts.
11. Serialize the staged canonical record sets and manifest inputs again and require byte equality for every canonical entry; binary bytes/hashes must also match.

### 13.3. Collision and schema rules

- The target namespace must contain no existing export record. Any existing ID fails `ROUND_TRIP_TARGET_NOT_EMPTY`; IDs are never remapped.
- Duplicate IDs within a record type, duplicate composite revision keys or incompatible reuse across record types fail `ROUND_TRIP_ID_COLLISION`.
- Unknown archive schema, logical schema, record type, field or enum fails closed. The v1 reader does not ignore unknown data.
- A newer reader MAY register a deterministic, tested migration from an older archive schema into its isolated validation model. It must retain the original archive bytes and produce a migration report; it MUST NOT rewrite the original archive.
- The `tradeproof_export_v1` reader must remain available for the product's documented data-retention lifetime. A new writer version cannot remove the old reader until all retained old archives have expired and regression fixtures remain available.
- An old reader receiving a new schema returns `ROUND_TRIP_UNSUPPORTED_SCHEMA`, not a partial import. A new reader receiving v1 must pass the stored v1 golden corpus.

### 13.4. Round-trip pass condition

A round trip passes only when stable IDs, revision numbers and chains, state events, as-of pointers, hashes, canonical decimal strings, timestamps, Unicode scalar content, nulls, arrays, source references, object-absence evidence, payload-free Restricted AI subject history, immutable AI confirmations, canonical Weekly Lab section payloads and retained attachment bytes are preserved. Reserializing each canonical entry under the same envelope and cutoff MUST be byte-identical. The validation namespace may use different internal surrogate storage keys, but they cannot appear in output.

## 14. Compatibility and migration policy

- Additive optional behavior is not automatically compatible: if it changes canonical bytes, record types, field meaning, layout, ordering or closure, bump the applicable schema identifier.
- A new domain algorithm/taxonomy value may appear inside `tradeproof_export_v1` only in a declared `ver<C:S>` field, only when the complete existing projection remains valid, and only as an exact `includedValues` member. The v1 reader preserves the value opaquely. The table's `writerBaselineValue` never changes under v1; a changed baseline, required shape, enum, hash basis, closure or record type requires new export and manifest schemas.
- Writer deployment is blocked until its matching reader, migrations from every supported older archive schema, old-reader rejection fixture and golden byte corpus pass.
- The manifest always names the actual archive/logical schema. Content negotiation, aliases and implicit latest-version selection are forbidden.
- A migration never changes an original ID, hash or immutable revision. If a target validation schema requires a derived representation, it stores both original canonical bytes and the derived form with a migration identifier.

## 15. SLA envelope and `export_conformance_profile_v1`

### 15.1. `export_sla_envelope_v1`

The 24-hour READY commitment applies exactly when the successful attempt's same-cutoff preflight satisfies every inclusive bound below. This is an export service envelope, not a product storage/import quota: it neither blocks writes nor limits workspace ownership.

| Snapshot measure | Inclusive STANDARD bound |
|---|---:|
| ImportBatch records | 10 |
| Upload + UploadStateEvent + UploadObjectAbsenceVerification records | 1,000 |
| ImportRow records | 1,000,000 |
| StagedFill records | 1,000,000 |
| StagedFillDisposition records | 1,000,000 |
| NormalizedFill records | 1,000,000 |
| TradeEpisode identities | 50,000 |
| TradeEpisodeProjection records | 60,000 |
| EpisodeFillAllocation records | 2,000,000 |
| AccountingLedgerEntry records | 4,000,000 |
| FeeConversion records | 1,000,000 |
| TradePlan records | 60,000 |
| TradePlanRevision records | 120,000 |
| all plan/setup state/association/resolution records | 240,000 |
| Review records | 10,000 |
| ReviewRevision records | 20,000 |
| ContextSnapshot records | 200,000 |
| reference-closed ContextAlgorithmRelease records | 1,000 |
| reference-closed MarketBarRevision records | 10,000,000 |
| reference-closed MarketBarConflict + MarketBarResolution records | 1,000,000 |
| reference-closed MarketBarSourceObservation records | 10,000,000 |
| reference-closed MarketDataSourceRequest records | 1,000,000 |
| reference-closed MarketDataIngestionBatch records | 100,000 |
| ProductMeasurementRun records | 1,000 |
| ProductMeasurementRunStateEvent records | 2,000 |
| all other Weekly Lab records from section 7.7 combined | 1,000 |
| ConsentRecord + AiRun + AiRunInputReference + AiOutputSubject + AiOutputSubjectStateEvent + AiOutput + AiOutputReference + confirmation records | 100,000 |
| retained attachment binary entries | 1,000 |
| retained attachment bytes | 2 GiB (`2,147,483,648`) |
| all canonical records combined, including public closure | 30,000,000 |
| estimated uncompressed fixed JSON + CSV bytes | 100 GiB (`107,374,182,400`) |
| estimated complete uncompressed archive bytes | 102 GiB (`109,521,666,048`) |

Preflight runs against `snapshotWatermark` before MATERIALIZING, follows the same reference-closure rules and computes exact record/object counts. Byte estimates are exact serializer size counts over canonical staged cursors and exact retained binary sizes, not statistical estimates despite the field name. The result and every exceeded measure are persisted without content.

If all bounds pass, the job appends `CLASSIFY_STANDARD`, sets `service_class = STANDARD`, and sets `sla_due_at = requested_at + 24 hours`. READY must occur no later than that strict deadline. Queue time, snapshot time, the one permitted attachment/content restart and validation all count against it.

### 15.2. Oversize path

If any bound is exceeded, the request remains accepted. The job appends `CLASSIFY_OVERSIZE`, sets `service_class = OVERSIZE`, keeps `sla_due_at = null` and exposes informational status `EXPORT_OVERSIZE_ACCEPTED` plus only the exceeded measure names, never their sensitive exact values to logs. It then uses the same lossless schema/closure/validation pipeline on a lower-priority, resumable oversize worker lane. It MUST NOT sample, truncate, split into semantically incomplete archives or ask the user to delete data.

OVERSIZE has no v1 24-hour READY guarantee. The owner's authenticated first-party control feed receives one classification notice, a still-processing notice at 24 hours and every subsequent 24-hour boundary, then READY or sanitized failure notice under section 3.4. Progress reports only the closed stage and coarse count/byte buckets. No email/webhook/external notifier exists. Workspace/item deletion still wins and no generated archive becomes a retention exception.

An attachment/content-deletion restart creates a new cutoff and reruns preflight; service class may change. A STANDARD job that misses its deadline emits `EXPORT_STANDARD_SLA_MISSED`, pages operations, notifies the owner and continues to completion unless deletion/cancellation intervenes. Missing the SLA does not authorize partial output.

### 15.3. Relationship to the release profile

`export_sla_envelope_v1` is the runtime eligibility rule for the `TP-SEC` 24-hour commitment. `export_conformance_profile_v1` is the deterministic release dataset used to prove the implementation at the envelope boundary. The profile MUST satisfy every STANDARD bound and hit the documented major boundaries; passing it is necessary but does not turn those bounds into a storage quota.

### 15.4. Dataset

The core profile uses one deterministic synthetic workspace at the cutoff. A release enabling any AI feature applies an overlay for exactly its enabled feature set to the same non-AI dataset and bounds:

- 10 imports of exactly 100,000 nonblank data rows each and at most 20 MiB each, including one exact 20 MiB file, with all ImportBatch/ImportRow states represented;
- 1,000,000 durable ImportRow records with the exact staged/normalized/duplicate/quarantine reference matrix, up to 1,000,000 StagedFills and dispositions, immutable admitted NormalizedFills, copied ImportPreview proof on every ImportBatch, and Upload purge deadlines/events/absence proofs represented;
- 50,000 TradeEpisode identities with at least two projection versions for 20%, full allocations/ledger entries and fee conversion mixtures;
- 60,000 plans with two revisions each, plan state events and ambiguous/late resolution history;
- 10,000 Reviews with at least two revisions each, frozen taxonomies and a mixture of retained and tombstoned screenshots, Attachment events and absence proofs;
- 200,000 ContextSnapshots across phase/timeframe/quality/revision scopes, their exact immutable ContextAlgorithmRelease tuples, plus the complete de-duplicated logical-bar -> optional conflict/resolution prefix -> selected revision/observation -> source request -> ingestion batch closure; ContextEpisodeTrigger and ManualContextRecomputeRequest controls are absent;
- 1,000 ProductMeasurementRuns with START and success/explicit-abandon/semantic-timeout branches, at most 2,000 state events and exact terminal ProductAnalyticsEvent mutual closure; PRODUCT_MEASUREMENT_TIMEOUT controls are absent;
- 1,000 combined other weekly artifacts across cohorts, cohort input revisions, ContextAvailabilityDecisions, MetricSnapshots, report revisions/state, experiments, completions, other ProductAnalyticsEvents and WorkspaceProductMetricSnapshots, including superseded history; no InternalAggregateProductMetricSnapshot is present;
- an AI-empty core variant; an AI-feature release additionally runs the same profile with retained ConsentRecord/AiRun/AiRunInputReference/AiOutputSubject/AiOutputSubjectStateEvent/AiOutput/AiOutputReference branches for each and only each enabled feature among TRANSCRIPTION, TAXONOMY_SUGGESTION and WEEKLY_SUMMARY, plus TranscriptConfirmation or TaxonomySuggestionConfirmation for its applicable enabled feature, within the combined bound;
- exactly 2 GiB of retained attachment bytes across at least 1,000 binary entries;
- at least one record/tombstone/null/empty set for every allowed v1 branch.

The generated public-provenance closure and all combined record/byte counts MUST stay within every `export_sla_envelope_v1` bound; retained attachment count and bytes, ImportBatch count, ImportRow count, episode count, Review count, ContextSnapshot count and weekly-artifact count hit their exact STANDARD boundaries.

Data generation is deterministic from a committed fixture seed. The dataset manifest and source-database checksum are release artifacts.

### 15.5. Environment and runs

Release evidence records commit SHA, schema migrations, exporter/reader build IDs, database/object-store versions, CPU model/count, RAM, disk class, network bandwidth/latency, start/end time and peak resource use. The baseline environment has at least 8 dedicated vCPU, 32 GiB RAM, SSD-backed database/object staging and 1 Gbit/s object-store connectivity; swapping is disabled for the measured services.

Run one warm-up, then three measured end-to-end exports from request acceptance through READY for the core profile. A release enabling any AI feature repeats one warm-up and three measured runs with the exact enabled-feature overlay; disabled-feature record sets remain empty and are not silently substituted by another branch. Download each archive once, run the isolated round-trip reader, and retain checksums/timings. No manual database patch, skipped entry or relaxed validator is allowed between runs.

### 15.6. Pass conditions

All three measured runs for each applicable profile variant must:

1. reach READY within 24 hours of `requested_at`;
2. independently pass archive, manifest, checksum, closure and byte-equal round-trip validation;
3. produce identical canonical entry bytes and archive SHA-256 when the complete deterministic envelope tuple from section 5.1 is held fixed, including request/job/attempt IDs and snapshot-engine/watermark metadata;
4. remain within 32 GiB RSS for exporter and 32 GiB RSS for reader, and use bounded streaming rather than loading the archive or a 100,000-row record set wholly into memory;
5. emit no secret/user-content log and no cross-workspace/global over-export;
6. leave no temporary object beyond 24 hours and obey signed delivery/expiry evidence from `TP-SEC`.

Any run over 24 hours or any integrity mismatch fails `EXPORT_PROFILE_SLA_EXCEEDED` and blocks release. Passing this profile proves the STANDARD boundary in the named environment. OVERSIZE remains lossless and supported but has the explicit notification/status behavior above instead of a time guarantee.

## 16. Golden and conformance fixtures

All fixtures pin the full deterministic envelope tuple from section 5.1: semantic records/binaries, workspace ID/timezone, every request/job/attempt and domain opaque ID, `exportAsOfAt`, `generatedAt`, snapshot-engine ID/version, snapshot-watermark hash, complete `domainVersions`, purge-class list and ZIP64 decision.

Fixture applicability is a closed v1 rule. Every row is `CORE` except exactly `G23`, `G44`, `G53`, `G55`, `G82` and `G88`. G23/G44/G53/G55/G88 are three independently gated branch matrices; their case suffixes and applicability are `/TRANSCRIPTION` -> `AI_TRANSCRIPTION`, `/TAXONOMY_SUGGESTION` -> `AI_TAXONOMY_SUGGESTION`, and `/WEEKLY_SUMMARY` -> `AI_WEEKLY_SUMMARY`. G82 has only the independently gated TRANSCRIPTION and TAXONOMY_SUGGESTION branches because weekly summaries have no confirmation command. A release runs CORE plus only the branches for each feature it enables; enabling one feature never requires data from a disabled feature. `G22` is CORE and mandatory for every release using an all-features-disabled fixture configuration. Any future fixture or branch must declare exactly CORE or one of those three feature values.

| ID | Fixture | Required assertion |
|---|---|---|
| `TP-EXP:G01_deterministic_archive` | Same frozen semantic input and complete envelope tuple exported by two independent implementations | Every canonical/CSV/binary entry, manifest and complete archive SHA-256 are identical |
| `TP-EXP:G02_empty_workspace` | Workspace/account with no imports or activity | All 20 fixed entries exist; record sets empty as allowed; CSV headers present; no attachment entry |
| `TP-EXP:G03_all_revisions` | Plans, Reviews, projections, measurement runs, metrics and reports have superseded/terminal history | Every revision/event prefix, including TradePlan lineage and ProductMeasurementRun state, is present and current pointers resolve as of cutoff |
| `TP-EXP:G04_cutoff_equal_timestamp` | Transaction before and after watermark share millisecond timestamp | Only watermark-visible transaction is included |
| `TP-EXP:G05_concurrent_edit` | Review/report edit commits after cutoff | Old history/pointer exported; new revision absent; closure complete |
| `TP-EXP:G06_concurrent_replay` | Accounting replay commits after cutoff | Old active projection, allocations, Review pair and report refs preserved |
| `TP-EXP:G07_concurrent_delete` | Workspace enters deleting during materialization before and after immutable archive/expiry-chain registration | Job is cancelled with materialization marker; absent object needs no expiry chain, while a registered exact version is revoked/frozen and its expiry fence cancellation hands cleanup to deletion; no READY URL/archive or late export-worker write |
| `TP-EXP:G08_attachment_changed_once` | Pinned object changes/disappears on attempt 1 | Attempt aborts; attempt 2 has a new cutoff and exact complete binary set |
| `TP-EXP:G09_attachment_changed_twice` | Object changes on both permitted attempts | Job fails; no partial archive or silent omission |
| `TP-EXP:G10_dangling_reference` | Review, report or context FK missing | `EXPORT_REFERENCE_DANGLING`; no archive |
| `TP-EXP:G11_cross_tenant_reference` | One child/reference targets another workspace | Generic denial/security event; zero foreign records or identifying output |
| `TP-EXP:G12_corrupt_entry` | One canonical byte changes after manifest creation | Checksum/size validation fails before round-trip import |
| `TP-EXP:G13_zip_slip_symlink` | Absolute, `..`, backslash or symlink entry | `EXPORT_ARCHIVE_UNSAFE`; no filesystem escape |
| `TP-EXP:G14_duplicate_entry` | Duplicate `manifest.json` or canonical path | Rejected before parsing either duplicate |
| `TP-EXP:G15_unicode_no_normalization` | NFC and NFD strings that display alike plus non-ASCII labels | Scalar sequences remain distinct; reserialization bytes match |
| `TP-EXP:G16_decimal_timestamp` | Negative zero, exponent, trailing zero and non-millisecond timestamp mutations | Invalid forms rejected; canonical forms round-trip exactly |
| `TP-EXP:G17_formula_injection` | Leading whitespace/control then `=,+,-,@`, quote, comma, CR/LF, Unicode | CSV opens inert in target spreadsheet tests; JSON unchanged |
| `TP-EXP:G18_purged_raw_csv` | Raw import object and temporary ImportPreview expired; durable batch copied proof, rows, staged/disposition/admitted history and hashes remain | No raw bytes/cells or ImportPreview row; explicit purge class/tombstone; copied preview proof and canonical import rows still verify |
| `TP-EXP:G19_deleted_attachment` | Historical Review join points to deleted screenshot | Join/hash plus typed tombstone present; no binary path |
| `TP-EXP:G20_context_closure` | Shared logical bar/revision reused by snapshots and fee conversion | One de-duplicated selected bar plus exact optional conflict/resolution, observation/request/batch closure; hashes verify offline |
| `TP-EXP:G21_global_subset` | Global market store has unrelated symbols/tenants | Only reference-closed public subset exported; no tenant fields on public records |
| `TP-EXP:G22_ai_absent` | AI flags always false | Empty AI sets; deterministic/domain export otherwise complete |
| `TP-EXP:G23_ai_present_deleted` | For each applicable feature branch, one retained complete bundle plus one separately deleted bundle of that feature | Retained feature-specific subject CREATE and run/input/output/citations resolve; deleted content has stable subject CREATE/DELETE plus matching AI_OUTPUT or TRANSCRIPT_DRAFT Tombstone and no content-bearing run/input/output/reference; no disabled-feature data or hidden reasoning |
| `TP-EXP:G24_pointer_mismatch` | Cached current revision disagrees with state events | Export fails `EXPORT_POINTER_MISMATCH`; cache is not trusted |
| `TP-EXP:G25_manifest_self_checksum` | Producer attempts to list manifest in `files` | Schema validation rejects recursive/self entry |
| `TP-EXP:G26_unknown_schema` | V1 reader receives a newer archive/record type | Fails `ROUND_TRIP_UNSUPPORTED_SCHEMA`, no partial import |
| `TP-EXP:G27_new_reader_old_archive` | Current reader receives committed v1 golden corpus | Full round-trip passes with byte-identical canonical entries |
| `TP-EXP:G28_round_trip_collision` | Target validation namespace non-empty or duplicate ID exists | Fails before reference materialization; no remapping |
| `TP-EXP:G29_profile_sla` | Full `export_conformance_profile_v1`, three measured runs | Each READY <=24h and passes resource/integrity conditions |
| `TP-EXP:G30_signed_delivery` | Stale auth, expired URL, READY URL and deletion revocation; crash/retry the successful gateway audit plus DOWNLOAD_AUTHORIZED notice transaction and mutate its workspace/audit/idempotency binding | Only recent-auth READY download succeeds; 15-minute/retention policy follows TP-SEC. Successful authorization atomically creates exactly one safe first-party notice with its audit, denial creates no notice, and no retry/external notifier duplicates or leaks it |
| `TP-EXP:G31_exact_version_registry` | Every current domain identifier represented, including import-preview/staged-fill/product-measurement/taxonomy-confirmation request versions and historical multi-value slots | Exact ACC/MCE/LAB/SEC identifiers and includedValues derivation are preserved; aliases and excluded-control version advertisements are rejected |
| `TP-EXP:G32_empty_and_null_sets` | Empty arrays, absent AI, null fee/context values and zero numeric values | Empty/null/zero remain distinct in JSON, CSV projection and round trip |
| `TP-EXP:G33_item_delete_before_ready` | Delete commits before reference registration, after registration, and between materialization and READY barrier | Guard generation/locks catch every interleaving; new-cutoff restart contains tombstones and no deleted content |
| `TP-EXP:G34_item_delete_after_ready` | Item in a READY archive is deleted | URL revoked; existing EXPORT_EXPIRY fence performs exact-version delete/verify and atomically reaches EXPIRED/EXPORT_ARCHIVE_EXPIRED; audit completes and a new request is required |
| `TP-EXP:G35_sla_boundary` | Every `export_sla_envelope_v1` measure at limit, then one-at-a-time limit + 1 | Boundary is STANDARD with exact due time; each +1 is OVERSIZE without rejection |
| `TP-EXP:G36_oversize_progress` | OVERSIZE job remains active beyond multiple 24-hour boundaries; crash/retry each classification, heartbeat and final transition, race deletion, mutate `tradeproof_export_control_feed_v1`/bucket/null/idempotency fields, and attempt email/webhook dispatch or post-terminal notifier work | No truncation/failure solely for size; exact idempotent first-party control-feed classification/heartbeat/final rows commit only in their guarded EXPORT transactions, contain only closed safe buckets, disappear under primary deletion and create no external/post-terminal worker; final archive still passes round trip |
| `TP-EXP:G37_product_analytics_privacy` | Same-workspace events/snapshots plus service-owned internal aggregate/definition/member/retirement controls and Restricted external projection/suppression/rotation/delivery/deletion receipt/inventory records exist | ProductAnalyticsEvent and WorkspaceProductMetricSnapshot round-trip; every internal aggregate/contribution/member/`InternalAggregateCohortRetirement` row and `internal_aggregate_product_metric_snapshot_v1`/`internal_aggregate_cohort_retirement_v1` value is absent; every `product_analytics_external_v1`, `product_analytics_external_suppression_receipt_v1` or `product_analytics_external_deletion_inventory_v1` record/payload/version value is absent |
| `TP-EXP:G38_fee_multi_observation` | One fee-conversion bar has multiple source observations and a second logical-key revision | Exact cutoff-visible lowest-sequence observation and persisted nullable resolution are exported with request/batch/conflict prefix; exporter never selects newest/current alternative |
| `TP-EXP:G39_zip64_reserved_boundaries` | ZIP size/offset values `0xfffffffe` then `0xffffffff`, count/disk values `0xfffe` then `0xffff` | Lower values use ordinary fields; reserved maxima use exact ZIP64 sentinels/extras; every general-purpose flag is `0x0800`; archive bytes match golden hashes |
| `TP-EXP:G40_tombstone_materialization` | All five receipt mappings, null/non-null reasons and idempotent retries | Deterministic `tmb_` IDs, exact time/hash/policy/reason mapping and byte-identical retry; changed receipt or missing outbox handoff fails `EXPORT_TOMBSTONE_INVALID` |
| `TP-EXP:G41_closed_pointer_registry` | Every pointer family, all allowed null cases, ProductMeasurementRun at deadline -1ms/equality/+1ms, same-millisecond reversed opaque IDs for sequenced streams including one ContextSnapshot revision scope, and one unknown family/state/key mutation | Exact pointer set/order/targets/bases derives at cutoff; measurement equality is semantic ABANDONED/TIMEOUT, every aggregate/revision stream selects greatest contiguous sequence with no timestamp/ID semantic tie-break, and unknown/missing/extra/mismatched pointer fails |
| `TP-EXP:G42_product_analytics_source_matrix` | Every product event source/measurement branch, ProductMeasurementRun START/SUCCEED/all ABANDON reasons/deadline states, composite episode key, and READY-derived `export_completed` | Exact envelope record keys, run tuple, complete event prefix and mutual terminal reference pass; export completion has no source/job key and exact retry idempotency; scalar/partial/wrong-type keys, second terminal, gap or mismatched run/event fail |
| `TP-EXP:G43_workspace_metric_digest` | Included/excluded composite record keys, reason counts, dimension variants and one late source event | Exact key shapes/canonical-byte order/partition and dimension hash pass; digest matches basis; late input creates the next immutable revision |
| `TP-EXP:G44_ai_bundle_delete_cutoff` | In each applicable feature branch, the same successful bundle exported immediately before and after DeleteAiOutput commits | Before cutoff has subject CREATE and the feature's complete run/input-reference/output/output-reference bundle; after cutoff preserves subject CREATE/DELETE and its matching AI_OUTPUT or TRANSCRIPT_DRAFT Tombstone but has no content-bearing bundle row |
| `TP-EXP:G45_projection_supersede_boundary` | Old/new episode projection share exact old `superseded_at = new.created_at = exportAsOfAt`; old business state is OPEN or CLOSED | New version alone is active, old history retains its original OPEN/CLOSED state, no synthetic SUPERSEDED value exists, and pointer/product-metric as-of selection agree |
| `TP-EXP:G46_attachment_delete_pending` | Attachment is DELETING at cutoff and DELETE_COMPLETE commits later | Descriptor is DELETE_PENDING with null path, no binary and no premature Tombstone; post-cutoff completion cannot expose deleted bytes |
| `TP-EXP:G47_attempt_generated_at_crash` | Worker crashes before and after the persisted generated-time compare-and-set, then resumes/rebuilds the same attempt | Pre-CAS resume assigns once; post-CAS resume reuses exact value; same attempt reproduces manifest/archive bytes, while a different time requires a new attempt/cutoff |
| `TP-EXP:G48_trade_episode_identity` | Unchanged replay, split, merge, removed-and-returned episode, wrong opening fill and header/projection atomicity mutations | Exact UUIDv5/header is reused or created per TP-ACC; opening fill/account/instrument/dedup and first projection time close; REMOVED header keeps history but has no active/eligibility/Review pointer until deterministic reappearance; duplicate/rewritten/fabricated header fails |
| `TP-EXP:G49_review_taxonomy_membership` | Historical review uses frozen exit/breach/emotion versions; mutate item version, taxonomy type, missing item, duplicate breach ID and emotion null coupling | Exact version/type/item closure passes; every active-version substitution or membership/null mutation fails closed without rewriting history |
| `TP-EXP:G50_csv_semantic_regeneration` | Start from valid canonical JSON, then omit/duplicate a CSV row or alter one checksummed cell and manifest hash/count consistently | Reader regeneration differs and rejects every mutation; untouched eight-file corpus is byte-equal |
| `TP-EXP:G51_deleted_attachment_metadata` | Deleted screenshot join is valid, then independently remove Attachment or AttachmentExportDescriptor while retaining Tombstone | Valid join retains both metadata records and no binary; Tombstone never substitutes for metadata and each removal fails closure |
| `TP-EXP:G52_opaque_domain_version` | V1-shaped record carries a new exact value in a declared version slot; then mutate baseline, use alias/latest or require a new shape | V1 reader preserves the registered opaque value byte-for-byte; all baseline/alias/shape mutations require or are rejected as a new schema |
| `TP-EXP:G53_ai_typed_provenance` | Feature branches: TRANSCRIPTION covers raw/retained voice; TAXONOMY_SUGGESTION covers source/version/all items; WEEKLY_SUMMARY covers exact overview/cost bindings/pointers, OVERALL dimension, null phase/timeframe, INTERVAL/object/scalar/null values, structured claims and composite episodes; each includes copied configuration and negative key/type/role/order/display/value/digest/cardinality cases | Each enabled branch independently closes exact inputs/outputs/options/hashes/schema/media/citations; duplicate report locations bind one metric ID to both pointers; setup/context same-name, bad RFC pointer, AVAILABLE display, malformed interval and scalar/current-source mutations fail; disabled branches stay empty |
| `TP-EXP:G54_delayed_cohort_lifecycle` | TP-LAB:G26 cutoffs before start, inside interval without OPEN, exact end before delayed LOCK and after atomic LOCK | Header and pointer resolve SCHEDULED, OPEN, LOCK_PENDING and LOCKED at exact bounds; stale OPEN/cache/event-only resolution fails |
| `TP-EXP:G55_ai_hash_basis_evolution` | In each applicable feature branch, old v1 input references are read after a newer source/export projection exists; WEEKLY_SUMMARY includes report binding/pointer, dimension and interval fragment bytes; mutate that branch's record/digest/fragment schema IDs, fragment and canonical-input hash | Reader selects persisted v1 bases and recomputes exact feature-specific audio/text/JSON hashes; missing/extra/reordered binding/pointer/value member and every unknown/mismatched/reinterpreted basis fails without requiring a disabled feature |
| `TP-EXP:G56_state_event_sequences` | Catalog, Review-taxonomy, setup, plan, eligibility, consent, cohort, timezone, report and experiment events share milliseconds with reverse-sorted IDs; then gap/duplicate/decreasing-time mutations | Greatest visible contiguous aggregate sequence wins every pointer/stream; no timestamp/ID tie-break changes state and each invalid stream fails |
| `TP-EXP:G57_zip64_manifest_crossover` | Prospective manifest places the first local offset one byte around `0xffffffff` while ZIP64 marker changes | Fixed-width `NONE`/`USED` does not move offsets; writer chooses one exact stable layout and archive/manifest bytes match the boundary golden |
| `TP-EXP:G58_owner_profile_history` | Header plus three profile revisions, equal-time edits, current pointer and a missing/misordered revision mutation | All IDs/numbers/idempotency/history round-trip; greatest visible revision is current and convenience view derives exactly; gap or omitted history fails |
| `TP-EXP:G59_public_projection_bytes` | TP-ACC:F27 and TP-LAB:G29 source fixtures produced independently with same-ms reversed publish IDs | Instrument/conversion catalog rows/events, Review taxonomy and behavioral taxonomy produce byte-identical records/hashes; family pointers use sequence; partial/reused/mutated versions fail |
| `TP-EXP:G60_replay_preview_closure` | TP-ACC:F19 exact preview/CONFIRM_REPLAY, then mutate proposal member/digest, mapping partition/order/cardinality, impact key, expiry, source digest and confirmation decision partition | Exact nested keys close to tenant records or the preview-local proposal set, all hashes recompute and valid confirmation round-trips; every missing/duplicate/dangling/stale/extra mutation fails without treating a proposal key as a published projection FK |
| `TP-EXP:G61_atomic_attempt_restart` | Binary/race restart before and after attempt-1 archive registration, with crashes around RESTART_ATTEMPT and duplicate event delivery | Crash exposes either old attempt only or one old-ABORTED/new-RUNNING transition; a registered old version keeps its immutable attempt tuple and reaches EXPORT_ARCHIVE_CLEANED before attempt 2 can READY. Exactly one new cutoff/current attempt exists, no job archive tuple is overwritten and no old-cutoff bytes reach READY |
| `TP-EXP:G62_mark_ready_delete_lock` | Run both serial orders of MARK_READY and Workspace delete on the shared guard lock, with both EXPORT and pre-registered EXPORT_EXPIRY chains and with no/open provider lease | Delete-first cancels and freezes every exact/inventory-found version without a grant; READY-first publishes only with live expiry fence, then deletion revokes/starts EXPIRING and hands off. An open lease permits only lookup+END+cancel marker, never dispatch/result commit. No bytes are exposed after delete commit; only verified deletion-owned finalization may append EXPIRE |
| `TP-EXP:G63_expiry_reference_saga` | Archive registration atomically creates exact EXPORT_EXPIRY subject/payload/version-hash/outbox/fence before READY; run normal 24-hour and subject-triggered expiry with an issued token, provider outage, crash after revoke, after delete and before finalization; cover FAILED/CANCELLED with no object, registered pre-READY object and retention trigger while still validating; mutate version hash/key/chain/terminal timing | No registered object reaches READY without the nonterminal expiry chain; every GET/Range GET fails after START_EXPIRY; all references reach DELETED only after exact absence. Normal EXPIRE atomically produces COMPLETE/EXPORT_ARCHIVE_EXPIRED; pre-READY/deadline cleanup FAILs with the stable code then produces COMPLETE/EXPORT_ARCHIVE_CLEANED without EXPIRE; safe-no-object has no chain. Resume is idempotent and later delete is no-op; bytes are absent by expiry, otherwise access stays revoked while job/fence remain EXPIRING/nonterminal and the retention owner is paged |
| `TP-EXP:G64_public_sequence_prefix` | Workspace references Review taxonomy v3 and cutoff catalog pointer is sequence 3; independently omit an event, target version or item/row set from sequence 1 or 2 | Complete per-family prefixes 1..3 pass and remain byte-stable; every omitted predecessor fails continuity/closure even though v3's direct record references still resolve; later unreferenced sequence 4 remains excluded |
| `TP-EXP:G65_owner_content_hash_bases` | TP-ACC:F29 and TP-LAB:G30 exact SetupPresetRevision, TradePlanRevision, ReviewRevision ordered attachment summary, WeeklyReportRevision and BehavioralExperimentRevision bases; mutate one field/order at a time | Independent producer/export reader hashes agree for valid records and every one-field/order mutation fails digest validation without rewriting historical content |
| `TP-EXP:G66_attempt_selector_integrity` | QUEUED/no-attempt, attempt-1 processing, registered old archive then atomic restart to attempt 2, READY/expiry and terminal-pre-READY cases; mutate selector/FK/outcome/timestamps/archive tuple or its payload-schema/work-type/subject-key/object-version-hash semantic binding, or add orphan/second RUNNING attempt | Exact unique/composite selector, joint archive fields, per-version resolution to the live expiry chain or its compacted marker, and state/outcome coupling pass without a permanent control-detail FK; READY job tuple equals selected SUCCEEDED attempt and all stale versions are CLEANED; every orphan, gap, overwrite, mismatch, multiple-current or READY/non-SUCCEEDED mutation fails before grant issuance |
| `TP-EXP:G67_derived_lifecycle_projection` | TradePlan direct ARM/revision-1 creation, later lineage and every terminal branch plus every Review, Upload, Attachment/descriptor, WeeklyCohort, TimezoneChangeSchedule, WeeklyReportRevision, BehavioralExperimentRevision and ProductMeasurementRun lifecycle branch; inject DRAFT or mutate armed/based-on/terminal/state/null/time/lineage | Same-cutoff event replay and pointer bytes agree for all valid branches; no persisted DRAFT/abandoned form exists, and each stale cache, wrong null/timestamp, wrong revision/event/target or historical-only COMPLETED Review fails `EXPORT_POINTER_MISMATCH` |
| `TP-EXP:G68_plan_proof_basis_closure` | TP-ACC:F30 zero/single/multiple candidates, every exclusion reason and same-boundary timestamp; ARM/revision/terminal/consume boundary evidence; mutate evaluated-plan order/key/time/reason/null triplet, candidate set and selectedCandidate | Exact nested selectors, consume event/episode, basis/candidate/status/link bytes close to included tenant records; only the byte-equal single candidate may be selected and every duplicate/missing/extra/stale/cross-workspace/one-field mutation fails |
| `TP-EXP:G69_import_resolution_payloads` | TP-ACC:F31 all four actions plus ImportRow/StagedFill/StagedFillDisposition/NormalizedFill transitions; ACCEPT dedup framing, MARK target, SET_SEQUENCE overlap component/order/digest/outer anchor and CONFIRM partition; mutate copied preview proof, staged/normalized/duplicate key, outcome, admission-only field, every payload key/order/digest/null branch | Exact payload/action/outer-reference/idempotency rules and same-workspace row/stage/disposition/fill/account/instrument/signature closure pass; NormalizedFill admission is existence/created_at only; scalar, duplicate-chain, partial group, bad topological order, stale digest, mutable admission field, raw-cell or changed-retry mutation fails |
| `TP-EXP:G70_ledger_entry_contract` | TP-ACC:F32 quote/base/third-asset fee, zero/unavailable fee, partial/final SELL and replay; delete/duplicate/reorder/split entries and mutate signs/null/conversion/UUID | Exactly two deterministic TRADE/FEE entries per allocation, contiguous sequence, UUIDv5, row formulas, recurrence and projection sums agree; every cardinality/formula/FK/order mutation fails |
| `TP-EXP:G71_fee_conversion_path_bytes` | TP-ACC:F33 direct/inverse bars with multiple observations/resolved conflicts and native/fill-rate/zero/unavailable cases; mutate path member/index/catalog/bar/observation/resolution/candidate/rate/value/current-source selection | Exact null table, one-element aligned typed key arrays, path RFC 8785 bytes, shared cutoff selector, version intervals, stored rate/value and full request provenance agree; every mutation fails without reselection |
| `TP-EXP:G72_north_star_snapshot_closure` | TP-ACC:F34 numeric range with superseded gaps/end sentinel, REGULAR/TRANSITION, eligibility precedence, exact 0.8, missing/late completion and pending replay; mutate range/ref/order/LOCK/candidate/event/partition/count/ratio/digest | Exact final cohort range, typed arrays, drilldown/copied fields/FKs, ratio18/null and input digest round-trip; pending preview leaves active evidence unchanged and every omitted/foreign/unlocked/nonfinal/stale/one-field mutation fails |
| `TP-EXP:G73_cohort_input_revision_closure` | INITIAL_LOCK plus REVIEW/ACCOUNTING/CONTEXT/BACKFILL revisions; mutate cohort/revision lock, context-only inherited as-of, episode/review order or copied scalar, Review selection/null branch, any of four context slots, taxonomy typed set and digest | Exact locks/as-of selectors, typed same-workspace/public closure, four-slot matrix, context-recovery isolation and RFC 8785 digest pass; every missing/extra/reordered/current-substituted/one-field mutation fails |
| `TP-EXP:G74_metric_snapshot_closed_payload` | Every dimension/value/status/exclusion/source branch including zero, null, infinity projection, multiple primary reasons and unavailable context; mutate partition/order/count/reason/source/value/coupling/digest | Exact dimension grammar, typed episode/source closure, closed reasons, counters, null/evidence rules and full input digest agree; malformed but reference-resolving JSON fails before round trip |
| `TP-EXP:G75_weekly_report_payload_not_renderer` | Exact seven-section REGULAR and TRANSITION reports, then missing/extra/reordered cells/panels/options, wrong section state/metric union/content hash and an injected cached rendered payload | Canonical section/cardinality/state/source closure and content hash pass; every mutation fails and request-time renderer bytes are absent from every archive entry |
| `TP-EXP:G76_context_availability_and_coverage` | All AVAILABLE quality branches and every PENDING/MISSING/NOT_APPLICABLE reason across initial and context-recovery cutoffs; mutate decision key/field/hash/time, slot coupling and each coverage category/count | AVAILABLE has snapshot/no decision; every non-AVAILABLE slot has exactly one byte-valid same-input decision; recovery cutoff remains isolated; the nine-member coverage object classifies every candidate once and all count/sum/panel-state mutations fail |
| `TP-EXP:G77_mce_resolution_revision_diagnostics` | TP-MCE tests 41-44: multiple bar observations/conflict resolutions, same-ms reverse-ID ContextSnapshot revisions, every quality/missing-interval/recompute branch, immutable ContextAlgorithmRelease and all six CONTEXT control triggers; mutate release tuple/digest/principal, sequence/prefix/candidate/selected/index/null/scope/predecessor/reason/order/coalescing/hash plus triggerId/triggerSha256/source sequence/extra member | Observation/resolution sequences, shared cutoff selection, three aligned input arrays, exact release closure/revision scope/as-of pointer, diagnostics/coverage and both hashes pass offline. Release rows round-trip; ContextEpisodeTrigger, ManualContextRecomputeRequest and work controls remain absent. Four control branches accept only authoritative triggerId, replay/upgrade only triggerSha256; malformed control allocates no work sequence and every archive/control mutation fails |
| `TP-EXP:G78_mean_r_ci_interval_profile` | TP-ACC:F35: N=0/1; `[1,3]`; `[-1.25,0.5,2.75]`; `[2,2,2]`; thirty zeros plus 31; then population-variance, binary-float, early-rounding, critical-value, bound/order/type and digest mutations | N<2 is null/INSUFFICIENT_SAMPLE; exact bounds are `[-10.706204736432095,14.706204736432095]`, `[-4.314530171362140456,5.647863504695473789]`, `[2,2]`, `[-0.959963984540054,2.959963984540054]`; interval JSON/input digest/CSV-blank bytes agree independently and every mutation fails |
| `TP-EXP:G79_import_error_detail_privacy` | TP-ACC:F36 every row-error family, column and row-level errors, exact/capped length/count boundaries; inject raw cell, filename, exception text, unknown/extra member, wrong column pair, status/null or hash mutation | Only exact eight-member `import_row_error_detail_v1` round-trips; cap sentinels, truncation bit, domain-separated hash and version slot agree independently; raw content is absent and every malformed or privacy-bearing mutation fails before staging |
| `TP-EXP:G80_product_metric_dimensions` | TP-LAB:G33 every product metric with exact OVERALL/STUDY dimension, two studies sharing a window, lowercase UUID boundaries and dimension/source/hash/revision mutations | Closed metric-to-dimension matrix, same-study ProductAnalyticsEvent coupling, RFC 8785 dimension hash, composite revision identity and input digest agree; arbitrary label/filter, cross-study source, UUID case and every unknown/extra/member mutation fail |
| `TP-EXP:G81_workspace_object_absence_evidence` | ACTIVE Workspace at multiple guard generations; each Upload kind at RECEIVE+20h/+24h, including QUARANTINED and VALIDATING stalls, ACCEPTED delayed purge and natural early trigger; screenshot/retained-voice atomic activation with source later PURGED; crash before/after each atomic verification+PURGE or verification+DELETE_COMPLETE transaction; drop/duplicate header/ACTIVATE or mutate contract/deadline/object version/generation/time/proof/event/tombstone link | Stalls append REJECT/RAW_UPLOAD_RETENTION_DEADLINE before PURGE; no proof-before-PURGE or proof-before-DELETE_COMPLETE state exists; on-time absence is by +24h and each terminal deletion closes one exact proof/receipt; prior ACCEPT remains valid after source PURGED; every mutation fails and deletion/work-fence/ingest controls stay absent |
| `TP-EXP:G82_ai_confirmation_closure` | TRANSCRIPTION covers plan/review targets, keep-original false/true and ACTIVE/DELETED subject; TAXONOMY_SUGGESTION covers all types and both states; mutate processor registration binding, confirmation request schema/hash, full-replacement hash, result revision content hash, exact confirmation hash objects, selected suggestion triples/order, subject key/state, active output mapping, deleted surviving shape/result/public item/Tombstone hash, attachment/source upload and idempotency; inject AiConfirmationCommandIntent/Receipt or AiProcessor registry/evidence | Both confirmations close to the stable subject and recomputed request/result/source/content hashes; ACTIVE proves exact source mapping, DELETED proves only immutable confirmation/result/public-item/Tombstone invariants and never reconstructs content; all processor/command controls remain absent and every branch-specific mutation fails |
| `TP-EXP:G83_metrics_decimal_profile` | TP-ACC:F37 exact quote/risk vectors 1/3, 2/3, -1/6; odd/even R samples, count/profit/fee ratio 1/3, payoff wins `[1,2]` and losses `[-1,-3,-5]`, complement, zero denominator, final half-even tie, overflow and unrounded-R/binary-float mutations | `r_multiple18` is `0.333333333333333333`, `0.666666666666666667`, `-0.166666666666666667`; downstream values consume only those bytes; count/profit 1/3 is `0.333333333333333333`, payoff `0.5`, fee percent `33.333333333333333333`; exact numerator/denominator/unit/slot/hash agree and early rounding, alternate scale, unrounded reuse or overflow publication fails |
| `TP-EXP:G84_metric_contract_matrix` | TP-LAB:G34 every report/supporting metric across each allowed dimension/policy, repeating R/count/fee ratios, value COMPLETE/NORMAL, mathematical-null COMPLETE/UNDEFINED, upstream UNAVAILABLE/UNAVAILABLE and every evidence threshold; mutate formula version, policy, unit, type, dimension, numerator/denominator, reason/state/evidence, context/review population or digest | The exhaustive matrix and `metrics_decimal_v1` bytes match independent producers; complement baselines use exact counts; unlisted pair, forbidden report POSITIVE_INFINITY, denominator drift, wrong population and every single-field mutation fail before report/content hash and round-trip staging |
| `TP-EXP:G85_average_cost_rounding` | TP-ACC:F38 open B/Q = 1/3, exact half-even ties `1/(2*10^18)` and `3/(2*10^18)`, partial SELL whose later digit changes under rounded-average reuse, full close and final average overflow | Persisted averages are `0.333333333333333333`, `0`, `0.000000000000000002`; partial cost uses exact pre-fill rational and one final round, full close leaves Q/B zero and average null; alternate scale/reuse, null coupling and overflow archive mutations fail |
| `TP-EXP:G86_reconciliation_rate_boundary` | TP-ACC:F39 numerator/denominator 1/3, 98/100, 981/1000, 99/100 and 0/0, huge cross-products, alias rows and binary-float/rounded-status/counter mutations | Rates are `0.333333333333333333`, `0.98`, `0.981`, `0.99`, null; exact 98% is NEEDS_ATTENTION, strict above-98/below-1 is PARTIAL absent blocking conflict, zero denominator is NEEDS_ATTENTION; integer cross-products, row counts, CSV bytes and canonical JSON agree and every alternate path fails |
| `TP-EXP:G87_deletion_generation_worker_fence` | Queue at guard generation 7 creates exact EXPORT/MATERIALIZE chain; archive registration creates exact EXPORT_EXPIRY/REVOKE_DELETE_VERIFY chain at generation 7; account FENCE advances Workspace to 8 before materialization boundaries and independently before/after READY expiry work. Exercise RESERVED/DISPATCHED crashes and deletion during each, both work types' same-schema retry/conflict/semantic dedup before/after compaction, distinct future payload-schema and marker-profile versions, and mutate either chain, lease, schema/profile/digest/HMAC/key-version/result or generation | Preterminal workers resolve the phase-appropriate chain and ACTIVE+7. Generation change revokes/freezes exact versions; an open lease permits only lookup, END and cancellation marker, never new dispatch/result commit. Schema-qualified uniqueness/dedup has no cross-payload-version alias; a marker-profile bump changes source evidence bytes but preserves semantic/retry identity. Exact markers still drain after compaction; missing expiry chain at READY, unowned object, late domain write, new retry sequence or any mutation fails |
| `TP-EXP:G88_ai_output_subject_lifecycle` | For every output kind, export CREATE/ACTIVE then CREATE+DELETE/DELETED at equal-time and later-time cutoffs; mutate composite receipt type/ID, sequence, event time, subject kind/hash/ID, pointer and active/deleted bundle/Tombstone presence | Subject ID/kind/hash/time equals active output; DELETE uses exact receipt composite identity and matching Tombstone while content-bearing bundle is absent; full history/pointer round-trip byte-identically and every gap, mismatch or fabricated receipt field fails |
| `TP-EXP:G89_object_ingest_reservation_handoff` | For RAW_UPLOAD and SANITIZED_ATTACHMENT, RESERVE atomically creates the exact `OBJECT_INGEST_FINALIZE` control job/fence/ENQUEUE with payload `{leaseGeneration,purpose}`; crash before conditional provider create, after create/before RECORD_BYTES and immediately before/after TRANSFER; replay the bound capability, attempt another key or extra immutable version, mutate control subject/payload/schema/hash, purpose/kind/target/version/hash/size, duplicate transfer, reach 15-minute abort/1-hour absence and exercise activated-shell cleanup before/after write expiry and second inventory; race Workspace deletion at each boundary | No capability or provider write exists without the matching fence. Replay is rejected before write and a create crash is recovered under that fence by inventory without a second create. Transfer first revokes capability and proves exactly the recorded version, moves one locator/version tuple into exactly Upload+RECEIVE+lease or Attachment+ACTIVATE+lease, and atomically clears the reservation locator/metadata; an extra/changed version aborts and proves all versions absent. Aborted reservation reaches ABORT_VERIFY plus COMPLETE/`INGEST_ABORT_ABSENCE_VERIFIED`; activated reservation reaches a clean post-expiry second inventory plus COMPLETE/`INGEST_ACTIVATED_CLEAN`. Each terminal creates the versioned marker, or a deletion race atomically CANCELLED_DELETION-hands every reserved-key version to TEMPORARY_OBJECTS before its marker. Shell purge occurs within 24 hours only after that marker/handoff, expiry, revocation and required inventory; every reservation/capability/staging/control row stays non-exported and every partial/replay/orphan/mismatch fails |
| `TP-EXP:G90_workspace_product_metric_output_matrix` | TP-LAB:G35 every product metric as PROVISIONAL and every FINAL value/null branch; ratio denominator-zero/count boundaries and half-even ties, valid/abandoned/missing measured runs with complete ProductMeasurementRun/state/event closure, retention 0/1, adoption pre-zero/nonzero; mutate run prefix/tuple/deadline/pointer, status, type, each typed value/count, reason, object member and digest | Exact `product_measurement_run_v1` history plus `product_metrics_v1` type/value/count/null matrix and round18 bytes match two independent producers and survive canonical round trip; PRODUCT_MEASUREMENT_TIMEOUT controls remain absent, and partial PROVISIONAL KPI, open-final run, mean-of-ratios, binary float, wrong reason, extra typed field and every mutation of the sole PRE_PERIOD_ZERO exception fail before pointer publication |

## 17. Definition of Done

Implementation is complete only when:

1. Export request/download authorization, tenant scoping, idempotency and deletion races pass `TP-SEC` and this contract.
2. Every successful job captures one explicit cutoff/watermark; all pages, object pins, history and pointers are consistent with it.
3. The closed allowlist exports all durable canonical workspace state, superseded history and required shared public closure, with no other-tenant or unrelated global record.
4. Raw CSV and other purged bytes are never claimed or fabricated; typed tombstones make deletion/purge state explicit.
5. All 20 fixed entries, dynamic binary paths, ZIP metadata and deterministic ordering match v1 exactly.
6. JSON is RFC 8785/UTF-8 canonical with exact timestamps, decimals, Unicode, nulls and stable record ordering.
7. Manifest record counts, media types, sizes and SHA-256 values verify exact uncompressed bytes and never checksum the manifest itself.
8. Every retained CLEAN/ACTIVE attachment is present once with matching immutable object version/hash; every allowed deleted binary is represented without resurrection.
9. All eight CSV tables follow their exact columns/order and formula-injection fixtures pass; product and docs label CSV non-lossless.
10. Every foreign key resolves to an included same-workspace record, required shared-public record, allowed tombstone or permitted scalar public reference.
11. The isolated `tradeproof_export_round_trip_v1` reader preserves IDs, revisions, pointers, hashes, canonical report payloads and binaries, with byte-equal canonical reserialization; disposable request-time renderer bytes are never exported.
12. Old-reader/new-schema and new-reader/v1 compatibility fixtures pass, and no unknown data is silently ignored.
13. Core export conformance passes every applicable `TP-EXP:G01` through `TP-EXP:G90` fixture, including mandatory AI-empty `G22`, under the closed applicability rule in section 16. Each enabled AI feature additionally passes its own branch of `G23`, `G44`, `G53`, `G55` and `G88`; TRANSCRIPTION and TAXONOMY_SUGGESTION also pass their own G82 branch. Disabling one feature is not grounds to skip CORE or another enabled feature's branch.
14. Three measured `export_conformance_profile_v1` runs meet the <=24-hour READY SLA and documented resource/integrity conditions.
15. Operational metrics, synchronous first-party control-feed notices, audit evidence, signed delivery and archive expiry contain no user content, create no external notification worker and meet `TP-SEC`.
