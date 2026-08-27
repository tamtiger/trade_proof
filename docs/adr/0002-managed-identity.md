# ADR 0002: Managed Identity

- Status: Accepted
- Date: 2026-08-27
- Owner: TamNT167

## Context

`TP-SEC` requires managed OIDC or magic link, no local passwords, byte-exact issuer identity keys, recent re-authentication for sensitive actions and exact one-user-one-workspace bootstrap.

## Decision

Use a managed OIDC provider with email magic link support for production. The implementation contract is provider-neutral but the first pilot configuration targets Auth0 Customer Identity Cloud with Authorization Code + PKCE. Local development uses a non-production fake identity adapter that cannot be enabled in production.

## Alternatives

- First-party password authentication: rejected by `TP-SEC`.
- Email as stable owner key: rejected because ownership must use `(issuer, subject)`.
- Multiple identity providers at launch: deferred until the issuer registry and deletion inventory prove one provider.

## Security/privacy impact

The app stores no password/hash/reset data. Issuer and subject are treated as immutable byte-exact keys. Re-authentication remains delegated to the managed provider.

## Rollback

A different managed OIDC provider can replace Auth0 before pilot if it supports exact issuer metadata, subject stability, callback replay safety, deletion/unlink inventory and recent-auth evidence.

