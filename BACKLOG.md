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
- **`T2` — Browser-automatable login + consent UI with a seeded conformance identity.** _Login/consent surface
  implemented._ The OpenID Provider has an opt-in interactive login/consent surface
  ([ADR-0065](docs/adr/0065-interactive-login-consent-op-hosted-auth-surface.md)): a cookie scheme the authorize
  challenge redirects into, plus a React + MUI `apps/auth` bundle (OP-hosted under `/account/*`) and credential
  endpoints reusing the local-account path, exposing the stable locators (`username`, `password`, `login`,
  `consent-allow`, `consent-deny`). Decision record:
  [ADR-0062](docs/adr/0062-opt-in-interactive-login-consent-ui.md). Detailed plan:
  [`docs/conformance/login-consent-ui.md`](docs/conformance/login-consent-ui.md). **Remaining for conformance:** a
  seeded conformance identity (`T4`) and the reachable HTTPS host to drive it headlessly end-to-end (`T3`); independent
  of `F2`/`F6`.
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

- **`U7` — Runtime consumption + auditing of resource-server secrets/settings.** [ADR-0066](docs/adr/0066-resource-server-settings-and-secret-owner.md)
  delivers resource-server settings/secrets **management** only. Two follow-ups remain: (1) **runtime consumption** —
  resource servers have no runtime domain object and are not resolved at token-issuance time, so the stored per-audience
  secrets (HS256 signing, JWE decryption) are not yet used to actually sign/encrypt access tokens; and (2) **auditing** —
  `ResourceServerApiEndpointHandler` emits no `IManagementAuditRecorder` events for **any** operation (create, update,
  delete, scopes, settings, secrets), unlike the client/tenant/server handlers. Retrofit audit uniformly across all
  resource-server operations rather than only the new leaves. Touch points:
  `src/NCode.Identity.OpenId.Management/Endpoints/ResourceServers/ResourceServerApiEndpointHandler.cs`,
  `src/NCode.Identity.OpenId.Management.Abstractions/Auditing`, and the token-issuance pipeline for consumption.
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
- **`S6` — Grow the management capabilities document.** `GET /api/capabilities`
  ([ADR-0067](docs/adr/0067-secret-type-capability-catalog-and-capabilities-discovery.md)) is composed from
  registered providers and currently publishes `secretTypes` and `eccCurves`
  ([ADR-0068](docs/adr/0068-unified-extensible-elliptic-curve-catalog.md)). Natural next sections: the supported
  signature/key-management **algorithms** (from `IAlgorithmCollectionProvider`) and per-type **key-size** options, so
  the console create-secret form drives its algorithm and key-size inputs from discovery instead of its remaining
  hard-coded defaults. Each is a new `ICommandHandler<DiscoverManagementCapabilitiesCommand>` provider.
  Touch points: `src/NCode.Identity.OpenId.Management/Endpoints/Capabilities/`,
  `src/frontend/packages/ui/src/components/SecretFormDialog.tsx`.

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
