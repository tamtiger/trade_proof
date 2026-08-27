# ADR 0007: AI Processor

- Status: Accepted
- Date: 2026-08-27
- Owner: TamNT167

## Context

AI is optional in MVP. Core release must work with all AI flags disabled and pass `TP-SEC:AI-00`.

## Decision

Keep `voice_transcription_enabled`, `ai_taxonomy_enabled` and `ai_weekly_summary_enabled` disabled for pilot baseline. Do not configure an AI processor, credential, outbound route or queue until the relevant consent, grounding, eval, deletion and processor-copy contracts pass.

## Alternatives

- Enable AI summary early: rejected because deterministic Weekly Lab is the core path.
- Use AI for finance/reconciliation: prohibited by Product Brief and `TP-SEC`.

## Security/privacy impact

No user data leaves the product for AI in the core profile. UI must not expose active AI controls while flags are disabled.

## Rollback

If an AI feature is later enabled, create a separate ADR amendment with processor, region, no-training terms, eval evidence, retention, deletion inventory and fallback behavior.

