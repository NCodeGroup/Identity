# Backlog — completed & retired items (archive)

This file is the **read-only archive of backlog items that have been completed or retired**. It is the companion to
[`BACKLOG.md`](BACKLOG.md): that file is the single, primary view of **pending** work; this file preserves the full
original text of every item that has **left** that view, so the richer problem/design context survives beyond the terse
[`CHANGELOG.md`](CHANGELOG.md) line that closed it.

**Why this exists.** Backlog IDs outlive their entries — ADRs, the [`CHANGELOG.md`](CHANGELOG.md), and code comments
reference a number long after the item is done. Previously a completed entry was simply **deleted** from
[`BACKLOG.md`](BACKLOG.md), so its original wording survived only in git history. This archive recovers that text and
keeps it discoverable without `git` archaeology, per
[ADR-0008](docs/adr/0008-conventions-and-lessons-live-in-the-repository.md) (conventions, reasoning, and recurring work
live in the repository). It realizes backlog item `X2`.

**How entries get here.** When a pending item in [`BACKLOG.md`](BACKLOG.md) is **fully** completed (or retired by being
absorbed into another item), move its full text here **in the same change** — do **not** delete it. Keep it under its
group heading in ID order, preserve the original wording verbatim, and add a trailing `_Closed:_` line pointing at the
[`CHANGELOG.md`](CHANGELOG.md) entry and/or ADR that closed it. The ID stays recorded in the
[ID ledger](BACKLOG.md#backlog-id-ledger--never-reuse-a-number) — a number is **never** reused or backfilled.

> **Never reuse a number.** Every ID at or below a group's high-water mark is **allocated** — whether still pending in
> [`BACKLOG.md`](BACKLOG.md), archived here, or retired without a surviving entry. To find the next free ID for a group,
> take the maximum of the ledger high-water mark, the highest pending ID in [`BACKLOG.md`](BACKLOG.md), and the highest
> archived ID here, then allocate strictly **above** it.

---

## Deferred — evaluated, intentionally not-yet (group `D`)

- **`D2` — Front-Channel Logout and Session Management (`check_session_iframe`).** The OpenID Provider
  implements RP-Initiated Logout and Back-Channel Logout (both cookie-independent), but **not** Front-Channel
  Logout 1.0 (per-RP hidden iframes) or Session Management 1.0 (`check_session_iframe` / `session_state`). Both
  depend on third-party cookies inside an iframe, which the browser platform (Safari ITP, Firefox partitioning,
  Chrome deprecation) is removing, so they are unreliable by construction. The session model records each
  participating client and the logout pipeline is a mediator seam, so a deployment that still needs front-channel
  behavior can add it without a redesign. Rationale: [ADR-0074](docs/adr/0074-session-management-and-logout.md).
  Touch points: `src/NCode.Identity.OpenId.Authentication/Endpoints/EndSession/`,
  `src/NCode.Identity.OpenId.Authentication/Sessions/`.
  _Closed: [`CHANGELOG.md`](CHANGELOG.md) [Unreleased]; [ADR-0074](docs/adr/0074-session-management-and-logout.md)._
- **`D3` — Index tuning for resource-server runtime query filters.** The resource-server reads now push their
  predicates to the DBMS ([ADR-0080](docs/adr/0080-store-query-methods-push-filtering-to-dbms.md)), but no supporting
  indexes were added — resource servers and scopes are a small, slowly-changing per-tenant set, so a sequential scan is
  cheap today. If a deployment carries large per-tenant resource-server/scope counts, add an index on
  `ScopeEntity.NormalizedValue` (for the owns-any-scope join) and consider a filtered index on
  `ResourceServerEntity.IsDisabled`; measure before adding, since the extra write cost may not pay off for small sets.
  Touch points: `src/NCode.Identity.OpenId.Persistence.EntityFramework.Core/Entities/ScopeEntity.cs`,
  `src/NCode.Identity.OpenId.Persistence.EntityFramework.Core/Entities/ResourceServerEntity.cs`.
  _Closed: [`CHANGELOG.md`](CHANGELOG.md) [Unreleased]; [ADR-0080](docs/adr/0080-store-query-methods-push-filtering-to-dbms.md)._
- **`D4` — CIBA push delivery durability and request-object encryption advertisement.** CIBA **push** delivery
  ([ADR-0085](docs/adr/0085-ciba-grant.md)) issues and `POST`s the tokens to the client's `client_notification_endpoint`
  on a best-effort basis — a failed delivery is logged, not retried or queued, and the client cannot fall back to
  polling. A deployment that needs at-least-once push should replace `ICibaNotificationService` with a durable
  (store-and-retry) transport; a built-in retry/outbox is deferred until a concrete need. Separately, signed backchannel
  request objects are validated and advertised via `backchannel_authentication_request_signing_alg_values_supported`,
  but the matching **encryption** alg/enc advertisement (`backchannel_authentication_request_encryption_*`) is not yet
  surfaced — encrypted request objects still decrypt through the shared JWT path, but the capability is not advertised.
  Touch points: `src/NCode.Identity.OpenId.Authentication/Endpoints/BackchannelAuthentication/`.
  _Closed: [`CHANGELOG.md`](CHANGELOG.md) [Unreleased]; [ADR-0090](docs/adr/0090-durable-outbound-delivery-dispatch-adapter.md),
  [ADR-0085](docs/adr/0085-ciba-grant.md)._
- **`D6` — `PageRequest` pagination DTO across the store `GetPageAsync` family.** The evolving _filter_ sets are now
  DTOs ([ADR-0099](docs/adr/0099-forward-compatible-evolving-parameters.md), `GrantFilter`), but the stable
  `(cursor, limit)` pagination pair is still positional on every `*Store.GetPageAsync` (`IGrantStore`, `IClientStore`,
  `IClientGrantStore`, `IResourceServerStore`, `IServerStore`, `ITenantStore`, `ILocalAccountStore`) and the generic
  `BaseStore`. Wrapping it in a shared `PageRequest { Cursor, Limit }` (in `NCode.Persistence.Abstractions`, beside
  `PagedResult<T>`) would future-proof pagination evolution (e.g. a sort direction) non-breakingly and unify the
  signatures. Deferred: the pair has not churned, so the value is low today and the change touches every store +
  implementation + call site + test. Do it as one mechanical sweep before GA if pagination needs to grow.
  _Closed: [`CHANGELOG.md`](CHANGELOG.md) [Unreleased]; [ADR-0099](docs/adr/0099-forward-compatible-evolving-parameters.md)._

## Feature gaps — implemented (group `F`)

- **`F1` — End-session / RP-initiated logout endpoint.** No OpenID Connect end-session endpoint, session
  termination, or back-channel logout notification exists. Until it does, there is no logout operation
  to audit. (ID-token `sid` claim for logout is also not yet emitted.)
  _Closed: [`CHANGELOG.md`](CHANGELOG.md) [Unreleased]; [ADR-0074](docs/adr/0074-session-management-and-logout.md)._
- **`F2` — Consent follow-ups (granular per-scope, scope/client display, authorized-apps management).** Durable
  consent persistence and enforcement **are implemented** ([ADR-0076](docs/adr/0076-consent-persistence-and-enforcement.md)):
  a first-class `PersistedConsent` aggregate + `IConsentStore` + `ConsentEntity` table, the `IConsentService` seam, the
  client `require_consent` setting (honored alongside `prompt=consent`) enforced in `DefaultAuthorizeHandler`, the
  `ConsentChallengeCommand` routing seam, the interactive `GET /account/consent/details` + decision-recording
  `POST /account/consent`, and the `apps/auth` consent screen showing the real client and requested scopes. **Remaining:**
  per-scope (granular) consent toggling rather than an all-or-nothing allow; friendly scope descriptions and a client
  display name on the consent screen (today it shows raw scope names and the client id); and the end-user
  "authorized applications" self-service surface to **list and revoke** consents (the store already supports it via
  `IConsentStore.GetBySubjectAsync` / `RemoveAsync` and `IConsentService.RevokeConsentAsync`). Touch points:
  `src/NCode.Identity.OpenId.Authentication/Consent/`,
  `src/NCode.Identity.OpenId.Authentication.Interactive/InteractiveAuthenticationEndpointHandler.cs`,
  `src/frontend/apps/auth/src/ConsentForm.tsx`.
  _Closed: [`CHANGELOG.md`](CHANGELOG.md) [Unreleased]; [ADR-0076](docs/adr/0076-consent-persistence-and-enforcement.md)._
- **`F3` — CIBA grant (OpenID Connect Client-Initiated Backchannel Authentication).** The OAuth 2.0 Device
  Authorization Grant (`device_code`, RFC 8628) **is implemented** — device authorization and verification endpoints,
  the `device_code` token grant handler, and the pending-request persistence model (see
  [ADR-0059](docs/adr/0059-device-authorization-grant.md)). CIBA is **not** implemented: there is no
  `backchannel_authentication_endpoint`, no `auth_req_id` grant, and no poll/ping/push token-delivery modes. CIBA
  reuses the same pending-authorization-as-persisted-grant pattern the device flow established, but adds two design
  questions that need their own ADR before implementation: resolving `login_hint` / `login_hint_token` /
  `id_token_hint` to a subject, and the decoupled **authentication-device notification** seam (deployment-specific, so
  it must be an injectable abstraction rather than a built-in transport). Touch points:
  `src/NCode.Identity.OpenId.Authentication/Endpoints/Token/` (the `// TODO: Ciba Grant` marker in
  `DefaultTokenEndpointProvider.cs`), plus a new `Endpoints/BackchannelAuthentication/` family.
  _Closed: [`CHANGELOG.md`](CHANGELOG.md) [Unreleased]; [ADR-0085](docs/adr/0085-ciba-grant.md)._
- **`F4` — `refresh_token_reuse_policy` (none / revoke_all).** Refresh-token replay is _detected and audited_
  (`token.refresh.replay`), but there is no configurable response that revokes the whole token family
  on reuse. See `// TODO: refresh_token_reuse_policy` in
  `src/NCode.Identity.OpenId.Authentication/Endpoints/Token/RefreshToken/DefaultRefreshTokenGrantHandler.cs`.
  _Closed: [`CHANGELOG.md`](CHANGELOG.md) [Unreleased]._
- **`F6` — Dynamic Client Registration (RFC 7591/7592).** There is no `registration_endpoint`; clients are
  provisioned only via the management API / seeding. Not required to certify the Basic/Config OP profiles
  (those allow manual client registration), but it is its own certification profile (Dynamic OP) and it
  removes manual client seeding _and_ unlocks parallel execution for the conformance harness (which
  serialises whenever a static client `alias` is used). Prerequisite for the Dynamic OP conformance
  profile — see [`docs/conformance-testing.md`](docs/conformance-testing.md). Touch points: a new
  `Endpoints/Registration/` family and discovery-metadata advertisement.
  _Closed: [`CHANGELOG.md`](CHANGELOG.md) [Unreleased]; [ADR-0063](docs/adr/0063-dynamic-client-registration.md)._
- **`F7` — DPoP / sender-constrained tokens (RFC 9449 + mTLS, `cnf` claim).** A single `IProofOfPossessionService`
  evaluates a DPoP proof and/or a mutual-TLS client certificate per token request, binds the issued access token's
  `token_type` (`DPoP`/`Bearer`) and `cnf` claim (`jkt` / `x5t#S256`) uniformly across every grant (passed first-class
  to `ITokenGrantHandler.HandleAsync` and carried on `CreateSecurityTokenRequest.Confirmation`), advertises
  `dpop_signing_alg_values_supported` and `tls_client_certificate_bound_access_tokens` in discovery, and enforces the
  `require_dpop` / `tls_client_certificate_bound_access_tokens` client policies. Certificate ingestion lives in the
  default `IClientCertificateResolver`, which reads the connection cert or a configured **trusted** forwarded header
  (`OpenId:MutualTls`) for any reverse-proxy topology; the DPoP replay/nonce stores run over `IDistributedCache`
  (in-memory by default, cross-node by registering a shared provider). The follow-ups that rode the same foundation are
  all implemented: (a) **resource-server-side** validation — `IProofOfPossessionService.ValidateResourceAccessAsync`
  verifies a presented DPoP proof and its `ath` (and the `x5t#S256` match) when a bound token is used at a protected
  resource, and the UserInfo / management-API endpoints accept both the `Bearer` and `DPoP` schemes; (b) **DPoP at the
  authorization endpoint** via the `dpop_jkt` parameter (RFC 9449 §10) that binds the authorization code (format
  validated at the authorization endpoint, matched at the token endpoint, and applied to the hybrid front-channel
  token); (c) **refresh-token binding continuity** — the `cnf` is persisted on `RefreshTokenGrant.Confirmation` so a
  rotated token stays bound to the same key and a bound refresh token presented without a matching proof is rejected;
  (d) `mtls_endpoint_aliases` (RFC 8705 §5) advertised via the `mtls_endpoint_alias_authority` setting.
  _Closed: [`CHANGELOG.md`](CHANGELOG.md) [Unreleased];
  [ADR-0097](docs/adr/0097-sender-constrained-access-tokens-dpop-and-mtls.md)._
- **`F8` — Pushed Authorization Requests (PAR, RFC 9126).** There is no `pushed_authorization_request_endpoint`; the
  authorize endpoint only accepts inline request parameters. See
  `// TODO: add support for OAuth 2.0 Pushed Authorization Requests (PAR)` in
  `src/NCode.Identity.OpenId.Authentication/Endpoints/Authorization/Handlers/DefaultLoadAuthorizationRequestHandler.cs`.
  Touch points: a new `Endpoints/PushedAuthorization/` family plus discovery-metadata advertisement.
  _Closed: [`CHANGELOG.md`](CHANGELOG.md) [Unreleased]; [ADR-0089](docs/adr/0089-pushed-authorization-requests.md)._
- **`F9` — Pairwise subject identifiers (PPID) via the `subject_type` setting.** The subject identifier emitted into
  tokens is always the raw public subject; the `subject_type` (`public`/`pairwise`) setting is not honored, so there is
  no per-client/RP pseudonymous `sub`. See `// TODO: use the subject_type setting to support PPID` in
  `src/NCode.Identity.OpenId.Authentication/Endpoints/Authorization/Handlers/DefaultAuthenticateHandler.cs`.
  _Closed: [`CHANGELOG.md`](CHANGELOG.md) [Unreleased]; [ADR-0093](docs/adr/0093-pairwise-subject-identifiers.md).
  Implemented as a stateless, deterministic projection (`ISubjectIdentifierService`) applied at the outbound `sub`
  seams, keeping the stable principal identifier as the single internal subject; no persistence/migration. Two
  second-order follow-ups were split out as new items: `F20` (sector_identifier_uri remote fetch + validation) and
  `F21` (end-session reverse-map for a pairwise `id_token_hint`)._
- **`F10` — Extension grant + token-endpoint `401`/`WWW-Authenticate`.** The token endpoint has no extension-grant seam
  (`// TODO: Extension Grant` in `DefaultTokenEndpointProvider.cs`) and collapses client-authentication failures to
  `400` instead of emitting `401` with a `WWW-Authenticate` challenge (`// TODO: add support for 401 with
WWW-Authenticate header`). (Device Code is implemented; CIBA is `F3`.) Touch points:
  `src/NCode.Identity.OpenId.Authentication/Endpoints/Token/DefaultTokenEndpointProvider.cs`.
  _Closed: [`CHANGELOG.md`](CHANGELOG.md) [Unreleased]; [ADR-0092](docs/adr/0092-token-endpoint-401-www-authenticate-challenge.md).
  The extension-grant seam already existed via `ITokenGrantHandler` grant-type routing (now documented); the challenge
  work added the `ISupportResponseHeaders` seam and corrected the `client_secret_basic` standard-Base64 decoder._
- **`F11` — `acr` claim emission and conditional `auth_time` (blocked on / pending foundational subsystems).** The
  `sid` claim **is** now emitted into ID tokens from the session-management subsystem
  ([ADR-0074](docs/adr/0074-session-management-and-logout.md)). The `acr` claim still needs authentication-context-class
  tracking, which does not exist, so it is never emitted. `DefaultTokenService` additionally always emits `auth_time`
  unconditionally (`// TODO: … should we do this conditionally?`). Touch points (all
  `src/NCode.Identity.OpenId.Authentication/Tokens/`): `Handlers/DefaultGetIdTokenPayloadClaimsHandler.cs`,
  `DefaultTokenService.cs`.
  _Closed: [`CHANGELOG.md`](CHANGELOG.md) [Unreleased]; [ADR-0091](docs/adr/0091-acr-emission-and-conditional-auth-time.md).
  Essential-`acr` enforcement was carved out as the follow-up item `F19`._
- **`F12` — Token-request validation hardening.** The authorization-code grant validator does not yet validate the
  requested `resource`, does not validate the actual `scope` values against the grant, and hardcodes the supported PKCE
  `code_challenge_method` set with no extensibility seam. See the `// TODO: validate resource`,
  `// TODO: validate actual scope values`, and `// TODO: provide a way to allow custom code challenge methods` markers
  in
  `src/NCode.Identity.OpenId.Authentication/Endpoints/Token/AuthorizationCode/DefaultValidateAuthorizationCodeGrantHandler.cs`.
  _Closed: [`CHANGELOG.md`](CHANGELOG.md) [Unreleased]._
- **`F13` — `max_age` re-authentication logs a WARN on the expected path.** When the end-user's `auth_time` exceeds the
  request `max_age` or the client's `subject_max_age`, `DefaultValidateSubjectAuthenticationHandler` sets
  `AccessDenied` and logs **`SubjectValidationFailed`** at warning level (event `2011`,
  "The end-user's authentication time is too old from the client's MaxAge."). On an interactive authorize that is the
  _normal_ freshness path — the flow re-challenges, the user re-authenticates, and the grant then succeeds
  (`authorization.granted` + `token.issued` follow). Surfaced while running the Aspire dev stack: a stale persistent
  dev cookie trips it on the first sign-in, so a successful re-auth looks like a failure in the logs. Decide whether to
  log the max-age-exceeded → re-prompt transition at a lower level (or as a distinct "re-authentication required" signal
  distinct from hard `AccessDenied` denials) so routine freshness re-prompts are not warnings. Touch points:
  `src/NCode.Identity.OpenId.Authentication/Subject/DefaultValidateSubjectAuthenticationHandler.cs` (the two
  `ValidateMaxAge` branches + `Logger.SubjectValidationFailed`).
  _Closed: [`CHANGELOG.md`](CHANGELOG.md) [Unreleased]._
- **`F14` — EF `QuerySplittingBehavior` not configured (multi-collection includes).** The OpenID EF Core persistence
  emits `Microsoft.EntityFrameworkCore.Query[20504]` at warning level — a query loads more than one collection
  navigation (via `Include`/projection) with no `QuerySplittingBehavior` configured, so it defaults to `SingleQuery`
  (a potential cartesian-explosion / slow-query risk on relational providers). Decide the default: configure
  `UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery)` at the provider (or `AsSplitQuery()` on the specific
  multi-collection query), or deliberately suppress the advisory where `SingleQuery` is intended. Touch points: the
  relational provider configuration (`src/NCode.Identity.OpenId.Playground/Program.cs` `UseSqlServer`/`UseSqlite`) and
  the multi-collection load in `src/NCode.Identity.OpenId.Persistence.EntityFramework*`.
  _Closed: [`CHANGELOG.md`](CHANGELOG.md) [Unreleased]._
- **`F15` — Test coverage for the logout endpoint + back-channel delivery.** `DefaultSessionService` has unit tests
  ([`tests/NCode.Identity.OpenId.Core.Tests/Sessions/DefaultSessionServiceTests.cs`](tests/NCode.Identity.OpenId.Core.Tests/Sessions/DefaultSessionServiceTests.cs)),
  but the RP-Initiated Logout `end_session_endpoint` handler and the `DefaultBackChannelLogoutNotifier`
  ([ADR-0074](docs/adr/0074-session-management-and-logout.md)) are not yet covered. Add handler unit tests (in
  `NCode.Identity.OpenId.Core.Tests`, which already sees this package's internals) and/or an end-to-end
  `NCode.Identity.OpenId.IntegrationTests` case (login → `sid` in the ID token → `end_session_endpoint` revokes the
  session/grants → a signed `logout_token` is POSTed to a stub `backchannel_logout_uri`). The **first-party**
  interactive logout shares this gap: the account page's `POST /account/logout` (which runs the same pipeline from the
  login cookie) and the `GET /account/me` session endpoint have only pure-helper unit tests (`BuildMeResponse`,
  `ResolveSafeReturnUrl`) plus the existing login-POST antiforgery integration test
  ([`tests/NCode.Identity.OpenId.IntegrationTests/Endpoints/InteractiveLoginAntiforgeryTests.cs`](tests/NCode.Identity.OpenId.IntegrationTests/Endpoints/InteractiveLoginAntiforgeryTests.cs));
  add an integration case (login → `GET /account/me` returns the session → `POST /account/logout` clears the cookie,
  revokes the session/grants, and fires back-channel). Touch points:
  `src/NCode.Identity.OpenId.Authentication/Endpoints/EndSession/`,
  `src/NCode.Identity.OpenId.Authentication/Sessions/`,
  `src/NCode.Identity.OpenId.Authentication.Interactive/`.
  _Closed: [`CHANGELOG.md`](CHANGELOG.md) [Unreleased]; [ADR-0074](docs/adr/0074-session-management-and-logout.md)._
- **`F17` — CIBA push delivery and signed backchannel request objects.** CIBA **poll** and **ping** delivery are
  implemented ([ADR-0085](docs/adr/0085-ciba-grant.md)): the `backchannel_authentication_endpoint`, the
  `urn:openid:params:grant-type:ciba` token grant, the pending-request persistence model, and the injectable
  subject-resolution (`AuthenticateBackchannelRequestCommand`) + notification (`ICibaNotificationService`) + approval
  (`ICibaRequestService`) seams all exist, and `backchannel_token_delivery_modes_supported` advertises `poll`/`ping`.
  **Push** delivery (`mode=push`, where the OP delivers the tokens inside the notification) is **not** implemented — it
  needs the notification channel to carry and secure a full token response; it layers onto the existing
  `ICibaNotificationService` seam. Signed/encrypted backchannel request objects
  (`backchannel_authentication_request_signing_alg_values_supported`, a FAPI-CIBA concern) are likewise a follow-up.
  Touch points: `src/NCode.Identity.OpenId.Authentication/Endpoints/BackchannelAuthentication/`.
  _Closed: [`CHANGELOG.md`](CHANGELOG.md) [Unreleased]; [ADR-0090](docs/adr/0090-durable-outbound-delivery-dispatch-adapter.md),
  [ADR-0085](docs/adr/0085-ciba-grant.md)._
- **`F18` — Migrate the device-authorization endpoint to the canonical endpoint shape.** The canonical OpenID endpoint
  shape — a thin `IEndpointProvider` over a strongly-typed `OpenIdMessage<T>` request and a mediator
  validate/process pipeline — is codified in [ADR-0086](docs/adr/0086-canonical-openid-endpoint-shape.md) and followed
  by the token, end-session, and CIBA endpoints. `DefaultDeviceAuthorizationEndpointHandler` predates the convention: it
  reads the form directly (`form.TryGetValue(...)`) and inlines its validation and persistence. Refactor it to a
  `DeviceAuthorizationRequest : OpenIdMessage<…>` + `ValidateDeviceAuthorizationRequestCommand` +
  `CreateDeviceAuthorizationRequestCommand` so the endpoint family is uniform. Touch point:
  `src/NCode.Identity.OpenId.Authentication/Endpoints/DeviceAuthorization/`.
  _Closed: [`CHANGELOG.md`](CHANGELOG.md) [Unreleased]; [ADR-0086](docs/adr/0086-canonical-openid-endpoint-shape.md)._
- **`F19` — Essential `acr` enforcement.** `acr` is now emitted (recorded at authentication, resolved through the
  replaceable `IAuthenticationContextReferenceResolver`, and copied into tokens — [ADR-0091](docs/adr/0091-acr-emission-and-conditional-auth-time.md)),
  and the satisfied `acr` is available on the subject principal. What is **not** yet implemented is _enforcement_ of a
  requested essential `acr`: when the `claims` request parameter asks for `id_token.acr` as an essential claim with
  specific `values`, and the satisfied `acr` is not among them, the OP should deny (or trigger re-authentication) rather
  than silently issue a token whose `acr` does not match. The existing `idp:`-prefixed `acr_values` matching in
  `DefaultValidateSubjectAuthenticationHandler` is the natural home; the satisfied `acr` claim is now present to
  enforce against. Touch point:
  `src/NCode.Identity.OpenId.Authentication/Subject/DefaultValidateSubjectAuthenticationHandler.cs`.
  _Closed: [`CHANGELOG.md`](CHANGELOG.md) [Unreleased]; [ADR-0091](docs/adr/0091-acr-emission-and-conditional-auth-time.md)._
- **`F20` — `sector_identifier_uri` remote fetch + validation at registration.** Pairwise subjects (`F9`,
  [ADR-0093](docs/adr/0093-pairwise-subject-identifiers.md)) honor the `sector_identifier_uri` client setting by using
  its **host** as the sector identifier, but the OpenID Connect Core 1.0 §8.1 registration-time step — fetching the
  JSON document at that URI and verifying it lists every one of the client's `redirect_uris` — is not performed. Add it
  as a Dynamic Client Registration (`F6`) validation, with the fetch guarded against SSRF (allow-list / timeout /
  size cap). Touch point: `src/NCode.Identity.OpenId.Authentication/Endpoints/Registration/`.
  _Closed: [`CHANGELOG.md`](CHANGELOG.md) [Unreleased]; [ADR-0095](docs/adr/0095-sector-identifier-uri-fetch-and-ssrf-guard.md).
  Added `subject_type` / `sector_identifier_uri` as first-class DCR metadata and the replaceable
  `ISectorIdentifierValidator` seam, whose default implementation performs the §8.1 fetch-and-validate behind an
  SSRF-guarded named `HttpClient` (always-on private-address block + no-redirect, plus an optional host allow-list, a
  fetch timeout, and a response size cap)._
- **`F21` — End-session subject resolution for a pairwise `id_token_hint`.** Because the pairwise projection is one-way
  and unstored (`F9`, [ADR-0093](docs/adr/0093-pairwise-subject-identifiers.md)), an `id_token_hint` presented at the
  end-session endpoint carries a pairwise `sub` the OP cannot map back to the stable principal. The original handler
  consumed that pseudonym as if it were the stable subject, silently no-op'ing subject-keyed revocation and
  double-projecting the back-channel `logout_token` `sub` for pairwise clients. Touch point:
  `src/NCode.Identity.OpenId.Authentication/Endpoints/EndSession/Handlers/DefaultResolveEndSessionSubjectHandler.cs`.
  _Closed: [`CHANGELOG.md`](CHANGELOG.md) [Unreleased]; [ADR-0096](docs/adr/0096-end-session-subject-from-server-state.md).
  Resolved **without** a reverse map (rejected as a correlation liability): the handler now reads only `sid` and
  `client_id` from the hint, recovers the stable subject forward from the persisted session grant (falling back to the
  OP login cookie), and never reads the hint's `sub` — mirroring the one-way-hashed grant-key model where a presented
  value is matched forward, never reversed._

- **`F22` — Enforce the `Subject.sub == PrincipalId` invariant at every ticket-construction site.** The principal is now
  resolved uniformly and `SubjectAuthentication.PrincipalId` is the single authoritative subject
  ([ADR-0094](docs/adr/0094-resolve-principal-at-every-authentication-entry-point.md)), and the interactive login path
  re-stamps the cookie's `sub` claim to the resolved id. The ROPC and bearer/UserInfo paths, however, still leave the
  principal's raw `sub` claim (`LocalAccountId` / the token's `sub`) on `SubjectAuthentication.Subject` while
  `PrincipalId` holds the resolved id — harmless today because emission reads `PrincipalId`, not the claim, but a
  latent divergence. Normalize the principal's subject claim to `PrincipalId` at those construction sites too (a shared
  ticket factory), so `Subject.sub` and `PrincipalId` can never disagree and the two-sources-of-truth trap cannot
  return. Touch points: `src/NCode.Identity.OpenId.Accounts.Authentication/DefaultAuthenticatePasswordGrantHandler.cs`,
  `src/NCode.Identity.OpenId.Authentication/Subject/DefaultAuthenticateSubjectHandler.cs`.
  _Closed: [`CHANGELOG.md`](CHANGELOG.md) [Unreleased];
  [ADR-0094](docs/adr/0094-resolve-principal-at-every-authentication-entry-point.md). Added the shared
  `SubjectAuthentication.Create` factory, which normalizes the raw principal's subject claim to the resolved
  `PrincipalId` through a new tenant-aware, host-replaceable `OpenIdContext.NormalizeSubjectId` seam (the inverse of
  `GetSubjectId`, configurable via `OpenIdOptions.NormalizeSubjectId`); every entry point — ROPC, bearer/UserInfo,
  interactive authorize, and the interactive login cookie re-stamp — routes through it, so the invariant holds by
  construction._

- **`F24` — Flow the resource-owner-password ticket's `AuthenticationProperties` from the request, not a fresh empty
  instance.** `DefaultAuthenticatePasswordGrantHandler` builds its `SubjectAuthentication` with a freshly-constructed
  empty `new AuthenticationProperties()`, so any properties the surrounding token-request flow could carry (issued/
  expiry timestamps, `amr`/`acr`-adjacent items, redirect state) are dropped on the ROPC path — unlike the interactive
  and bearer paths, which propagate the authentication's own `AuthenticationProperties`. Thread the properties in
  through the `AuthenticatePasswordGrantCommand` (or resolve them at the point the grant is authenticated) so the ROPC
  ticket carries the same authentication metadata as the other entry points. Touch point:
  `src/NCode.Identity.OpenId.Accounts.Authentication/DefaultAuthenticatePasswordGrantHandler.cs` (the `// TODO`
  on the `new AuthenticationProperties()` argument).
  _Closed: [`CHANGELOG.md`](CHANGELOG.md) [Unreleased]. `DefaultPasswordGrantHandler` now captures the authentication
  instant once at the start of the token-request flow, carries it as `AuthenticationProperties.IssuedUtc` through a new
  required `AuthenticatePasswordGrantCommand.AuthenticationProperties` parameter, and reuses the same timestamp for the
  issued token's `CreatedWhen`, so the ROPC ticket's `auth_time` and the token's `iat` are coherent and the leaf
  `DefaultAuthenticatePasswordGrantHandler` no longer materializes an empty `new AuthenticationProperties()`._

- **`F25` — Session claims & lifecycle enhancements (build on the existing session subsystem).** The end-user session
  subsystem already exists — `ISessionService`, `SessionGrant`, the `sid` claim in the ID token, RP-Initiated /
  Back-Channel / (opt-in) Front-Channel Logout, and session-scoped grant revocation
  ([ADR-0074](docs/adr/0074-session-management-and-logout.md),
  [ADR-0077](docs/adr/0077-session-scoped-grant-revocation-on-logout.md),
  [ADR-0088](docs/adr/0088-opt-in-front-channel-logout-and-session-management.md),
  [ADR-0096](docs/adr/0096-end-session-subject-from-server-state.md)). **Mostly delivered** — see
  [`CHANGELOG.md`](CHANGELOG.md) and [ADR-0098](docs/adr/0098-session-lifecycle-termination-seam.md): the setting-gated
  `sid` in the access token, the server-authoritative idle (inactivity) timeout, the concurrent-session limit with a
  swappable `ISessionEvictionPolicy`, the subject-scoped session query / bulk revocation (`ISessionService.GetSessionsAsync`
  / `RevokeAsync` / `RevokeAllAsync`), and session auditing/logging are all done. **Remaining:**
    - **A surface to view and terminate sessions.** The `ISessionService` query/revoke capability exists and is
      tested, but there is no dedicated surface yet: (a) a **user-facing** "active sessions" + "sign out everywhere"
      view on the OP-hosted account page (`apps/auth`), which runs in the ambient `OpenIdContext` and can call
      `GetSessionsAsync` / `RevokeAllAsync` directly; and (b) a tenant-addressed **admin** endpoint
      (`api/tenants/{tenantId}/sessions`). The admin endpoint is **blocked on a design decision**: management endpoints
      are addressed by a route `{tenantId}` while `ISessionService` is bound to the ambient `OpenIdContext.Tenant`, so
      it must first resolve a per-tenant context (or `ISessionService` must gain a tenant-parameterized overload). The
      existing grants management surface (`GET`/`DELETE api/tenants/{tenantId}/grants?subjectId=…`) already provides
      subject-filtered listing and bulk revoke in the interim (though it does not run the termination seam's
      back-channel/audit side effects).
      _Closed: [`CHANGELOG.md`](CHANGELOG.md) [Unreleased];
      [ADR-0100](docs/adr/0100-explicit-tenant-context-materialization.md). Both remaining surfaces shipped: (a) the
      self-service **active sessions** page on `apps/auth` (per-session revoke, "sign out everywhere else", and "sign out
      everywhere"), backed by `GET /account/sessions/list` + `POST /account/sessions/revoke` + `.../revoke-all`, with
      sign-in `UserAgent`/`RemoteIpAddress` captured on `SessionGrant`, a new `SessionTerminationReason.UserInitiated`, and
      `ISessionService.RevokeAllExceptAsync`; and (b) the tenant-addressed admin endpoint
      `GET`/`DELETE api/tenants/{tenantId}/sessions`. The admin design block is resolved by materializing an `OpenIdContext`
      for the route-addressed tenant (`IOpenIdContextFactory.CreateForTenantAsync` /
      `IOpenIdTenantFactory.CreateTenantByIdAsync`) and delegating to the context-bound `ISessionService`, so admin
      revocation runs the full termination seam (back-channel logout + audit) in the addressed tenant's security domain —
      not a tenant-parameterized service overload._

- **`F23` — OAuth 2.0 Token Exchange (RFC 8693).** The token endpoint had no token-exchange grant, so a client could
  not exchange a presented `subject_token` (and optional `actor_token`) for a delegation/impersonation token.
  _Closed: [`CHANGELOG.md`](CHANGELOG.md) [Unreleased];
  [ADR-0101](docs/adr/0101-oauth-token-exchange-rfc8693.md). A `DefaultTokenExchangeGrantHandler : ITokenGrantHandler`
  for `urn:ietf:params:oauth:grant-type:token-exchange` now mints `access_token` / `refresh_token` / `id_token` outputs
  (`issued_token_type` in the response), supporting both impersonation and delegation with spec-correct `act` / nested
  `act` chains and `may_act` actor authorization (RFC 8693 §4). Presented tokens are validated through a replaceable
  `ISubjectTokenValidator` seam whose default accepts only **self-issued** JWTs (tenant signing keys, issuer, lifetime);
  the requesting client must be confidential and be the client the token was issued to (or named in its `aud`); the
  subject is resolved forward through `IPrincipalResolver` (never by reversing a pairwise `sub`); scopes/audiences are
  downscoped via the RFC 8707 `resource` and RFC 8693 `audience` parameters; and refresh-token issuance is gated per
  client by `token_exchange_allow_refresh_token`. The grant is secure-by-default (not advertised in the baseline
  `grant_types_supported`); the conformance tenant opts in, flipping the `grant-token-exchange` coverage probe. External
  / enterprise-IdP federation is intentionally out of scope and tracked as `F26` (the `ISubjectTokenValidator` seam is
  its extension point)._

- **`F30` — Centralize persistence of a _given_ shared-secret string.**
  [ADR-0110](docs/adr/0110-shared-secret-vs-cryptographic-key-storage.md) added `ISecretGenerator.GenerateSharedSecret`
  for _generating_ a presented secret (stored as the UTF-8 bytes of its opaque string). The complementary operation —
  persisting a **caller-supplied** shared-secret string in that same shape — was duplicated inline in three places
  (`ConformanceSeedHandler.CreatePersistedSecret`, `BootstrapAdminSeedHandler.CreatePersistedSecret`, and the
  integration-test `PlaygroundApplicationFactory`), each re-implementing the data-protect-and-wrap logic.
  _Closed: [`CHANGELOG.md`](CHANGELOG.md) [Unreleased];
  [ADR-0110](docs/adr/0110-shared-secret-vs-cryptographic-key-storage.md). Added
  `ISecretGenerator.ImportSharedSecret(ImportSharedSecretRequest, ReadOnlySpan<byte>)` — the complement to
  `GenerateSharedSecret` — which persists a caller-supplied `client_secret` (the UTF-8 octets of its opaque string) in
  the identical `symmetric_raw` shape, deriving the bit length from the value and sharing one internal construction with
  `GenerateSharedSecret` so the stored UTF-8 contract cannot drift. The value is accepted as `ReadOnlySpan<byte>` (not a
  managed `string`) so the seed handlers keep the secret in their pinned, zeroed-on-return buffers. All three sites now
  route through it._

- **`F28` — Complete outbound-fetch SSRF coverage and bound the `request_uri` fetch.** The shared
  `IOutboundHttpForgeryGuard` ([ADR-0106](docs/adr/0106-runtime-jwks-uri-client-key-resolution-and-shared-ssrf-guard.md))
  guarded the `sector_identifier_uri`, `jwks_uri`, and JAR `request_uri` fetches, but two gaps remained. **(a)** The JAR
  `request_uri` fetch (`DefaultLoadAuthorizationRequestHandler.FetchRequestUriAsync`) read the response body with **no
  size cap** (a DoS vector) and had no fetch-timeout/allowed-hosts settings. **(b)** The **server→client deliveries**
  that POST to client-registered URLs — back-channel logout (`backchannel_logout_uri`) and CIBA push notification
  (`client_notification_endpoint`) — used plain `IHttpClientFactory` clients with **no** SSRF guard.
  _Closed: [`CHANGELOG.md`](CHANGELOG.md) [Unreleased];
  [ADR-0111](docs/adr/0111-complete-outbound-fetch-ssrf-coverage.md). The `request_uri` fetch now requires an absolute
  `https` URI, enforces an optional host allow-list, bounds the request with a wall-clock timeout, and caps the response
  body via a new non-discoverable `request_uri_\*` settings family (`request\*uri*allowed_hosts`,
`request_uri_fetch_timeout`default 10 s,`request_uri_max_size_bytes`default 100 KB). The back-channel-logout and
CIBA deliveries now POST through dedicated SSRF-guarded named clients (connection pinned away from
private/loopback/link-local addresses, auto-redirect disabled), so a registered internal URL cannot turn the OP into a
  confused deputy. The streamed response-size-cap read was extracted into one replaceable, DI-registered
  `IOutboundHttpContentReader`seam (buffer-writer in,`ReadOnlySequence<byte>` out) consumed by all three body-reading
  fetches.*

- **`F31` — User-facing HTML error page for non-redirectable authorization errors (content negotiation).** When the
  authorize endpoint cannot safely redirect an error back to the client — an unregistered/invalid `redirect_uri` or an
  unresolvable `client_id` (OIDC Core §3.1.2.4: the server MUST NOT redirect to an invalid URI and SHOULD **inform the
  resource owner**) — the OP currently returns a raw `application/json` error body
  (`DefaultOpenIdExceptionHandler`/`OpenIdExceptionEndpointFilter`), which a browser user sees as unstyled JSON. The
  correct behavior is a content-negotiated, human-readable HTML error page for an interactive (`Accept: text/html`)
  request to the authorize endpoint, while programmatic callers keep the JSON body. Deliverables: a non-redirectable
  interactive error renderer (reuse the `InteractiveAuthenticationPageRenderer` login/consent markup seam, with a stable
  element id/heading for automation) wired into the authorize error path via content negotiation; HTML for
  `text/html`, JSON otherwise. This also hardens the conformance harness: the
  `oidcc-ensure-registered-redirect-uri` browser override currently matches the `unauthorized_client` code in the JSON
  body (`build/conformance/oidcc-basic.json`); once a stable error-page element exists it can match that instead.
  Touch points: `src/NCode.Identity.OpenId.Authentication/Endpoints/OpenIdExceptionEndpointFilter.cs`,
  `src/NCode.Identity.OpenId.Core/Exceptions/DefaultOpenIdExceptionHandler.cs`,
  `src/NCode.Identity.OpenId.Authentication.Interactive/InteractiveAuthenticationPageRenderer.cs`,
  `src/NCode.Identity.OpenId.Authentication/Endpoints/Authorization/DefaultAuthorizationEndpointHandler.cs`.
  _Closed: [`CHANGELOG.md`](CHANGELOG.md) [Unreleased];
  [ADR-0114](docs/adr/0114-non-redirectable-authorization-errors-are-content-negotiated.md). Delivered as a general,
  extensible **content-negotiation seam** rather than an error-specific renderer: `OpenIdResult<T>` is now a thin
  `IResult` that delegates to `IResultExecutor<IOpenIdResult>` (`DefaultOpenIdResultExecutor` in Core), which applies the
  response envelope and negotiates an `IOpenIdResponseFormatter` strategy collection — each formatter declares the media
  types it emits and a `CanFormat(IOpenIdResponse)` predicate, so the seam is open on both the response-type and
  media-type axes (not hardcoded to errors→HTML), and only an explicit `Accept` match (never a `*/*` wildcard) selects a
  formatter. The interactive package registers `InteractiveOpenIdErrorResponseFormatter`, which renders via the shared
  `InteractiveAuthenticationPageRenderer.RenderErrorPage` (stable `authorization-error` element + `data-error-code`) and
  writes the body with `SecureEncoding.UTF8`. A deployment without the interactive surface degrades gracefully to JSON.
  The planned `OpenIdExceptionEndpointFilter`/`DefaultOpenIdExceptionHandler`/`DefaultAuthorizationEndpointHandler` touch
  points were unnecessary — both non-redirectable error paths (the direct `error.AsHttpResult()` return and the thrown
  `OpenIdException`) already funnel through `OpenIdResult<T>`, so negotiating there covered both without touching the
  authorize handler or exception plumbing._

- **`F29` — EdDSA (Ed25519 / Ed448) signing algorithm support.** The JOSE library implemented RSA (`RS*`/`PS*`) and
  ECDSA (`ES*`) signatures but not **EdDSA** (the JWA `alg` value `EdDSA`, RFC 8037), the modern Edwards-curve signature
  scheme FAPI 2.0 permits. The algorithm code constant (`AlgorithmCodes.DigitalSignature.EdDsa`) and the FAPI
  signing-algorithm ceiling already listed `EdDSA`, so it is advertised/permitted the moment the implementation is
  registered (the ceiling intersects with the server's actually-supported set). Unlike the library's other
  self-implemented algorithms — AES-KW, Concat-KDF/ECDH, PBES2, AES-CBC-HMAC, which compose **BCL primitives** — EdDSA
  has no BCL primitive to compose: `System.Security.Cryptography` on .NET 10 ships the PQC set (`MLDsa`/`SlhDsa`/`MLKem`)
  but **no** Edwards-curve type, and native .NET EdDSA is a later-release proposal. Only the two hashes EdDSA needs
  (SHA-512 for Ed25519, SHAKE256 for Ed448) are in the BCL.
  _Closed: [`CHANGELOG.md`](CHANGELOG.md) [Unreleased];
  [ADR-0117](docs/adr/0117-eddsa-clean-room-implementation-opt-in-package.md). Delivered as the opt-in
  `NCode.Identity.Jose.EdDsa` package: a **clean-room** implementation of Ed25519 and Ed448 from RFC 8032 / 8037 / 7748 /
  8410 — constant-time field, scalar, and group arithmetic composing only the BCL hash primitives, with **zero**
  third-party cryptographic dependency in shipping code. The package depends only on abstractions
  (`NCode.Identity.Jose.Abstractions`, `NCode.Identity.Secrets.Abstractions`). It adds a first-class `OKP` (Octet Key
  Pair) `AsymmetricSecretKey` (`OkpSecretKey`, JWK `kty` `OKP`, `crv` `Ed25519`/`Ed448`) with its
  `ISecretKeyTypeDescriptor` (generate, JWK/PEM import, JWK+SPKI-PEM export, PKCS#8/SPKI deserialize; private seed
  data-protected in memory), a single `EdDsaSignatureAlgorithm` that selects the curve from the key's `crv`, and a
  `serviceCollection.AddEdDsa()` turnkey registration. Discovery advertises `EdDSA` in the asymmetric signing metadata
  automatically once registered (the signing-alg partition keys on `AsymmetricSecretKey`). Correctness is pinned by
  differential tests that cross-check the output byte-for-byte against an independent implementation used solely as a
  test oracle (never referenced by shipping code). The curve catalog is deliberately `internal` (RFC 8032 defines a
  closed set of exactly two curves — unlike the open, public ECC curve catalog). Not wired into the reference host by
  default (it stays opt-in to avoid changing the conformance discovery baseline); enabling it in the Playground and
  seeding an OKP developer signing key is a follow-up. The planned touch points in `src/NCode.Identity.Jose/Algorithms/`
  and `src/NCode.Identity.Secrets/Logic/` were superseded by the standalone package so the core JOSE library gains no new
  dependency and no always-on algorithm._

- **`F33` — First-class JWK / JWKS types + JWK Thumbprint (RFC 7517 / 7638 / 9278).** Keys lived only as `SecretKey`,
  the JWK form existed only as a raw `JsonObject` produced by the descriptor seam, the JWKS endpoint hand-assembled a
  result of `JsonObject` keys, and the DPoP validator computed the `jkt` from a hand-rolled canonical JSON string —
  three duplicative treatments of one concept.
  _Closed: [`CHANGELOG.md`](CHANGELOG.md) [Unreleased];
  [ADR-0119](docs/adr/0119-first-class-jwk-jwks-and-thumbprint.md). Delivered as the new **`NCode.Identity.Jwk`**
  package holding rich, immutable `JsonWebKey` / `JsonWebKeySet` types backed by a detached `JsonElement` (net9+, no
  `JsonDocument`/clone), with typed standard-member accessors, `GetMember` for key-type-specific material,
  `ContainsPrivateKeyMaterial` / `ToPublicKey`, and `ComputeThumbprint` / `ComputeThumbprintUri` (RFC 7638 / 9278) as
  instance methods. Construction is cohesive: public `JsonWebKey(JsonElement)` / `JsonWebKey(JsonObject)` constructors
  and `Parse(string)` for text. Both `NCode.Identity.Secrets.Abstractions` and `NCode.Identity.Jose.Abstractions`
  reference it, so JWK is first-class in both stacks while every `*.Abstractions` package stays logic-free. The
  descriptor seam ([ADR-0105](docs/adr/0105-secret-key-type-descriptor-first-class-seam.md)) still owns per-key-type
  member production; `ExportedPublicKey.Jwk` is now a `JsonWebKey`, the JWKS endpoint returns a `JsonWebKeySet` (the old
  `JsonWebKeySetResult` is retired), the management public-key path exposes `JsonWebKey.AsElement()`, and the DPoP proof
  validator parses the embedded key into a `JsonWebKey` and calls `ComputeThumbprint()` — one representation end-to-end,
  no hand-rolled canonical form._

- **`F35` — Explicit `typ` / media-type validation hardening (RFC 8725 BCP + RFC 9068 `at+jwt`).** A
  validation-policy (not algorithm) feature: enforce an explicit, expected `typ` on decode to defeat token-substitution
  / cross-JWT-confusion, and recognize the registered application types (`at+jwt` access tokens, `dpop+jwt`,
  `logout+jwt`, `secevent+jwt`). Extends the decode/verify options already on `IJoseSerializer`. Touch points:
  [`src/NCode.Identity.Jose/JoseSerializer.Jws.Verify.cs`](src/NCode.Identity.Jose/JoseSerializer.Jws.Verify.cs),
  [`src/NCode.Identity.Jose.Abstractions/JoseTokenTypes.cs`](src/NCode.Identity.Jose.Abstractions/JoseTokenTypes.cs).
  _Closed: [`CHANGELOG.md`](CHANGELOG.md) [Unreleased]. Delivered at the JWT-validation layer (the cohesive seam where
  `ValidateIssuer` / `ValidateAudience` / `ValidateTokenLifeTime` already live) rather than as raw-JOSE decode options:
  the `typ` header lives in the JWS protected header the JOSE serializer already deserializes, but the policy decision
  is a JWT concern, so a new `ValidateJwtParameters.ValidateTokenType(params string[])` validator performs the check.
  Matching follows the media-type rules of RFC 7515 §4.1.9 (optional `application/` prefix, case-insensitive), exposed
  as reusable `JoseTokenTypes.Normalize` / `JoseTokenTypes.AreSame` helpers; the generic `ValidateClaim` was unsuitable
  because it compares with `StringComparer.Ordinal`. `JoseTokenTypes` gained the registered application `typ` constants
  `DPoPJwt`, `LogoutJwt`, `SecurityEventJwt`, and `AuthorizationRequestJwt` alongside `Jwt` / `AccessTokenJwt`. To make
  the capability real, the resource-server access-token path (`DefaultAuthenticateAccessTokenHandler`) now requires
  `at+jwt` per RFC 9068 §4 (single-valued — the generic `JWT` type is rejected to preserve the substitution defense),
  and the `access_token_type` setting default moved from `JWT` to `at+jwt` per RFC 9068 §2.1 so issued JWT access
  tokens are explicitly typed by default. The point-in-time backlog note that this would "extend the decode/verify
  options already on `IJoseSerializer`" was inaccurate — no such options object existed, and the JWT-validation layer is
  the correct home._

- **`F36` — JWS/JWE JSON serialization (General + Flattened) with multiple signatures / recipients.** The serializer is
  compact-only; RFC 7515/7516 also define the General and Flattened **JSON** serializations, which carry **multiple
  signatures** (JWS) / **multiple recipients** (JWE) and **unprotected** header members — the shape many
  verifiable-credential and multi-audience scenarios require, and a prerequisite for some `F34`/`F38` use. Additive to
  the existing encode/decode paths. Touch points: [`src/NCode.Identity.Jose`](src/NCode.Identity.Jose),
  [`src/NCode.Identity.Jose.Abstractions`](src/NCode.Identity.Jose.Abstractions).
  _Closed: [`CHANGELOG.md`](CHANGELOG.md) [Unreleased]. Rather than bolt a second, parallel JSON-only options family
  onto the existing compact-only one, the encode options were **unified**: `JoseSigningOptions` / `JoseEncryptionOptions`
  now model **any** serialization via a `JoseSerializationFormat` (`Compact` / `Flattened` / `General`) property, with
  signatures/recipients always expressed as collections (`JoseSignatureOptions` / `JoseRecipientOptions`) — compact is
  simply the degenerate one-element, no-unprotected-header case. The old per-operation `JoseEncodingOptions` base and the
  `ExtraHeaders` / single-credential shapes were removed (greenfield clean cutover); the shared content-encryption
  (`enc`) + compression (`zip`) algorithms moved to the operation level while per-recipient key-management stays on
  `JoseKeyManagementCredentials`. `KeyManagementAlgorithm.SupportsKeyWrapping` gates multi-recipient encodes (direct
  `dir` / `ECDH-ES` agreement is single-recipient only). Decode auto-detects the JSON serialization (leading `{`) on the
  existing `Decode` / `Deserialize` surface and verifies/decrypts against each signature/recipient until one matches the
  supplied key. Buffers use `NCode.Buffers` pooled writers throughout._

- **`T2` — Browser-automatable login + consent UI with a seeded conformance identity.** _Login/consent surface
  implemented._ The OpenID Provider has an opt-in interactive login/consent surface
  ([ADR-0065](docs/adr/0065-interactive-login-consent-op-hosted-auth-surface.md)): a cookie scheme the authorize
  challenge redirects into, plus a React + MUI `apps/auth` bundle (OP-hosted under `/account/*`) and credential
  endpoints reusing the local-account path, exposing the stable locators (`username`, `password`, `login`,
  `consent-allow`, `consent-deny`). Decision record:
  [ADR-0062](docs/adr/0062-opt-in-interactive-login-consent-ui.md). Detailed plan:
  [`docs/conformance/login-consent-ui.md`](docs/conformance/login-consent-ui.md). **Remaining for conformance:** the
  reachable HTTPS host to drive it headlessly end-to-end is **done** (`T3`); the seeded conformance identity is **done**
  (`T4`); independent of `F6`.
  _Closed: [`CHANGELOG.md`](CHANGELOG.md) [Unreleased];
  [ADR-0065](docs/adr/0065-interactive-login-consent-op-hosted-auth-surface.md),
  [ADR-0062](docs/adr/0062-opt-in-interactive-login-consent-ui.md)._
- **`T3` — Reachable HTTPS Playground host for the external harness.** _Done._ The only launchable host used to be
  the in-process `TestServer`; the external OpenID Foundation suite needs a real HTTPS origin whose discovery
  `issuer` byte-matches that origin. Delivered as the dedicated **`Conformance` launch profile**
  ([`launchSettings.json`](src/NCode.Identity.OpenId.Playground/Properties/launchSettings.json)): it binds all
  interfaces on the stable port (`applicationUrl = https://0.0.0.0:5001;http://0.0.0.0:5000`) so the suite's Docker
  containers reach it via `host.docker.internal:5001` (a `localhost`-only bind is not reachable through
  `host.docker.internal`, which maps to the host network, not loopback), and pins the discovery `issuer` to the reached
  origin via `OpenId__Server__Settings__tenant_issuer = https://host.docker.internal:5001`. The pin rides the
  parent-owned (`Keep`) `tenant_issuer` setting ([ADR-0038](docs/adr/0038-setting-merge-lattice-duals.md)), so the
  server-level value flows down to the single static tenant deterministically, independent of request host / proxy
  headers. It lives in the launch profile rather than `appsettings.Conformance.json` because the shared `Conformance`
  environment overlay is also loaded by the Aspire-orchestrated `ConformanceAspire` dashboard instance on `:5003`, which
  must keep its own request-derived issuer. The profile reuses the conformance tenant/seed (`T4` — `AddConformanceSeed`,
  via `ASPNETCORE_ENVIRONMENT=Conformance` → `appsettings.Conformance.json`) and the interactive login/consent UI (`T2`
  — the `apps/auth` bundle served under `/account/*`). Run recipe:
  `dotnet run --project src/NCode.Identity.OpenId.Playground --launch-profile Conformance`. The live
  container-reachability + discovery byte-match re-confirmation and the launch→compose-up→run-test-plan→teardown
  orchestration script are `T1` Phase C/D, not this item. Detailed plan:
  [`docs/conformance/reachable-host.md`](docs/conformance/reachable-host.md).
  _Closed: [`CHANGELOG.md`](CHANGELOG.md) [Unreleased]._
- **`T4` — Deterministic conformance tenant, clients, and test user seeding.** Seeding exists only in test
  infrastructure; a launched host seeds only the bootstrap-admin client. Add an idempotent, config-driven
  startup seeder (bootstrap-admin-style) for the conformance tenant, the three Basic-OP clients, and a
  fixed test user. Detailed plan:
  [`docs/conformance/conformance-seeding.md`](docs/conformance/conformance-seeding.md). Static client half
  is retired by `F6` (DCR); tenant/user seeding is not.
  _Closed: [`CHANGELOG.md`](CHANGELOG.md) [Unreleased] (`AddConformanceSeed`)._
- **`T5` — Discovery-metadata alignment audit.** Discovery is data-driven from discoverable settings;
  `appsettings.json` over-advertises `grant_types_supported` (`implicit`) and the response-type set
  includes unverified flows, which fails Config OP. Audit every advertised field down to verified support.
  Detailed plan: [`docs/conformance/discovery-alignment.md`](docs/conformance/discovery-alignment.md).
  _Closed: [`CHANGELOG.md`](CHANGELOG.md) [Unreleased]; [ADR-0083](docs/adr/0083-secure-by-default-discovery-metadata.md)._

- **`T7` — Real Node-crypto signer for the probe runner's asymmetric legs.** _Done._ The Phase-3 probe runner's
  `private_key_jwt` and DPoP probes accepted an **injected** `ClientAssertionSigner` / `DpopProofFactory` (unit-tested
  with a fake), but the CLI did not wire a real signer, so those legs **skipped** with "no client key configured" even
  when the OP advertised them. Delivered a Node-`crypto`-backed signer
  ([`src/frontend/apps/conformance-runner/src/signer.ts`](src/frontend/apps/conformance-runner/src/signer.ts)) that
  loads a PEM- or JWK-encoded private key, resolves the JWA signature algorithm (inferred from the key — the curve for
  EC `ES256`/`ES384`/`ES512`, `EdDSA` for Ed25519/Ed448, `PS256` for RSA — overridable to any `ES*`/`PS*`/`RS*`/`EdDSA`
  via `--private-key-alg` / `--dpop-key-alg`), and builds the two compact JWS artifacts: the `private_key_jwt` client
  assertion (OIDC Core §9, carrying the JWK `kid` when present) and the DPoP proof (RFC 9449 §4, embedding the public
  JWK in the `dpop+jwt` header with a fresh `jti`/`iat`). ECDSA signatures use the JOSE raw `ieee-p1363` encoding and
  RSA-PSS uses digest-length salt. New `--private-key` / `--dpop-key` options load the keys at the process boundary and
  feed the signers into the runner. Unit-tested with freshly generated EC/RSA/Ed25519 keys (algorithm inference,
  override validation, JWK loading, public-only JWK projection, and round-trip signature verification). Lands most
  usefully once the backend advertises `private_key_jwt` / DPoP (`S3`, `F7`, `F16`). Touch points:
  [`src/frontend/apps/conformance-runner`](src/frontend/apps/conformance-runner).
  _Closed: [`CHANGELOG.md`](CHANGELOG.md) [Unreleased]._

- **`T8` — Triage the Basic OP (DCR) live-run dispositions.** _Done._ The module verdicts produced by the **external
  OpenID Foundation Conformance Suite** (the `T1` oracle), run **locally** via
  [`build/conformance/run-conformance.ps1 -IncludeBrowserPlans`](build/conformance/run-conformance.ps1) (`T6`). The
  first full browser-driven Basic OP run under Dynamic Client Registration completed `18 PASSED, 11 WARNING, 4 SKIPPED,
4 FAILED` (38 modules); after triage and fixes the plan runs **clean** — `32 PASSED, 0 WARNING, 3 REVIEW, 4 SKIPPED,
0 FAILED` (39 modules incl. Config OP) with a green gate (`Conformance run PASSED`), and `expected-failures.json` has
  no entries. The full disposition log — every fixed backend bug, the codified intentional skips, and the harness
  refinements — lives in [`docs/conformance/external-suite.md`](docs/conformance/external-suite.md) (§ "Live-run
  dispositions (`T8`)"), with the individual fixes also in [`CHANGELOG.md`](CHANGELOG.md) and their ADRs. **Both
  harness follow-ups are resolved:**
    - **Unreachable-suite hang.** `run-conformance.ps1` now runs each `run-test-plan.py` invocation under a hard
      wall-clock watchdog (`-PlanTimeoutSeconds`, default `1800`) that tree-kills a plan on overrun, and pins the suite
      script's own retry bounds explicitly, so a suite that drops out mid-plan fails fast instead of hanging overnight.
    - **Parallel browser-session cookie collision (`oidcc-server`, `oidcc-idtoken-signature`).** The plan **keeps
      `parallel=2`** for the speed boost rather than falling back to `parallel=1`: cross-module contamination is
      structurally impossible because the suite isolates browser state **per test module** (each module gets its own
      `BrowserControl` with a per-instance `CookieManager`), the plan runs under `client_registration=dynamic_client` so
      each module registers its **own** client and `redirect_uri`, and the OP's interactive login is **request-bound**
      (`PostLoginAsync` round-trips the `ReturnUrl` carrying the parked request's `state` grant key, with no ambient
      server-side pending-request state). The one earlier observation did not reproduce and two consecutive full harness
      runs are green at `parallel=2`.

    Design, phases, and the baseline schema are in [`docs/conformance-testing.md`](docs/conformance-testing.md)
    (Appendix F) and [ADR-0084](docs/adr/0084-spec-coverage-conformance-harness.md). Touch points:
    [`build/conformance`](build/conformance), the authorization/token endpoint handlers and the interactive consent
    surface under [`src/NCode.Identity.OpenId.Authentication`](src/NCode.Identity.OpenId.Authentication) /
    [`src/NCode.Identity.OpenId.Authentication.Interactive`](src/NCode.Identity.OpenId.Authentication.Interactive), and
    the SPA consent form under [`src/frontend/apps/auth`](src/frontend/apps/auth).
    _Closed: [`CHANGELOG.md`](CHANGELOG.md) [Unreleased]; [ADR-0084](docs/adr/0084-spec-coverage-conformance-harness.md)._

## Frontend presentation platform (group `U`)

- **`U1` — Management auth bootstrap: silent renewal + end-to-end sign-in.** _Done._
  `@ncode/auth` gained `createTokenRenewer` (an expiry-driven renewal loop that re-arms from each new token and falls
  back to interactive sign-in on failure), driven by the console's `AuthGate` while signed in. The interactive login
  surface (`T2`, [ADR-0065](docs/adr/0065-interactive-login-consent-op-hosted-auth-surface.md)) and the seeded public
  SPA client + administrator user (`AddDeveloperInteractiveSeed`) let the auth-code-with-PKCE flow complete end to end
  against the Playground. **Verified live:** authorize → interactive login → code → token issues a
  `urn:ncode:management` access token whose `GlobalAdmin` role (projected by `DeveloperInteractiveSubjectClaimsHandler`,
  ADR-0055) authorizes management reads/writes (`GET /api/servers`, `/api/tenants`, `/api/tenants/{id}/clients` all
  return `200`). Three latent bugs surfaced and were fixed to get there: the auth-code grant was persisted in the wrong
  shape (subject dropped), access-token validation failures were silently swallowed, and multi-audience `aud` validation
  wrongly required every audience to be expected (see CHANGELOG `Fixed`). Touch points: `src/frontend/packages/auth`,
  `packages/store` (`authSlice`), `apps/mgmt` (`AuthGate`, `auth.ts`),
  `src/NCode.Identity.Server/DevelopmentEnvironment` (`DeveloperInteractiveSeedHandler`).
  _Closed: [`CHANGELOG.md`](CHANGELOG.md) [Unreleased]; [ADR-0065](docs/adr/0065-interactive-login-consent-op-hosted-auth-surface.md)._
- **`U2` — Management console secrets/settings for the remaining owners + initial-access-token mint.** _Backend
  complete; frontend remaining for resource servers._ The console extends the client **Secrets**/**Settings** pattern
  (one-time reveal per [ADR-0050](docs/adr/0050-secret-material-revealed-once-on-create.md); read-only
  effective-settings view + top-level add/remove/replace JSON Patch diff via the `@ncode/api` `diffPatch` helper) to
  **tenants** (a tenant-scope **Tenant** tab) and the **server** (a global-scope **Servers** tab), and surfaces the
  initial-access-token mint (RFC 7591) in the **Grants** tab. The shared `SecretsManager`/`SettingsManager` containers
  back every owner. **Resource-server (API) settings/secrets are now supported by the backend**
  ([ADR-0066](docs/adr/0066-resource-server-settings-and-secret-owner.md)): `ResourceServerApiEndpointHandler` exposes
  `GET`/`PATCH .../settings`, `GET .../effective-settings`, and the full `.../secrets` CRUD. **Remaining:** wire the
  resource-server **Settings**/**Secrets** surface in the console (drill-in from the resource-servers list, same
  pattern as clients) — add the `@ncode/api` contracts + `managementApi` endpoints + a `ResourceServerDetailPage`. A
  client-secret rotate convenience is tracked separately under `S1`. Touch points: `src/frontend/packages/api`,
  `packages/store` (`managementApi`), `packages/ui`, `apps/mgmt/src` (`containers`).
  _Closed: [`CHANGELOG.md`](CHANGELOG.md) [Unreleased]; [ADR-0066](docs/adr/0066-resource-server-settings-and-secret-owner.md)._
- **`U3` — Build out the Playground and conformance surfaces.** _Done._ The Auth/Consent/Login surface is a full app on
  the shared platform (the `apps/auth` consent screen shows the real client and requested scopes and records the
  decision — [ADR-0076](docs/adr/0076-consent-persistence-and-enforcement.md)), and the Playground (`apps/playground`)
  and conformance (`apps/conformance`) surfaces are now **real apps** on the same `@ncode/*` platform
  ([ADR-0064](docs/adr/0064-frontend-presentation-platform.md), [ADR-0082](docs/adr/0082-playground-and-conformance-surfaces.md)).
  The Playground is an interactive OAuth 2.1 / OIDC **authorization-code + PKCE (S256) flow explorer** (surfacing the
  PKCE challenge, `state`, `nonce`, authorize URL, token response, decoded claims, UserInfo, refresh, RP-Initiated
  Logout; implicit/hybrid/password flows are intentionally unavailable). The conformance app is a **security-posture &
  conformance-readiness dashboard** that audits the OP's published discovery metadata against modern guidance (OAuth
  2.1, RFC 9700, OIDC Core/Discovery, RFC 7636/8414/7009/7662/9126) and reports Basic/Config OP readiness plus the
  external-suite config hints — it does **not** duplicate the `apps/auth` login/consent surface the suite drives (`T2`).
  Both are standalone Vite SPAs served via the Aspire AppHost (`playground` → `5174`, `conformance` → `5175`), each
  with a surface-aware bootstrap (`?surface=`), and the Playground has its own seeded public `playground-spa` client
  and dev CORS origin. Touch points: `src/frontend/apps/playground/`, `src/frontend/apps/conformance/`,
  `src/NCode.Identity.Server/Frontend/`, `src/NCode.Identity.Server/DevelopmentEnvironment/`.
  _Closed: [`CHANGELOG.md`](CHANGELOG.md) [Unreleased]; [ADR-0082](docs/adr/0082-playground-and-conformance-surfaces.md)._
- **`U4` — Runtime bootstrap endpoint on the OP host.** The tier-agnostic bundle fetches `/config/bootstrap.json` from
  the serving host; only a dev copy exists (`apps/mgmt/public/config/bootstrap.json`). Add a host endpoint that emits
  the bootstrap document (API base, issuer, feature flags, branding) per deployment so one bundle serves every tier.
  _Closed: [`CHANGELOG.md`](CHANGELOG.md) [Unreleased]._
- **`U7` — Runtime consumption + auditing of resource-server secrets/settings.** [ADR-0066](docs/adr/0066-resource-server-settings-and-secret-owner.md)
  delivers resource-server settings/secrets **management** only. Two follow-ups remain: (1) **runtime consumption** —
  resource servers have no runtime domain object and are not resolved at token-issuance time, so the stored per-audience
  secrets (HS256 signing, JWE decryption) are not yet used to actually sign/encrypt access tokens; and (2) **auditing** —
  `ResourceServerApiEndpointHandler` emits no `IManagementAuditRecorder` events for **any** operation (create, update,
  delete, scopes, settings, secrets), unlike the client/tenant/server handlers. Retrofit audit uniformly across all
  resource-server operations rather than only the new leaves. Touch points:
  `src/NCode.Identity.OpenId.Management/Endpoints/ResourceServers/ResourceServerApiEndpointHandler.cs`,
  `src/NCode.Identity.OpenId.Management.Abstractions/Auditing`, and the token-issuance pipeline for consumption.
  _Closed: [`CHANGELOG.md`](CHANGELOG.md) [Unreleased];
  [ADR-0066](docs/adr/0066-resource-server-settings-and-secret-owner.md),
  [ADR-0075](docs/adr/0075-runtime-consumption-of-resource-server-secrets.md)._
- **`U8` — Strip internal `$`-prefixed ticket parameters from the authorization response.** The interactive flow
  surfaced that an internal ticket parameter (`$created_when`, `OpenIdConstants.Parameters.CreatedWhen`) leaks into the
  authorization response callback query string sent back to the client. `$`-prefixed parameters are internal and should
  never appear on the wire; the authorization-response/ticket rendering should omit them. Low priority (cosmetic — the
  client ignores unknown params), but it is protocol hygiene. Touch point:
  `src/NCode.Identity.OpenId.Core/Endpoints/Authorization/Results/AuthorizationResultExecutor.cs`.
  _Closed: [`CHANGELOG.md`](CHANGELOG.md) [Unreleased]._
- **`U9` — Server create/edit management (UI + backend).** The Servers surface is read-only today: `ServersPage`
  lists servers and drills into `ServerDetailPage` for a server's **settings and secrets** (which already exist),
  but there is **no way to add a new server or edit a server's top-level record** (e.g. display name). Unlike
  tenants and resource-servers — which have full create/update/delete contracts, API endpoints, and frontend
  dialogs — servers have only `listServers` plus server-secrets/settings endpoints; there is no
  `createServer`/`updateServer`/`deleteServer`. The current design treats the server inventory as provisioned
  out of band (documented in `ServersPage.tsx`/`ServerTable.tsx`), so closing this gap is a cross-stack feature:
  new management contracts, API endpoints (with `IManagementAuditRecorder` audit parity), persistence, and
  `PublicAPI` changes on the backend, plus store mutations, a `ServerFormDialog`, and row-level actions on the
  frontend. Decide first whether a server is intended to be a user-creatable, multi-instance entity or a
  provisioned singleton; that product/security question gates the scope. Touch points:
  `src/NCode.Identity.OpenId.Management.Abstractions`, `src/NCode.Identity.OpenId.Management/Endpoints`,
  `src/frontend/packages/store/src/managementApi.ts`, `src/frontend/apps/mgmt/src/containers/ServersPage.tsx`,
  `src/frontend/packages/ui/src/components/ServerTable.tsx`.
  _Closed: [`CHANGELOG.md`](CHANGELOG.md) [Unreleased]._
- **`U10` — Re-home the interactive-auth capability out of the composition root, and expose its seams.** A design
  review of how the interactive login/consent UI is served ([ADR-0065](docs/adr/0065-interactive-login-consent-op-hosted-auth-surface.md))
  found **one real inconsistency** and **one latent gap**; most other concerns raised were already resolved by existing
  ADRs (recorded here so the review does not have to be re-derived).

    **Actionable #1 — extract the capability to a leaf package (recommended).** The interactive-auth code currently lives
    under [`src/NCode.Identity.Server/InteractiveAuthentication/`](src/NCode.Identity.Server/InteractiveAuthentication),
    but `NCode.Identity.Server` is the **composition root** (`AddIdentityServer()`, `DefaultRegistration`, the builder) of
    the capability packages — not a home for a capability itself. The folder _is_ a capability: a cookie scheme, four
    endpoints, credential verification, antiforgery wiring, and a static-file server. Every other capability (Accounts,
    Management, Authentication) is a leaf package; this one got inlined. ADR-0065 even calls it "a reusable opt-in module …
    surfaced from `NCode.Identity.Server`" — the module intent exists, the package boundary was just never drawn.
    **Proposal:** move it to a leaf package (candidate name `NCode.Identity.OpenId.Authentication.Interactive`, with the
    `Defaults`/`Options` + stable-locator contract in a thin `.Abstractions` pair); `NCode.Identity.Server` then only
    _references_ it like any other capability. Entails `PublicAPI.Unshipped.txt` churn + a new ADR (both expected under the
    pre-release posture, [ADR-0025](docs/adr/0025-pre-release-posture-and-auth0-parity-plus.md)). **Open fork:** confirm
    the package name (`NCode.Identity.OpenId.Authentication.Interactive` vs. a `Server`-family name) before moving.
    Files to move: all of
    [`src/NCode.Identity.Server/InteractiveAuthentication/`](src/NCode.Identity.Server/InteractiveAuthentication)
    (`InteractiveAuthenticationRegistration.cs`, `…ApplicationBuilderExtensions.cs`, `…EndpointRouteBuilderExtensions.cs`,
    `…EndpointHandler.cs`, `…Options.cs`, `…Defaults.cs`); wiring to repoint in
    [`src/NCode.Identity.OpenId.Playground/Program.cs`](src/NCode.Identity.OpenId.Playground/Program.cs) (lines ~30, 78,
    135, 156) and `launchSettings.json` (`InteractiveAuthentication__BundleRootPath`).

    **Actionable #2 — invert the credential verifier behind an interface (latent gap).** The real UI extensibility seam is
    _already three seams_: (a) the **bundle** is swappable via `InteractiveAuthenticationOptions.BundleRootPath`; (b) the
    **render surface** is swappable by pointing the cookie `LoginPath`/scheme elsewhere and serving anything at
    `/account/*`; but (c) the **credential verifier** is **not** pluggable —
    [`InteractiveAuthenticationEndpointHandler`](src/NCode.Identity.Server/InteractiveAuthentication/InteractiveAuthenticationEndpointHandler.cs)
    hard-wires `ILocalAccountStore.VerifyCredentialAsync` and the `ClaimsPrincipal` construction. LDAP / external-IdP /
    passwordless backends have no interface to replace. **Open fork:** invert (c) behind an interface now, or leave it
    local-account-only and just document the (a)/(b) seams until a real second backend exists. (Decide with #1, since the
    extraction is the natural moment to introduce the abstraction.)

    **Already-resolved concerns (do not re-litigate; captured for the next session):**
    - _Same-origin is the hard constraint for `apps/auth`._ The cookie `LoginPath` redirect, the antiforgery POST, and the
      `ReturnUrl` round-trip all require same-origin with the OP issuing the cookie (ADR-0065). "Same-origin" ≠ "served by
      Kestrel" — a CDN/reverse-proxy may front `/account/*` at the _same origin_, but the OP-hosted static root is the
      correct default.
    - _Serve assets by purpose._ `apps/auth` must be OP same-origin (cookie flow); `apps/mgmt` is a pure API client with
      **no** same-origin requirement — it is the tier-agnostic "build once, discover at runtime" bundle
      ([ADR-0064](docs/adr/0064-frontend-presentation-platform.md) #5) and is the textbook CDN/static-host workload, not a
      .NET-runtime concern. So production serving is **both, by purpose**: CDN-first for the console; OP-hosted (or
      same-origin CDN path) for auth.
    - _Dev orchestrator already exists._ The VS Code `API + UI (F5)` compound launch runs the Playground plus
      [`build/dev-ui.ps1`](build/dev-ui.ps1) → `pnpm dev` → `turbo run dev` across every app; each app's `dev` script
      self-describes its serving model and new surfaces join automatically. Accepted cost: `apps/auth` gets build-watch
      (`vite build --watch`), **not** HMR, _because_ the OP serves it same-origin (ADR-0065). If the auth inner loop
      becomes painful, a **separate** decision is whether to adopt **.NET Aspire** as the orchestrator (model "API + node
      dev servers" as one app graph) instead of the current PowerShell + compound-launch combo — a real fork, not a free
      win; it would be its own ADR/backlog item.
    - _Bundling is already decided._ ADR-0064 **rejected** the single unified SPA and chose **per-surface bundles sharing
      `@ncode/*` packages** (auth vs. mgmt sit on different security boundaries / blast radius). Dev vs. prod use the
      **same** bundle — "one bundle, every tier," nothing baked at build time; `@ncode/config` discovers API base / issuer
      / flags / branding at boot.

    _Closed: [`CHANGELOG.md`](CHANGELOG.md) [Unreleased];
    [ADR-0065](docs/adr/0065-interactive-login-consent-op-hosted-auth-surface.md). The capability now lives in the
    `NCode.Identity.OpenId.Authentication.Interactive(.Abstractions)` leaf packages._

- **`U11` — Adopt .NET Aspire as the local dev orchestrator (needs its own ADR).** **Decided (2026-10-11):** adopt
  Aspire to realize the Playground's intended purpose — a single one-stop entry to build/run/debug the entire stack
  locally. Aspire is **not** added _inside_ the Playground (the Playground is the reference OP host — one process); it
  is a new **sibling AppHost project** (`NCode.Identity.OpenId.Playground.AppHost`) that orchestrates the Playground +
  the frontend apps (+ optional backing services) as one resource graph. This closes the orchestration fork left open
  by the (now-complete) interactive-auth extraction and
  [ADR-0065](docs/adr/0065-interactive-login-consent-op-hosted-auth-surface.md) and supersedes the
  dev-orchestration half of that ADR (the `build/dev-ui.ps1` + VS Code `API + UI (F5)` compound launch, which has **no
  dependency ordering** — the root symptom being `apps/auth`/`apps/mgmt` booting before the API is listening and failing
  in the browser). **Why Aspire:** `WaitFor(api)` + ASP.NET Core health-check integration gates each UI resource on the
  API being **healthy** (not merely "process started"), deterministically eliminating the race; `WithReference(api)`
  provides service discovery that feeds `@ncode/config`'s runtime-discovery model
  ([ADR-0064](docs/adr/0064-frontend-presentation-platform.md)); the dashboard gives one place for ordered startup,
  logs, and traces. **Topology:**
  `builder.AddProject<Projects.NCode_Identity_OpenId_Playground>("api")`, then
  `builder.AddViteApp("auth", …).WithReference(api).WaitFor(api)` and the same for `"mgmt"`; backing SQL Server/Redis
  modeled as containers is **opt-in** (keep SQLite/in-memory to avoid a hard container-runtime prerequisite). **Scope:**
  new `*.AppHost` project (`IsPackable=false`, excluded from `dod.ps1` packing) + Aspire SDK/hosting packages pinned in
  [`Directory.Packages.props`](Directory.Packages.props); node apps modeled as Aspire resources
  (`AddViteApp`/`AddNpmApp`); repoint the F5/dev experience onto the AppHost and retire/relegate
  [`build/dev-ui.ps1`](build/dev-ui.ps1). **Sequencing:** its own ADR'd change — a toolchain-wide
  orchestration change on a different axis than the completed interactive-auth package extraction; keep the two
  separate. Touch points: new
  `src/NCode.Identity.OpenId.Playground.AppHost/`, [`Directory.Packages.props`](Directory.Packages.props),
  [`build/dev-ui.ps1`](build/dev-ui.ps1), `.vscode/launch.json` (the `API + UI (F5)` compound), `NCode.Identity.slnx`.
  _Closed: [`CHANGELOG.md`](CHANGELOG.md) [Unreleased]; [ADR-0071](docs/adr/0071-aspire-local-dev-orchestrator.md)._
- **`U12` — Aspire migration-worker resource (optional runtime polish).** [ADR-0072](docs/adr/0072-self-healing-dev-database-and-model-drift-gate.md)
  keeps database bootstrap inside the Playground `api` process. The Aspire-idiomatic alternative is a dedicated
  migration/initializer resource in the AppHost graph that other resources `WaitFor`, so a migration failure surfaces
  as a failed dashboard resource rather than an `api` crash. This only relocates the failure (the self-healing +
  Definition-of-Done drift gate already prevent drift), and costs a new project, so it is deferred.
  Touch points: `src/NCode.Identity.OpenId.Playground.AppHost/Program.cs`.
  _Closed: [`CHANGELOG.md`](CHANGELOG.md) [Unreleased];
  [ADR-0072](docs/adr/0072-self-healing-dev-database-and-model-drift-gate.md)._

## Secrets management (group `S`)

- **`S2` — Server-secret change auditing.** Tenant- and client-secret create/delete emit audit events
  (`RecordTenantSecretChangedAsync` / `RecordClientSecretChangedAsync`), but there is no
  `RecordServerSecretChangedAsync` — server-secret operations are not audited. Decide whether this is
  intentional (servers are the `GlobalAdmin`-only control plane) or a parity gap to close.
  Touch points: `src/NCode.Identity.OpenId.Management/Endpoints/Servers/ServerApiEndpointHandler.cs`,
  `IManagementAuditRecorder`.
  _Closed: [`CHANGELOG.md`](CHANGELOG.md) [Unreleased] (`ServerSecretChangedAuditEvent`,
  `RecordServerSecretChangedAsync`)._
- **`S3` — Key import / BYOK.** Secret creation was server-generation-only; there was no way to onboard a
  `private_key_jwt` client (client generates its keypair and registers the **public** half) or to import an
  `x509` certificate. Resolved by importing a **public** asymmetric key as JWK / PEM / certificate across the
  management API (`POST .../secrets/import` on every secret owner) and Dynamic Client Registration (inline `jwks`),
  persisted as a first-class secret via the new `SecretEncoding` axis (`symmetric_raw` / `pkcs8` / `spki` / `x509`)
  and consumed for client authentication / JAR verification. Import is **public-only by construction** (private /
  symmetric material is rejected), so the reveal-once model remains the sole path a symmetric value leaves the
  server. The key-type **descriptor** (`ISecretKeyTypeDescriptor`) became the first-class seam for the full key
  lifecycle (validate / generate / import / export / deserialize), collapsing the parallel `IJsonWebKeyConverter`
  registry. The FAPI-preferred runtime `jwks_uri` resolver remains tracked under `F16` Phase 0.
  Touch points: `src/NCode.Identity.Secrets.Persistence/Logic/DefaultSecretImporter.cs`,
  `src/NCode.Identity.OpenId.Management/Endpoints/`,
  `src/NCode.Identity.OpenId.Authentication/Endpoints/DynamicClientRegistration/`.
  _Closed: [`CHANGELOG.md`](CHANGELOG.md) [Unreleased];
  [ADR-0104](docs/adr/0104-import-public-asymmetric-secret-material.md),
  [ADR-0105](docs/adr/0105-secret-key-type-descriptor-first-class-seam.md)._
- **`S4` — Asymmetric public-material exposure.** No read path returns public key material (JWK/PEM) for a
  client's registered public key or for server/tenant signing keys. ADR-0011/ADR-0050 reserve this as a
  deliberate additive option (public material only; private keys stay unreachable).
  Touch points: `src/NCode.Identity.OpenId.Management.Abstractions/Contracts/Secrets/`.
  _Closed: [`CHANGELOG.md`](CHANGELOG.md) [Unreleased];
  [ADR-0011](docs/adr/0011-secret-management-api.md), [ADR-0050](docs/adr/0050-secret-material-revealed-once-on-create.md)._
- **`S6` — Grow the management capabilities document.** `GET /api/capabilities`
  ([ADR-0067](docs/adr/0067-secret-type-capability-catalog-and-capabilities-discovery.md)) is composed from
  registered providers and currently publishes `secretTypes` and `eccCurves`
  ([ADR-0068](docs/adr/0068-unified-extensible-elliptic-curve-catalog.md)). Natural next sections: the supported
  signature/key-management **algorithms** (from `IAlgorithmCollectionProvider`) and per-type **key-size** options, so
  the console create-secret form drives its algorithm and key-size inputs from discovery instead of its remaining
  hard-coded defaults. Each is a new `ICommandHandler<DiscoverManagementCapabilitiesCommand>` provider.
  Touch points: `src/NCode.Identity.OpenId.Management/Endpoints/Capabilities/`,
  `src/frontend/packages/ui/src/components/SecretFormDialog.tsx`.
  _Closed: [`CHANGELOG.md`](CHANGELOG.md) [Unreleased];
  [ADR-0067](docs/adr/0067-secret-type-capability-catalog-and-capabilities-discovery.md),
  [ADR-0068](docs/adr/0068-unified-extensible-elliptic-curve-catalog.md)._

## Tooling & agent customization (group `X`)

- **`X2` — Archive completed backlog items (full text) for reference.** Completed entries were **deleted** from
  [`BACKLOG.md`](BACKLOG.md) when their work landed, so their original wording survived only in git history and in the
  terse [`CHANGELOG.md`](CHANGELOG.md) line that closed them. Mine the history — `git log --follow -p -- BACKLOG.md` —
  to recover every removed entry, and collect the full text into a dedicated, read-only archive (this
  `BACKLOG-COMPLETED.md`), keyed by backlog ID, each cross-referenced to the [`CHANGELOG.md`](CHANGELOG.md) entry and/or
  ADR that closed it. Going forward, when an entry is completed and removed, copy its text into the archive in the same
  change (and the ID stays recorded in the [ID ledger](BACKLOG.md#backlog-id-ledger--never-reuse-a-number)). This
  preserves the richer problem/design context the changelog omits, and makes the retired IDs' rationale discoverable
  without archaeology. Touch points: [`BACKLOG.md`](BACKLOG.md), [`CHANGELOG.md`](CHANGELOG.md),
  [`BACKLOG-COMPLETED.md`](BACKLOG-COMPLETED.md).
  _Closed: this archive; the going-forward process is documented in [`BACKLOG.md`](BACKLOG.md)._
