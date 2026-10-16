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
| `F` — Feature gaps                    | `F32`     | `F31`      |
| `T` — Conformance & interop testing   | `T9`      | `T8`       |
| `U` — Frontend presentation platform  | `U14`     | `U13`      |
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
    `iss` authorization-response parameter (the ticket carries `Issuer` and the OP advertises
    `authorization_response_iss_parameter_supported`); resource indicators RFC 8707
    ([ADR-0078](docs/adr/0078-resource-indicators-rfc-8707.md)); reused-authorization-code rejection. **Strong client
    authentication `private_key_jwt` + `client_secret_jwt`** is **done**
    ([ADR-0102](docs/adr/0102-jwt-bearer-client-authentication.md)), and the **verify-only public-key `SecretKey`
    primitive** that `private_key_jwt` needs is **done** ([ADR-0103](docs/adr/0103-public-only-asymmetric-secret-keys.md)).
    **Client public-key onboarding** is **done**: inline JWK Set import at registration
    ([ADR-0104](docs/adr/0104-import-public-asymmetric-secret-material.md)) and **runtime `jwks_uri` resolution** via the
    `IClientKeyResolver` seam, consumed by the `client_assertion` handlers and the JAR loader, with the
    `sector_identifier_uri` SSRF floor generalized into a shared, injectable, additive-deny-extensible
    `IOutboundHttpForgeryGuard` now applied to every outbound client-URL fetch (including the previously-unguarded JAR
    `request_uri` fetch) ([ADR-0106](docs/adr/0106-runtime-jwks-uri-client-key-resolution-and-shared-ssrf-guard.md)).
    **mTLS client authentication** is **done**: `tls_client_auth` (subject-DN / SAN match) + `self_signed_tls_client_auth`
    (registered-certificate thumbprint match), gated by the client's `token_endpoint_auth_method`, reusing
    `IClientCertificateResolver` and auto-advertised in `token_endpoint_auth_methods_supported`
    ([ADR-0107](docs/adr/0107-mutual-tls-client-authentication.md)).
    **The FAPI security-profile mechanism** is **done**: a `security_profile=fapi2` setting resolved through an
    extensible **profile registry** (`ISecurityProfileCatalog` over `ISecurityProfile` definitions) that injects the
    FAPI 2.0 ceilings into the merge lattice (algorithms `PS256`/`ES256`/`EdDSA`, strong-client-auth-only,
    `response_type=code`, PKCE `S256`, require PKCE + PAR, short code/PAR lifetimes, and a ≤ 60s `clock_skew` ceiling),
    plus permitted-`token_endpoint_auth_methods_supported` enforcement in the client-auth service (rejecting weak/public
    auth under `fapi2`) ([ADR-0108](docs/adr/0108-fapi-security-profile-merge-lattice-ceiling-injection.md)).
    **The FAPI behavioral rules and discovery/validation details** are **done**
    ([ADR-0109](docs/adr/0109-fapi-behavioral-rules-and-discovery-validation-details.md)): the rules that cannot be
    expressed as a setting ceiling are modeled as composable **requirement objects** a profile composes and the catalog
    runs per enforcement phase — a confidential-client rule (public clients rejected at the authorization endpoint), a
    sender-constrained-access-token rule (DPoP **or** mTLS required at the token endpoint), and an issuer-only
    `client_assertion` audience rule — so a new requirement kind or a whole new profile is additive. The OP also
    advertises `authorization_response_iss_parameter_supported` (RFC 9207), uses **303 See Other** for the authorization
    response (FAPI §5.2.2 / OAuth 2.1), honors the operator-configured `clock_skew` when validating signed request
    objects / client assertions, and enforces single-use authorization codes with an exact-matched, required
    `redirect_uri`.

    **Remaining work.** The OP-side FAPI 2.0 implementation is **complete** (the building blocks plus the security-profile
    mechanism, behavioral rules, and discovery/validation details above); what is left is conformance wiring and one
    independent registration feature.
    - **Conformance wiring — handed to the conformance track (`T1` / `T6`).** The FAPI 2.0 conformance pieces are owned
      by the conformance work-stream (the spec-coverage harness and headless probe runner), not this OP item: flip the
      `apps/conformance` `coverage.ts` FAPI rows (today they already classify from discovery — `profile-fapi2` reports
      `implemented` once PAR + a sender-constraining mechanism + PKCE `S256` are advertised, which the OP now does); add a
      FAPI-2.0 seed client (`security_profile=fapi2`, `private_key_jwt`) to `AddConformanceSeed` (this first needs the
      conformance seed model extended — `ConformanceClientSeed` is secret-only today, with no asymmetric-key /
      `security_profile` fields); and wire the OpenID Foundation FAPI 2.0 test plan into `T1` and the `T6` matrix.
    - **OP-side follow-up — accept non-secret `token_endpoint_auth_method` values at Dynamic Client Registration.**
      Extend DCR so `private_key_jwt`, `client_secret_jwt`, `tls_client_auth`, and `self_signed_tls_client_auth` are
      accepted — the DCR `SupportedTokenEndpointAuthMethods` allow-list currently lists only the secret-based + `none`
      methods, so a client using an advertised strong-auth method must be provisioned through the management API — and
      project the `tls_client_auth_*` metadata from the registration request.
    - **Deferred hardening — disable CORS on the authorization endpoint (FAPI §5.2.3).** No current exposure: the
      repository ships only a development-only, explicit-origin CORS policy that is not applied to the authorization
      endpoint in production. Add a defense-in-depth guard that asserts no CORS policy can ever attach to the
      front-channel endpoints (low priority).

    **Spun-out items (record when starting):** a **JARM + authorization response-mode extensibility seam** work item (the
    response modes are a hardcoded switch in `DefaultAuthorizationResultExecutor` today); **FAPI 1.0 Advanced**; and **S3**
    BYOK static public-key import (secondary to `jwks_uri`).

- **`F29` — EdDSA (Ed25519 / Ed448) signing algorithm support.** The JOSE library implements RSA (`RS*`/`PS*`) and ECDSA
  (`ES*`) signatures but not **EdDSA** (the JWA `alg` value `EdDSA`, RFC 8037), the modern Edwards-curve signature scheme
  FAPI 2.0 permits. The algorithm code constant exists (`AlgorithmCodes.DigitalSignature.EdDsa`) and the FAPI security
  profile already lists `EdDSA` in its signing-algorithm ceiling, so it is **advertised/permitted the moment the
  implementation lands** (the ceiling intersects with the server's actually-supported set). Deliverables: an
  `Ed25519`/`Ed448` `SignatureAlgorithm` (sign + verify) registered in `DefaultAlgorithmDataSource`, an `OKP`-family
  `SecretKey` / key-type descriptor (`crv` = `Ed25519`/`Ed448`, the JWK `OKP` key type) with JWK/PEM import and export,
  and coverage in the JOSE round-trip tests. Touch points: `src/NCode.Identity.Jose/Algorithms/`,
  `src/NCode.Identity.Secrets/Logic/` (a new OKP descriptor alongside the RSA/ECC ones),
  `src/NCode.Identity.Jose/Algorithms/DefaultAlgorithmDataSource.cs`.

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
- **`F28` — JOSE/COSE algorithm coverage beyond RFC 7518 (EdDSA, modern curves, post-quantum).** The JOSE library today
  is a complete, faithful implementation of **RFC 7518 (JWA)** — 43 algorithm instances: JWS `none`/HS\*/RS\*/PS\*/ES\*,
  JWE key-management `dir`/A\*KW/A\*GCMKW/RSA1_5/RSA-OAEP\*/ECDH-ES(+A\*KW)/PBES2-\*, JWE content-encryption
  A\*CBC-HS\*/A\*GCM, plus `DEF` compression — registered in
  [`src/NCode.Identity.Jose/Algorithms/DefaultAlgorithmDataSource.cs`](src/NCode.Identity.Jose/Algorithms/DefaultAlgorithmDataSource.cs)
  over three `SecretKey` types (`Symmetric`, `Rsa`, `Ecc` with NIST P-256/384/521 only). Everything standardized **after**
  RFC 7518 is absent. Candidate additions, in rough priority order:
    1. **EdDSA (RFC 8037)** — the only _ratified_ standards-track gap: `EdDSA` signatures over **Ed25519**/**Ed448**,
       **ECDH-ES with X25519/X448** (Montgomery curves), and a new **`OKP` (Octet Key Pair) `SecretKey`** type alongside
       `Ecc`. .NET 10 provides native Ed25519/ML-DSA primitives, so this no longer requires BouncyCastle.
    2. **Post-quantum (FIPS 203/204/205)** — **ML-DSA** (Dilithium) and **SLH-DSA** (SPHINCS+) signatures, **ML-KEM**
       (Kyber) key encapsulation for JWE, and **hybrid** schemes (e.g. X25519+ML-KEM). The IETF COSE/JOSE registrations
       are still **draft** (codes may change), so this is prototype-behind-the-draft-codes, not a stable-surface commit.
    3. **Ecosystem-specific, add-on-demand** — `ES256K`/secp256k1 (RFC 8812, blockchain/DID; deliberately discouraged
       for general OIDC), `ECDH-1PU` (RFC 9278 / draft, authenticated ECDH; DIDComm/SSI), and Brainpool curves (BSI/German
       government). ChaCha20-Poly1305 is intentionally **not** a gap — it was never standardized for JOSE `enc`.

    **Pending final decision — exactly which algorithms to implement, and in which packages.** The standing secure-by-
    default posture points at EdDSA (RFC 8037) as the clear next addition because it is finalized, broadly expected for
    OIDC/FAPI interop, and lays the `OKP` groundwork that X25519 key-agreement and future PQC hybrids reuse; the PQC and
    ecosystem-specific sets are not yet decided. Decide the concrete algorithm list (and whether PQC lands behind a
    feature flag / separate package while its registrations are draft) before implementing; capture the rationale in an
    ADR. Touch points:
    [`src/NCode.Identity.Jose/Algorithms/DefaultAlgorithmDataSource.cs`](src/NCode.Identity.Jose/Algorithms/DefaultAlgorithmDataSource.cs),
    [`src/NCode.Identity.Jose.Abstractions/Algorithms`](src/NCode.Identity.Jose.Abstractions/Algorithms) (algorithm-code
    constants), and the `SecretKey` model under
    [`src/NCode.Identity.Secrets.Abstractions/Keys`](src/NCode.Identity.Secrets.Abstractions/Keys) /
    [`src/NCode.Identity.Secrets`](src/NCode.Identity.Secrets) (new `OKP` key type).

## Conformance & interop testing (group `T`)

- **`T1` (subsumes `T6`) — Independent OIDC/OAuth conformance validation via the OpenID Foundation Conformance Suite,
  delivered through the in-house spec-coverage harness (remaining: nightly CI).** An independent, spec-author-maintained
  oracle to catch misreadings our own integration tests share, driven by an **in-house measurement harness** (the
  spec-coverage matrix + the headless probe-runner pre-flight) **and** the committed automation
  ([`build/conformance/run-conformance.ps1`](build/conformance/run-conformance.ps1)) that drives the external certified
  suite. Everything runs **locally in Docker** against the Playground OP over `host.docker.internal:5001` — nothing is
  exposed publicly; `T8` (complete, archived in [`BACKLOG-COMPLETED.md`](BACKLOG-COMPLETED.md)) triaged the module
  verdicts a run produces. Built: all prerequisites (DCR, reachable HTTPS host, browser-automatable login/consent,
  deterministic seeding, discovery-metadata alignment); the spec-coverage matrix (OP vs the full published
  OAuth/OIDC/CIBA/FAPI catalog, classified from live discovery metadata); the browser-drivable active probes; the
  headless `@ncode/app-conformance-runner` pre-flight (confidential + server-initiated legs); and the Phase 4 harness
  that launches the OP → runs the pre-flight → brings up the prebuilt-image suite → drives the Config OP + Basic OP plans
  gated on committed baselines → tears down → one exit code. The **Config OP** plan passes cleanly (`44 successes, 0
failures, 0 warnings`); the **Basic OP** (browser-driven) plan runs end-to-end under Dynamic Client Registration
  (opt-in via `-IncludeBrowserPlans`). The harness is a **measurement** surface — it tracks backend gaps, it does not
  implement them. **Remaining:** a **nightly / opt-in CI pipeline** (not `dod.ps1`) that invokes `run-conformance.ps1`
  (Docker + Python 3, no local Java) and publishes the `.results/` zips — deferred until the repo's broader CI bootstrap
  lands (no `.github/workflows` exists yet). Design and phases are in
  [ADR-0084](docs/adr/0084-spec-coverage-conformance-harness.md),
  [`docs/conformance-testing.md`](docs/conformance-testing.md), and
  [`docs/conformance/external-suite.md`](docs/conformance/external-suite.md). Touch points:
  [`build/conformance`](build/conformance),
  [`src/frontend/apps/conformance`](src/frontend/apps/conformance),
  [`src/frontend/apps/conformance-runner`](src/frontend/apps/conformance-runner),
  [`src/frontend/packages/conformance-core`](src/frontend/packages/conformance-core).
- **`T6` — Spec-coverage conformance harness (folded into `T1`).** The in-house measurement harness and the committed
  `run-conformance.ps1` automation that drives the external certified suite are the mechanism by which `T1`'s
  independent-oracle goal was delivered; they are tracked together under `T1` above. See there for status, the lone
  remaining item (nightly CI), design ([ADR-0084](docs/adr/0084-spec-coverage-conformance-harness.md)), and touch points.

## Frontend presentation platform (group `U`)

The platform foundation landed under [`src/frontend`](src/frontend) with the Management console as the first surface
([ADR-0064](docs/adr/0064-frontend-presentation-platform.md)). Remaining work builds additively on it.

- **`U5` — Frontend CI job.** The frontend toolchain is intentionally outside `dod.ps1`. Add a separate CI job
  (`corepack` + `pnpm install` + `turbo run lint typecheck test build`) so the platform is gated independently.
- **`U6` — Per-tenant branding.** `@ncode/ui` themes from a runtime `BrandingConfig` (the seam exists); wire
  end-to-end per-tenant brand/theme resolution through the discovery channel for customer-facing surfaces.
- **`U13` — Buffer-writer (pipe) render model for the server-rendered interactive surface.** The server-side HTML
  renderer `InteractiveAuthenticationPageRenderer` (login, consent, and the `F31` non-redirectable error page) builds
  each page as a `string` via `StringBuilder` and injects it into the SPA bundle's `#root` with `string.Replace`, then
  the caller writes it with `HttpResponse.WriteAsync(html, SecureEncoding.UTF8, …)`. This matches the form-post writer
  (`DefaultAuthorizationResultExecutor.GetFormPostHtml`) but allocates a full form string **and** a full combined-document
  string per request. The family already has the lower-allocation idiom — `IBufferWriter<char>` / `Sequence<char>` +
  `SecureEncoding.UTF8` written straight to `HttpResponse.BodyWriter` (`NCode.Buffers`, as `CommonJoseEncoder` /
  `JoseSerializer` use it). Deliverables: render each page into an `IBufferWriter<char>` (pooled `Sequence<char>`)
  instead of returning a `string`; split the bundle `index.html` **once** at the `<div id="root"></div>` marker into
  head/tail `ReadOnlyMemory<char>` slices and write `head → form → tail` segments to the response pipe (no combined
  document string); apply the same seam to the login, consent, and error renderers **and** the form-post executor so the
  whole interactive render surface is consistent. Independently, **cache the bundle `index.html`** (it is re-read from
  disk per request today via `InteractiveBundleReader`, with a file-watcher/`IChangeToken` invalidation for dev
  hot-reload) — the per-request disk read dominates any string-allocation cost, so this is the larger win. Not hot-path
  critical for the `F31` error page (a cold error path), but login/consent are warmer and the cohesion/allocation
  benefit applies surface-wide. Touch points:
  `src/NCode.Identity.OpenId.Authentication.Interactive/InteractiveAuthenticationPageRenderer.cs`,
  `src/NCode.Identity.OpenId.Authentication.Interactive/InteractiveBundleReader.cs`,
  `src/NCode.Identity.OpenId.Authentication.Interactive/InteractiveOpenIdErrorResponseFormatter.cs`,
  `src/NCode.Identity.OpenId.Authentication.Interactive/InteractiveAuthenticationEndpointHandler.cs`,
  `src/NCode.Identity.OpenId.Authentication/Endpoints/Authorization/Results/DefaultAuthorizationResultExecutor.cs`.

## Secrets management (group `S`)

Follow-ups surfaced while implementing [ADR-0050](docs/adr/0050-secret-material-revealed-once-on-create.md)
(client-secret material revealed once on create).

- **`S1` — Client-secret rotate endpoint.** Rotation is create-new + delete-old (two calls) per
  [ADR-0011](docs/adr/0011-secret-management-api.md); ADR-0050 notes a convenience `rotate` would inherit
  the same one-time reveal. A `POST api/tenants/{tenantId}/clients/{clientId}/secrets/{secretId}/rotate`
  returning `CreatedSecretResource` is the natural next increment.
  Touch points: `src/NCode.Identity.OpenId.Management/Endpoints/Clients/ClientApiEndpointHandler.cs`.
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
