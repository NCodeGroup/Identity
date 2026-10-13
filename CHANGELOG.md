# Changelog

All notable changes to this project are documented here. The format is based on
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and the project aims to adhere to
[Semantic Versioning](https://semver.org/spec/v2.0.0.html). Versioning is stamped in lockstep across the family by
Nerdbank.GitVersioning ([ADR-0003](docs/adr/0003-central-package-management-and-lockstep-versioning.md)); a breaking
change to the public API is a **major** version bump.

## [Unreleased]

### Added

- **Front-Channel Logout 1.0 and Session Management 1.0, available but disabled by default — completes backlog `D2`.**
  The OP now implements both browser-mediated session mechanisms behind opt-in server settings, complementing the
  cookie-independent RP-Initiated and Back-Channel Logout it already shipped. **Front-Channel Logout:** when
  `frontchannel_logout_supported` is enabled, the `end_session_endpoint` renders a hidden iframe per participating client
  (each client's `frontchannel_logout_uri`, with `iss`/`sid` appended when `frontchannel_logout_session_required`) so
  relying parties clear their own sessions in the user agent. **Session Management:** when `session_management_supported`
  is enabled, the authorization response carries the `session_state` parameter, the OP maintains an opaque browser-state
  cookie, and discovery advertises a `check_session_iframe` the relying party polls via `postMessage`. Both default to
  **off** because they depend on third-party cookies the browser platform is removing (prefer Back-Channel Logout); a
  deployment that still needs them enables the corresponding setting
  ([ADR-0088](docs/adr/0088-opt-in-front-channel-logout-and-session-management.md), which reverses the earlier deferral
  in [ADR-0074](docs/adr/0074-session-management-and-logout.md)). New settings: `frontchannel_logout_supported`,
  `frontchannel_logout_session_supported`, per-client `frontchannel_logout_uri` /
  `frontchannel_logout_session_required`, and `session_management_supported`.

- **Supporting index for resource-server scope lookups — completes backlog `D3`.** The resource-server runtime reads
  push their scope-value predicate to the DBMS ([ADR-0080](docs/adr/0080-store-query-methods-push-filtering-to-dbms.md)),
  but the scope-driven `SelectMany` join could not seek on scope value: the only index leading with `NormalizedValue`
  was the composite unique `(ResourceServerId, NormalizedValue)`, whose leading column is the owner. A standalone
  non-unique index on `ScopeEntity.NormalizedValue` (new EF migration) backs the owns-any-scope lookup. The companion
  option — a filtered index on `ResourceServerEntity.IsDisabled` — was deliberately **not** added: `IsDisabled` is a
  low-selectivity boolean (nearly every resource server is enabled), so a standalone or partial index on it would not be
  chosen by the query planner and would only add write cost; the selective scope-value index above is what carries those
  queries.

- **Refresh-token reuse detection with family revocation — completes backlog `F4`.** A revoked refresh token presented
  again is a replay signal; the OP already detected and audited it (`token.refresh.replay`), and now responds per the
  new per-client `refresh_token_reuse_policy` setting: `revoke_all` (the secure default) revokes the entire **rotation
  family** — every refresh token descended from the same authorization grant — so a stolen token cannot be exchanged
  even if the attacker races ahead of the legitimate client, per the OAuth 2.0 Security BCP
  ([RFC 9700](https://www.rfc-editor.org/rfc/rfc9700)); `none` preserves audit-only behavior
  ([ADR-0087](docs/adr/0087-refresh-token-reuse-detection-and-family-revocation.md)). Rotated refresh tokens are now
  linked by a server-generated **lineage id** persisted on the grant (new nullable `RefreshTokenLineageId` column on the
  grants table, with an EF migration); a fresh issuance starts a new family and each rotation inherits its parent's
  lineage. New public surface: `OpenIdConstants.RefreshTokenReusePolicy`, the `refresh_token_reuse_policy` setting
  (`OpenIdSettingNames`/`OpenIdSettingKeys`), `IGrantStore.RevokeByRefreshTokenLineageAsync`,
  `IPersistedGrantService.RevokeByRefreshTokenLineageAsync`, and the `RefreshTokenLineageId` members on `PersistedGrant`,
  `PersistedGrant<TPayload>`, `CreateSecurityTokenRequest`, and `GrantEntity`.

- **Device-authorization endpoint migrated to the canonical endpoint shape — completes backlog `F18`.** The
  `/oauth2/device_authorization` endpoint (`DefaultDeviceAuthorizationEndpointHandler`) is now a thin orchestrator over
  a strongly-typed `DeviceAuthorizationRequest : OpenIdMessage<…>` and mediator commands
  (`ValidateDeviceAuthorizationRequestCommand`, `CreateDeviceAuthorizationRequestCommand` →
  `DeviceAuthorizationResult`), matching the token, end-session, and CIBA endpoints and the shape codified in
  [ADR-0086](docs/adr/0086-canonical-openid-endpoint-shape.md). It no longer reads the form directly or inlines its
  validation and persistence, so each protocol step is independently testable and replaceable. Behavior is unchanged.
  New public surface across `NCode.Identity.OpenId.Authentication(.Abstractions)`: the `IDeviceAuthorizationRequest` /
  `DeviceAuthorizationRequest` messages and the two mediator commands.

- **CIBA grant (OpenID Connect Client-Initiated Backchannel Authentication) — completes backlog `F3` and `F17`.** The
  OP gains a decoupled backchannel-authentication flow (**poll, ping, and push** delivery) that reuses the device flow's
  pending-authorization-as-persisted-grant pattern with **no new table or EF migration**
  ([ADR-0085](docs/adr/0085-ciba-grant.md)). A new `backchannel_authentication_endpoint` (`/oauth2/bc-authorize`) accepts
  a strongly-typed `BackchannelAuthenticationRequest` and — like the token and end-session endpoints — is a thin
  orchestrator over mediator commands (`ValidateBackchannelAuthenticationRequestCommand`,
  `AuthenticateBackchannelRequestCommand` → `AuthenticateSubjectDisposition`,
  `CreateBackchannelAuthenticationRequestCommand` → `BackchannelAuthenticationResult`); the
  `urn:openid:params:grant-type:ciba` token grant polls by `auth_req_id` with the same
  `authorization_pending` / `slow_down` / `expired_token` / `access_denied` semantics as the device grant. Login-hint
  resolution is an injectable mediator seam whose default reports `unknown_user_id` (a deployment registers a CIBA
  authentication capability that replaces it); the authentication-device notification (`ICibaNotificationService`) and
  the out-of-band approval (`ICibaRequestService`) are injectable services with safe defaults, mirroring
  `IBackChannelLogoutNotifier`. New settings `backchannel_token_delivery_modes_supported` (default `poll` / `ping` /
  `push`) and `backchannel_user_code_parameter_supported`, plus `ciba_request_lifetime` / `ciba_polling_interval`, are
  advertised in discovery along with the endpoint and grant — which flips the spec-coverage harness's CIBA rows to
  _implemented_ ([ADR-0084](docs/adr/0084-spec-coverage-conformance-harness.md)). **Push** delivery issues the tokens
  when the user approves on their authentication device and delivers them to the client's registered
  `client_notification_endpoint` (the default `ICibaNotificationService` performs the ping and push `POST`s; the
  authentication-device transport stays a deployment seam), and the token endpoint rejects a push-mode poll. A client
  may also send its parameters as a signed (and optionally encrypted) `request` JWT, validated against the client's keys
  and advertised via `backchannel_authentication_request_signing_alg_values_supported`. New public surface across
  `NCode.Identity.OpenId.Abstractions`,
  `.Messages.Abstractions`, and `.Authentication(.Abstractions)` (grant type, persisted-grant type, endpoint name/path,
  CIBA parameters, `unknown_user_id`, `BackchannelTokenDeliveryModes`, the settings — including
  `client_notification_endpoint` and `backchannel_authentication_request_signing_alg_values_supported` — the
  request/result messages with the `request` parameter, the mediator commands including
  `LoadBackchannelAuthenticationRequestCommand`, and the `ICibaNotificationService` (with push token delivery) /
  `ICibaRequestService` seams).

- **Spec-coverage conformance harness + exercisable consent — advances backlog `T6`.** The conformance surface
  (`apps/conformance`) gained a **spec-coverage matrix** that measures the OP against the full published
  OAuth/OIDC/CIBA/FAPI catalog — independent of what is implemented — so missing functionality (CIBA `F3`, DPoP/mTLS
  `F7`, PAR `F8`, token exchange, pairwise `F9`, `acr` `F11`, the FAPI 2.0 profile `F16`) surfaces as explicit,
  attributable gap rows classified from the **live discovery document**; because the classifier is pure over discovery,
  the same matrix is a **regression** detector (a capability that stops being advertised, or flips to non-conformant,
  changes its row). It also adds a user-triggered **active-probe** panel (Phase 2) that exercises the browser-reachable
  endpoints — JWKS validity, the RFC 6749 §5.2 token-error contract, UserInfo's `401`, and PAR when advertised — to
  turn _advertised_ into _verified_. Design and the `browser` / `probe-runner` / `external-suite` exercise boundary are
  in [ADR-0084](docs/adr/0084-spec-coverage-conformance-harness.md). Separately, the Playground flow explorer gained a
  **Force consent (`prompt=consent`)** toggle and the developer-seeded `playground-spa` client now sets
  `require_consent = true`, so the OP's durable, granular consent surface is exercised out of the box; `@ncode/auth`'s
  `buildAuthorizeUrl` gained an optional OIDC `prompt`. New public surface: `DeveloperSpaClientSeed.RequireConsent`.
  The dashboard is organized into **tabs** (Overview · Security audit · Coverage · Probes · Suite config) with
  **collapsible** coverage categories (gaps start expanded) so the growing surface stays scannable; adds
  `@mui/icons-material` to the app. Development/conformance convenience only
  ([ADR-0025](docs/adr/0025-pre-release-posture-and-auth0-parity-plus.md)).

- **Deterministic conformance tenant, clients, and test-user seeding — completes backlog `T4`.** A new
  **conformance-only**, config-driven startup seeder provisions the confidential clients and a fixed test user the
  OpenID Foundation conformance suite drives, into each workload tenant, so a launched Playground host always exposes
  the same identities with **no manual API calls**. `AddConformanceSeed(IConfiguration)` (chained off
  `AddIdentityServer()`) registers an idempotent `SeedTenantCommand` handler modeled on the bootstrap-admin seeder: it
  is inert unless the `ConformanceSeed` configuration section is supplied, re-running never duplicates, and it never
  targets the control-plane root tenant. The new `Conformance` launch profile and `appsettings.Conformance.json` overlay
  seed the three Basic-OP clients (two `client_secret_basic`, one `client_secret_post`, all `S256`) and the
  `conformance-user`. New public surface: `ConformanceSeedRegistration.AddConformanceSeed`, `ConformanceSeedOptions`,
  and `ConformanceClientSeed` (one type per file under a `ConformanceEnvironment/` folder). Client secrets are supplied
  `Base64Url`-encoded and never linger on the GC heap. Must not be used in production. Development/conformance
  convenience only ([ADR-0025](docs/adr/0025-pre-release-posture-and-auth0-parity-plus.md)).

- **Playground flow explorer and conformance posture dashboard — completes backlog `U3`.** The two frontend scaffolds
  became real surfaces on the shared `@ncode/*` platform
  ([ADR-0082](docs/adr/0082-playground-and-conformance-surfaces.md)). `apps/playground` is an interactive OAuth 2.1 /
  OpenID Connect **authorization-code + PKCE (S256) flow explorer** that discovers the provider and walks the flow end
  to end — surfacing the PKCE verifier/challenge, `state`, `nonce`, authorize URL, token response, and decoded JWT
  header/payload claims, then exercising UserInfo, refresh, and RP-Initiated Logout; implicit, hybrid, and
  resource-owner-password flows are intentionally not offered. `apps/conformance` is a **security-posture &
  conformance-readiness dashboard** that audits the OP's published discovery metadata against modern guidance (OAuth
  2.1, RFC 9700, OpenID Connect Core/Discovery, RFC 7636/8414/7009/7662/9126), flags insecure/deprecated configuration,
  and reports OpenID Foundation Basic/Config OP readiness plus the external-suite configuration (issuer, alias, RP
  callback URL). Both are standalone Vite SPAs wired into the Aspire AppHost (`playground` → `5174`, `conformance` →
  `5175`). The host bootstrap document is now **surface-aware**: `@ncode/config`'s `loadRuntimeConfig` sends the
  bundle's `SurfaceKind` as `?surface=`, and `FrontendBootstrapOptions.Surfaces` maps each surface to a per-surface
  override (client id + scopes **replace** the default so a read-only surface can publish none; feature flags merge;
  branding falls back). `@ncode/auth`'s `buildAuthorizeUrl` gained an optional OIDC `nonce`. **Breaking (pre-release):**
  `DeveloperInteractiveSeedOptions` now seeds a **list** of public SPA clients (`SpaClients`, defaulting to `mgmt-spa`
    - `playground-spa`); the single `SpaClientId`/`SpaRedirectUris`/`SeedSpaClient` members were removed, and the
      developer CORS defaults now include the `5174`/`5175` dev-server origins. New public surface:
      `FrontendBootstrapOptions.Surfaces`, `FrontendSurfaceOptions`, and `DeveloperSpaClientSeed`. The frontend
      bootstrap types (`FrontendBootstrapOptions`, `FrontendSurfaceOptions`, `FrontendBrandingOptions`) live in the
      `NCode.Identity.Server.Frontend` namespace, one type per file under a `Frontend/` folder. Greenfield, pre-release
      — no compatibility shims ([ADR-0025](docs/adr/0025-pre-release-posture-and-auth0-parity-plus.md)).

- **Resource-server runtime reads push filtering into the DBMS — completes backlog `D3`.** The six runtime readers that
  previously paged the tenant's **entire** resource-server set and filtered in memory
  (`DefaultDiscoverScopesHandler`, `DefaultClientScopeService.GetAllowedScopesAsync` / `ResolveAudiencesAsync` /
  `GetEnabledResourceServerIdentifiersAsync`, `DefaultScopeDescriptionService.DescribeScopesAsync`, and
  `DefaultResourceServerCredentialResolver`) now call discrete, intent-named `IResourceServerStore` query methods that
  translate their predicate to SQL ([ADR-0080](docs/adr/0080-store-query-methods-push-filtering-to-dbms.md)). New public
  surface: `IResourceServerStore.GetEnabledScopesAsync`, `GetEnabledIdentifiersAsync`,
  `GetEnabledScopeProjectionsByScopesAsync`, `GetEnabledScopeProjectionsBySystemOrIdsAsync`, and `FindEnabledByScopesAsync`,
  plus the lightweight `ResourceServerScopeProjection` data contract. The identifier/scope readers no longer load server
  settings or secrets. **Behavior change:** `IScopeDescriptionService.DescribeScopesAsync` now ignores **disabled**
  resource servers, consistent with every other runtime reader. Case-sensitive scope/identifier matching (RFC 6749 / RFC 8707) is preserved: the DBMS filter is a widening pre-filter on the normalized column and the exact match is re-applied
  in memory. Greenfield, pre-release — no compatibility shims
  ([ADR-0025](docs/adr/0025-pre-release-posture-and-auth0-parity-plus.md)).

- **Token-request validation hardening — completes backlog `F12`.** Three related gaps in the authorization-code
  grant validator are closed. (1) **Resource indicators (RFC 8707)** are now a first-class, multi-valued `resource`
  parameter on the authorization and token requests
  ([ADR-0078](docs/adr/0078-resource-indicators-rfc-8707.md)): the authorization endpoint rejects an unknown or disabled
  resource (`invalid_target`), the authorization-code token endpoint rejects a resource not authorized at the
  authorization endpoint, and `IResourceServerCredentialResolver.ResolveAsync` — the single choke point every grant
  funnels through — **narrows** the access-token `aud` to the requested resources and rejects any that do not resolve to
  an enabled resource server owning one of the token's effective scopes. A new `invalid_target` error code and
  `IOpenIdErrorFactory.InvalidTarget(...)` carry the rejections. (2) The hardcoded PKCE `plain`/`S256` switch becomes an
  extensible **`ICodeChallengeMethod` strategy collection**
  ([ADR-0079](docs/adr/0079-extensible-pkce-code-challenge-methods.md)); the boolean
  `allow_plain_code_challenge_method` setting is **removed** in favor of the standard, discoverable
  `code_challenge_methods_supported` set (default `["plain", "S256"]`, `Intersect` merge), now advertised in the
  discovery document. (3) Requested **scope values** are confirmed against the live resource-server scope definitions
  (already enforced via `GetAllowedScopesAsync`). New public surface: `OpenIdCommonParameters.Resources`,
  `IBaseAuthorizationRequestValues.Resources`/`IAuthorizationRequest.Resources`/`ITokenRequest.Resources`,
  `CreateSecurityTokenRequest.Resources`, `IClientScopeService.GetEnabledResourceServerIdentifiersAsync`,
  `ICodeChallengeMethod` + `ICodeChallengeMethodCollectionProvider`, and `OpenIdSettingKeys.CodeChallengeMethodsSupported`;
  `IResourceServerCredentialResolver.ResolveAsync` gains a `requestedResources` parameter. Greenfield, pre-release — no
  compatibility shims ([ADR-0025](docs/adr/0025-pre-release-posture-and-auth0-parity-plus.md)).

- **Granular per-scope consent, friendly consent screen, and self-service authorized applications — completes backlog
  `F2`.** Durable consent ([ADR-0076](docs/adr/0076-consent-persistence-and-enforcement.md)) becomes **granular**: the
  end-user grants a **subset** of the requested scopes and the OP issues only that subset (OIDC down-scoping), reviews
  and revokes standing consent from a new surface, and sees friendly names instead of raw identifiers
  ([ADR-0081](docs/adr/0081-granular-consent-and-authorized-applications.md)). The consent record now tracks both the
  scopes the subject was asked to **decide** on and the subset they **granted** (`PersistedConsent.RequestedScopes` +
  `ConsentEntity.RequestedScopesJson`, an `AddConsentRequestedScopes` EF migration), so the authorize endpoint prompts
  only when a request introduces a previously-undecided scope and otherwise issues the request ∩ granted intersection;
  `HasConsentAsync` is replaced by `ResolveConsentAsync` returning a `ConsentResolution`, `GrantConsentAsync` takes the
  requested **and** granted sets (re-consent can revoke a scope), and a new mutable `IAuthorizationRequest.GrantedScopes`
  override carries the narrowed set into the authorization code and tokens so the token endpoint cannot re-expand it. The
  consent subsystem resolves the client's `client_name` and each scope's friendly description (a new
  `IScopeDescriptionService`): `ResolvePendingConsentAsync` and the `GET /account/consent/details` payload now carry the
  client display name and per-scope descriptions and already-granted state, the `apps/auth` consent screen renders a
  per-scope checkbox (keeping the `consent-allow` / `consent-deny` locators), and a new OP-hosted authorized-applications
  surface (`/account/applications` page, `/account/applications/list` JSON, and an antiforgery-protected
  `/account/applications/revoke` POST, backed by `IConsentService.GetAuthorizedApplicationsAsync` / `RevokeConsentAsync`)
  lets the end-user review and revoke the applications they have authorized.
- **Session-scoped grant revocation on logout — completes backlog `F17`.** Logout now revokes **only the terminating
  session's grants** instead of every grant the subject holds, so signing out of one login no longer invalidates the
  subject's other sessions ([ADR-0077](docs/adr/0077-session-scoped-grant-revocation-on-logout.md)). A grant records the
  opaque `sid` of the session it was issued under: `PersistedGrant` (and the typed `PersistedGrant<TPayload>` envelope)
  gains a nullable `SessionId`, `GrantEntity` gains a `SessionId` column and `(TenantId, SessionId)` index (an
  `AddGrantSessionId` EF migration), and the authorization-code, refresh-token, and per-access-token UserInfo-claims
  grants — plus the session grant itself — stamp the `sid` read from the authenticated subject (a new
  `SubjectAuthentication.SessionId` convenience). `IGrantStore.RevokeWhereAsync` and `GetPageAsync` gain an optional
  `sessionId` filter (combined with the existing subject/client filters), surfaced on the management grant list and
  bulk-revoke endpoints, `GrantResource`, and the bulk-revocation audit event;
  `DefaultRevokeGrantsOnEndSessionHandler` revokes by `sid` when a session is resolved and falls back to subject-wide
  revocation only when it is not. Subject-wide "sign out everywhere" remains the deliberate management bulk-revoke
  ([ADR-0022](docs/adr/0022-grant-filters-and-bulk-revocation.md)). The redundant `ISessionService.RevokeAsync` (which
  revoked only the session grant, leaving its child tokens live) is **removed**.

    records and enforces end-user **consent** ([ADR-0076](docs/adr/0076-consent-persistence-and-enforcement.md)). Consent
    is a first-class persisted aggregate — a new `PersistedConsent` DTO, `IConsentStore`, and `ConsentEntity` table (with
    an `AddConsents` EF migration) keyed by `(tenant, client, subject)` and **upserted** in place — deliberately **separate
    from the ephemeral grant store** so it survives logout and powers a future "authorized applications" surface. A new
    client **`require_consent`** setting (default off; also forced by `prompt=consent`) makes `DefaultAuthorizeHandler`
    route a subject whose recorded consent does not cover the requested scopes to the interactive consent screen (via a new
    `AuthorizeDisposition.ConsentRequired` and a `ConsentChallengeCommand` seam the interactive module handles), or return
    the OIDC `consent_required` error under `prompt=none`. The interactive surface gains
    `GET /account/consent/details` (resolving the pending request from the return URL without consuming it) and a
    decision-recording `POST /account/consent`, and the `apps/auth` consent screen now shows the **real** client and
    requested scopes. The Playground (`apps/playground`) and OIDC conformance (`apps/conformance`) surfaces are scaffolded
    as apps on the shared `@ncode/*` platform. The unused `InteractiveAuthenticationOptions.AutoApproveConsent` placeholder
    is **removed** (use `require_consent = false`).

- **Runtime consumption of resource-server secrets/settings and uniform resource-server auditing — completes backlog
  `U7`.** Access tokens now consume the per-audience material a resource server owns
  ([ADR-0075](docs/adr/0075-runtime-consumption-of-resource-server-secrets.md)): at issuance the OP resolves the
  resource servers a token targets (a new `IResourceServerCredentialResolver`) and, when the token targets **exactly
  one** whose settings require it, **encrypts** the token to that resource server's own key so only it can read the
  token — falling back to the tenant's key otherwise. **Signing is always the tenant's asymmetric key** validated via
  JWKS (RFC 9068; aligned with FAPI's `PS256`/`ES256`-only, no-symmetric rule) — a resource server never holds a
  token-signing key. A token spanning several resource servers where any one **requires** encryption is rejected rather
  than silently downgraded. Separately, the resource-server management API now emits audit events uniformly for
  **every** operation (create/update/delete, scopes, settings, and secrets) through `IManagementAuditRecorder`,
  matching the client/tenant/server handlers; new `ResourceServerChangedAuditEvent`, `ResourceServerScopeChangedAuditEvent`,
  and `ResourceServerSecretChangedAuditEvent` carry the non-sensitive snapshots.

- **PostgreSQL developer database orchestrated by Aspire, with a dedicated migration worker — completes backlog `U12`.**
  The Playground's local database is now **PostgreSQL** run by the Aspire AppHost
  ([ADR-0073](docs/adr/0073-postgres-dev-database-and-aspire-migration-worker.md)), replacing the in-process
  SQLite/SQL Server/in-memory provider selection. A stable **named data volume** makes data persist across restarts; a
  new `build/dev-db-reset.ps1` (auto-detecting Docker or Podman) purges it **on demand**. A dedicated
  `NCode.Identity.OpenId.Playground.MigrationService` worker applies the EF Core migrations and exits, and the `api`
  `WaitForCompletion`s on it — so a migration failure surfaces as a **failed dashboard resource** that blocks the `api`
  instead of crashing it. The migrations and Npgsql wiring move into a new host-owned
  `NCode.Identity.OpenId.Playground.Database` project shared by the `api` and the worker. The developer **Data
  Protection key ring** is now persisted in the **same** database (`PersistKeysToDbContext`) rather than a file under
  `App_Data/dp-keys`, so one purge resets the OpenID data, the seeded signing key, and the ring that protects it
  together, with nothing left on the filesystem. A container runtime (Docker or Podman) is now required to run the
  AppHost.

- **End-user sessions, the `sid` claim, and RP-Initiated + Back-Channel Logout — completes backlog `F1` and the `sid`
  portion of `F11`.** The OpenID Provider now establishes a first-class, server-side **session** at interactive
  sign-in (persisted as a `PersistedGrant` of type `session` keyed by an opaque `sid`, with no new table or migration),
  mints the `sid` onto the login cookie, and emits the `sid` claim into ID tokens. A new RP-Initiated Logout
  `end_session_endpoint` (self-advertised in discovery) validates the `id_token_hint`, clears the OP login, revokes the
  subject's grants and session, notifies participating relying parties, and redirects to a validated
  `post_logout_redirect_uri` (echoing `state`). **Back-Channel Logout** is delivered by a pluggable
  `IBackChannelLogoutNotifier` whose default implementation builds a signed `logout_token` and `POST`s it,
  server-to-server, to each client's `backchannel_logout_uri`. The endpoint follows the family's discrete-message +
  mediator-pipeline conventions: the request is a typed `IEndSessionRequest` loaded via `LoadEndSessionRequestCommand`,
  and the logout side effects are an extension seam — applications add an `ICommandHandler<OnEndSessionCommand>` (for
  example, custom auditing) alongside the default revocation and back-channel handlers. New per-client settings
  (`post_logout_redirect_uris`, `backchannel_logout_uri`, `backchannel_logout_session_required`) and server discovery
  settings (`backchannel_logout_supported`, `backchannel_logout_session_supported`) are added. The two
  third-party-cookie/iframe mechanisms (Front-Channel Logout and Session Management `check_session_iframe`) are
  **deliberately not** implemented because the browser platform is removing third-party cookies; the decision and the
  forward-looking design are captured in [ADR-0074](docs/adr/0074-session-management-and-logout.md).
- **End-user and administrator sign-out UX, and a first-party account page on the OP.** Both front-ends gained a
  sign-out control. The **Management console** (`@ncode/app-mgmt`) shows the signed-in subject and a header **Sign out**
  that clears local tokens and redirects to the OP's `end_session_endpoint` (RP-Initiated Logout), replaying the ID
  token as `id_token_hint`; the auth store now persists the ID token (across silent renewals) for exactly this, and the
  logout URL is built by a new `buildEndSessionUrl` helper in `@ncode/auth`. The OP's **interactive surface**
  (`@ncode/app-auth`) gained a first-class account ("me") page at `/account` that fetches `/account/me` (a new
  same-origin JSON endpoint) and renders the current session (subject, `auth_time`, `amr`, `idp`, `sid`) plus a
  **Sign out** control. That control `POST`s (antiforgery-protected) to `/account/logout`, which is hardened from a bare
  cookie-clearing `GET` into a `POST` that runs the **full server-side logout pipeline** — the same `EndSessionCommand`
  used by the `end_session_endpoint`, but cookie-identified (no `id_token_hint`), so a first-party sign-out also revokes
  the session and grants and fires back-channel notifications ([ADR-0074](docs/adr/0074-session-management-and-logout.md)).
  The `apps/auth` shell now resolves its surface (account/consent/login) through a small **declarative route table**
  instead of nested conditionals.
- **Runtime frontend bootstrap endpoint on the OpenID Provider host — completes backlog `U4`.** The tier-agnostic
  frontend bundles ([ADR-0064](docs/adr/0064-frontend-presentation-platform.md)) discover their deployment coordinates
  at boot from `/config/bootstrap.json` on their serving host; previously only a static dev copy existed.
  `NCode.Identity.Server` now exposes a first-class
  `IIdentityServerBuilder.AddFrontendBootstrap(configuration)` seam that binds the `Frontend` configuration section
  into `FrontendBootstrapOptions` and registers the endpoint (as an `IEndpointProvider`) that publishes the bootstrap
  document (API base, issuer, client id, scopes, feature flags, branding) — so the host's single `MapEndpointGroups()`
  call maps it alongside every other endpoint. The document falls back to the serving request's own origin for the API
  base and issuer when they are unset, so a single-origin host serves every tier from one bundle with no configuration.
  The Playground opts in (`AddFrontendBootstrap`) and publishes the dev document (`mgmt-spa`, `dcr` feature flag, dev
  branding). For local development the standalone Management Vite dev server no longer ships a static copy: it
  **proxies** `/config/bootstrap.json` to the Playground host (the Aspire service-discovery target injected via
  `WithReference(api)`, [ADR-0071](docs/adr/0071-aspire-local-dev-orchestrator.md)), so local dev consumes the real
  runtime endpoint and cannot drift.
- **Algorithm capabilities discovery drives the create-secret form — completes backlog `S6`.** The management
  capabilities document ([ADR-0067](docs/adr/0067-secret-type-capability-catalog-and-capabilities-discovery.md)) grows
  an `algorithms` section published by a new `AlgorithmCapabilitiesProvider`
  (`ICommandHandler<DiscoverManagementCapabilitiesCommand>`). It projects every supported signature and key-management
  algorithm from the live `IAlgorithmCollectionProvider` as an `AlgorithmCapabilityResource` carrying its `code`, the
  implied `use` (`sig`/`enc`), the compatible secret-type discriminators (cross-referenced against the
  `ISecretKeyTypeCatalog`), and its legal `keyBitSizes` ranges — so the console create-secret form drives its
  **algorithm**, **use**, and **key-size** inputs from discovery instead of free-text defaults: the algorithm picker
  offers only algorithms compatible with the chosen secret type, selecting one derives the read-only use and clamps the
  key size into the algorithm's legal range (min/max/step). A new capability is still added only by registering a
  provider, never by editing the endpoint or resource type.
- **Server create/edit management across the stack — completes backlog `U9`.** The Servers surface is no longer
  read-only: an OpenID server now carries an editable top-level **display name**, and the management console lists
  servers with a **Create server** action plus row-level **edit/delete** (mirroring tenants and resource servers) in
  addition to the existing drill-in to a server's secrets/settings. Backend: `ServerResource`/`CreateServerRequest`
  gain `DisplayName`, a new `UpdateServerRequest` + `PATCH /api/servers/{serverId}` endpoint lands (JSON Patch with
  `If-Match`, validated by `IServerValidator.ValidateUpdateAsync`), and `DisplayName` is persisted on `PersistedServer`
  (and the EF `ServerEntity`) and round-tripped through `IServerStore`. Audit reaches parity with the other resources:
  a new `ServerChangedAuditEvent` (`ManagementResourceTypes.Server`) and `IManagementAuditRecorder.RecordServerChangedAsync`
  record **create/update/delete** (servers are the `GlobalAdmin`-only control plane, so the event carries no tenant).
  Frontend: new `createServer`/`updateServer`/`deleteServer` RTK Query mutations and a `ServerFormDialog`.
- **Public-key export, a secret-type capability catalog, and composable capabilities discovery
  ([ADR-0067](docs/adr/0067-secret-type-capability-catalog-and-capabilities-discovery.md)).** The management console
  can now export an asymmetric secret's **public key** (JWK + PEM, copy/download) from the secrets table; the action
  is gated by server discovery, not a hard-coded list. Server-side, a single `ISecretKeyTypeCatalog` (core
  `NCode.Identity.Secrets`) becomes the one source of truth for the secret key-type taxonomy, key-size validation,
  key generation, and public-key-export capability — the generator, validator, and public-key converter all consult
  it and their per-type `switch` statements are deleted. PEM export moves into the domain
  (`AsymmetricSecretKey.ExportAsymmetricAlgorithm()` + base `ExportSubjectPublicKeyInfoPem()`). A new
  `GET /api/capabilities` publishes a `[JsonExtensionData]` discovery document composed — like the OpenID discovery
  document — from registered `ICommandHandler<DiscoverManagementCapabilitiesCommand>` providers, so new capabilities
  are exposed by registering a provider rather than editing the endpoint.
- **Unified, extensible elliptic-curve catalog with per-request curve selection
  ([ADR-0068](docs/adr/0068-unified-extensible-elliptic-curve-catalog.md)).** A single core `IEccCurveCatalog`
  replaces the generator's hard-coded `bits → curve` switch **and** the authentication-layer
  `IEccCurveSpecificationRegistry`; curves are selected by identity (name/OID), not by size, so same-size curves
  (for example `secp256k1` vs `P-256`) are disambiguated for both generation and JWK `crv` naming. `CreateSecretRequest`
  gains an optional `curve`, generation flows through a growable `SecretKeyGenerationParameters`, the JWKS converter
  names `crv` from the key's actual curve, the capabilities document exposes an `eccCurves` section, and the console
  create-secret form offers a server-driven curve picker. Applications add a curve by registering one
  `EccCurveSpecification`.

- **Management console resource-server secrets/settings (`apps/mgmt`) — completes backlog `U2`.** Selecting a resource
  server (API) in a tenant scope now drills into a `ResourceServerDetailPage` with **Scopes**/**Secrets**/**Settings**
  tabs, reusing the shared owner-agnostic `SecretsManager`/`SettingsManager` containers so the resource server behaves
  exactly like clients/tenants/the server. The tabs wire the backend owner endpoints added in
  [ADR-0066](docs/adr/0066-resource-server-settings-and-secret-owner.md) via new scoped RTK Query
  endpoints (`listResourceServerSecrets`/`createResourceServerSecret`/`deleteResourceServerSecret`,
  `getResourceServerSettings`/`getResourceServerEffectiveSettings`/`updateResourceServerSettings`), honoring the
  one-time secret reveal ([ADR-0050](docs/adr/0050-secret-material-revealed-once-on-create.md)) and the top-level
  add/remove/replace JSON Patch `diffPatch` save with the owner `concurrencyToken` as `If-Match`. This closes `U2`
  (management console secrets/settings for every backend-supported owner + the initial-access-token mint); runtime
  consumption of resource-server secrets and resource-server audit events remain tracked under `U7`.
- **Resource servers are first-class settings and secret owners (backend).** Completes the backend half of backlog
  `U2`: a resource server (an API, in Auth0 terms) now owns a wrapped settings object (its own concurrency token) and a
  secret collection, structurally identical to clients/tenants/servers. New management endpoints —
  `GET`/`PATCH api/tenants/{tenantId}/resource-servers/{id}/settings`, `GET .../effective-settings` (server → tenant →
  resource-server merge), and the full `.../secrets` CRUD honoring the one-time reveal
  ([ADR-0050](docs/adr/0050-secret-material-revealed-once-on-create.md)) with optimistic concurrency
  (`concurrencyToken` → `If-Match`). The persistence model gains `PersistedResourceServerSettings`/`…Secrets` (and a
  `BasePersistedResourceServerResource` base, `ISupportResourceServerId`, the `resource_server` resource type),
  `IResourceServerStore` gains `UpdateSettingsAsync` + secret CRUD, and EF Core adds
  `SettingsConcurrencyToken`/`SecretsConcurrencyToken` columns and a tenant-scoped `ResourceServerSecrets` join table
  (migration `AddResourceServerSecrets`); deleting a resource server cascades its scopes and secrets. A
  `resource_server_secrets` scope family is seeded. Rationale and the "an API _is_ a secret owner (per-audience HS256 /
  JWE material)" decision: [ADR-0066](docs/adr/0066-resource-server-settings-and-secret-owner.md). Runtime consumption
  of resource-server secrets and resource-server audit events are deferred (see [`BACKLOG.md`](BACKLOG.md)).
- **Management console secrets/settings for tenants + the server, and initial-access-token mint (`apps/mgmt`).**
  Extends the client **Secrets**/**Settings** pattern to the remaining backend-supported settings/secret owners and
  surfaces protected dynamic-client-registration bootstrap. The client secrets/settings tab logic was factored into
  shared, owner-agnostic `SecretsManager`/`SettingsManager` containers (the client detail view now consumes them).
  **Tenant:** a tenant-scope **Tenant** tab manages the tenant's own secrets and settings
  (`listTenantSecrets`/`createTenantSecret`/`deleteTenantSecret`, `getTenantSettings`/`getTenantEffectiveSettings`/
  `updateTenantSettings`). **Server:** a global-scope **Servers** tab (new stateless MUI `ServerTable`) drills into a
  server to manage its secrets and settings (`listServerSecrets`/`createServerSecret`/`deleteServerSecret`,
  `getServerSettings`/`getServerEffectiveSettings`/`updateServerSettings`). Both honor the one-time secret reveal
  ([ADR-0050](docs/adr/0050-secret-material-revealed-once-on-create.md)) and the top-level add/remove/replace JSON
  Patch `diffPatch` save with the owner `concurrencyToken` as `If-Match`. **Initial access token (RFC 7591):** the
  **Grants** tab gained a **Mint initial access token** action (`createInitialAccessToken`, `Grant`-tag invalidation)
  with a form (optional description + lifetime) and a one-time `RevealInitialAccessTokenDialog` that shows the plaintext
  token exactly once. Fourth slice of backlog `U2`; the only `U2` remnant is resource-server (API) settings/secrets,
  which is blocked on backend owner endpoints that do not yet exist.
- **Management console grants, secrets, and settings write operations (`apps/mgmt`).** Advances backlog `U2` by
  extending the shared platform to the remaining management surfaces. **Grants:** a new tenant **Grants** tab lists
  persisted grants with subject/client filters and revokes them one-by-one or in a filtered bulk operation
  (`listGrants`/`revokeGrant`/`revokeGrants` with `Grant`-tag invalidation; the server requires a bulk revoke to carry
  at least one filter). Grants are read-only administratively, so only active grants expose a revoke control. **Client
  secrets:** selecting a client drills into a detail view whose **Secrets** tab creates/lists/deletes secrets
  (`listClientSecrets`/`createClientSecret`/`deleteClientSecret`), honoring the one-time reveal — the generated key
  material is shown exactly once in a `RevealSecretDialog` with a copy affordance and never re-fetched
  ([ADR-0050](docs/adr/0050-secret-material-revealed-once-on-create.md); asymmetric secrets have no material to
  reveal). **Client settings:** the detail view's **Settings** tab edits the opaque persisted settings JSON alongside a
  read-only effective-settings view (`getClientSettings`/`getClientEffectiveSettings`/`updateClientSettings`); because
  settings are an open object, saves send a top-level add/remove/replace JSON Patch diff (a new UI-free `@ncode/api`
  `diffPatch` helper) with the owner `concurrencyToken` as `If-Match`. New stateless MUI `GrantTable`/`SecretTable`/
  `SecretFormDialog`/`RevealSecretDialog`/`SettingsEditor` (all Storybook-driven and unit-tested). All client-owned
  surfaces are addressed on the tenant-nested path so a tenant administrator can never reach another tenant.
- **Management console resource-server + scope write operations (`apps/mgmt`).** Within a tenant scope the console now
  creates/edits/deletes resource servers (APIs) and manages their nested scopes (permissions): scoped RTK Query
  mutations (`createResourceServer`/`updateResourceServer`/`deleteResourceServer` and `createScope`/`updateScope`/
  `deleteScope`) with `ResourceServer`-tag invalidation, stateless MUI `ResourceServerTable`/`ScopeTable` with row
  actions and `ResourceServerFormDialog`/`ScopeFormDialog`, reusing the shared `ConfirmDialog`. Resource servers embed
  their scopes inline, so selecting one drills into a scopes view fed by the same list query (a scope mutation
  refreshes both); resource-server edits honor optimistic concurrency (`concurrencyToken` → `If-Match` over a
  `replace`-only JSON Patch) while a scope update is a whole-resource `PUT` (the value is immutable), and system-owned
  resource servers/scopes expose no delete control. The tenant workspace switches between **Clients** and **Resource
  servers** via tabs. All addressed on the tenant-nested path so a tenant administrator can never reach another tenant.
  Third slice of backlog `U2`.
- **Management console client write operations (`apps/mgmt`).** Within a tenant scope the console now creates/edits/
  deletes clients: scoped RTK Query mutations (`createClient`/`updateClient`/`deleteClient`) with `Client`-tag
  invalidation, a stateless MUI `ClientTable` (id + status + row actions) and `ClientFormDialog`, reusing the shared
  `ConfirmDialog`. Writes honor optimistic concurrency (`concurrencyToken` → `If-Match` over a `replace`-only JSON
  Patch) and the tenant-nested path (`api/tenants/{tenantId}/clients/...`), so a tenant administrator can never address
  another tenant. Second slice of backlog `U2`.
- **Management console tenant write operations (`apps/mgmt`).** The console gained full tenant create/edit/delete on
  the shared frontend platform: RTK Query mutations (`createTenant`/`updateTenant`/`deleteTenant`) with tenant-scoped
  cache-tag invalidation, a stateless MUI `TenantFormDialog` and reusable `ConfirmDialog` (Storybook-driven), and
  row-level edit/delete actions on the tenant table. Edits honor optimistic concurrency by sending the resource's
  `concurrencyToken` as an `If-Match` header over a `replace`-only JSON Patch document; a UI-free `replacePatch` helper
  (`@ncode/api`) and an RFC 7807-aware `apiErrorMessage` helper (`@ncode/store`) are unit-tested directly. First slice
  of backlog `U2`.
- **Interactive login/consent surface (`apps/auth`, OP-hosted).** The OpenID Provider gained its first interactive
  surface: a React + Material UI login/consent app (`@ncode/app-auth`) on the shared frontend platform, served by the
  host as same-origin static assets under `/account/*`, backed by an opt-in leaf capability package
  `NCode.Identity.OpenId.Authentication.Interactive` (+ `.Abstractions`)
  (`AddInteractiveAuthentication` / `UseInteractiveAuthentication`). Its endpoints register as an
  `IEndpointProvider` under an `InteractiveAuthenticationEndpointGroup`, so the host's single `MapEndpointGroups` call
  maps the `/account/*` surface alongside every other endpoint (no bespoke mapping extension). It registers a
  cookie scheme (`Identity.Application`) that the authorize endpoint's challenge redirects into, defers credential
  verification to a pluggable `IInteractiveCredentialVerifier` seam (the local-account implementation,
  `AddLocalAccountInteractiveAuthentication()`, reuses the same path the resource-owner password grant uses), issues the
  cookie session, and honors a server-validated local return URL. The forms expose stable automation ids (`username`,
  `password`, `login`, `consent-allow`, `consent-deny`). This resolves the rendering and first-class-vs-dev choices left
  open by [ADR-0062](docs/adr/0062-opt-in-interactive-login-consent-ui.md); the surface is hosted per
  [ADR-0065](docs/adr/0065-interactive-login-consent-op-hosted-auth-surface.md) and extracted into its own leaf package
  with the pluggable verifier seam per
  [ADR-0070](docs/adr/0070-interactive-login-consent-leaf-package-and-credential-verifier-seam.md). Backlog `T2`, `U3`.
- **Silent access-token renewal for the Management console.** `@ncode/auth` gained a framework-agnostic renewal loop
  (`createTokenRenewer`) that refreshes the access token ahead of expiry, re-arms itself from each new token, and falls
  back to interactive sign-in when renewal is no longer possible; the console's `AuthGate` drives it while signed in.
  Backlog `U1`.
- **Development seeding for end-to-end sign-in (`AddDeveloperInteractiveSeed`).** A development-only, idempotent seeder
  provisions a public (PKCE-only) single-page-application client (`mgmt-spa`, with redirect URIs) and a deterministic
  administrator user into each workload tenant, so the authorization-code + PKCE flow and the management console sign in
  end to end. Inert unless configured; the administrator password has no default. Backlog `U7`, `U1`.

- **Management console sign-in (authorization-code + PKCE).** The frontend gained a framework-free `@ncode/auth`
  package implementing OAuth 2.0 authorization-code with PKCE (S256) — verifier/challenge generation, the authorize
  redirect, state/CSRF validation, code-for-token exchange, refresh, and JWT subject extraction, all injectable and
  unit-tested (including the RFC 7636 test vector). The Management console (`@ncode/app-mgmt`) wires an `AuthGate` that
  completes the redirect callback for a `urn:ncode:management`-audience token and populates the auth slice, or renders a
  sign-in screen to start the flow; when no client id is discovered it runs open (local dev). The SPA's public client
  id and login scopes are discovered at runtime via the bootstrap document (tier-agnostic). Backlog `U1`.
- **One-stop full-stack dev launch via .NET Aspire ([ADR-0071](docs/adr/0071-aspire-local-dev-orchestrator.md)).** A
  sibling AppHost project (`NCode.Identity.OpenId.Playground.AppHost`) orchestrates the whole local stack — the
  Playground OP host (`api`), the interactive-login build-watcher (`auth`), and the Management console Vite dev server
  (`mgmt`) — as one Aspire resource graph behind the `Aspire: Full stack (F5)` launch. Aspire gates each UI resource on
  the API being **healthy** (`WithHttpHealthCheck("/health")` + `WaitFor`), deterministically eliminating the previous
  compound launch's UI-before-API race, and its dashboard is one place for ordered startup, logs, and traces. Ports
  stay pinned (`api` on `https://localhost:5001`, `mgmt` on `http://localhost:5173`, un-proxied) so the developer seed
  redirect URI, developer CORS origins, and the browser bootstrap document stay valid; `apps/auth` is modeled as a
  build-watcher (no endpoint) whose `dist` the Playground serves. The AppHost is never packed. This replaces the VS
  Code `API + UI (F5)` compound; the old `build/dev-ui.ps1` is relegated to `build/frontend-install.ps1`, a one-time
  pnpm-workspace bootstrap, now that Aspire owns `pnpm dev`. Backing SQL Server/Redis remain opt-in (SQLite/in-memory
  stays the default, so there is no container-runtime prerequisite). Backlog `U11`.
- **Opt-in developer CORS for the OpenID server (`AddDeveloperCors` / `UseDeveloperCors`).** A reusable
  development-oriented registration (in `NCode.Identity.Server`, beside `AddDeveloperKeys`) lets a local SPA dev server
  read discovery and call the management API cross-origin. Allowed origins come from the `Cors:AllowedOrigins`
  configuration array, defaulting to the local Vite dev/preview origins in Development; it is a no-op otherwise. The
  Playground opts in with a single call.

- **Frontend presentation platform (`src/frontend`).** Introduced a single cohesive TypeScript UI platform shared by
  every surface (Management, Auth/Consent/Login, Playground, OIDC conformance): React + Material UI, Redux Toolkit +
  RTK Query, Vite, Vitest + React Testing Library, and Storybook, orchestrated by pnpm workspaces + Turborepo and
  pinned via Corepack. Shared `@ncode/*` packages (`config` runtime discovery, `api` RTK Query client + global/tenant
  scope model, `store` Redux assembly, `ui` MUI design system) back thin per-surface app shells; the first is the
  Management console (`@ncode/app-mgmt`) with global-vs-tenant boundary browsing. Bundles are tier-agnostic — all
  configuration is discovered at runtime from the serving host, nothing is baked at build time — and the branding seam
  is in place for future per-tenant theming. The toolchain is isolated under `src/frontend` and does not touch the
  .NET Definition of Done. See [ADR-0064](docs/adr/0064-frontend-presentation-platform.md).

- **Initial access token provisioning for protected Dynamic Client Registration.** The management API can now mint the
  `initial_access_token` that protected registration (the default, `registration_endpoint_allow_anonymous = false`)
  requires: a tenant-scoped `POST api/tenants/{tenantId}/grants/initial-access-tokens` creates a hash-keyed
  `initial_access_token` grant (optional `description` and `lifetimeSeconds`) and returns the one-time plaintext token
  — revealed once, never retrievable again — together with the grant id for later revocation through the grant
  management endpoint, recording an `InitialAccessTokenCreatedAuditEvent`. This closes the provisioning gap in
  [ADR-0063](docs/adr/0063-dynamic-client-registration.md): a deployment that wants protected registration no longer
  has to seed grants out of band or replace the `IInitialAccessTokenValidator` seam. See
  [ADR-0063](docs/adr/0063-dynamic-client-registration.md).
- **OAuth 2.0 Dynamic Client Registration (RFC 7591/7592).** Clients can now register themselves and manage their own
  registration. A new `registration_endpoint` (`POST /oauth2/register`), advertised in discovery, accepts a JSON
  client-metadata document and returns the assigned `client_id`, a one-time `client_secret` for a confidential client,
  and a `registration_access_token` plus `registration_client_uri`; the per-client management endpoint
  (`GET`/`PUT`/`DELETE /oauth2/register/{client_id}`, RFC 7592) lets the client read, replace, or delete its
  registration using that token. Metadata maps onto the existing client settings document and the `client_secret` is
  the existing one-time secret, so client authentication works unchanged; both the `registration_access_token` and the
  RFC 7591 `initial_access_token` are modeled as hash-keyed persisted grants — no new schema. Registration is
  configurable and protected by default: a new `registration_endpoint_allow_anonymous` setting (default `false`, merged
  with the boolean meet so it cannot be loosened downward) governs whether an initial access token is required, and the
  deployment-specific `IInitialAccessTokenValidator` seam decides what counts as a valid initial access token. New
  error codes `invalid_client_metadata` and `invalid_redirect_uri` are added. See
  [ADR-0063](docs/adr/0063-dynamic-client-registration.md).
- **OAuth 2.0 Device Authorization Grant (RFC 8628).** Input-constrained clients (smart TVs, CLIs, IoT devices) can now
  obtain tokens by having the end user authorize on a second device. A new `device_authorization_endpoint`
  (`POST /oauth2/device_authorization`) issues a `device_code`, a human-typable `user_code`, a `verification_uri`, and
  a polling `interval`; a user-facing verification endpoint (`/oauth2/device`) authenticates the end user through the
  existing subject-authentication seam and records the approve/deny decision; and the
  `urn:ietf:params:oauth:grant-type:device_code` token grant polls the request, returning `authorization_pending`,
  `slow_down`, `expired_token`, or `access_denied` until it is approved and redeemed for tokens exactly once. The
  pending request is persisted as a `PersistedGrant` payload keyed by `device_code` with a companion `user_code`
  pointer grant — no new schema — enabled by a new general-purpose `IPersistedGrantService.UpdatePayloadAsync`. New
  settings `device_code_lifetime`, `device_code_polling_interval`, and `user_code_length` tune the flow, and
  `user_code` generation is a replaceable `IUserCodeGenerator` seam. The endpoint and grant type are advertised in
  discovery automatically. See [ADR-0059](docs/adr/0059-device-authorization-grant.md).
- **Asymmetric secret public-key read endpoints.** A new read-only sub-resource,
  `GET /api/.../{servers|tenants|clients}/{ownerId}/secrets/{secretId}/public-key`, returns an asymmetric secret's
  **public** key material as a `PublicKeyResource` carrying both a JWK (RFC 7517, the same representation the JWKS
  endpoint publishes, reusing the registered `IJsonWebKeyConverter` pipeline) and a SubjectPublicKeyInfo PEM
  (`-----BEGIN PUBLIC KEY-----`) for copy-paste into an admin UI or crypto tooling. Private key material is never
  exposed; a symmetric secret has no public-key sub-resource and responds `404 Not Found`. The read is gated by the
  same `Operations.Read` authorization as reading the secret. See
  [ADR-0058](docs/adr/0058-asymmetric-public-key-material-read-path.md).
- **Local-account management API.** A new `local-accounts` resource family under
  `/api/tenants/{tenantId}/local-accounts` lets a tenant/global administrator create, list, get, update, delete,
  disable, enable, reset the credential of, manage the profile claims of, and read/set the server-owned metadata of a
  local account. Account creation eagerly provisions the owning `FederatedPrincipal` and its one-to-one self-issued
  `FederatedIdentity` in the same unit of work, so principal-level metadata (`ProfileMetadata` / `SystemMetadata`) has a
  stable target from the moment the account exists; the metadata endpoint is the administrator-gated write path that
  upholds the "`SystemMetadata` is never end-user-writable" invariant. Local accounts are end-user subjects rather than
  ownable resources, so every operation authorizes against the owning tenant scope. The endpoints require a registered
  `ILocalAccountStore` and otherwise respond `501 Not Implemented`. Changes are audited via
  `LocalAccountChangedAuditEvent` and gated by `*:local_accounts` scopes. See
  [ADR-0060](docs/adr/0060-local-account-one-store-seam-and-dependency-inverted-authentication.md) and
  [ADR-0057](docs/adr/0057-local-account-eager-provisioning-and-management-seam.md).
- New dependency-free **`NCode.Json`** package holding the shared `JsonElements` helper (`EmptyObject` /
  `OrEmptyObject` / `IsNullOrUndefined`, namespace `NCode.Json`), promoted out of `NCode.Identity.Abstractions` so the
  persistence layer (and any package beneath `NCode.Identity.*`) can reuse it without inheriting that package's ASP.NET
  Core framework reference. See [ADR-0055](docs/adr/0055-subject-metadata-is-a-principal-level-concept-resolved-at-issuance.md).
- Two new abstractions packages split out of `NCode.Identity.Abstractions`:
  **`NCode.Identity.AspNetCore.Abstractions`** (the ASP.NET Core HTTP surface — `UriDescriptor`, `HttpResultException`,
  and the `Results/*` contracts; it now carries the `Microsoft.AspNetCore.App` framework reference) and
  **`NCode.Identity.Settings.Abstractions`** (the `NCode.Identity.Settings.*` subsystem). Namespaces are unchanged, so
  the move is transparent to source. See
  [ADR-0056](docs/adr/0056-abstractions-split-into-framework-free-tiered-packages.md).

### Fixed

- **Client-aborted requests at the OpenID endpoint pipeline are no longer rendered as `server_error` or audited.**
  When a client disconnects mid-request (for example a browser/SPA cancelling an in-flight discovery fetch), the
  resulting `OperationCanceledException` whose token is `HttpContext.RequestAborted` is a normal disconnect, not a
  protocol failure. `OpenIdExceptionEndpointFilter` now rethrows request-abort cancellations so ASP.NET Core abandons
  the request, instead of invoking the OpenID exception handler to write an error response to a dead connection and
  record an `openid.error` audit row (whose own write would then fail against the already-cancelled token).
- **The UserInfo endpoint now authenticates the presented self-issued bearer access token in-environment, instead of a
  host-configured default authentication scheme that never existed.** `DefaultAuthenticateSubjectHandler` resolved the
  subject via `HttpContext.AuthenticateAsync()` (the host's default scheme), but per-tenant signing keys and issuer are
  only known after the request environment resolves the tenant, so no pre-routing scheme can validate the token — the
  reference host registered none, and every valid access token presented to `/userinfo` was rejected `401
invalid_token`. The handler now validates a presented `Authorization: Bearer` token through the same in-environment
  self-issued `AuthenticateAccessTokenCommand` path the management API uses ([ADR-0044](docs/adr/0044-management-api-authentication-and-globaladmin-bootstrap.md)),
  requiring the OpenID identity audience (`urn:ncode:openid`); requests that instead carry an interactive session (for
  example device verification) have no bearer header and still fall back to the session cookie. Invalid/expired tokens
  keep their `401` semantics.
- **A stale or invalid end-user session cookie now self-heals at the authorization endpoint instead of dead-ending with
  `access_denied`.** When `AuthenticateAsync` returned a _failure_ (a present-but-invalid session cookie — for example
  undecryptable after a data-protection key change, expired, or tampered), the authorize flow surfaced
  `access_denied` ("Failed to authenticate the end-user") back to the client, so the user could never reach the login
  form. An invalid cookie is now treated as "no session": the authorize flow prompts for interactive login (or returns
  `login_required` under `prompt=none`), and a successful login re-issues a fresh cookie. Bearer-token subject
  authentication (userinfo/token) keeps its `401` semantics and is unchanged.
  response executor serialized every ticket parameter directly, so an internal, `$`-prefixed parameter
  (`$created_when`) surfaced in the redirect callback query string / form-post body sent back to the client. The
  executor now honors the same `ShouldSerialize(…, SerializationFormat.OpenId)` contract the JSON serializer uses — the
  identical mechanism that already marks `$created_when`/`$authorization_source_type` as JSON-only — so internal
  parameters are omitted from every on-the-wire authorization response (query, fragment, and form-post).
- **Management console client mirror referenced a nonexistent `displayName`.** The frontend `ClientResource` type (and
  the client list UI) read a `displayName` field that the server's `ClientResource` never emits — a client has no
  display name; it is identified by its server-generated id. The TypeScript mirror was corrected to the real shape
  (`{ clientId, tenantId, concurrencyToken, isDisabled }`) and the console now lists clients by id.
- **Multi-audience access tokens are no longer wrongly rejected.** JWT claim validation for a multi-valued claim (the
  `aud` audience validator) required _every_ token value to appear in the expected set, so a token minted with more than
  one audience (for example `["urn:ncode:management", "urn:ncode:openid"]`) failed validation at any resource that
  expected only one of them. Validation now follows RFC 7519 §4.1.3 audience semantics: the claim is valid when it
  contains **at least one** of the expected values, and extra audiences are permitted. This was the blocker that kept a
  signed-in Management console from calling the management API (it was rejected as `invalid_token`). Single-valued
  issuer validation is unchanged.
- **Access-token validation failures at protected endpoints are now observable.** The self-issued access-token
  authenticator silently swallowed both validation exceptions and invalid results, so a rejected token surfaced only as
  an opaque `401`. It now emits a debug log (with the triggering exception) explaining why the token was rejected
  (issuer, audience, lifetime, or signature), while still returning `invalid_token`.
- **Interactive authorization-code exchange no longer drops the authenticated subject.** The authorization-code grant
  was persisted as the authorization request alone, but the token endpoint resumes it as an authorization _grant_
  (request **plus** authenticated subject); the shape mismatch deserialized the request to `null` (a server error on
  exchange) and discarded the subject. The grant is now persisted in the shape the token endpoint consumes.
- **Repeated interactive logins no longer fail antiforgery validation.** The Playground ordered `UseAntiforgery` before
  `UseAuthentication`/`UseAuthorization`. Because the interactive surface registers a single cookie scheme — which
  ASP.NET Core promotes to the default authenticate scheme — the login page authenticates the user once a session
  cookie exists and mints a claims-bound antiforgery token, but the middleware validated the POST before authentication
  ran, i.e. against an anonymous user. Every login after the first failed with _"The provided antiforgery token was
  meant for a different claims-based user than the current user"_. The middleware is now ordered after authentication;
  an integration test (`InteractiveLoginAntiforgeryTests`) locks the sequence in.
- **Management console sign-in no longer dead-ends on the OAuth callback under React StrictMode.** `AuthGate` completed
  the authorization-code callback in an effect guarded by a cancellation flag, but StrictMode double-invokes effects in
  development: the first run consumed the single-use code and one-shot PKCE transaction, so the second run found no
  transaction and showed _"No in-flight authorization transaction was found."_ Completion is now guarded by a run-once
  ref so it executes exactly once.
- **Interactive login bundle is now produced by the full-stack dev launch.** The `apps/auth` login surface is served by
  the host from a prebuilt `dist`, which nothing in the F5 path built, so `/account/login` returned a 503 _"interactive
  login bundle is not available"_. The dev launch now build-watches every frontend app (see the full-stack dev launch
  entry above), so the login bundle is produced automatically.

### Changed

- **Comprehensive XML-documentation quality pass across the published packages.** The `///` doc comments that reach
  consumers through IntelliSense and the generated OpenAPI descriptions were swept for correctness and consistency:
  property summaries follow the Microsoft convention (an `init`-only property opens with "Gets", not "Gets or sets");
  mediator-command types carry substantive, purpose-first summaries with complete `<param>` documentation; ADR numbers
  and other internal citations were moved out of `///` comments into `//` code comments; and a batch of grammar/typo
  defects was corrected (including a copy-paste error where the access-token payload-claims handler's summary described
  an ID token). No API or behavior change.

- **Discovery metadata is now secure-by-default, operator-overridable — completes backlog `T5`.** The OpenID Provider
  no longer advertises deprecated/insecure flows out of the box
  ([ADR-0083](docs/adr/0083-secure-by-default-discovery-metadata.md)). `response_types_supported` defaults to
  `["code"]` (dropping the implicit/hybrid combinations the formatter expands), `grant_types_supported` drops
  `implicit`, `code_challenge_methods_supported` defaults to `["S256"]` only, and `subject_types_supported` now defaults
  to `["public"]` so the required OpenID Connect Discovery field is present. The cross-trust-boundary token-signing
  fields (`id_token`/`userinfo`/`access_token`) advertise **asymmetric** algorithms only — never symmetric `HS256`
  (`request_object` and `token_endpoint_auth` signing keep the full set for `client_secret_jwt`). Because discovery and
  request-time enforcement share one settings source, the implicit/hybrid authorization code paths are **fail-closed**
  by default; a deployment re-enables any of these — and simultaneously re-advertises them — by widening the setting in
  configuration (host `OpenId:Server:Settings` overrides are applied last and overwrite the baseline). The Playground
  `appsettings.json` no longer over-advertises `implicit`. Greenfield, pre-release — no compatibility shims
  ([ADR-0025](docs/adr/0025-pre-release-posture-and-auth0-parity-plus.md)).

- **Developer-key registration no longer owns the Data Protection key-ring location (breaking,
  `NCode.Identity.Server`).** `AddDeveloperKeys(DeveloperKeyMode)` drops its `IHostEnvironment` parameter and
  `AddDeveloperSigningKey()` drops its `DirectoryInfo keyRingDirectory` parameter — the library now only seeds the
  signing key, and the **host** owns where Data Protection keys are persisted (the Playground persists them in its dev
  database, [ADR-0073](docs/adr/0073-postgres-dev-database-and-aspire-migration-worker.md)). The `App_Data/dp-keys`
  file key ring and the `Microsoft.EntityFrameworkCore.SqlServer`/`.Sqlite` Playground provider references are removed.

- **Opt-in framework capabilities now register through compile-time marker builders
  ([ADR-0069](docs/adr/0069-registration-library-compile-time-marker-nesting.md)).** The registration library's
  `IServiceBuilder<TMarker>` becomes the one way bolt-on capabilities attach, so a capability is **un-addable** to a
  host that lacks its prerequisite library (a compile error, not a runtime surprise). `AddInteractiveAuthentication(...)`
  now extends `IServiceBuilder<OpenIdAuthenticationLibrary>`, and the local-account capabilities extend their OpenId
  library builder and take the store as a **required parameter**
  (`AddLocalAccountAuthentication(IServiceBuilder<LocalAccountLibrary>)` /
  `AddLocalAccountInteractiveAuthentication(IServiceBuilder<LocalAccountLibrary>)` on
  `IServiceBuilder<OpenIdAuthenticationLibrary>`; `AddLocalAccountManagement(IServiceBuilder<LocalAccountLibrary>)` on
  `IServiceBuilder<OpenIdManagementLibrary>`), each returning its receiver builder for chaining, instead of a naked
  `IServiceCollection`. The `OpenIdManagementLibrary` marker moves from `NCode.Identity.OpenId.Management`
  into `NCode.Identity.OpenId.Management.Abstractions` so a capability can name it without an implementation reference. Hosts
  now chain these off the builders the composition root exposes — for example
  `AddIdentityServer().OpenIdAuthenticationLibraryBuilder.AddLocalAccountAuthentication(localAccountStore)` —
  rather than a flat `serviceCollection.AddX()` list. The persistence framework gains a `StoreManagerLibrary` marker
  (`NCode.Persistence.Abstractions`): `AddEntityFrameworkCorePersistence<TDbContext>()` mints it, and the local-account
  store slices now receive `IServiceBuilder<StoreManagerLibrary>` (not a naked collection) and return
  `IServiceBuilder<LocalAccountLibrary>`. Chain roots and generic utility helpers intentionally stay bare-collection
  extensions.
- **Foundational JOSE/JWT libraries now gate at compile time, completing the marker-nesting model
  ([ADR-0069](docs/adr/0069-registration-library-compile-time-marker-nesting.md)).** `AddJoseLibrary()` now extends
  `IServiceBuilder<SecretsLibrary>` and `AddJsonWebTokensLibrary()` extends `IServiceBuilder<JoseLibrary>` (each
  returns its own library builder) instead of taking a naked `IServiceCollection` and asserting the prerequisite at
  runtime via `VerifyIsRegistered<…>` — so these libraries, like the rest of the chain, are compile-time un-addable
  without their prerequisite. A new **`NCode.Identity.JsonWebTokens.Abstractions`** package now holds the
  `JsonWebTokensLibrary` marker, and the `OpenIdCoreLibrary` marker moves from `NCode.Identity.OpenId.Core` into
  `NCode.Identity.OpenId.Abstractions`, so every library marker lives in its abstractions package. Hosts chain
  `AddSecretsLibrary().AddJoseLibrary().AddJsonWebTokensLibrary()` (the composition root already does this). The
  `NCode.Identity.OpenId.Persistence.EntityFramework.Core` registration class was renamed `CoreRegistration` →
  `DefaultRegistration` for naming consistency. Cross-library registration helpers invoked from multiple library
  builders (`AddMessageFactory<TMessage>`, `AddEndpointProvider<T>`, `AddEndpointGroup<T>`) retain the non-generic
  `IServiceBuilder` receiver by design. The development-only `AddDeveloperKeys`, `AddEphemeralDeveloperKeys`,
  `AddDeveloperSigningKey`, and `AddDeveloperInteractiveSeed` helpers now extend `IServiceBuilder<IdentityServer>`
  (chained off `AddIdentityServer()`) instead of a naked `IServiceCollection`, because they replace the server's tenant
  factory and hook its command handlers and are meaningless without the composed server; `AddDeveloperCors` (a
  standalone CORS policy) stays a bare-collection extension.
- **Every registration prerequisite is compile-time via the receiver plus required `IServiceBuilder<…>` parameters;
  `VerifyIsRegistered` and the `DisplayName`/`ConfigureMethod` marker members were removed
  ([ADR-0069](docs/adr/0069-registration-library-compile-time-marker-nesting.md),
  [ADR-0060](docs/adr/0060-local-account-one-store-seam-and-dependency-inverted-authentication.md),
  [ADR-0061](docs/adr/0061-local-account-management-is-an-opt-in-capability.md)).** A registration with one prerequisite
  receives `IServiceBuilder<TMarker>`; one with a **second** prerequisite takes it as a **required `IServiceBuilder<…>`
  parameter** — the builder that prerequisite's own registration returned — so the method is uncallable without it (a
  missing prerequisite is a compile error), and the receiver builder is returned so the chain continues on the primary
  library. A new `LocalAccountLibrary` marker (`NCode.Identity.OpenId.Accounts.Abstractions`) is minted and returned by
  every local-account store registration (`AddLocalAccountEntityFramework()`,
  `AddAspNetIdentityLocalAccountStore<TUser>()`, or a custom store), and `AddLocalAccountAuthentication()` /
  `AddLocalAccountInteractiveAuthentication()` (on `IServiceBuilder<OpenIdAuthenticationLibrary>`) and
  `AddLocalAccountManagement()` (on `IServiceBuilder<OpenIdManagementLibrary>`) take `IServiceBuilder<LocalAccountLibrary>`
  as a required parameter — a local-account capability cannot even be written without a store. Likewise
  `AddOpenIdAuthenticationLibrary()` (on `IServiceBuilder<IdentityLibrary>`) takes `IServiceBuilder<JoseLibrary>`. (A
  composite `IServiceBuilder<T1, T2>` receiver + `.And()` combinator was prototyped and dropped: a required parameter
  proves the same thing with less machinery and a cleaner call site, and a marker _value_ can't serve as the proof
  because an empty struct is forgeable.) The `NCode.Registration.ServiceCollectionExtensions.VerifyIsRegistered<TMarker>()`
  method is **removed** (the `IsRegistered<TMarker>()` query remains), and markers are now **empty `readonly struct`s**:
  the `DisplayName` and `ConfigureMethod` members and the `IMarker<TMarker>` base were removed, leaving
  `IRegistrationMarker<TMarker>` whose type identity is the whole signal.
- **The event bus is now a marker-bearing library
  ([ADR-0069](docs/adr/0069-registration-library-compile-time-marker-nesting.md)).** `AddEventServices()` mints a new
  `EventsLibrary` marker (`NCode.Identity.Events.Abstractions`) and returns `IServiceBuilder<EventsLibrary>`; its opt-ins
  `AddEventHandler<TEvent, THandler>()` and `AddBackgroundEventDelivery()` (moved to a new `EventLibraryRegistration`
  class, since one static class may hold only one `extension` receiver shape) now receive that builder, so an event
  opt-in is compile-time un-addable without the event services.
- **The interactive login/consent backend moved out of the composition root into its own leaf package with a pluggable
  verifier ([ADR-0070](docs/adr/0070-interactive-login-consent-leaf-package-and-credential-verifier-seam.md)).** The
  cookie scheme, `/account/*` endpoints, antiforgery wiring, and static-file server move from
  `NCode.Identity.Server/InteractiveAuthentication/` into **`NCode.Identity.OpenId.Authentication.Interactive`**
  (+ **`.Abstractions`**), so `NCode.Identity.Server` only references the capability like any other. The surface is now
  backend-agnostic: it depends on an `IInteractiveCredentialVerifier` seam (in the `.Abstractions` package), whose
  local-account implementation lives in `NCode.Identity.OpenId.Accounts.Authentication` and is registered by
  `AddLocalAccountInteractiveAuthentication()`. The old `NCode.Identity.Server.InteractiveAuthentication.*` public
  surface is removed and reintroduced under `NCode.Identity.OpenId.Authentication.Interactive.*`. Resolves backlog `U10`.

- **`IPersistedGrantService.AddAsync` now returns the server-generated grant id.** Its return type changes from
  `ValueTask` to `ValueTask<string>`, handing back the opaque public `GrantId` of the persisted grant so callers (such
  as the new initial-access-token management endpoint) can reference it for later revocation. Existing callers that
  ignore the result are unaffected. See [ADR-0063](docs/adr/0063-dynamic-client-registration.md).

- **Local-account management is now an opt-in capability that plugs into the Management slice, not part of it.**
  Mirroring the authentication inversion, the local-account management endpoints, their validator
  (`ILocalAccountValidator` / `DefaultLocalAccountValidator`), the wire contracts, and the `LocalAccountChangedAuditEvent`
  move into two new account-owned packages — **`NCode.Identity.OpenId.Accounts.Management`** (`AddLocalAccountManagement()`)
  and **`NCode.Identity.OpenId.Accounts.Management.Abstractions`** — so `NCode.Identity.OpenId.Management` no longer
  references `NCode.Identity.OpenId.Accounts.Abstractions`. The shared endpoint base the capability reuses is promoted
  into `NCode.Identity.OpenId.Management.Abstractions`: a public `ManagementApiEndpointHandler` (the generic
  authorization / `GET` / collection helpers; the implementation keeps an internal `BaseApiEndpointHandler` deriving from
  it for its secret/settings helpers), plus the now-public `DefaultResourceValidator`, `Operations`, `TenantScopeResource`,
  and a new `ManagementEndpointGroups.Tenant` group-name constant a capability uses to attach a keyed `IEndpointProvider`
  to the tenant group. The management audit recorder drops its bespoke `RecordLocalAccountChangedAsync` for a generic
  `RecordResourceChangeAsync(HttpContext, ResourceChangeAuditEvent, ct)` seam, and `ManagementResourceTypes.LocalAccount`
  moves to `AccountManagementResourceTypes.LocalAccount` in the new package. The management wire contracts move from
  `NCode.Identity.OpenId.Management.Contracts.LocalAccounts` to `NCode.Identity.OpenId.Accounts.Management.Contracts`
  (JSON shape unchanged). The `LocalAccountFeature` marker and the per-request `501 Not Implemented` feature-detection
  are **removed**: a capability is enabled by registering its package, so the library no longer checks whether the
  library itself is wired up. See
  [ADR-0061](docs/adr/0061-local-account-management-is-an-opt-in-capability.md).

- **Local accounts have one pluggable seam, and the password grant is dependency-inverted out of the authentication
  package.** `ILocalAccountSource` and `ILocalAccountProvisioner` are removed: `ILocalAccountStore` is the single,
  backing-store-agnostic seam, with credential handling expressed as first-class operations
  (`AddAsync(account, password?, ct)`, `SetPasswordAsync`, `VerifyCredentialAsync`) instead of a raw `PasswordHash` on
  `PersistedLocalAccount` (which is removed; `SecurityStamp` is store-managed). The runtime `LocalAccount` payload and
  the `LocalAccount*Request` DTOs are removed. The resource-owner password grant moves out of
  `NCode.Identity.OpenId.Authentication` into a new opt-in **`NCode.Identity.OpenId.Accounts.Authentication`** package
  (`AddLocalAccountAuthentication()`), so the authentication package no longer depends on the account package; without
  that capability the base package answers the grant with `unsupported_grant_type`. `CreatePasswordGrantSubjectCommand`
  moves to the new package, and the metadata projection claim types move from `AccountConstants` to
  `NCode.Identity.OpenId.Principals.SubjectMetadataClaimTypes`. Local-account management drops its owners sub-resource
  and the `local_account` resource-node type and authorizes against the tenant scope (`TenantScopeResource` is now a
  public authorization resource). The **ASP.NET Core Identity** adapter
  (`NCode.Identity.OpenId.Accounts.AspNetIdentity`) now implements `ILocalAccountStore` over `UserManager<TUser>`
  (`AddAspNetIdentityLocalAccountStore<TUser>()`), and the Entity Framework `EntityStoreManager` gained a
  DbContext-less store-factory fallback so a non-EF store resolves without referencing `OpenIdDbContext`.
  `IFederatedPrincipalStore` gains `UpdateAsync` and `RemoveAsync`; `IFederatedIdentityStore` gains `RemoveAsync`. See
  [ADR-0060](docs/adr/0060-local-account-one-store-seam-and-dependency-inverted-authentication.md).

- **`NCode.Identity.Abstractions` is now framework-free.** Its ASP.NET Core HTTP surface
  (`Models/UriDescriptor`, `Exceptions/HttpResultException`, `Results/*`) moved to the new
  `NCode.Identity.AspNetCore.Abstractions` package and its Settings subsystem moved to
  `NCode.Identity.Settings.Abstractions`, so the core drops the `Microsoft.AspNetCore.App` framework reference and is
  referenceable by any lower-tier package (including the JOSE/Secrets crypto branch). The JOSE
  `JsonElementExtensions.TryGetPropertyValue<T>` helper folded into `NCode.Json.JsonElements`, leaving a single
  universal `JsonElement` helper home, and detached JSON parsing standardizes on `JsonElement.Parse(string)`.
  `NCode.Identity.OpenId.Abstractions` now references `NCode.Registration.AspNetCore` directly (it exposes
  `ReadOnlyEndpointDisposition` on its public surface) rather than inheriting it transitively from the core. Namespaces
  are unchanged. See [ADR-0056](docs/adr/0056-abstractions-split-into-framework-free-tiered-packages.md).

- **Subject metadata (`ProfileMetadata` / `SystemMetadata`) is now a principal-level concept, resolved fresh at
  issuance.** The two bags moved off `LocalAccount` (and `PersistedLocalAccount` / `LocalAccountCreationRequest`) onto
  `FederatedPrincipal` / `PersistedFederatedPrincipal`, and are resolved at token/UserInfo issuance through a new
  pluggable `IPrincipalMetadataProvider` (default over `IFederatedPrincipalStore`) keyed by the resolved `PrincipalId`.
  They are therefore projected identically for every grant flow (ROPC, interactive, refresh) and every connection kind
  (local **or** external IdP), where before only ROPC-authenticated local accounts received them. The provider reads
  fresh per issuance (cluster-consistent); there is no built-in cache — host-side caching is a replaceable-provider
  concern that must be distributed. The ROPC subject builder no longer stamps metadata onto the subject. See
  [ADR-0055](docs/adr/0055-subject-metadata-is-a-principal-level-concept-resolved-at-issuance.md) (amends
  [ADR-0054](docs/adr/0054-local-account-metadata-projection.md)).

- Entity Framework persistence was split into a reusable **framework** package
  (`NCode.Identity.OpenId.Persistence.EntityFramework`) plus domain **slice** packages that plug into the shared,
  now-`DbSet`-less `OpenIdDbContext` via `IOpenIdModelContributor`. The core OpenID schema moved to
  **`NCode.Identity.OpenId.Persistence.EntityFramework.Core`** and the local-account schema moved from
  `NCode.Identity.OpenId.Accounts.EntityFramework` to **`NCode.Identity.OpenId.Persistence.EntityFramework.Accounts`**;
  each slice's root namespace matches its package id (`…EntityFramework.Core.*` / `…EntityFramework.Accounts.*`). The
  framework's store base (`BaseStore`, `BaseStoreWithResourceId`), the `AddStore` registration helper, and the
  `UseIdGenerator` attribute are now public so a slice reuses them instead of re-implementing the boilerplate. Hosts
  register the framework plus each slice (`AddEntityFrameworkCorePersistence<TDbContext>()`), and a slice may expose
  typed `DbSet` accessors for its own entities as extension members. See
  [ADR-0052](docs/adr/0052-entity-framework-persistence-is-a-framework-plus-slices.md).
- All persisted OpenID data is now **tenant-scoped except the server root**. `FederatedPrincipal`/`FederatedIdentity`
  (previously global) and the new `LocalAccount` are tenant-owned: their persisted DTOs gained a required `TenantId`,
  and their server-generated ids and natural keys are now unique **per tenant** rather than globally — the same human
  authenticating in two tenants is two principals. The tenant-scope global query filter is navigation-free: the
  `ISupportTenantEntity` contract now carries a denormalized `NormalizedTenantId` discriminator (its former `TenantId`
  foreign-key member was removed), so the filter is a single indexed-column comparison and the framework stays
  decoupled from the concrete tenant entity. See
  [ADR-0035](docs/adr/0035-federated-principals-and-identity-resolution.md) (amended) and
  [ADR-0018](docs/adr/0018-tenant-scoped-data-access-at-the-persistence-layer.md).

- The `NCode.Identity.OpenId.Playground` host adopted the first-party .NET 10 OpenAPI stack: `Swashbuckle.AspNetCore`
  was replaced by `Microsoft.AspNetCore.OpenApi` (`AddOpenApi()`/`MapOpenApi()`) for document generation and
  `Scalar.AspNetCore` (`MapScalarApiReference()`) for the UI. `Microsoft.OpenApi` was aligned to the 2.x line that the
  net10 OpenAPI stack targets, and the two OpenID endpoint operation builders (`DefaultAuthorizationEndpointHandler`,
  `DefaultTokenEndpointProvider`) now use the 2.x `OpenApiMediaType` model.
- The `NCode.Identity.OpenId.Playground` host migrated to the modern ASP.NET Core minimal-hosting model: the
  `Startup` class and the `Host.CreateDefaultBuilder`/`UseStartup` bootstrap were replaced by a single top-level
  `Program.cs` using `WebApplication.CreateBuilder(...)`. The integration tests are unaffected — they still boot
  through `WebApplicationFactory<PlaygroundApiMarker>`.
- The **bootstrap administrator** `ClientSecret` is now supplied **Base64Url-encoded** rather than as plaintext: the
  seed handler decodes it into a pinned, zeroed buffer (threaded as `ReadOnlyMemory<byte>`) so the plaintext is not left
  on the GC heap, and the OpenAPI example and Scalar prefill that surface the presentable secret are gated to the
  development environment. See [ADR-0044](docs/adr/0044-management-api-authentication-and-globaladmin-bootstrap.md).
- The development-only signing-key opt-ins moved out of the `NCode.Identity.OpenId.Playground` host and into the
  `NCode.Identity.Server` composition root, so any host can consume them rather than each app re-implementing them
  (the Playground is just the EXE host that wires them in). `AddEphemeralDeveloperKeys()`,
  `AddDeveloperSigningKey(DirectoryInfo)`, and the `EphemeralOpenIdTenantFactory` /
  `DeveloperSigningKeyOpenIdTenantFactory` types now live under the `NCode.Identity.Server` namespace. See
  [ADR-0002](docs/adr/0002-ephemeral-development-keys.md) and
  [ADR-0021](docs/adr/0021-developer-signing-keys-seeded-through-persistence.md).
- The hierarchical endpoint-group mechanism (`IEndpointGroup`, `IEndpointGroupBuilder`, `IEndpointProvider`, the
  route-tree builder, and the endpoint/operation disposition types) moved out of `NCode.Identity.Abstractions` into a new
  cross-cutting package, **`NCode.Registration.AspNetCore`**, that layers on `NCode.Registration` and the ASP.NET Core
  shared framework. `IEndpointGroupBuilder` now **is** an `IServiceBuilder` (it inherits it) rather than exposing a bare
  `IServiceCollection`, so a group's `ConfigureServices` registers its endpoints, child groups, and the services those
  endpoints depend on — including `AddMessageFactory<T>()` — through one registration surface. The separate per-family
  `AddXxxEndpoint()` chain is gone: each endpoint family registers its own provider and services, and the group invokes
  each family once, so no endpoint is listed twice. Public types moved namespace
  (`NCode.Identity.Endpoints` → `NCode.Registration.AspNetCore`) and three symbols were renamed
  (`IIdentityEndpointRouteBuilder` → `IEndpointTreeRouteBuilder`, `MapIdentityEndpoints()` → `MapEndpointGroups()`, and
  the route-builder default implementation). See
  [ADR-0043](docs/adr/0043-endpoint-registration-is-a-cross-cutting-aspnetcore-package.md).
- Endpoint route-group composition is now declarative, hierarchical, and unified across the OpenID protocol and
  management surfaces. A named `IEndpointGroup` **owns its contents**: it exposes a `Name` and `Prefix`, an optional
  `ConfigureRoutes` that attaches shared route-group filters when routes are mapped, and a
  `ConfigureServices(IEndpointGroupBuilder)` that registers at startup the endpoints (`AddEndpoint<T>()`) and child
  groups (`AddGroup<T>()`) it contains, plus the services those endpoints depend on (via the builder's
  `ServiceCollection`). Registering a single root group (`AddEndpointGroup<TRoot>()`) walks that declaration and
  registers the whole subtree; the route builder materializes
  each group with its prefix and filters, maps the group's endpoints, and recurses into its child groups. The
  management surface composes a hierarchy — `api` (`/api`) → `api/tenant` (`/tenants/{tenantId}`) → `api/tenant/client`
  (`/clients/{clientId}`) — so tenant-bound families (clients, grants, resource servers, client grants) nest under the
  tenant and map only their relative sub-routes, and the protocol surface is a single `openid` group. This replaces the
  imperative `IEndpointGroupProvider` tier, the per-family marker interfaces, and the separate keyed
  `AddEndpointProvider<T>(groupName)` / `AddOpenIdEndpointProvider<T>()` registration calls, which were removed. See
  [ADR-0042](docs/adr/0042-management-api-route-driven-tenant-scope.md).
- The management `/api` surface derives its ambient tenant scope from the route's `{tenantId}` segment rather than the
  request's protocol-resolved tenant, so a central-admin caller can administer a tenant by id while the persistence-layer
  query filter confines the read fail-closed ([ADR-0018](docs/adr/0018-tenant-scoped-data-access-at-the-persistence-layer.md));
  absent a route `{tenantId}` (control-plane routes) it falls back to the resolved tenant, a no-op for the unscoped
  server/tenant rows those routes act on. The tenant effective-settings endpoint is consequently addressed by id —
  `GET /api/tenants/{tenantId}/effective-settings` (was `GET /api/tenant/effective-settings`, the request's current
  tenant) — reconstructing the view from the server baseline merged with the addressed tenant's persisted overrides.
  Nesting the remaining tenant-bound families (clients, grants, resource servers) under `/api/tenants/{tenantId}/…`
  follows. See [ADR-0042](docs/adr/0042-management-api-route-driven-tenant-scope.md).

### Added

- Authenticated **effective-settings** preview endpoints that return the resolved, merged settings view (server
  baseline + tenant/client overrides + descriptor defaults) as a flat `name → value` JSON object — using the same value
  representation as the discovery document but including settings that are not discoverable — for each settings-bearing
  entity: `GET /api/tenants/{tenantId}/effective-settings`, `GET /api/clients/{clientId}/
effective-settings`, and `GET /api/servers/{serverId}/effective-settings`. Each is gated by the same `Read`
  authorization as reading that entity's raw settings and carries no `ETag` (a merged view spans multiple persisted
  entities). This is the gated replacement for the removed anonymous discovery `showAll` override. New wire contracts
  `TenantEffectiveSettingsResource`, `ClientEffectiveSettingsResource`, and `ServerEffectiveSettingsResource`. See
  [ADR-0040](docs/adr/0040-effective-settings-preview-endpoints.md) (the tenant endpoint is addressed by id per
  [ADR-0042](docs/adr/0042-management-api-route-driven-tenant-scope.md)).
- End-to-end support for the OpenID Connect `claims` request parameter
  ([OIDC Core 5.5](https://openid.net/specs/openid-connect-core-1_0.html#ClaimsParameter)), now advertised as
  `claims_parameter_supported: true` in discovery. Requested `claims.id_token` claims are added on top of the
  scope-driven claims during ID-token issuance, and requested `claims.userinfo` claims are honored at the UserInfo
  endpoint: when an access token is issued for a request that carried `claims.userinfo`, the requested claims are
  persisted as a short-lived stateful grant (`OpenIdConstants.PersistedGrantTypes.UserInfoClaims`) keyed by the access
  token's `jti` and bounded by its lifetime, then resolved at UserInfo — the access token itself is never inflated.
  `value`/`values` are advisory and `essential` is best-effort; a host can impose stricter semantics with a
  higher-priority claims handler. See [ADR-0039](docs/adr/0039-claims-request-parameter.md).
- Data-driven resource ownership: persisted `(principal, role, resource-node)` role assignments
  (`PersistedRoleAssignment` / `IRoleAssignmentStore`), an additive `OwnershipHandler` that grants a principal every
  operation except `Create` on a resource it owns (or an ancestor node), and creator-as-owner on client, resource
  server, and tenant creation. Each of those resources gains owner-management endpoints
  (`GET`/`POST`/`DELETE /{resource}/{id}/owners`) returning the rich `OwnerResource`, with a one-owner invariant that
  refuses to orphan a resource (`409`). The ownership service and resource-node abstraction (`IResourceOwnershipService`,
  `IResourceNode`, `ResourceNode`, `ResourceNodeTypes`) are public so applications can own and enforce their own
  resource types. See [ADR-0034](docs/adr/0034-persisted-role-assignments-and-ownership.md).
- A single management resource server (`urn:ncode:management`) that gates the whole management API through granular
  per-leaf scopes composed as `{verb}:{family}` (for example `read:clients`, `update:tenant_settings`,
  `create:server_secrets`). Its control-plane scope families (`servers`, `server_settings`, `server_secrets`,
  `tenants`) are seeded only into the root (control-plane) tenant, while its tenant-plane families (a tenant's own
  settings and secrets, plus its clients, client secrets, resource servers, client grants, and grants) are seeded into
  every tenant. A `SystemResourceServerDescriptor` now declares a `Plane` (`SystemResourceServerPlane.Tenant` (default)
  or `Control`); the system-resource-server seeder is plane-aware and merges every provider that contributes to the same
  audience into one resource server per tenant, seeding `Control`-plane families only into the root tenant, whose
  identifier is `TenantResolutionOptions.RootTenantId` (default `"root"`). The static single-tenant strategy provisions
  the root tenant lazily alongside its workload tenant, so a single-tenant deployment gains its control plane with no
  extra configuration. See [ADR-0033](docs/adr/0033-authorization-model-scopes-roles-and-ownership.md) and
  [ADR-0031](docs/adr/0031-control-plane-management-resource-server-and-root-tenant-seeding.md).
- An `OpenID Connect` UserInfo endpoint (`GET`/`POST /oauth2/userinfo`,
  [OpenID Connect Core 5.3](https://openid.net/specs/openid-connect-core-1_0.html#UserInfo), auto-advertised as
  `userinfo_endpoint` in discovery). The endpoint is a thin shell over two mediator seams: a reusable
  `AuthenticateSubjectCommand` whose default handler authenticates the caller via the host's ASP.NET Core authentication
  scheme (the same mechanism the management API uses) and maps it to `SubjectAuthentication`; and a
  `GetUserInfoClaimsCommand` enricher pipeline whose default handler supplies only the required `sub` claim, with
  applications registering additional `ICommandHandler<GetUserInfoClaimsCommand>` handlers to contribute profile/email/
  other claims. The response is an extensible `[JsonExtensionData]` result (`UserInfoResult`). See
  [ADR-0030](docs/adr/0030-userinfo-endpoint-and-subject-authentication-seam.md).
- An `OAuth 2.0` token introspection endpoint (`POST /oauth2/introspect`,
  [RFC 7662](https://datatracker.ietf.org/doc/html/rfc7662), auto-advertised as `introspection_endpoint` in discovery).
  An authenticated client may probe a token's active state: the default handler validates a JWT access token against the
  tenant signing keys, issuer, and lifetime, otherwise resolves a refresh-token grant, and reports `{"active": false}`
  for anything else. The response is a discovery-style, extensible result (`IntrospectionResult` with
  `[JsonExtensionData]` claims) that additional `ICommandHandler<IntrospectTokenCommand>` handlers may enrich. The
  endpoint is a thin `IOpenIdEndpointProvider` that authenticates the client and parses a typed
  `ITokenIntrospectionRequest`, then delegates to the `IntrospectTokenCommand` mediator handler. See
  [ADR-0029](docs/adr/0029-token-introspection-endpoint.md).
- An `OAuth 2.0` token revocation endpoint (`POST /oauth2/revoke`,
  [RFC 7009](https://datatracker.ietf.org/doc/html/rfc7009), auto-advertised as `revocation_endpoint` in discovery). An
  authenticated client may revoke one of its own refresh tokens; an unknown or non-revocable token (including a stateless
  JWT access token) is a no-op, and the endpoint always responds `200 OK` so it never reveals token state. The endpoint
  is a thin `IOpenIdEndpointProvider` that authenticates the client and parses a typed `ITokenRevocationRequest`, then
  delegates to the `RevokeTokenCommand` mediator handler. See
  [ADR-0028](docs/adr/0028-token-revocation-refresh-tokens-only.md).
- Shared tenant-resolution abstraction (`ITenantResolver` / `ITenantStrategy` / `TenantResolutionOptions` in
  `NCode.Identity.OpenId.Abstractions`, default implementation in `NCode.Identity.OpenId.Core`) so the OpenID runtime
  and the management API select the ambient tenant from a single source of truth. See
  [ADR-0018](docs/adr/0018-tenant-scoped-data-access-at-the-persistence-layer.md).
- Tenant-scoped data access for the management API: an `IAmbientTenantAccessor`
  (`NCode.Identity.OpenId.Persistence.Abstractions`) plus an `OpenIdDbContext` global query filter on tenant-child
  entities so that, when tenancy is request-derived (dynamic-by-host / dynamic-by-path), a resource outside the
  request's tenant is never materialized (a cross-tenant read returns `404`, closing the fetch-then-reject leak
  window). A reusable `TenantScopeEndpointFilter` establishes the scope; tenant and server administration remain
  central-admin (unscoped). See
  [ADR-0018](docs/adr/0018-tenant-scoped-data-access-at-the-persistence-layer.md).
- Collection `GET` endpoints for the management API — `GET api/clients`, `GET api/servers`, `GET api/tenants` — that
  return a page of resources (`CollectionResource<T>` = `{ items, continuationToken }`) using keyset (cursor)
  pagination (`?limit=` clamped to 100, `?cursor=` opaque continuation token). The client list is scoped to the
  request's tenant; the server and tenant lists are central-admin. There is deliberately no query protocol (no OData);
  see [ADR-0019](docs/adr/0019-collection-endpoints-keyset-pagination.md).
- Grant administration for the management API — a tenant-scoped, read-only surface: `GET api/grants` (paged, with
  optional `?subjectId=` / `?clientId=` filters), `GET api/grants/{grantId}`, `DELETE api/grants/{grantId}` (single
  soft-revoke), and `DELETE api/grants?subjectId=&clientId=` (bulk soft-revoke of every active match, requires at least
  one filter, returns `GrantRevocationResult { revoked }`). Revocation sets `RevokedWhen` and keeps the row for audit;
  it is idempotent. Grants are addressed by a new opaque, server-generated `GrantId` (`PersistedGrant.GrantId` /
  `GrantEntity.GrantId`), and `GrantResource` projects metadata only — the internal `HashedKey` and the grant
  `PayloadJson` are never exposed. The shared keyset-pagination mechanics move from `BaseStoreWithResourceId` to
  `BaseStore` so a grant store not keyed by a resource id can paginate on its chronological surrogate id. See
  [ADR-0020](docs/adr/0020-grant-management-read-surface.md) and
  [ADR-0022](docs/adr/0022-grant-filters-and-bulk-revocation.md).
- Scope and resource management (Auth0-aligned): a tenant-owned **resource server** (an API/audience) owns its
  **scopes** (permissions), and a **client grant** authorizes a client to a resource server with a subset of its
  scopes. Adds the persistence model (`ResourceServerEntity` / `ScopeEntity` / `ClientGrantEntity`, DTOs,
  `IResourceServerStore` / `IClientGrantStore`), seeds a reserved OpenID Connect system resource server
  (`urn:ncode:openid`) with the standard scopes into each tenant via `ISystemResourceServerProvider`, and the
  management surface `GET/POST/GET/PATCH/DELETE api/resource-servers` plus the `/{id}/scopes` sub-resource, and the
  nested `GET/POST/GET/PUT/DELETE api/clients/{clientId}/grants[/{resourceServerId}]` client-grant surface. Granted
  scopes are validated (write-time) against the target resource server's scopes. Core preconditions for both surfaces
  are asserted up-front by replaceable validators (`IResourceServerValidator` / `IClientGrantValidator`), consistent
  with the existing client/server/tenant validators. A reserved tenant-plane management resource server
  (`urn:ncode:management`) is also seeded into each tenant, whose `{verb}:{family}` scopes (for example `read:clients`,
  `create:resource_servers`, `delete:grants`) gate the per-tenant management families; control-plane families
  (servers, tenants) are intentionally excluded and belong to the root tenant's control plane. System
  resource servers and scopes cannot be deleted. See [ADR-0023](docs/adr/0023-scopes-and-resources-management-model.md)
  and [ADR-0024](docs/adr/0024-control-plane-and-per-tenant-planes.md).
- A persistent developer-keys opt-in for the Playground, `AddDeveloperSigningKey(...)`, that seeds an `RSA` signing key
  through the tenant secret store (so the runtime, the JWKS endpoint, and the management API share one source of truth)
  instead of overriding the read path. Paired with a local SQLite database and a persistent Data Protection key ring
  (DPAPI-protected on Windows), the key survives restarts and self-heals if the key ring is lost; the existing
  `AddEphemeralDeveloperKeys()` stays the hermetic path for tests. Selected via `DeveloperKeys:Mode=PersistentSigningKey`
  (set in `launchSettings.json`). See [ADR-0021](docs/adr/0021-developer-signing-keys-seeded-through-persistence.md).

### Changed

- Setting merge is now a first-class policy expressed through a public `SettingMerge` vocabulary of lattice dual pairs
  (`Min`/`Max`, `And`/`Or`, `Intersect`/`Union`, `Keep`/`Replace`) in `NCode.Identity.Abstractions`, so host-authored
  descriptors reuse it. Each built-in setting is mapped to a **ceiling** (a parent caps the child), a **floor** (a
  parent sets an un-loosenable minimum), or an **override**, chosen by the setting's value polarity. A more-specific
  scope (tenant/client) can no longer loosen a broader scope's security-relevant setting: token/code lifetimes,
  `clock_skew`, and `subject_max_age` are ceilings a child may only shorten; `require_pkce`, the `*_encryption_required`
  flags, `claims_supported_is_strict`, `refresh_token_rotation_enabled`, and the `federated_identity_*` requirement
  flags are floors; `allow_*` and `request(_uri)_parameter_supported` are ceilings; `allowed_identity_providers` and
  `subject_types_supported` merge as intersect ceilings; and `tenant_issuer` is parent-owned (`Keep`).
  `allowed_identity_providers` is now fail-closed — unset means unrestricted, but an explicit (or narrowed-to-empty)
  allowlist denies every IdP. See [ADR-0038](docs/adr/0038-setting-merge-lattice-duals.md).
- Relocated the default `claims_supported` list from the descriptor literal into the replaceable
  `IDefaultSettingsProvider` baseline, so a host that enables `claims_supported_is_strict` can extend the allow-list
  instead of being frozen by the library's defaults; the advertised default is unchanged
  ([ADR-0010](docs/adr/0010-supported-settings-unset-means-unrestricted.md)).
- Probe the tenant cache before reading the tenant store on the resolution hot path. `ITenantResolver` and
  `ITenantStrategy` gain `TryGetTenantId(httpContext, out tenantId)`, a store-free identity probe satisfied by the
  static-single (configured id) and dynamic-by-path (route value) strategies; `DefaultOpenIdTenantFactory` uses it to
  serve a warm tenant with no tenant-store round-trip. Dynamic-by-host still performs a full resolve because mapping a
  domain to a tenant requires the store. A cache hit no longer re-reads the store, so a tenant disabled after it was
  cached serves until its cache entry expires. See
  [ADR-0037](docs/adr/0037-tenant-cache-probe-before-store-load.md).
- Renamed the authorization endpoint's subject pipeline to drop the redundant `Subject` noun (the namespace already
  supplies the context) now that a general subject-authentication seam exists: `AuthenticateSubjectCommand` →
  `AuthenticateCommand`, `AuthorizeSubjectCommand`/`AuthorizeSubjectDisposition` → `AuthorizeCommand`/
  `AuthorizeDisposition`, `ChallengeSubjectCommand` → `ChallengeCommand` (and the matching `Default*` handlers). The
  cross-slice `ValidateSubjectCommand`/`DefaultValidateSubjectHandler` were renamed to
  `ValidateSubjectAuthenticationCommand`/`DefaultValidateSubjectAuthenticationHandler` to reflect that they validate an
  existing login-session `SubjectAuthentication` (tenant/`max_age`/`acr`), not the generic authenticate pipeline. The
  root `Subject` capability keeps the `Subject` noun. See
  [ADR-0030](docs/adr/0030-userinfo-endpoint-and-subject-authentication-seam.md).
- Collapsed the per-strategy OpenID tenant providers, their selector, and the thin tenant factory into a single
  `DefaultOpenIdTenantFactory` that composes `ITenantResolver` for selection and performs only tenant materialization.
  Tenant-selection option types moved from `NCode.Identity.OpenId.Authentication` to
  `NCode.Identity.OpenId.Abstractions` (`NCode.Identity.OpenId.Tenants` namespace); `OpenIdTenantOptions` now carries
  only materialization settings.
- A managed client is always owned by the request's tenant, so `POST api/clients` no longer accepts a tenant
  identifier: `CreateClientRequest.TenantId` is removed and the server derives the owning tenant from the request
  (resolved for every tenant strategy, static-single included). This removes the redundant, error-prone echo of a
  tenant the server already resolved.
- The management API endpoints are now served under a shared top-level `/api` route group (`/api/clients`,
  `/api/servers`, `/api/tenants`, …) via a `ManagementEndpointGroupProvider` ([ADR-0009](docs/adr/0009-endpoint-families-own-their-route-group.md)),
  keeping them distinct from the OpenID protocol endpoints at the root.
- Grants are now strictly tenant-scoped: the previously nullable `TenantId` on the grant vertical slice
  (`PersistedGrantId`, `PersistedGrant`, `PersistedGrant<TPayload>`, `GrantEntity`, `GrantResource`) and the
  `IPersistedGrantService.CreateGrantId` parameter are now required (non-nullable). Every grant is created against the
  resolved request tenant, so the unused non-tenant (global) key space is removed; a future server-level grant type, if
  needed, would be modeled explicitly rather than by an absent tenant.
- Runtime scope enforcement is now driven by the resource-server / client-grant model instead of a `scopes_supported`
  setting, which is retired (its `SettingKey`, descriptor, and default are removed). A replaceable `IClientScopeService`
  resolves a client's allowed scopes — every scope of a system resource server (`IsSystem`) is implicitly available to
  all clients, while a non-system API is available only through a client grant intersected with the API's current
  scopes (read-time intersection) — and the authorization/token validators reject out-of-scope requests. Discovery's
  `scopes_supported` is derived from the tenant's resource-server catalog. `offline_access` availability follows from the
  system identity resource server; refresh-token issuance remains governed by the request and the `refresh_token` grant
  type. See [ADR-0026](docs/adr/0026-scope-enforcement-via-resource-servers-and-client-grants.md).
- An access token's `aud` is now bound to the resource servers that own its effective scopes (resolved via
  `IClientScopeService.ResolveAudiencesAsync`) instead of the requesting client, so a resource server can validate that a
  token was minted for it; a token spanning several resource servers carries a multi-valued `aud`, and a scopeless token
  falls back to the client id. The ID token's `aud` remains the client. See
  [ADR-0027](docs/adr/0027-access-token-audience-from-resource-servers.md).

### Security

- Removed the undocumented `showAll` query-string override from the discovery endpoint
  (`/.well-known/openid-configuration`). It was unauthenticated and, when set, bypassed both the per-setting
  `IsDiscoverable` filter and the per-endpoint discoverability filter, letting any anonymous caller enumerate every
  non-advertised setting and endpoint. Discovery now always returns only the settings and endpoints explicitly marked
  discoverable. An authenticated management view of a tenant's effective settings is tracked as a separate follow-up.

### Fixed

- The unique index on a tenant's domain name is now a filtered index (`WHERE NormalizedDomainName IS NOT NULL`), so
  multiple tenants may omit the optional domain name without colliding on relational providers.

- Adopted repository-wide engineering conventions: Central Package Management, Nerdbank.GitVersioning lockstep
  versioning, SourceLink-to-GitHub provenance with portable symbols, a scripted Definition of Done, CSharpier
  formatting, and scoped coding-convention instruction files. See [`docs/adr/`](docs/adr/).

[Unreleased]: https://github.com/NCodeGroup/Identity/commits/dev
