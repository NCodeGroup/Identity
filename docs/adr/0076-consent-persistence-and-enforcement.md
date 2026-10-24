# 76. Durable consent — a first-class persisted aggregate, enforced at authorize

- **Status:** Accepted (amended by
  [ADR-0229](0229-a-refused-end-user-cannot-sign-in-and-the-client-is-answered-access-denied.md): a consent denial
  answers the parked authorization request with access_denied)
- **Date:** 2026-10-11
- **Deciders:** NCode Group

## Context

The authorization endpoint negotiated `prompt`, validated the subject, and audited the grant/deny decision, but it
never checked or recorded **consent**: `DefaultAuthorizeHandler` carried a bare `// TODO: check consent`, so every
authenticated subject was authorized for whatever scopes a client asked for, every time. The interactive surface shipped
a consent _page_ (`apps/auth`, the `consent-allow` / `consent-deny` locators from
[ADR-0062](0062-opt-in-interactive-login-consent-ui.md)), but it was a stopgap: it displayed a generic "an application
is requesting access" message, posted a decision the server merely redirected on, and an unused
`InteractiveAuthenticationOptions.AutoApproveConsent` placeholder stood in for real enforcement. Durable consent
(backlog `F2`) and the remaining shared-platform surfaces (backlog `U3`) were the open work.

The natural temptation was to persist consent the way [ADR-0074](0074-session-management-and-logout.md) persisted the
end-user **session** — as a `PersistedGrant` payload over the existing grant store, with no new table or migration. But
consent is **not** the same kind of record as the grants that store holds. The grant store is a one-stop shop for
**ephemeral, operation-scoped** artifacts — authorization codes, refresh tokens, device codes, continuations, the
session itself — that are hash-keyed, single-use or expiring, and **bulk-revoked on logout**:
`DefaultRevokeGrantsOnEndSessionHandler` calls `IGrantStore.RevokeWhereAsync(subjectId, clientId: null, …)`, which
revokes _every_ grant a subject holds. Consent has the opposite lifecycle: it is **durable, cross-session, upserted in
place by a natural (tenant, client, subject) key, and must survive logout** — you do not re-consent on every login — and
it is a **user-manageable** record (the future "authorized applications" surface enumerates and revokes it). Forcing
consent into the grant store would either be silently wiped by the logout bulk-revoke, or require special-casing the
grant type out of that sweep and inventing re-activation semantics for a deterministic key the consume-once grant model
never needed.

This is a greenfield, pre-release family ([ADR-0025](0025-pre-release-posture-and-auth0-parity-plus.md)); the standing
posture is to choose the cohesive long-term model over the minimal patch, even when it means new schema and public-API
churn.

## Decision

**Model durable consent as its own first-class persisted aggregate — a dedicated `PersistedConsent` DTO, `IConsentStore`,
and `ConsentEntity` table (with an EF migration) — distinct from the ephemeral grant store. Enforce it at the authorize
endpoint behind a client `require_consent` setting (and `prompt=consent`), routing a missing decision to the interactive
consent surface through a dedicated mediator seam, and realize the consent screen (plus the remaining Playground and
conformance surfaces) as apps on the shared `@ncode/*` frontend platform.**

Concretely:

- **Consent is a dedicated aggregate, not a grant.** `PersistedConsent` (tenant id, client id, subject id, granted
  scopes, created/updated timestamps, concurrency token) is persisted through `IConsentStore` and the
  `ConsentEntity` table, keyed uniquely by `(TenantId, ClientId, NormalizedSubjectId)` with a `(TenantId,
NormalizedSubjectId)` index for subject enumeration. `SaveAsync` **upserts** (widening granted scopes in place); the
  table is **not** touched by session termination, so consent outlives logout. Because it is its own table, the future
  consent-management UI and per-client revocation get a clean, queryable home rather than an opaque hashed blob.
- **`IConsentService` is the single seam.** `DefaultConsentService` (in the authentication layer, alongside the session
  service) exposes `HasConsentAsync` (does the recorded consent already cover every requested scope?), `GrantConsentAsync`
  (union-merge + persist), `RevokeConsentAsync`, and `ResolvePendingConsentAsync`.
- **Enforcement lives at authorize.** `DefaultAuthorizeHandler` checks consent after the subject is validated: when the
  client's `require_consent` setting is on **or** the request carries `prompt=consent`, a subject whose recorded consent
  does not cover the requested scopes yields a new `AuthorizeDisposition.ConsentRequired` — unless `prompt=none` forbids
  interaction, which instead returns the OIDC `consent_required` error (and audits the denial). `require_consent`
  defaults to `false` (a floor-merged client/tenant setting), so the change is opt-in and non-breaking.
- **Consent routing mirrors the login challenge, through its own command.** The endpoint logic parks the same
  continuation it uses for a login challenge and sends a `ConsentChallengeCommand`. The core registers a no-op default
  handler that leaves the command **unhandled** (the endpoint then degrades to `interaction_required`); the interactive
  module **replaces** it with a handler that redirects the user agent to `/account/consent` carrying the continuation as
  the return URL. The server-driven cookie-challenge flow is unchanged — the SPA is only the form surface.
- **The consent surface resolves its own authoritative request.** The interactive module exposes
  `GET /account/consent/details?ReturnUrl=…`, which calls `ResolvePendingConsentAsync` to resolve the pending
  authorization request (client + requested scopes) **without consuming** the continuation — the same pattern
  IdentityServer's `IIdentityServerInteractionService.GetAuthorizationContextAsync(returnUrl)` uses.
  `POST /account/consent` re-resolves the request authoritatively (never trusting posted scopes), records the decision
  for the cookie-resolved subject via `GrantConsentAsync` on allow, and redirects to the continuation to resume; a deny
  answers the parked authorization request with `access_denied` at the client's redirect URI
  ([ADR-0229](0229-a-refused-end-user-cannot-sign-in-and-the-client-is-answered-access-denied.md)). The `apps/auth`
  consent form fetches those details to show the real client and scopes while keeping the `consent-allow` /
  `consent-deny` locators.
- **The remaining surfaces join the shared platform.** The consent screen stays an `apps/auth` surface, and the
  Playground (`apps/playground`) and conformance (`apps/conformance`) surfaces are scaffolded as apps on the same
  `@ncode/*` platform ([ADR-0064](0064-frontend-presentation-platform.md)) rather than bespoke UIs.

## Consequences

- **A new table and migration.** `Consents` is added to the core model and a `AddConsents` migration ships in the
  host database project. This is the deliberate cost of treating consent as its own aggregate; the dev database is
  volume-reset ([ADR-0073](0073-postgres-dev-database-and-aspire-migration-worker.md)), so no data migration is needed.
- **Consent survives logout by construction.** Because consent is not a grant, the end-session bulk-revoke never
  touches it — the correct semantics (logout ends the session, not standing authorizations) fall out for free, and the
  grant store's consume-once model is left unburdened by a durable, deterministic-keyed record it was never shaped for.
- **Opt-in, non-breaking enforcement.** With `require_consent` defaulting off, existing deployments behave as before
  until a tenant/client turns it on (or a client sends `prompt=consent`); a deployment that enables it without an
  interactive consent surface degrades to `interaction_required` rather than failing hard.
- **`AutoApproveConsent` is removed.** The unused placeholder option is deleted (public-API churn is expected
  pre-release); "skip the consent screen" is now simply `require_consent = false` for that client.
- **Follow-ups remain.** Per-scope (granular) consent toggling, friendly scope descriptions / client display name on
  the consent screen, and the end-user "authorized applications" management + revocation surface are layered onto this
  aggregate additively; they are tracked in [`BACKLOG.md`](../../BACKLOG.md).
