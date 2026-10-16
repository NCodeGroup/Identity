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
| `F` — Feature gaps                    | `F31`     | `F30`      |
| `T` — Conformance & interop testing   | `T9`      | `T8`       |
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

- **`T1` — Independent OIDC/OAuth conformance validation via the OpenID Foundation Conformance Suite (remaining: nightly CI).**
  Our own integration tests encode our own reading of the specs; an independent, spec-author-maintained oracle is needed
  to catch shared misreadings. **This is not un-started — its runnable realization is already committed:** the external
  suite is wired behind a one-command harness ([`build/conformance/run-conformance.ps1`](build/conformance/run-conformance.ps1)),
  delivered under `T6` Phase 4 (see `T6`). **How it runs (local, not public):** the certified suite runs **locally in
  Docker** against the Playground OP and reaches it over `host.docker.internal:5001` — nothing is exposed to the public
  internet. Running against the OpenID Foundation's **hosted** instance (`https://www.certification.openid.net/`), which
  _would_ require a publicly reachable OP, is an optional alternative, **not** a requirement and **not** what this item
  is about. **Prerequisites — all done:** the Dynamic OP profile / Dynamic Client Registration
  ([ADR-0063](docs/adr/0063-dynamic-client-registration.md); the harness enables anonymous registration,
  `registration_endpoint_allow_anonymous`); the reachable HTTPS host (`T3` — the `Conformance` launch profile binds all
  interfaces on `:5001` and pins the discovery `issuer` to `https://host.docker.internal:5001`); the browser-automatable
  login/consent surface (`T2` — the OP-hosted `apps/auth` surface,
  [ADR-0065](docs/adr/0065-interactive-login-consent-op-hosted-auth-surface.md)); the deterministic conformance tenant,
  clients, and test-user seeding (`T4` — `AddConformanceSeed`); and discovery-metadata alignment (the OP advertises
  secure-by-default, verified capabilities, [ADR-0083](docs/adr/0083-secure-by-default-discovery-metadata.md)).
  **Remaining:** the nightly (not `dod.ps1`) CI pipeline that invokes `run-conformance.ps1` — shared with `T6` — plus the
  ongoing triage of live-run dispositions (`T8`). Full design and automation plan — harness architecture, the
  `BrowserControl`/`run-test-plan.py` automation model, target profiles, and the prerequisites above — lives in its
  dedicated doc: [`docs/conformance-testing.md`](docs/conformance-testing.md).
- **`T6` — Spec-coverage conformance harness: wire the external certified suite as the oracle (remaining: nightly CI).**
  **Relationship to `T1`/`T8` (read first):** this item owns the **in-house measurement harness** (the spec-coverage
  matrix + the headless probe-runner pre-flight) **and** the committed automation
  ([`build/conformance/run-conformance.ps1`](build/conformance/run-conformance.ps1)) that drives the external certified
  suite — i.e. this is **where `T1`'s external oracle was actually delivered**; `T8` then triages the module verdicts that
  run produces. All three run **locally**; none exposes the OP publicly.
  The in-house harness is built: the spec-coverage matrix measures the OP against the **full published
  OAuth/OIDC/CIBA/FAPI catalog** classified from **live discovery metadata** (a gap finder _and_ a regression detector),
  plus browser-drivable active probes (Phase 2) and a headless Node/backend **probe runner** (Phase 3,
  `@ncode/app-conformance-runner`) for the confidential and server-initiated legs a browser cannot drive
  (`client_credentials`, introspection/revocation with client auth, device-code polling, CIBA, `private_key_jwt`,
  DPoP/mTLS, back-channel logout). The runner exits non-zero on any failed probe, so it is already a usable local
  pre-flight. **Phase 4 is now committed:** the external **OpenID Foundation Conformance Suite** (`T1`) is wired as the
  certified oracle behind the probe-runner pre-flight by a committed, runnable harness —
  [`build/conformance/`](build/conformance) (`run-conformance.ps1` launches the OP → runs the pre-flight → brings up the
  prebuilt-image suite → drives the Config OP + Basic OP plans gated on committed baselines → tears down → one exit
  code), with config templates + baselines resolved against the `AddConformanceSeed` values and documented in
  [`docs/conformance/external-suite.md`](docs/conformance/external-suite.md). **First live-run outcome:** the **Config
  OP** certification plan passes cleanly against the harness (`44 successes, 0 failures, 0 warnings`) after three
  conformance fixes it surfaced — the `client_credentials`/`password` omitted-scope crash, and two non-standard discovery
  parameters (`discovery_endpoint`, `backchannel_authentication_request_encryption_{alg,enc}_values_supported`). The
  **Basic OP** (browser-driven) plan now drives the full authorization-code flow: the no-JS login/consent gap is
  **fixed** (the server renders a working `<form>` as a progressive-enhancement baseline the SPA still enhances), so
  13+ interactive modules complete. Four more backend bugs it surfaced are **fixed**: an `AuthorizationResult` crash on
  an omitted `state`; `expires_in` serialized as a JSON string instead of a number (new `DurationSecondsParser`); and
  the `prompt=login` / `select_account` / `create` re-authentication **loop** (`DefaultAuthorizeHandler` now guards them
  with `!IsContinuation` like `prompt=consent`, and the suite confirms the second id\*token's `auth_time` advances). The
  Basic OP plan now runs under **Dynamic Client Registration** (the config drops the static `alias` + static clients so
  the suite registers a fresh client per module, eliminating the `Stopping test due to alias conflict` serialization —
  [ADR-0063](docs/adr/0063-dynamic-client-registration.md)), which surfaced and **fixed** two further backend bugs: the
  DCR deregistration `DELETE /oauth2/register/{client_id}` failing on a client that still owned a secret
  (`ClientStore.RemoveAsync` guarded with a database query blind to the same-unit-of-work pending secret deletes), and
  **server-generated `client_secret`s failing `invalid_client`** because a symmetric secret was stored as raw bytes
  instead of the UTF-8 bytes client authentication compares against (new `ISecretGenerator.GenerateSharedSecret`,
  [ADR-0110](docs/adr/0110-shared-secret-vs-cryptographic-key-storage.md)). **First full Basic OP (DCR) run:**
  `18 PASSED, 11 WARNING, 4 SKIPPED, 4 FAILED` across 38 modules with no mass-abort (up from every module interrupting).
  **Remaining:** (a) triage the live-run dispositions (the 4 `FAILED`, 11 `WARNING`, and 4 `SKIPPED` modules) — tracked
  as `T8`. (b) a **nightly / opt-in CI pipeline** that invokes
  `run-conformance.ps1` (Docker + Python 3, no local Java) and publishes the `.results/` zips — deferred until the
  repo's broader CI bootstrap lands (no `.github/workflows` exists yet). The browser-driven Basic OP plan is opt-in in
  `run-conformance.ps1` via `-IncludeBrowserPlans`. Design, phases, and the
  `browser` / `probe-runner` / `external-suite` exercise boundary are in
  [ADR-0084](docs/adr/0084-spec-coverage-conformance-harness.md). The harness is a **measurement** surface — it tracks
  backend gaps, it does not implement them. Touch points:
  [`build/conformance`](build/conformance),
  [`src/frontend/apps/conformance`](src/frontend/apps/conformance),
  [`src/frontend/apps/conformance-runner`](src/frontend/apps/conformance-runner),
  [`src/frontend/packages/conformance-core`](src/frontend/packages/conformance-core),
  [`docs/conformance-testing.md`](docs/conformance-testing.md).
- **`T8` — Triage the Basic OP (DCR) live-run dispositions.** These are the module verdicts produced by the **external
  OpenID Foundation Conformance Suite** (the `T1` oracle), run **locally** via
  [`build/conformance/run-conformance.ps1 -IncludeBrowserPlans`](build/conformance/run-conformance.ps1) (`T6`) — **not** an
  in-house test engine. The first full browser-driven Basic OP certification run
  under Dynamic Client Registration completed `18 PASSED, 11 WARNING, 4 SKIPPED, 4 FAILED` (38 modules, no
  mass-abort). Each non-pass was investigated against the suite's per-module log and classified as a **backend gap to
  fix**, a **harness/config refinement**, or a **documented, intentional deviation** codified in
  `build/conformance/expected-failures.json` / `expected-skips.json`. **Fixed backend bugs** (see `CHANGELOG.md`):
    - **`oidcc-refresh-token`** — the consent screen (no-JS form + SPA) pre-checked a requested scope's checkbox only
      when it was _previously_ granted, so a first-time `prompt=consent` + `offline_access` consent posted an empty
      `scope` set; the recorded grant lost `openid`/`offline_access` and the token response dropped the `id_token` and
      `refresh_token`. The consent screen now pre-selects all requested scopes. (The second validation run then
      surfaced a residual — the token endpoint omitted `Cache-Control: no-store` — see below.)
    - **Token-endpoint `Cache-Control: no-store`** — RFC 6749 §5.1/§5.2 require it (and `Pragma: no-cache`) so tokens
      are never cached; the OP omitted them. Now set on every token-endpoint response. Revealed by `oidcc-refresh-token`
      once the consent fix let its refresh flow reach the refresh request (56 → 157 passing conditions).
    - **`oidcc-prompt-none-not-logged-in`** — the inline `prompt=none` → `login_required` (and no-consent-surface →
      `interaction_required`) errors omitted `state` (they bypass the `Authenticate`/`Authorize` post-processors that
      stamp it). Both now echo the request `state` (OIDC Core §3.1.2.6).
    - **`oidcc-max-age-1`** — authentication-age freshness added the 5-min `clock_skew` (so `max_age=1` never forced
      re-auth) **and** was re-evaluated at the token/refresh endpoints against a later clock (rejecting valid exchanges
      with `access_denied`). It now uses a pure elapsed-time comparison and runs only at the authorization endpoint via
      `ValidateSubjectAuthenticationCommand.EnforceAuthenticationAge`.
    - **`oidcc-prompt-none-logged-in` / `oidcc-id-token-hint` (intermittent, all-or-nothing per process)** — a
      value-type-default bug in `SettingDescriptor<TValue>` made `HasDefault` always `true` for a value-type `TValue`
      (the `notnull`-constrained `TValue?` is not `Nullable<TValue>`), so the root settings source loaded
      `default(TValue)` for every value-type descriptor lacking an explicit default. For `subject_max_age` that was
      `TimeSpan.Zero` — a zero-second freshness window that rejected every just-completed login and returned
      `login_required` to a logged-in `prompt=none` request. `HasDefault` now tracks an explicit flag, so an unset
      value-type setting stays genuinely absent. A new `IReadOnlySettingCollection.GetValueOrNull<TValue>()` (struct
      constraint, genuine `Nullable<TValue>`) is the reusable optional-value reader. Guarded by a 32-concurrent
      `prompt=none` integration regression test.
    - **`oidcc-ensure-registered-redirect-uri`** — the OP correctly refuses an unregistered `redirect_uri` with a 400
      `unauthorized_client`, but the exception handler logged that expected 4xx client error at Error level as "an
      unexpected error". 4xx `OpenIdException`s now log at Warning. The disposition itself is a **harness-automation
      limitation** (the headless scripted-browser `Verify Complete` step waits for a happy-path callback that correctly
      never comes and times out), codified in `expected-failures.json`.
    - **Codified intentional deviations.** The four `SKIPPED` modules (`oidcc-idtoken-unsigned` and the three
      request-object `…-supported-correctly-or-rejected-as-unsupported` / `oidcc-ensure-request-object-with-redirect-uri`
      tests) are spec-conformant optional-feature skips — the OP does not advertise the OPTIONAL `alg:none` value in
      `id_token_signing_alg_values_supported` / `request_object_signing_alg_values_supported` (asymmetric signing is the
      secure default) — now in `expected-skips.json`. `oidcc-ensure-registered-redirect-uri` is in
      `expected-failures.json` (both its `WebRunner` timeout and test-marker conditions).
    - **UserInfo scope-claims + ID-token claim placement (`WARNING`: `oidcc-server`, `oidcc-scope-{profile,email,address,phone,all}`,
      `oidcc-alternate-happy-flow`, `oidcc-claims-essential`).** Per OIDC Core §5.4 the `profile`/`email`/`address`/`phone`
      scope claims belong in the **UserInfo** response, not the `id_token`, whenever an access token is issued (the code
      flow). Fixed as a cohesive change: (a) `DefaultGetIdTokenSubjectClaimsHandler` now gates the scope-driven claim set
      on _no access token issued_ (a non-null `AccessToken` on the token request signals the code flow), so the ID token
      no longer carries them — requested `claims.id_token` claims, `sub`, `auth_time`, `acr`, `idp`, `amr` are
      unaffected; (b) a new account-sourced UserInfo enricher (`DefaultGetLocalAccountUserInfoClaimsHandler`, registered
      by `AddLocalAccountAuthentication` / `AddLocalAccountInteractiveAuthentication`) resolves the caller's local
      account from its self-issued connection and returns the scope-permitted claims from the account store; and (c) the
      conformance seed now provisions `given_name`/`family_name`/`name` (new `ConformanceSeedOptions.TestUserGivenName` /
      `TestUserFamilyName`). (See `CHANGELOG.md`.)
    - **`acr` emission (`WARNING`: `oidcc-ensure-request-with-acr-values-succeeds`).** Fixed by configuring the
      conformance environment, not the backend: the OP now advertises `acr_values_supported` and sets a `default_acr`
      baseline (server settings in `appsettings.Conformance.json`), so the password login's resolved `acr`
      ([ADR-0091](docs/adr/0091-acr-emission-and-conditional-auth-time.md)) is emitted and matches the suite's requested
      `acr_values`. The ACR resolver already honored `default_acr`; only the conformance environment lacked a baseline.
    - **UserInfo access token in the POST body (`WARNING`: `oidcc-userinfo-post-body`).** `DefaultAuthenticateSubjectHandler`
      now accepts the bearer token as an `access_token` form parameter (RFC 6750 §2.2 / OIDC §5.3.1) for a non-GET
      `application/x-www-form-urlencoded` request, in addition to the preferred `Authorization: Bearer` header (which
      takes precedence when both are present); the URI-query form (§2.3) stays unsupported because RFC 9700 deprecates
      it. (See `CHANGELOG.md`.)
    - **`alg:none` request object → `request_not_supported` (the `SKIPPED` residual).** OIDC Core §6.1:
      `DefaultLoadAuthorizationRequestHandler` now detects an unsigned (`alg:none`) request object (inline `request` or a
      fetched `request_uri`) up front and returns exactly `request_not_supported`; a genuinely malformed request object
      still returns `invalid_request_object`. (See `CHANGELOG.md`.)
    - **Access-token revocation on authorization-code reuse (`WARNING`: `oidcc-codereuse-30seconds`) — accepted,
      documented deviation (not a backend gap).** RFC 6749 §4.1.2 SHOULD revoke the tokens previously issued from a
      reused code _"when possible"_. The OP correctly rejects the second code use with `invalid_grant` (one-time codes)
      and enforces PKCE `S256`, which already prevents authorization-code injection (RFC 9700). Its access tokens are
      **stateless, short-lived JWTs** (the modern default), so an already-issued access token cannot be revoked without a
      denylist / reference-token store that would contradict the stateless design — the short lifetime bounds the
      exposure, which is exactly the trade-off RFC 9700 accepts for self-contained tokens. Aggressively revoking the
      legitimate client's refresh token on a replay would itself be a denial-of-service on the honest client. This
      WARNING is therefore an accepted, intentional deviation; the suite does not fail on it (warnings do not gate the
      run). Revisit only if the OP ever adopts reference/stateful access tokens.
    - **Headless auto-fill of screenshot-review placeholders (`oidcc-prompt-login`, `oidcc-max-age-1`).** These two
      double-login modules complete every automated assertion, then deliberately park in `WAITING` on a REVIEW
      "browser-interaction placeholder" (OIDCC-3.1.2.1: _"a screenshot of the second login … must be uploaded"_). The OP
      is not involved — it already passed all checks (e.g. `auth_time is later in the second id_token`). Headless, nobody
      uploads the screenshot, so each module burned the full 240 s module timeout and left the run `WAITING`/incomplete.
      `run-conformance.ps1` now runs a background watcher during the browser plan that polls `GET /api/runner/running`,
      finds pending placeholders via `GET /api/log/{id}/images` (an unfilled placeholder is a log entry with an `upload`
      field and no `img`), and `POST`s a 1×1 PNG to `/api/log/{id}/images/{placeholder}` — exactly the human-certifier
      upload step, automated. The module then finalizes within one poll as `FINISHED`/`REVIEW` (a `REVIEW` result with no
      failing conditions does not gate the run), removing ~4 min of dead wait per browser run. The suite runs
      CSRF-disabled behind an injected dummy admin, so the POST is authorized without a token.
    - **Remaining (documented follow-ups, non-blocking for Basic OP certification):**
        - **Validate the screenshot-placeholder auto-filler end-to-end; it is implemented but UNVERIFIED.** The watcher in
          `run-conformance.ps1` (see the "Headless auto-fill" item above) is coded and parse-checked, but the two browser
          runs attempted to confirm it both ended with the **conformance-suite container becoming unreachable mid-run**
          (`httpx … /api/runner/{id}/wait-state … All connection attempts failed … retrying`), so the auto-filler never
          got a clean window to prove it finalizes `oidcc-prompt-login` / `oidcc-max-age-1` fast. The OP itself was
          healthy throughout (tokens issued normally). Next session: re-run `./build/conformance/run-conformance.ps1
          -IncludeBrowserPlans -SkipFrontendInstall` on a stable suite and confirm both modules reach `FINISHED`/`REVIEW`
          within a few seconds (not the 240 s timeout), then move the "Headless auto-fill" item to `BACKLOG-COMPLETED.md`.
        - **Suite goes unreachable mid-run → `run-test-plan.py` retries forever (wrapper hangs).** When the suite's
          Docker container drops out mid-plan, the suite script's `wait-state` reconnect loop retries with unbounded
          backoff, so `run-conformance.ps1` blocks indefinitely instead of failing fast. Investigate why the prebuilt
          suite becomes unreachable under the browser plan (container OOM/health? Docker Desktop resource limits?), and
          add a bounded overall deadline / max-reconnect-attempts in the wrapper so a dead suite surfaces a fast,
          clear failure instead of an overnight hang.
        - **Parallel browser-session cookie collision (`oidcc-server`, `oidcc-idtoken-signature`).**
          The Basic OP plan runs with `parallel=2`; concurrent browser modules share the OP's login cookie on the single
          `host.docker.internal:5001` origin, so one module's scripted login can land on another module's `continue` URL
          (observed: a login submitted for `state=1fEiOJ…` completed on a page for `state=lTOSHx…`, then "Unexpected URL for
          Verify Complete"). These modules pass in isolation and passed in the first run; the collision is
          harness parallelism, not a backend defect. A harness accommodation (run the browser plan at `parallel=1`, or
          isolate cookie jars per module) would make the run deterministic.
    - Design, phases, and the baseline schema are in [`docs/conformance-testing.md`](docs/conformance-testing.md)
      (Appendix F) and [ADR-0084](docs/adr/0084-spec-coverage-conformance-harness.md). Touch points:
      [`build/conformance`](build/conformance), the authorization/token endpoint handlers and the interactive consent
      surface under [`src/NCode.Identity.OpenId.Authentication`](src/NCode.Identity.OpenId.Authentication) /
      [`src/NCode.Identity.OpenId.Authentication.Interactive`](src/NCode.Identity.OpenId.Authentication.Interactive), and
      the SPA consent form under [`src/frontend/apps/auth`](src/frontend/apps/auth).

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
