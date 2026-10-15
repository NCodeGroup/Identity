# Backlog — known pending work

This file is the **single source of truth for documented known pending work** in NCode.Identity:
deferred decisions, feature gaps, and action items. It is durable, versioned repository content — not
an agent's volatile memory or a chat transcript. This file is the **primary, default view of the backlog** — it
lists **only pending work**, so scanning it is the fastest way to see what is still open. The companion archive
[`BACKLOG-COMPLETED.md`](BACKLOG-COMPLETED.md) preserves the full original text of every **completed or retired**
item (keyed by ID, cross-referenced to the [`CHANGELOG.md`](CHANGELOG.md) entry and/or ADR that closed it), so
completed work is just as discoverable without reading git history.

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

Entry convention: a short title, one-line context, and (where useful) the touch points. Because this file tracks
**pending** work only, never mark an entry "Done" and leave it here. When work is fully done, **move** the entry's
full text to [`BACKLOG-COMPLETED.md`](BACKLOG-COMPLETED.md) in the same change — do not just delete it — appending a
trailing `_Closed:_` line that points at the [`CHANGELOG.md`](CHANGELOG.md) entry and/or ADR that closed it (and note
the change in [`CHANGELOG.md`](CHANGELOG.md) if user-facing). When an entry is only **partially** done, keep it here but
rewrite it so its title and context describe only the **remaining** work (the completed portion is stated as
already-existing context, not as a "done" status).

**Allocating an ID:** take the next free number for the group from the [ID ledger](#backlog-id-ledger--never-reuse-a-number)
below and bump that group's high-water mark — **never reuse or backfill a number**, because ADRs, the changelog, and
code comments reference it long after the entry is gone. The ledger is the quick reference; the authoritative upper
bound for a group is the **maximum** of its high-water mark, the highest pending ID here, and the highest archived ID
in [`BACKLOG-COMPLETED.md`](BACKLOG-COMPLETED.md) — allocate strictly above that.

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

## Backlog ID ledger — never reuse a number

Backlog IDs outlive their entries: ADRs, the [`CHANGELOG.md`](CHANGELOG.md), and code comments reference a number long
after the item is completed and moved out of this file. So **a number is never reused or backfilled.** Allocate the next
ID for a group strictly **above** its high-water mark, then bump the mark here. Every number at or below a mark is
**allocated** — whether still active in the body below, archived in [`BACKLOG-COMPLETED.md`](BACKLOG-COMPLETED.md)
(completed or retired), or retired without a surviving entry (absorbed into another item). A completed item's full text
is preserved in [`BACKLOG-COMPLETED.md`](BACKLOG-COMPLETED.md), cross-referenced to the [`CHANGELOG.md`](CHANGELOG.md)
entry and/or ADR that closed it.

| Group                                 | Next free | High-water |
| ------------------------------------- | --------- | ---------- |
| `D` — Deferred, intentionally not-yet | `D7`      | `D6`       |
| `F` — Feature gaps                    | `F26`     | `F25`      |
| `T` — Conformance & interop testing   | `T8`      | `T7`       |
| `U` — Frontend presentation platform  | `U13`     | `U12`      |
| `S` — Secrets management              | `S7`      | `S6`       |
| `X` — Tooling & agent customization   | `X3`      | `X2`       |

---

## Deferred — evaluated, intentionally not-yet (group `D`)

- **`D1` — UserInfo access auditing.** The UserInfo endpoint returns end-user PII but is not audited. A
  `userinfo.accessed` audit event would give a PII-access trail for compliance, but it is a read on a
  hot path (one event per request) and the subject is already captured at token issuance. Deferred as
  an opt-in; revisit if a compliance requirement needs per-access PII records.
  Touch points: `src/NCode.Identity.OpenId.Authentication/Endpoints/UserInfo/`,
  `IAuditEventRecorder`.
- **`D5` — Durable / distributed `IJobDispatcher` adapter (transactional outbox, Quartz, or message broker).** The
  generic background job-dispatch port and its in-process, self-scheduling default shipped with
  [ADR-0090](docs/adr/0090-durable-outbound-delivery-dispatch-adapter.md); the in-process adapter retries transient
  failures but loses in-flight jobs on a process crash and does not coordinate across nodes. A deployment that needs
  **crash-durable, at-least-once, cluster-safe** delivery (guaranteed CIBA push or back-channel logout) must register
  its own `IJobDispatcher`. Ship a reference durable adapter — a **transactional outbox** (which also realizes the
  deferred durable-audit outbox of [ADR-0048](docs/adr/0048-durable-audit-delivery-outbox.md) as one adapter of this
  port), a **Quartz** scheduling-cluster adapter (persist jobs in its job store, honoring `DispatchOptions.Delay`
  natively), and/or a **message-broker** adapter (RabbitMQ/SQS/Kafka). A durable adapter that persists a job must
  protect payloads at rest — CIBA push jobs carry issued tokens and logout jobs carry a signed `logout_token`. Deferred
  until a concrete need justifies the persistence/migration (or broker/scheduler) operational cost. Touch points:
  implement `NCode.Identity.Dispatch.IJobDispatcher` in a new adapter package and register it before
  `AddInProcessJobDispatcher` (or omit that call).

## Feature gaps — not yet implemented (group `F`)

- **`F5` — First-class claim-shaping pipeline (Auth0-Actions-equivalent).** Custom claim shaping is already possible
  today by registering an `ICommandHandler` on the subject-claims / UserInfo commands (the mediator pipeline is the
  extension seam, per [ADR-0054](docs/adr/0054-local-account-metadata-projection.md)), but there is no first-class
  surface: a strongly-typed shaping context (event/api), per-key namespaced flattening helpers, or ordering/management.
  Layer these on the existing pipeline additively. (Subject metadata already resolves fresh per issuance by default, per
  [ADR-0055](docs/adr/0055-subject-metadata-is-a-principal-level-concept-resolved-at-issuance.md).)
  Touch points: `src/NCode.Identity.OpenId.Authentication/Tokens/Handlers/`,
  `src/NCode.Identity.OpenId.Authentication/Endpoints/UserInfo/Handlers/`.
- **`F16` — FAPI (Financial-grade API) readiness epic.** An umbrella tracking the work to make the OP certifiable
  against the OpenID Foundation **FAPI** security profiles ([FAPI 1.0 Advanced](https://openid.net/specs/openid-financial-api-part-2-1_0.html),
  [FAPI 2.0](https://openid.net/specs/fapi-security-profile-2_0.html)).

    **Target decision (2026-10-14).** **FAPI 2.0 is the primary certification target**; it is the modern, formally-analyzed
    profile and matches the repo's secure-by-default posture (code-only authorization responses, PAR, PKCE `S256`,
    sender-constrained tokens). **FAPI 1.0 Advanced is a separate future item** — it mandates hybrid `code id_token`
    flows, front-channel ID tokens, `s_hash` detached signatures, and JARM, which conflict with the posture forbidding
    hybrid/front-channel-token flows. Scope decisions: deliver **both** `private_key_jwt` **and** mTLS client auth; client
    **`jwks_uri` is the primary** public-key onboarding path with **BYOK static import secondary** (`S3`); the FAPI
    constraints are enforced via a **named security-profile mechanism** (a `security_profile` setting that tightens the
    merged setting ceilings and feeds discovery, per [ADR-0038](docs/adr/0038-setting-merge-lattice-duals.md) /
    [ADR-0083](docs/adr/0083-secure-by-default-discovery-metadata.md)); **JARM is deferred** to its own item (not required
    by FAPI 2.0, which reduces the authorization response to `code`+`iss`).

    **Already implemented (FAPI 2.0 building blocks).** Sender-constrained tokens — DPoP + mTLS with `dpop_jkt`
    authorization-code binding (`F7`, [ADR-0097](docs/adr/0097-sender-constrained-access-tokens-dpop-and-mtls.md)); PAR +
    `require_pushed_authorization_requests` ([ADR-0089](docs/adr/0089-pushed-authorization-requests.md)); JAR signed
    request objects (inline `request` + remote `request_uri`); asymmetric-only token signing `PS256`/`ES256`
    ([ADR-0075](docs/adr/0075-runtime-consumption-of-resource-server-secrets.md)); PKCE `S256` enforcement and
    `response_type=code` secure default ([ADR-0083](docs/adr/0083-secure-by-default-discovery-metadata.md)); RFC 9207
    `iss` authorization-response parameter **behavior** (the ticket carries `Issuer`; only the
    `authorization_response_iss_parameter_supported` discovery flag is missing); resource indicators RFC 8707
    ([ADR-0078](docs/adr/0078-resource-indicators-rfc-8707.md)); reused-authorization-code rejection. **Strong client
    authentication `private_key_jwt` + `client_secret_jwt`** is **done**
    ([ADR-0102](docs/adr/0102-jwt-bearer-client-authentication.md)), and the **verify-only public-key `SecretKey`
    primitive** that `private_key_jwt` needs is **done** ([ADR-0103](docs/adr/0103-public-only-asymmetric-secret-keys.md)).

    **Remaining work, in order.**
    - **Phase 0 — client public-key onboarding (continues).** The verify-only key primitive (ADR-0103) is done. Still to
      do: **(a)** a JWK-JSON → verify-only `SecretKey` parser (RSA `n`/`e`, EC `crv`/`x`/`y` imported via `RSAParameters`
      / `ECParameters` into `ISecretKeyFactory.CreateRsaPublicKey` / `CreateEccPublicKey`); **(b)** an SSRF-guarded
      `jwks_uri` fetch + cache — generalize the existing `SectorIdentifierHttpGuard.ConnectAsync` SSRF floor
      ([ADR-0095](docs/adr/0095-sector-identifier-uri-fetch-and-ssrf-guard.md)) into a shared outbound guard and add a
      named HTTP client + fetch settings (timeout / max-size / allowed-hosts / cache-TTL) mirroring the
      `sector_identifier_uri_*` settings; **(c)** an `IClientKeyResolver` that merges the client's stored `SecretKeys`
      with keys resolved from its `jwks_uri` setting (currently **stored but never consumed** —
      `RegisterClientRequest.JwksUri` → setting, but `DefaultOpenIdClientFactory` builds `SecretKeys` from stored secrets
      only), wired into `ClientAssertionClientAuthenticationHandler` and the JAR loader; then **(d)** a
      `private_key_jwt`-via-`jwks_uri` end-to-end test. Needs its own ADR for the `jwks_uri` resolution + SSRF control.
    - **Phase 1 — mTLS client authentication.** `tls_client_auth` + `self_signed_tls_client_auth` (RFC 8705 §2), distinct
      from the already-implemented cert-bound **tokens**. Reuse `IClientCertificateResolver` (exists, under
      `src/NCode.Identity.OpenId.Authentication/Tokens/ProofOfPossession/`). `self_signed_tls_client_auth` matches the
      presented certificate against the client's registered certificate keys by thumbprint (now representable via the
      public-cert path, ADR-0103); `tls_client_auth` matches the presented cert's subject DN / SAN against new per-client
      metadata (`tls_client_auth_subject_dn` + SAN variants). New handler(s) under
      `src/NCode.Identity.OpenId.Authentication/Clients/Handlers/`; discovery auto-advertises via the handler-derived
      `token_endpoint_auth_methods_supported`.
    - **Phase 2 — FAPI security-profile mechanism (own ADR).** A `security_profile` setting (e.g. `fapi2`) at tenant/
      client scope that **injects ceilings into the merge lattice** so discovery and enforcement share one source of truth:
      algorithms → `PS256`/`ES256`/`EdDSA`; require PAR; require PKCE `S256`; require sender-constrained; strong-client-
      auth-only; confidential-only; `authorization_code_lifetime` ≤ 60s (default is 5 min today); PAR `request_uri`
      lifetime < 600s; `response_type=code` only. Plus a thin request-time validator for the behavioral rules not
      expressible as setting ceilings (confidential-client rejection, client-assertion `aud` = issuer string, non-PAR
      rejection surface). Touch points: `src/NCode.Identity.OpenId.Core/Settings/DefaultSettingDescriptorDataSource.cs`,
      `OpenIdSettingKeys` / `OpenIdSettingNames`, `DefaultDiscoverMetadataHandler`.
    - **Phase 3 — remaining discovery/validation details.** Advertise `authorization_response_iss_parameter_supported`
      (behavior already emitted); apply a ≤ 60s `iat`/`nbf` future tolerance in `ValidateJwtParameters`; verify
      303-redirect / no-307 / auth-code single-use / `redirect_uri`-required-in-PAR; disable CORS on the authorization
      endpoint (FAPI §5.2.3).
    - **Phase 4 — conformance & docs.** Flip the `apps/conformance` `coverage.ts` FAPI rows to `verified`; add a FAPI-2.0
      seed client (`security_profile=fapi2`, `private_key_jwt`) to `AddConformanceSeed`; wire the OpenID Foundation
      FAPI 2.0 test plan into `T1` and the `T6` matrix.

    **Spun-out items (record when starting):** a **JARM + authorization response-mode extensibility seam** work item (the
    response modes are a hardcoded switch in `DefaultAuthorizationResultExecutor` today); **FAPI 1.0 Advanced**; and **S3**
    BYOK static public-key import (secondary to `jwks_uri`).

- **`F27` — JARM (JWT Secured Authorization Response Mode) + response-mode extensibility seam.** Signed/encrypted
  authorization responses (`query.jwt`, `fragment.jwt`, `form_post.jwt`, `jwt`). **Not required by FAPI 2.0** (which
  reduces the response to `code`+`iss`), so this was spun out of `F16`; it **is** required by FAPI 1.0 Advanced and is
  independently useful. Current state: the inbound JAR half (RFC 9101 request objects) is fully implemented, but
  **outbound** response modes are a hardcoded switch (`query` / `fragment` / `form_post`) in
  `src/NCode.Identity.OpenId.Authentication/Endpoints/Authorization/Results/DefaultAuthorizationResultExecutor.cs` with
  **no extensibility point** (unlike token grants' `ITokenGrantHandler` strategy collection). Deliverables: **(a)** a
  response-mode strategy seam (e.g. `IAuthorizationResponseMode`) replacing the switch; **(b)** a JWT-composition
  service (sign with the tenant key, optionally encrypt with the client key) that wraps the response ticket
  (`code`/`state`/`iss`/…) as JWT claims before serialization; **(c)** the `query.jwt`/`fragment.jwt`/`form_post.jwt`/
  `jwt` response-mode constants (`OpenIdConstants.ResponseModes`); **(d)** discovery settings
  `authorization_signing_alg_values_supported` (+ `_encryption_alg_`/`_enc_values_supported`) and the `.jwt` entries in
  `response_modes_supported`. The `apps/conformance` `coverage.ts` `sec-jarm` row flips to `verified` once the signing
  alg / `.jwt` modes are advertised. Touch points: `DefaultAuthorizationResultExecutor`,
  `OpenIdConstants.ResponseModes`, `IAuthorizationTicket`, `DefaultSettingsProvider` (`response_modes_supported`),
  `DefaultSettingDescriptorDataSource`. Needs its own ADR.
- **`F26` — Enterprise connections / identity brokering (external upstream IdP federation).** Compete with Auth0/Okta
  "enterprise connections" by letting a tenant federate to external/enterprise identity providers — **OIDC** (EntraID,
  Ping, Okta, Auth0, Google), **SAML 2.0**, and **WS-Fed/ADFS** — and map the upstream identity onto the existing
  federated-principal model (`FederatedIdentity` / `IPrincipalResolver` / `IFederatedIdentityLinkingPolicy`, ADR-0035 /
  ADR-0094). This is a dedicated subsystem, **not** part of token exchange (`F23`, done): token exchange accepts only
  self-issued tokens, and this epic is where external tokens become trusted. Deliverables: a per-tenant **IdP connection
  configuration** model (issuer, OIDC discovery/JWKS cache, SAML entityID/signing certificates/metadata, protocol,
  claim mapping, allowed audiences); **interactive browser federation** that redirects the user to their corporate IdP
  via the OP-hosted `apps/auth` surface and the `IInteractiveCredentialVerifier` seam (ADR-0065 / ADR-0070);
  **external→internal token exchange** that registers an external `ISubjectTokenValidator` (the seam `F23` left open) so
  a configured upstream token can be exchanged for an OP token; and **inbound assertion grants** (RFC 7523 JWT-bearer /
  RFC 7522 SAML-bearer) over the same `ITokenGrantHandler` extension seam. Open decisions deferred to this epic:
  protocol sequencing (OIDC/OAuth first, then SAML 2.0, then WS-Fed/ADFS) and whether SAML/WS-Fed handling integrates a
  maintained SAML library behind an abstraction seam or is built natively. Touch points: a new connections/persistence
  model under `src/`, the `apps/auth` surface, and a new external validator registered against `ISubjectTokenValidator`.

## Conformance & interop testing (group `T`)

- **`T1` — Independent OIDC/OAuth conformance validation (OpenID Foundation Conformance Suite).** Our own
  integration tests encode our own reading of the specs; an independent, spec-author-maintained oracle is
  needed to catch shared misreadings. The full design and automation plan — harness architecture, the
  `BrowserControl`/`run-test-plan.py` automation model, target profiles, the reachable-host /
  automatable-login / discovery-alignment prerequisites, and the nightly (not `dod.ps1`) CI wiring — lives
  in its dedicated doc: [`docs/conformance-testing.md`](docs/conformance-testing.md). The Dynamic OP profile
  prerequisite (Dynamic Client Registration) is implemented ([ADR-0063](docs/adr/0063-dynamic-client-registration.md));
  the harness enables anonymous registration (`registration_endpoint_allow_anonymous`). The reachable HTTPS host the
  harness drives is **done** (`T3` — the `Conformance` launch profile binds all interfaces on `:5001` and pins the
  discovery `issuer` to `https://host.docker.internal:5001`). The browser-automatable login/consent surface
  prerequisite is **done** (`T2` — the OP-hosted `apps/auth` interactive login/consent surface,
  [ADR-0065](docs/adr/0065-interactive-login-consent-op-hosted-auth-surface.md)). The deterministic conformance tenant,
  clients, and test-user seeding prerequisite is **done** (`T4` — `AddConformanceSeed`). The discovery-metadata
  alignment prerequisite is **done**: the OP advertises secure-by-default, verified capabilities
  ([ADR-0083](docs/adr/0083-secure-by-default-discovery-metadata.md)).
- **`T6` — Spec-coverage conformance harness: wire the external certified suite as the oracle (remaining Phase 4).**
  The in-house harness is built: the spec-coverage matrix measures the OP against the **full published
  OAuth/OIDC/CIBA/FAPI catalog** classified from **live discovery metadata** (a gap finder _and_ a regression detector),
  plus browser-drivable active probes (Phase 2) and a headless Node/backend **probe runner** (Phase 3,
  `@ncode/app-conformance-runner`) for the confidential and server-initiated legs a browser cannot drive
  (`client_credentials`, introspection/revocation with client auth, device-code polling, CIBA, `private_key_jwt`,
  DPoP/mTLS, back-channel logout). The runner exits non-zero on any failed probe, so it is already a usable local
  pre-flight. **Remaining:** wire the external **OpenID Foundation Conformance Suite** (`T1`) as the certified oracle in
  CI, with this harness as the fast pre-flight that runs first and the external suite as the authoritative gate — the
  work tracked by `T1` (reachable host, automatable login, nightly CI). Design, phases, and the
  `browser` / `probe-runner` / `external-suite` exercise boundary are in
  [ADR-0084](docs/adr/0084-spec-coverage-conformance-harness.md). The harness is a **measurement** surface — it tracks
  backend gaps, it does not implement them. Touch points:
  [`src/frontend/apps/conformance`](src/frontend/apps/conformance),
  [`src/frontend/apps/conformance-runner`](src/frontend/apps/conformance-runner),
  [`src/frontend/packages/conformance-core`](src/frontend/packages/conformance-core),
  [`docs/conformance-testing.md`](docs/conformance-testing.md).
- **`T7` — Real Node-crypto signer for the probe runner's asymmetric legs.** The Phase-3 probe runner's `private_key_jwt`
  and DPoP probes ([`src/frontend/apps/conformance-runner/src/probeRunner.ts`](src/frontend/apps/conformance-runner/src/probeRunner.ts))
  accept an **injected** `ClientAssertionSigner` / `DpopProofFactory` (unit-tested with a fake), but the CLI entry does
  not yet wire a real signer, so those legs **skip** with "no client key configured" even when the OP advertises them.
  Add a Node-`crypto`-backed signer that loads a PEM/JWK private key (`ES256`/`PS256`/`RS256`), builds a compact JWS for
  the `private_key_jwt` client assertion (OIDC Core §9) and the DPoP proof (RFC 9449 §4, embedding the public JWK in the
  header), and feed it into the runner from new `--private-key` / `--dpop-key` options. Lands most usefully once the
  backend advertises `private_key_jwt` / DPoP (`S3`, `F7`, `F16`). Touch points:
  [`src/frontend/apps/conformance-runner`](src/frontend/apps/conformance-runner).

## Frontend presentation platform (group `U`)

The platform foundation landed under [`src/frontend`](src/frontend) with the Management console as the first surface
([ADR-0064](docs/adr/0064-frontend-presentation-platform.md)). Remaining work builds additively on it.

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
  room. Import needs its own validation + threat-model ADR before implementation. The **key-model** half of
  this gap is now resolved — a verify-only public key is representable and a public-only certificate
  deserializes to one ([ADR-0103](docs/adr/0103-public-only-asymmetric-secret-keys.md)) — so `S3` is now the
  **import/validation API surface** (management + DCR) over that primitive, and the FAPI-preferred alternative is
  the runtime `jwks_uri` resolver tracked under `F16` Phase 0.
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
