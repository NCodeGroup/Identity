# 1081. Consent is a durable record of decided and granted scopes, enforced at the authorization endpoint

- **Status:** Accepted
- **Date:** 2026-10-12
- **Deciders:** NCode Group

## Context

The authorization endpoint negotiates `prompt`, validates the subject and audits the grant or deny decision. A subject
who is authenticated is authorized for the scopes a client asks for only when the subject has consented to them, and
that consent has to be remembered: a subject does not consent again on every login.

Consent does not have the lifecycle of the artifacts in the grant store. That store holds ephemeral, operation-scoped
records (authorization codes, refresh tokens, device codes, continuations and the end-user session) that are
hash-keyed, single-use or expiring, and revoked in bulk on logout: `DefaultRevokeGrantsOnEndSessionHandler` calls
`IGrantStore.RevokeWhereAsync(subjectId, clientId: null, …)`, which revokes every grant a subject holds. Consent is
durable and cross-session, is upserted in place by a natural `(tenant, client, subject)` key, must survive logout, and
is a record the end user manages. As a grant payload it would be swept away by that revocation, or the grant type would
need special-casing out of the sweep and a re-activation semantics the consume-once model never needed.

Letting a subject grant a subset of the requested scopes forces the endpoint to issue a narrower scope set than was
requested (OIDC down-scoping), and it raises a re-prompt question: once a subject has decided on a scope, granted or
denied, a later identical request must not prompt for it again, yet a request that introduces a new scope must. A
record of only the granted scopes cannot tell "denied" from "never asked". The resumed continuation after the consent
screen still carries `prompt=consent` and must not loop back into the screen.

The family is greenfield and pre-release
([ADR-1025](1025-the-family-is-greenfield-and-pre-release-tracks-its-public-api-and-models-auth0-parity-plus.md)), so
the cohesive model wins over a minimal patch.

## Decision

**Consent is its own persisted aggregate that records which scopes the subject was asked to decide and which they
granted. The authorization endpoint prompts only when a request introduces an undecided scope, and otherwise issues the
requested scopes narrowed to the granted subset.**

- **Consent is an aggregate, not a grant.** `PersistedConsent` holds the tenant, client and subject ids,
  `RequestedScopes` (the decided set), `GrantedScopes`, created and updated timestamps and a concurrency token. It is
  stored through `IConsentStore` and `ConsentEntity`, unique on `(TenantId, ClientId, NormalizedSubjectId)` and indexed
  on `(TenantId, NormalizedSubjectId)` for a subject's enumeration. Logout and session termination never touch it; it is
  removed with its client or principal ([ADR-1212](1212-deleting-a-client-resource-server-principal-or-tenant-removes-what-it-owns.md)).
- **`IConsentService` is the single seam.** `DefaultConsentService` exposes `ResolveConsentAsync`, which returns
  `ConsentResolution(IsDecided, GrantedScopes)` (`IsDecided` is true when every requested scope is in the decided set,
  and `GrantedScopes` is the requested-ordered intersection with the granted subset), `GrantConsentAsync(requested,
  granted)`, `RevokeConsentAsync`, `GetAuthorizedApplicationsAsync` and `ResolvePendingConsentAsync`.
- **A grant grows the decided set and replaces the granted subset within the request.** `GrantConsentAsync` makes the
  decided set the decided set united with the requested scopes, and replaces the granted scopes inside the requested
  set, so re-consenting can revoke a scope that was granted while the decisions about scopes outside the request are
  kept. A granted scope outside the request is never trusted.
- **Enforcement lives at the authorization endpoint.** After `DefaultAuthorizeHandler` validates the subject, it checks
  consent when the client's `require_consent` setting is on or the request carries `prompt=consent`. `require_consent`
  defaults to `false` and merges from client and tenant with the `Or` floor. The handler prompts when
  `(prompt=consent && !IsContinuation) || !IsDecided`: `prompt=consent` forces a fresh decision on the initial request
  and not on the resumed continuation, which breaks the resume loop. Under `prompt=none` a needed prompt is answered
  `consent_required` and audited; otherwise the handler returns an `AuthorizeDisposition` with its `ConsentRequired`
  flag set.
- **Down-scoping flows through one override.** Once consent is satisfied, `IAuthorizationRequest.GrantedScopes`
  overrides `Scopes`. The override is serialized in the authorization-request envelope, so the authorization code and
  the tokens carry only consented scopes and the token endpoint cannot re-expand them. When it is null, the requested
  scopes are issued.
- **Consent routing mirrors the login challenge.** The endpoint parks the continuation and sends
  `ConsentChallengeCommand`. The core's no-op handler leaves it unhandled, which degrades to `interaction_required`; the
  interactive module redirects to `/account/consent` with the continuation as `ReturnUrl`.
- **The consent endpoints never trust the browser.** `GET /account/consent/details?ReturnUrl=…` resolves the pending
  request through `ResolvePendingConsentAsync` without consuming the continuation. `POST /account/consent` resolves it
  again and never trusts posted scopes. Allow records the decision for the subject the cookie names and resumes the
  continuation. Deny answers the parked authorization request with `access_denied` at the client's redirect URI
  ([ADR-1229](1229-a-blocked-disabled-or-deleted-end-user-loses-its-sessions-and-is-refused-with-access-denied.md)).
- **The consent subsystem owns presentation.** The consent screen shows the client's `client_name` and each scope's
  description from `IScopeDescriptionService`, with a checkbox per scope that is pre-checked for the granted scopes and
  the `consent-allow` and `consent-deny` locators. It lives in the `apps/auth` bundle.
- **The user manages standing consent.** The `/account/applications` page, its JSON list and its antiforgery-protected
  revoke `POST` let the end user review each application's granted scopes and revoke it through `RevokeConsentAsync`.

## Options considered

- **Consent as a grant-store payload.** It needs no table, but the subject-wide and session-wide revocation sweeps it,
  and a durable record keyed by a natural key does not fit the consume-once model. Rejected.
- **Storing only the granted scopes.** It cannot tell a denied scope from a scope that was never asked, so a client
  that keeps requesting a declined scope would prompt the subject every time. Rejected.
- **A dedicated aggregate with a decided set and a granted set.** Consent survives logout, answers the re-prompt
  question and gives the management surface a queryable home. Chosen.

## Consequences

- Consent outlives logout by construction, and the grant store is not burdened with a durable, naturally keyed record.
- A client that keeps requesting a declined scope does not re-prompt the subject, a genuinely new scope does, and the
  consent screen appears once per decision.
- Down-scoping is enforced end to end: the narrowed set is the code's scope set, so the token endpoint, introspection
  and the emitted claims see only consented scopes.
- Enforcement is opt-in through `require_consent`. A host that enables it without an interactive consent surface
  degrades to `interaction_required` instead of failing.
- `DefaultConsentService` depends on the client resolver and `IScopeDescriptionService` for its presentation methods,
  which keeps presentation in one place.
- Tests cover the decided and granted semantics, the prompt rule, down-scoping through the code, and the consent and
  applications endpoints.

## References

- Code: `PersistedConsent`, `IConsentStore`, `ConsentEntity`, `IConsentService`, `DefaultConsentService`,
  `ConsentResolution`, `IScopeDescriptionService`, `DefaultAuthorizeHandler`, `AuthorizeDisposition`,
  `IAuthorizationRequest.GrantedScopes`, `ConsentChallengeCommand`
- [ADR-1062](1062-the-op-serves-an-opt-in-interactive-sign-in-and-consent-surface-with-a-pluggable-credential-verifier.md): the interactive surface that
  serves the consent screen
- [ADR-1380](1380-logout-ends-the-session-found-in-server-state-confirmed-when-unsure-and-notifies-relying-parties.md): the session whose logout leaves consent in place
- [ADR-1212](1212-deleting-a-client-resource-server-principal-or-tenant-removes-what-it-owns.md),
  [ADR-1229](1229-a-blocked-disabled-or-deleted-end-user-loses-its-sessions-and-is-refused-with-access-denied.md)
- Issue [#40](https://github.com/NCodeGroup/identity/issues/40)
