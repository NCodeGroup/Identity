# 81. Granular per-scope consent and the self-service authorized-applications surface

- **Status:** Accepted (refines [ADR-0076](0076-consent-persistence-and-enforcement.md))
- **Date:** 2026-10-12
- **Deciders:** NCode Group

## Context

[ADR-0076](0076-consent-persistence-and-enforcement.md) established durable consent as a first-class persisted
aggregate — a `PersistedConsent` record keyed by `(tenant, client, subject)`, an `IConsentService` seam, enforcement at
the authorize endpoint behind the client `require_consent` setting (and `prompt=consent`), a `ConsentChallengeCommand`
routing seam, and an interactive consent screen on the shared `apps/auth` surface. That record stored only the scopes
the subject **granted**, and enforcement was all-or-nothing: the subject consented to the entire requested set or the
flow could not proceed. The consent screen presented a generic client identifier and raw scope names, and there was no
end-user surface to review or revoke standing consent. ADR-0076 explicitly deferred three follow-ups: per-scope
(granular) consent, friendly scope/client display on the screen, and the self-service "authorized applications"
management surface.

Granular consent is the architecturally significant piece, because letting the subject grant a **subset** of the
requested scopes forces the authorization endpoint to issue a **narrower** scope set than requested (OIDC
down-scoping), and it raises a re-prompt question the all-or-nothing model never had: once the subject has decided on a
scope (granted **or** denied it), a later identical request must not keep re-prompting for the scope they already
declined, yet a request that introduces a **new** scope must prompt. Storing only granted scopes cannot distinguish
"never asked" from "asked and denied", so it cannot answer that question; and resuming the parked continuation after the
consent screen (which, under `prompt=consent`, still carries `prompt=consent`) must not loop back into the consent
screen.

## Decision

**Track two scope sets on the consent record — the scopes the subject was asked to decide on (the "decided" universe)
and the subset they granted — so the authorization endpoint prompts only when a request introduces a previously-
undecided scope and otherwise issues the intersection of the request's scopes with the granted subset (down-scoping).
Render the consent screen and a new self-service authorized-applications surface from the enriched consent subsystem,
which resolves client display names and scope descriptions.**

Concretely:

- **The consent record carries a decided set and a granted set.** `PersistedConsent` gains `RequestedScopes` (the
  decided universe) alongside the existing `GrantedScopes`; the `ConsentEntity` gains a `RequestedScopesJson` column
  (an `AddConsentRequestedScopes` migration ships in the host database). `GrantConsentAsync(requestedScopes,
grantedScopes)` grows the decided set monotonically (`decided ∪ requested`) and replaces the granted subset **within
  the requested set** (`(granted \ requested) ∪ granted`), so re-consenting can **revoke** a previously-granted scope
  while decisions about scopes outside the current request are preserved. A granted scope that was not part of what was
  requested is never trusted.
- **The authorize endpoint prompts on undecided scopes and down-scopes otherwise.** `IConsentService.ResolveConsentAsync`
  returns `ConsentResolution(IsDecided, GrantedScopes)`: `IsDecided` is true when every requested scope is contained in
  the decided set, and `GrantedScopes` is the requested-ordered intersection with the granted subset.
  `DefaultAuthorizeHandler` computes `mustPrompt = (prompt=consent && !IsContinuation) || !IsDecided` — `prompt=consent`
  forces a fresh decision on the initial request but **not** on the resumed continuation (the subject just completed the
  screen), which breaks the resume loop; when consent is satisfied it narrows the request's issued scopes to the granted
  subset. (`HasConsentAsync` is replaced by `ResolveConsentAsync`; public-API churn is expected pre-release.)
- **Down-scoping flows through a single request override.** `IAuthorizationRequest` gains a mutable `GrantedScopes`
  override (serialized in the authorization-request envelope, mirroring the existing `IsContinuation` flag): when set, it
  overrides `Scopes`, so the narrowed set flows into the authorization code and the issued tokens — and, because the code
  captures the narrowed `Scopes`, the token endpoint cannot re-expand beyond what was consented. When null, no narrowing
  is applied and the originally-requested scopes are issued.
- **The consent subsystem owns presentation.** `IConsentService.ResolvePendingConsentAsync` resolves the pending request
  enriched with the client's friendly display name (the `client_name` registration metadata) and each requested scope's
  friendly description (resolved by a new `IScopeDescriptionService` from the owning resource servers) and already-granted
  state, so the consent screen pre-selects previously-granted scopes. `GetAuthorizedApplicationsAsync` enumerates the
  subject's standing consents with the same enrichment.
- **The consent screen is granular; a new surface manages applications.** The `apps/auth` consent screen renders a
  per-scope checkbox (pre-checked for already-granted scopes, labelled with the friendly description) and posts the
  selection, keeping the `consent-allow` / `consent-deny` automation locators. A new OP-hosted authorized-applications
  page (`/account/applications`, with a JSON list endpoint and an antiforgery-protected revoke POST) lets the end-user
  review each application's granted scopes and revoke it, calling `RevokeConsentAsync`.

## Consequences

- **A column and a migration.** `PersistedConsent.RequestedScopes` / `ConsentEntity.RequestedScopesJson` and the
  `AddConsentRequestedScopes` migration are the cost of distinguishing "decided" from "granted". The dev database is
  volume-reset ([ADR-0073](0073-postgres-dev-database-and-aspire-migration-worker.md)), so no data migration is needed.
- **Correct re-prompt semantics without a resume loop.** Because the decided set records denials, a client that keeps
  requesting a previously-declined scope does not re-prompt the subject, while a genuinely new scope does; and because
  `prompt=consent` is neutralized on the continuation, the consent screen is shown exactly once per decision.
- **Down-scoping is enforced end to end.** The narrowed scope set is the authorization code's scope set, so the token
  endpoint, introspection, and emitted claims all see only the consented scopes — the client cannot recover a denied
  scope at token exchange.
- **The consent subsystem depends on client and scope resolution.** `DefaultConsentService` now resolves client display
  names (via the client resolver) and scope descriptions (via `IScopeDescriptionService`) for its presentation methods;
  this keeps presentation cohesive in one place at the cost of those dependencies.
- **`F2` is complete.** The granular, friendly, and self-service follow-ups ADR-0076 deferred are delivered; the backlog
  item is removed.
