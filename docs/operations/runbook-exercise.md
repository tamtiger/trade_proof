# Runbook exercise

Phase 8 records tabletop exercises for the local candidate. These exercises do not replace a pilot-equivalent environment drill; they make the required owner, trigger, evidence and rollback path explicit before pilot onboarding.

## Incident exercise

- Trigger: suspected account compromise, cross-tenant exposure, secret exposure or processor retention issue.
- Owner: security owner.
- First response: revoke affected sessions, block sensitive operation paths and preserve audit evidence.
- Communication: record user/authority notification decision and timeline.
- Expected evidence: incident ticket, severity, affected component, containment time, communication decision and post-incident review owner.

## Backup/restore exercise

- Trigger: backup restore request or disaster recovery rehearsal.
- Owner: platform owner.
- Target: encrypted daily backup in Azure Southeast Asia.
- RPO <=24 hours.
- RTO <=8 hours.
- Restore rule: deletion tombstones are applied before traffic resumes.
- Expected evidence: backup timestamp, restore environment, tombstone application log, smoke test result and rollback decision.

## Deletion exercise

- Trigger: owner requests Delete TradeProof account, export expiry or retention purge.
- Owner: data rights owner.
- Expected path: request, fence, drain queued/running jobs, verify object/cache/index absence, apply tombstone and reject old callback.
- Expected evidence: `workspace_deletion_v1` inventory hash, terminal marker drain, absence verification and restore tombstone check.

## Processor dependency exercise

- Trigger: analytics processor, identity provider, AI processor or monitoring backend cannot provide deletion/retention proof.
- Owner: security owner.
- Expected path: disable or retire affected processor generation, preserve current local core flow, open vendor ticket and update disclosure before pilot.
- Expected evidence: processor ticket, no-training/retention statement, deletion SLA proof, subprocessor/location review and closure decision.
