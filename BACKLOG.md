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
entry when the work lands (and note it in [`CHANGELOG.md`](CHANGELOG.md) if user-facing).

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

## Feature gaps — not yet implemented (group `F`)

- **`F1` — End-session / RP-initiated logout endpoint.** No OpenID Connect end-session endpoint, session
  termination, or back-channel logout notification exists. Until it does, there is no logout operation
  to audit. (ID-token `sid` claim for logout is also not yet emitted.)
- **`F2` — Consent persistence and enforcement.** The authorize flow negotiates `prompt` and audits the
  grant/deny decision, but there is no persisted consent record or scope pre-approval. See
  `// TODO: check consent` in
  `src/NCode.Identity.OpenId.Authentication/Endpoints/Authorization/Handlers/DefaultAuthorizeHandler.cs`.
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
- **`F11` — `sid` / `acr` claim emission (blocked on foundational subsystems).** The access- and ID-token claims
  handlers document that `sid` needs a session-management subsystem and `acr` needs authentication-context-class
  tracking; neither exists, so the claims are never emitted (`sid` also feeds `F1` logout). `DefaultTokenService`
  additionally always emits `auth_time` unconditionally (`// TODO: … should we do this conditionally?`). Touch points
  (all `src/NCode.Identity.OpenId.Authentication/Tokens/`): `Handlers/DefaultGetAccessTokenPayloadClaimsHandler.cs`,
  `Handlers/DefaultGetIdTokenPayloadClaimsHandler.cs`, `DefaultTokenService.cs`.
- **`F12` — Token-request validation hardening.** The authorization-code grant validator does not yet validate the
  requested `resource`, does not validate the actual `scope` values against the grant, and hardcodes the supported PKCE
  `code_challenge_method` set with no extensibility seam. See the `// TODO: validate resource`,
  `// TODO: validate actual scope values`, and `// TODO: provide a way to allow custom code challenge methods` markers
  in
  `src/NCode.Identity.OpenId.Authentication/Endpoints/Token/AuthorizationCode/DefaultValidateAuthorizationCodeGrantHandler.cs`.

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
- **`T2` — Browser-automatable login + consent UI with a seeded conformance identity.** No interactive
  login/consent UI exists; the authorize flow challenges a cookie scheme the Playground never registers.
  Add an opt-in, dev-grade cookie scheme + login/consent pages exposing the stable Selenium locators the
  suite drives, reusing the local-account credential path. Decision record:
  [ADR-0062](docs/adr/0062-opt-in-interactive-login-consent-ui.md). Detailed plan:
  [`docs/conformance/login-consent-ui.md`](docs/conformance/login-consent-ui.md). Depends on `T3`, `T4`;
  independent of `F2`/`F6`.
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

- **`U1` — Management auth bootstrap (authorization-code + PKCE).** _Done (client + wiring)._ The `@ncode/auth`
  package implements the browser auth-code-with-PKCE flow (verifier/challenge, authorize redirect, state validation,
  code/refresh token exchange, JWT subject) and the console's `AuthGate` completes the callback into `signedIn(...)` for
  a `urn:ncode:management`-audience token. **Still open:** automatic silent token renewal (a `refreshAccessToken`
  helper exists but no expiry-driven renew timer), and end-to-end sign-in requires a seeded/registered **public SPA
  client** (redirect URI + PKCE) and the interactive login UI (`T2`). Touch points: `src/frontend/packages/auth`,
  `packages/store` (`authSlice`), `apps/mgmt` (`AuthGate`, `auth.ts`).
- **`U7` — Dev UI↔API connectivity.** _CORS done._ A VS Code compound launch starts both servers (`API + UI`), and
  the Playground now opts into `AddDeveloperCors`/`UseDeveloperCors` (in `NCode.Identity.Server`), so the browser UI at
  `http://localhost:5173` can read discovery and call the management API cross-origin. **Still open:** the SPA is
  **unauthenticated** — management calls return `401` until a **public SPA client** (redirect URI + PKCE) is seeded and
  the interactive login (`T2`) exists; that end-to-end auth work is tracked under `U1`. Touch points:
  `src/NCode.Identity.OpenId.Playground/Program.cs`, `src/NCode.Identity.Server/DevelopmentEnvironment`,
  `apps/mgmt/public/config/bootstrap.json` (set `clientId` once the client is seeded).
- **`U2` — Management write operations (create/update/delete + settings/secrets).** The console reads tenants/clients
  today; add RTK Query mutations with tag invalidation and MUI forms for the full CRUD surface already exposed by the
  REST API (tenants, clients, resource servers/scopes, grants, secrets, settings PATCH, effective-settings), honoring
  the one-time secret reveal and optimistic-concurrency (`concurrencyToken`). Keep forms stateless + Storybook-driven.
- **`U3` — Remaining surfaces (Auth/Consent/Login, Playground, conformance) on the shared platform.** Build the `T2`
  login/consent surface ([ADR-0062](docs/adr/0062-opt-in-interactive-login-consent-ui.md)) and the eventual `F2`
  consent screen as apps on this platform rather than bespoke UIs, honoring the stable-locator contract
  (`id=username`/`password`/`login`/`consent-allow`/`consent-deny`). The server-driven cookie challenge flow stays
  unchanged; the SPA is only the form surface.
- **`U4` — Runtime bootstrap endpoint on the OP host.** The tier-agnostic bundle fetches `/config/bootstrap.json` from
  the serving host; only a dev copy exists (`apps/mgmt/public/config/bootstrap.json`). Add a host endpoint that emits
  the bootstrap document (API base, issuer, feature flags, branding) per deployment so one bundle serves every tier.
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
