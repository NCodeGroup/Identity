# Backlog — known pending work

This file is the **single source of truth for documented known pending work** in NCode.Identity:
deferred decisions, feature gaps, and action items. It is durable, versioned repository content — not
an agent's volatile memory or a chat transcript.

**This catalog is not exhaustive.** It lists only work that has already been _identified_ and
deliberately written down — primarily durable storage for AI chat sessions and items a human asked to
record. Unidentified work, and pending work tracked elsewhere (issues, `// TODO:` comments, ADR
follow-ups, test gaps), also exists and is not necessarily duplicated here. Absence from this file is
not evidence that something is done or that nothing is pending.

**For contributors and automated agents:** when you identify pending/deferred/follow-up work during a
change, record it **here** (and, for a non-obvious _decision_, in an [ADR](docs/adr/)) rather than
leaving it only in chat history or agent memory. This follows
[ADR-0008](docs/adr/0008-conventions-and-lessons-live-in-the-repository.md): conventions, reasoning,
and recurring work live in the repository. Code-local reminders may also use `// TODO:` comments; this
file is the catalog that makes those discoverable in one place.

Entry convention: a short title, one-line context, and (where useful) the touch points. Remove an
entry when the work lands (and note it in [`CHANGELOG.md`](CHANGELOG.md) if user-facing). Because this
file tracks **pending** work only, never mark an entry "Done" and leave it here — when work is fully
done, delete the entry; when it is **partially** done, rewrite the entry so its title and context
describe only the **remaining** work (the completed portion is stated as already-existing context, not
as a "done" status).

---

## Standing design posture (read first)

This family is **greenfield and pre-release** — everything lands on `dev`, nothing is shipped, and there is **no
backward-compatibility to preserve** ([ADR-0025](docs/adr/0025-pre-release-posture-and-auth0-parity-plus.md)).

**Default to the best long-term design.** When a change has a fork between a minimal, compatibility-preserving patch and
the _cohesive, comprehensive, correct_ model, **choose the comprehensive model by default** — do not weigh "how hard is
it to refactor now" or "will this break the public surface." Breaking changes are expected and welcome here: public-API
churn (`PublicAPI.Unshipped.txt`), setting renames/removals, signature changes, moved types, and a clean cutover are all
preferred over shims, adapters, or "add-only" compromises. Only call out a fork explicitly when the _options are
genuinely equivalent in quality_ and the choice is a real value judgment (e.g. a security/semantics trade-off), not when
one option is merely more work.

---

## Deferred — evaluated, intentionally not-yet (group `D`)

- **`D1` — UserInfo access auditing.** The UserInfo endpoint returns end-user PII but is not audited. A
  `userinfo.accessed` audit event would give a PII-access trail for compliance, but it is a read on a
  hot path (one event per request) and the subject is already captured at token issuance. Deferred as
  an opt-in; revisit if a compliance requirement needs per-access PII records.
  Touch points: `src/NCode.Identity.OpenId.Authentication/Endpoints/UserInfo/`,
  `IAuditEventRecorder`.
- **`D2` — Front-Channel Logout and Session Management (`check_session_iframe`).** The OpenID Provider
  implements RP-Initiated Logout and Back-Channel Logout (both cookie-independent), but **not** Front-Channel
  Logout 1.0 (per-RP hidden iframes) or Session Management 1.0 (`check_session_iframe` / `session_state`). Both
  depend on third-party cookies inside an iframe, which the browser platform (Safari ITP, Firefox partitioning,
  Chrome deprecation) is removing, so they are unreliable by construction. The session model records each
  participating client and the logout pipeline is a mediator seam, so a deployment that still needs front-channel
  behavior can add it without a redesign. Rationale: [ADR-0074](docs/adr/0074-session-management-and-logout.md).
  Touch points: `src/NCode.Identity.OpenId.Authentication/Endpoints/EndSession/`,
  `src/NCode.Identity.OpenId.Authentication/Sessions/`.
- **`D3` — Index tuning for resource-server runtime query filters.** The resource-server reads now push their
  predicates to the DBMS ([ADR-0080](docs/adr/0080-store-query-methods-push-filtering-to-dbms.md)), but no supporting
  indexes were added — resource servers and scopes are a small, slowly-changing per-tenant set, so a sequential scan is
  cheap today. If a deployment carries large per-tenant resource-server/scope counts, add an index on
  `ScopeEntity.NormalizedValue` (for the owns-any-scope join) and consider a filtered index on
  `ResourceServerEntity.IsDisabled`; measure before adding, since the extra write cost may not pay off for small sets.
  Touch points: `src/NCode.Identity.OpenId.Persistence.EntityFramework.Core/Entities/ScopeEntity.cs`,
  `src/NCode.Identity.OpenId.Persistence.EntityFramework.Core/Entities/ResourceServerEntity.cs`.

## Feature gaps — not yet implemented (group `F`)

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
- **`F4` — `refresh_token_reuse_policy` (none / revoke_all).** Refresh-token replay is _detected and audited_
  (`token.refresh.replay`), but there is no configurable response that revokes the whole token family
  on reuse. See `// TODO: refresh_token_reuse_policy` in
  `src/NCode.Identity.OpenId.Authentication/Endpoints/Token/RefreshToken/DefaultRefreshTokenGrantHandler.cs`.
- **`F5` — First-class claim-shaping pipeline (Auth0-Actions-equivalent).** Custom claim shaping is already possible
  today by registering an `ICommandHandler` on the subject-claims / UserInfo commands (the mediator pipeline is the
  extension seam, per [ADR-0054](docs/adr/0054-local-account-metadata-projection.md)), but there is no first-class
  surface: a strongly-typed shaping context (event/api), per-key namespaced flattening helpers, or ordering/management.
  Layer these on the existing pipeline additively. (Subject metadata already resolves fresh per issuance by default, per
  [ADR-0055](docs/adr/0055-subject-metadata-is-a-principal-level-concept-resolved-at-issuance.md).)
  Touch points: `src/NCode.Identity.OpenId.Authentication/Tokens/Handlers/`,
  `src/NCode.Identity.OpenId.Authentication/Endpoints/UserInfo/Handlers/`.
- **`F7` — DPoP / sender-constrained tokens (RFC 9449 + mTLS, `cnf` claim).** Every token-issuance path hardcodes
  `TokenType = Bearer` with a `// TODO: add support for DPoP` marker, the token-request validators never inspect a
  DPoP proof, and the emitted-claims handlers cannot populate the `cnf` confirmation claim because no
  proof-of-possession subsystem exists. This is the single most recurring code-local TODO theme. Touch points (all
  `src/NCode.Identity.OpenId.Authentication/`): `Endpoints/Authorization/Handlers/DefaultCreateAuthorizationTicketHandler.cs`,
  `Endpoints/Token/AuthorizationCode/DefaultAuthorizationCodeGrantHandler.cs`,
  `Endpoints/Token/AuthorizationCode/DefaultValidateAuthorizationCodeGrantHandler.cs` (`// TODO: validate DPoP`),
  `Endpoints/Token/ClientCredentials/DefaultClientCredentialsGrantHandler.cs`,
  `Endpoints/Token/Password/DefaultPasswordGrantHandler.cs`,
  `Endpoints/Token/RefreshToken/DefaultRefreshTokenGrantHandler.cs`,
  `Tokens/Handlers/DefaultGetAccessTokenPayloadClaimsHandler.cs` /
  `Tokens/Handlers/DefaultGetIdTokenPayloadClaimsHandler.cs` (the `cnf` claim).
- **`F8` — Pushed Authorization Requests (PAR, RFC 9126).** There is no `pushed_authorization_request_endpoint`; the
  authorize endpoint only accepts inline request parameters. See
  `// TODO: add support for OAuth 2.0 Pushed Authorization Requests (PAR)` in
  `src/NCode.Identity.OpenId.Authentication/Endpoints/Authorization/Handlers/DefaultLoadAuthorizationRequestHandler.cs`.
  Touch points: a new `Endpoints/PushedAuthorization/` family plus discovery-metadata advertisement.
- **`F9` — Pairwise subject identifiers (PPID) via the `subject_type` setting.** The subject identifier emitted into
  tokens is always the raw public subject; the `subject_type` (`public`/`pairwise`) setting is not honored, so there is
  no per-client/RP pseudonymous `sub`. See `// TODO: use the subject_type setting to support PPID` in
  `src/NCode.Identity.OpenId.Authentication/Endpoints/Authorization/Handlers/DefaultAuthenticateHandler.cs`.
- **`F10` — Extension grant + token-endpoint `401`/`WWW-Authenticate`.** The token endpoint has no extension-grant seam
  (`// TODO: Extension Grant` in `DefaultTokenEndpointProvider.cs`) and collapses client-authentication failures to
  `400` instead of emitting `401` with a `WWW-Authenticate` challenge (`// TODO: add support for 401 with
WWW-Authenticate header`). (Device Code is implemented; CIBA is `F3`.) Touch points:
  `src/NCode.Identity.OpenId.Authentication/Endpoints/Token/DefaultTokenEndpointProvider.cs`.
- **`F11` — `acr` claim emission and conditional `auth_time` (blocked on / pending foundational subsystems).** The
  `sid` claim **is** now emitted into ID tokens from the session-management subsystem
  ([ADR-0074](docs/adr/0074-session-management-and-logout.md)). The `acr` claim still needs authentication-context-class
  tracking, which does not exist, so it is never emitted. `DefaultTokenService` additionally always emits `auth_time`
  unconditionally (`// TODO: … should we do this conditionally?`). Touch points (all
  `src/NCode.Identity.OpenId.Authentication/Tokens/`): `Handlers/DefaultGetIdTokenPayloadClaimsHandler.cs`,
  `DefaultTokenService.cs`.
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

- **`F16` — FAPI (Financial-grade API) readiness epic.** An umbrella tracking the work to make the OP certifiable
  against the OpenID Foundation **FAPI** security profiles ([FAPI 1.0 Advanced](https://openid.net/specs/openid-financial-api-part-2-1_0.html),
  [FAPI 2.0](https://openid.net/specs/fapi-security-profile-2_0.html)). FAPI mandates **sender-constrained access
  tokens** (DPoP / mTLS — `F7`), **Pushed Authorization Requests** (`F8`), **signed request objects and responses**
  (JAR / JARM — not yet tracked separately), and **strong client authentication** (`private_key_jwt` or mTLS — the
  `private_key_jwt` half rides `S3`). It also restricts signing to **`PS256`/`ES256`** and forbids symmetric (`HS256`)
  signing — the always-tenant-asymmetric access-token signing from
  [ADR-0075](docs/adr/0075-runtime-consumption-of-resource-server-secrets.md) already satisfies the algorithm rule; a
  FAPI **profile** would additionally constrain the advertised `*_signing_alg_values_supported` settings to that set.
  This item is the tracking anchor; the concrete deliverables are `F7`, `F8`, `S3`, and a new JAR/JARM work item.

## Conformance & interop testing (group `T`)

- **`T1` — Independent OIDC/OAuth conformance validation (OpenID Foundation Conformance Suite).** Our own
  integration tests encode our own reading of the specs; an independent, spec-author-maintained oracle is
  needed to catch shared misreadings. The full design and automation plan — harness architecture, the
  `BrowserControl`/`run-test-plan.py` automation model, target profiles, the reachable-host /
  automatable-login / discovery-alignment prerequisites, and the nightly (not `dod.ps1`) CI wiring — lives
  in its dedicated doc: [`docs/conformance-testing.md`](docs/conformance-testing.md). The Dynamic OP profile
  prerequisite (Dynamic Client Registration) is implemented ([ADR-0063](docs/adr/0063-dynamic-client-registration.md));
  the harness enables anonymous registration (`registration_endpoint_allow_anonymous`). Depends on the prerequisite
  work items `T2`–`T5` below (the seeded, automatable, correctly-advertised OP the harness drives).
- **`T2` — Browser-automatable login + consent UI with a seeded conformance identity.** _Login/consent surface
  implemented._ The OpenID Provider has an opt-in interactive login/consent surface
  ([ADR-0065](docs/adr/0065-interactive-login-consent-op-hosted-auth-surface.md)): a cookie scheme the authorize
  challenge redirects into, plus a React + MUI `apps/auth` bundle (OP-hosted under `/account/*`) and credential
  endpoints reusing the local-account path, exposing the stable locators (`username`, `password`, `login`,
  `consent-allow`, `consent-deny`). Decision record:
  [ADR-0062](docs/adr/0062-opt-in-interactive-login-consent-ui.md). Detailed plan:
  [`docs/conformance/login-consent-ui.md`](docs/conformance/login-consent-ui.md). **Remaining for conformance:** a
  seeded conformance identity (`T4`) and the reachable HTTPS host to drive it headlessly end-to-end (`T3`); independent
  of `F6`.
- **`T3` — Reachable HTTPS Playground host for the external harness.** The only launchable host is the
  in-process `TestServer`; the suite needs a real HTTPS origin (Windows/Docker: `host.docker.internal`)
  whose discovery `issuer` byte-matches that origin (pin via the `tenant_issuer` setting). Detailed plan:
  [`docs/conformance/reachable-host.md`](docs/conformance/reachable-host.md).
- **`T4` — Deterministic conformance tenant, clients, and test user seeding.** Seeding exists only in test
  infrastructure; a launched host seeds only the bootstrap-admin client. Add an idempotent, config-driven
  startup seeder (bootstrap-admin-style) for the conformance tenant, the three Basic-OP clients, and a
  fixed test user. Detailed plan:
  [`docs/conformance/conformance-seeding.md`](docs/conformance/conformance-seeding.md). Static client half
  is retired by `F6` (DCR); tenant/user seeding is not.
- **`T5` — Discovery-metadata alignment audit.** Discovery is data-driven from discoverable settings;
  `appsettings.json` over-advertises `grant_types_supported` (`implicit`) and the response-type set
  includes unverified flows, which fails Config OP. Audit every advertised field down to verified support.
  Detailed plan: [`docs/conformance/discovery-alignment.md`](docs/conformance/discovery-alignment.md).

## Frontend presentation platform (group `U`)

The platform foundation landed under [`src/frontend`](src/frontend) with the Management console as the first surface
([ADR-0064](docs/adr/0064-frontend-presentation-platform.md)). Remaining work builds additively on it.

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
- **`U5` — Frontend CI job.** The frontend toolchain is intentionally outside `dod.ps1`. Add a separate CI job
  (`corepack` + `pnpm install` + `turbo run lint typecheck test build`) so the platform is gated independently.
- **`U6` — Per-tenant branding.** `@ncode/ui` themes from a runtime `BrandingConfig` (the seam exists); wire
  end-to-end per-tenant brand/theme resolution through the discovery channel for customer-facing surfaces.

## Secrets management (group `S`)

Follow-ups surfaced while implementing [ADR-0050](docs/adr/0050-secret-material-revealed-once-on-create.md)
(client-secret material revealed once on create).

- **`S1` — Client-secret rotate endpoint.** Rotation is create-new + delete-old (two calls) per
  [ADR-0011](docs/adr/0011-secret-management-api.md); ADR-0050 notes a convenience `rotate` would inherit
  the same one-time reveal. A `POST api/tenants/{tenantId}/clients/{clientId}/secrets/{secretId}/rotate`
  returning `CreatedSecretResource` is the natural next increment.
  Touch points: `src/NCode.Identity.OpenId.Management/Endpoints/Clients/ClientApiEndpointHandler.cs`.
- **`S3` — Key import / BYOK (needs its own ADR).** Secret creation is server-generation-only; there is no way
  to onboard a `private_key_jwt` client (client generates its keypair and registers the **public** half)
  or to import an `x509` certificate — `SecretTypes.Certificate` is defined but `DefaultSecretGenerator`
  refuses it. ADR-0011 explicitly defers import; the `GenerateSecretRequest` `// Future:` note reserves
  room. Import needs its own validation + threat-model ADR before implementation.
  Touch points: `src/NCode.Identity.Secrets.Persistence.Abstractions/Logic/GenerateSecretRequest.cs`,
  `src/NCode.Identity.Secrets.Persistence/Logic/DefaultSecretGenerator.cs`.
- **`S5` — Client-secret storage: hash-at-rest vs reversible protection (needs its own ADR).** A client secret
  is reveal-once and never returned again, so the server only needs to _verify_ a presented value — a
  one-way hash (OWASP-preferred) is safer than today's reversible data-protected `EncodedValue`. But
  symmetric secrets used as actual signing keys (`HS256`) require reversible material. The
  authentication-secret vs signing-key fork deserves an ADR before changing storage.
  Touch points: `src/NCode.Identity.Secrets.Persistence.Abstractions/DataContracts/PersistedSecret.cs`,
  `src/NCode.Identity.Secrets.Persistence/Logic/DefaultSecretGenerator.cs`,
  `src/NCode.Identity.Secrets.Persistence/Logic/DefaultSecretSerializer.cs`.

## Tooling & agent customization (group `X`)

- **`X1` — `audit-conventions` duplicated as prompt + skill.** The convention-audit workflow now exists **twice**:
  the original VS Code prompt [`.github/prompts/audit-conventions.prompt.md`](.github/prompts/audit-conventions.prompt.md)
  and a migrated Agent Host skill [`.github/skills/audit-conventions/SKILL.md`](.github/skills/audit-conventions/SKILL.md)
  (prompt files are not loaded by Agent Host / Copilot CLI, which reads `SKILL.md`). The skill originally carried
  `disable-model-invocation: true` to preserve the prompt's explicit-invocation-only semantics, but that flag makes the
  skill **unreachable** in Copilot CLI (it returns "Skill not found" even on explicit invocation — upstream bug
  github/copilot-cli#4438), so it was **removed**. **Trade-off accepted:** the skill is now both explicitly invocable
  **and** model-auto-invocable (it no longer enforces explicit-only invocation); its `description` is the only guard on
  when the model reaches for it. The prompt's `agent: "agent"` frontmatter has no skill equivalent and was dropped.
  **Runtime-validated (2026-10-10):** the skill was confirmed discoverable and loadable in a live Agent Host / Copilot
  CLI session (it surfaced in the session's available-skills list), and its five relative links and LF line endings were
  verified; the body is identical to the prompt apart from the intended `+1 ../` link-depth adjustment. **Decision still
  deferred:** whether to delete the prompt (Agent-Host-only) or keep both (dual VS-Code-chat + Agent-Host support). Keep
  both for now — the prompt still serves VS Code chat via its `agent:` frontmatter, and `disable-model-invocation: true`
  is intended to return once github/copilot-cli#4438 is fixed (restoring explicit-only semantics). If kept, edits must be
  mirrored across both files to avoid drift.
