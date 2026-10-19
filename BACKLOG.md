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

This family is **greenfield and pre-release** — everything lands on the single `main` trunk
([ADR-0125](docs/adr/0125-trunk-based-development-on-main.md)), nothing is shipped, and there is **no
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
| `D` — Deferred, intentionally not-yet | `D15`     | `D14`      |
| `F` — Feature gaps                    | `F60`     | `F59`      |
| `T` — Conformance & interop testing   | `T10`     | `T9`       |
| `U` — Frontend presentation platform  | `U17`     | `U16`      |
| `S` — Secrets management              | `S12`     | `S11`      |
| `X` — Tooling & agent customization   | `X5`      | `X4`       |

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
- **`D11` — Time/date extension methods carry logic inside the `*.Abstractions` package.**
  `NCode.Identity.Jose.Abstractions/Extensions/` still holds `DateTimeExtensions`, `DateTimeOffsetExtensions`, and
  `TimeProviderExtensions` — each a C# 14 `extension(...)` member with a real method body (`WithPrecisionInSeconds`,
  `GetUtcNowWithPrecisionInSeconds`, `GetTimestampWithPrecisionInSeconds`) — which violates the house rule that
  `*.Abstractions` packages hold only dumb DTOs / POCOs / interfaces / contracts (no logic). This is the same violation
  the now-closed `D9` fixed for the dictionary accessor, but these are **general time/date helpers**, not JSON, so they
  cannot follow `D9` into `NCode.Json`; they need a different non-abstractions home (a general NCode utility library).
  They are consumed by ~28 OpenId handler files via `using NCode.Identity.Jose.Extensions;`
  (`GetUtcNowWithPrecisionInSeconds`), so the relocation is a larger cross-cutting change best done as its own focused
  pass. Touch points: `src/NCode.Identity.Jose.Abstractions/Extensions/DateTimeExtensions.cs`,
  `src/NCode.Identity.Jose.Abstractions/Extensions/DateTimeOffsetExtensions.cs`,
  `src/NCode.Identity.Jose.Abstractions/Extensions/TimeProviderExtensions.cs`, and every
  `NCode.Identity.Jose.Extensions` consumer.

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
    `response_type=code`, PKCE `S256`, require PKCE + PAR, short code/PAR lifetimes, a ≤ 60s `clock_skew` ceiling, and a
    pinned five-minute `client_assertion_max_lifetime` ceiling ([ADR-0124](docs/adr/0124-jwt-lifetime-cap-and-client-assertion-max-lifetime.md))),
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
    objects / client assertions (rejecting an `iat` or `nbf` beyond it, FAPI 2.0 §5.3.2.1), and enforces single-use
    authorization codes with an exact-matched, required `redirect_uri`.

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
      methods, and a client authenticates only with the method it registers
      ([ADR-0145](docs/adr/0145-a-client-authenticates-only-with-its-registered-method.md)), so a dynamically registered
      client cannot use an advertised strong-auth method at all and must be provisioned through the management API —
      and project the `tls_client_auth_*` metadata from the registration request. An asymmetric method needs its key
      material at registration (`jwks` / `jwks_uri` for `private_key_jwt` and `self_signed_tls_client_auth`), and only
      the secret-based methods (`client_secret_*`) are issued a `client_secret`.
    - **Deferred hardening — disable CORS on the authorization endpoint (FAPI §5.2.3).** No current exposure: the
      repository ships only a development-only, explicit-origin CORS policy that is not applied to the authorization
      endpoint in production. Add a defense-in-depth guard that asserts no CORS policy can ever attach to the
      front-channel endpoints (low priority).

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
- **`F28` — JOSE/COSE algorithm coverage beyond RFC 7518 (EdDSA, modern curves, post-quantum).** The JOSE library today
  is a complete, faithful implementation of **RFC 7518 (JWA)** — 43 algorithm instances: JWS `none`/HS\*/RS\*/PS\*/ES\*,
  JWE key-management `dir`/A\*KW/A\*GCMKW/RSA1_5/RSA-OAEP\*/ECDH-ES(+A\*KW)/PBES2-\*, JWE content-encryption
  A\*CBC-HS\*/A\*GCM, plus `DEF` compression — registered in
  [`src/NCode.Identity.Jose/Algorithms/DefaultAlgorithmDataSource.cs`](src/NCode.Identity.Jose/Algorithms/DefaultAlgorithmDataSource.cs)
  over three `SecretKey` types (`Symmetric`, `Rsa`, `Ecc` with NIST P-256/384/521 only). Everything standardized **after**
  RFC 7518 is absent. Candidate additions, in rough priority order:
    1. **EdDSA (RFC 8037)** — **signing is done** (see completed `F29`): `EdDSA` over **Ed25519**/**Ed448** and a new
       **`OKP` (Octet Key Pair) `SecretKey`** type ship as the opt-in `NCode.Identity.Jose.EdDsa` package, a clean-room
       RFC 8032 implementation with zero third-party cryptographic dependency. The remaining EdDSA-family gap is
       key-agreement: **ECDH-ES with X25519/X448** (Montgomery curves) for JWE, which has no BCL primitive and is a
       separate future addition.
    2. **Post-quantum (FIPS 203/204/205)** — **ML-DSA signing is done**: `ML-DSA-44`/`65`/`87` ship in the **core**
       Jose + Secrets libraries over a new `AKP` (Algorithm Key Pair) `SecretKey`, composing the in-box .NET 10
       `System.Security.Cryptography.MLDsa` primitive (no third-party dependency), advertised only where
       `MLDsa.IsSupported` — its JOSE binding is the finalized **RFC 9964** (not a draft), captured in
       [ADR-0118](docs/adr/0118-ml-dsa-post-quantum-signatures-in-core.md). The remaining PQC gaps are **SLH-DSA**
       (SPHINCS+) signatures — blocked on **both** axes: the BCL `SlhDsa` class is `[Experimental]` and the COSE
       SPHINCS+ draft is expired — **ML-KEM** (Kyber) key encapsulation for JWE — blocked on the **spec** axis only:
       the BCL `MLKem` is **stable/non-experimental**, but its JOSE binding is still `draft-ietf-jose-pqc-kem` with
       **TBD** code points and an open WG question (the `AKP` single-`alg` model doesn't yet fit KEM's Direct-Key-
       Agreement vs Key-Wrap split), so committing core `alg`/`enc` surface would pin it to codes that will change —
       and **hybrid** schemes (e.g. X25519+ML-KEM).
    3. **Ecosystem-specific, add-on-demand** — `ES256K`/secp256k1 (RFC 8812, blockchain/DID; deliberately discouraged
       for general OIDC), `ECDH-1PU` (RFC 9278 / draft, authenticated ECDH; DIDComm/SSI), and Brainpool curves (BSI/German
       government). ChaCha20-Poly1305 is intentionally **not** a gap — it was never standardized for JOSE `enc`.

    **Pending final decision — exactly which remaining algorithms to implement, and in which packages.** EdDSA
    (RFC 8037, delivered as `F29`) and **ML-DSA** (RFC 9964, delivered — [ADR-0118](docs/adr/0118-ml-dsa-post-quantum-signatures-in-core.md))
    are done; the still-open PQC set (SLH-DSA, ML-KEM, hybrids) and the ecosystem-specific set are not yet decided.
    Decide the concrete algorithm list before implementing each; capture the rationale in an ADR. Touch points:
    [`src/NCode.Identity.Jose/Algorithms/DefaultAlgorithmDataSource.cs`](src/NCode.Identity.Jose/Algorithms/DefaultAlgorithmDataSource.cs),
    [`src/NCode.Identity.Jose.Abstractions/Algorithms`](src/NCode.Identity.Jose.Abstractions/Algorithms) (algorithm-code
    constants), and the `SecretKey` model under
    [`src/NCode.Identity.Secrets.Abstractions/Keys`](src/NCode.Identity.Secrets.Abstractions/Keys) /
    [`src/NCode.Identity.Secrets`](src/NCode.Identity.Secrets) (new `OKP` key type).

- **`F32` — DPoP sender-constrained proof-of-possession (RFC 9449), JOSE building block.** The highest-value modern,
  non-algorithm JOSE addition and the OAuth 2.1 / FAPI 2.0 direction. Add a first-class capability to **create and
  verify `dpop+jwt` proofs**: the `typ: dpop+jwt` header with an embedded public `jwk`, the `htm`/`htu`/`jti`/`iat`
  claims, the `ath` access-token hash, the `nonce` challenge/replay flow, and the `jkt` (JWK SHA-256 thumbprint)
  confirmation that binds a token to the proof key. This is the JOSE primitive the OpenId authentication stack then
  consumes to issue and validate DPoP-bound access/refresh tokens; it complements the shipped `private_key_jwt`
  client-authentication work ([ADR-0102](docs/adr/0102-jwt-bearer-client-authentication.md)). The `F33` JWK thumbprint
  (`jkt`) it needs is delivered. Touch points: [`src/NCode.Identity.Jose`](src/NCode.Identity.Jose),
  [`src/NCode.Identity.Jose.Abstractions`](src/NCode.Identity.Jose.Abstractions),
  [`src/NCode.Identity.OpenId.Authentication`](src/NCode.Identity.OpenId.Authentication). Capture the design in an ADR.

- **`F34` — SD-JWT + SD-JWT VC (selective disclosure).** The hottest bleeding-edge JOSE area (verifiable credentials,
  EUDI wallet). Issuer signs over salted-hash claim digests; the holder discloses a chosen subset plus a key-binding
  JWT. Builds additively on the existing JWS encoder: salted disclosure generation/parsing, the `_sd` / `_sd_alg`
  digest structures, `~`-separated combined format, and holder key binding (`cnf`/`kb+jwt`). Larger, standalone
  feature — likely its own opt-in package. Track the IETF SD-JWT and SD-JWT VC specs (approaching RFC). Touch points:
  [`src/NCode.Identity.Jose`](src/NCode.Identity.Jose),
  [`src/NCode.Identity.Jose.Abstractions`](src/NCode.Identity.Jose.Abstractions). Capture the design in an ADR.

- **`F37` — COSE / CWT (RFC 9052 / 9053 / 8392).** CBOR Object Signing and Encryption plus CBOR Web Tokens — the basis
  for ISO mdoc/mDL (18013-5) and EUDI-wallet credentials, and genuinely bleeding-edge for .NET. A large, standalone
  effort (needs a CBOR codec) that reuses the existing algorithm/`SecretKey` model but a different wire format; almost
  certainly its own package family (`NCode.Identity.Cose*`). Track IETF COSE RFCs. Capture the design in an ADR.

- **`F38` — JOSE-HPKE (Hybrid Public Key Encryption, RFC 9180 + `draft-ietf-jose-hpke-encrypt`).** The modern
  KEM-based JWE path and the vehicle most **post-quantum hybrid** encryption rides on (pairs with the ML-KEM work in
  `F28`). Adds HPKE key-encapsulation as a JWE key-management mode. The JOSE registration is still **draft** (codes may
  change), so this is prototype-behind-the-draft-codes, likely behind a feature flag / separate package until stable —
  same posture as the PQC item in `F28`. Touch points:
  [`src/NCode.Identity.Jose/Algorithms/KeyManagement`](src/NCode.Identity.Jose/Algorithms/KeyManagement),
  [`src/NCode.Identity.Jose.Abstractions/Algorithms`](src/NCode.Identity.Jose.Abstractions/Algorithms).

- **`F40` — Bound the lifetime of signed request objects (JAR, CIBA, FAPI Message Signing).** Signed request objects —
  the authorization endpoint's inline `request` / `request_uri` (RFC 9101) and CIBA signed authentication requests —
  have their `nbf` / `iat` / `exp` validated (and `exp` required) but no lifetime cap, so a pre-generated request object
  stays usable until an arbitrarily distant `exp`. FAPI 1.0 Advanced and FAPI 2.0 Message Signing
  require a request object to carry `nbf` and `exp`, with `exp` no more than 60 minutes after `nbf` and `nbf` no more
  than 60 minutes in the past; CIBA Core §7.1.1 requires `iat`, `nbf`, `exp`, and `jti` on a signed authentication
  request. The general `TokenLifetimeRequirement.MaxLifetime` cap built for `F39`
  ([ADR-0124](docs/adr/0124-jwt-lifetime-cap-and-client-assertion-max-lifetime.md)) provides the lifetime half: add a
  `request_object_max_lifetime` setting (`Min` ceiling, pinned by the FAPI profile), require the claims each profile
  demands (`nbf`, and CIBA's `iat` / `jti`) in both loaders, and decide the default. The CIBA loader also still
  validates with the JWT library's default 5-minute clock skew rather than the client's `clock_skew` setting, and with
  only the client's stored keys rather than `IClientKeyResolver` (runtime `jwks_uri`); align it with the JAR loader in
  the same pass. Touch points:
  `src/NCode.Identity.OpenId.Authentication/Endpoints/Authorization/Handlers/DefaultLoadAuthorizationRequestHandler.cs`,
  `src/NCode.Identity.OpenId.Authentication/Endpoints/BackchannelAuthentication/Handlers/DefaultLoadBackchannelAuthenticationRequestHandler.cs`,
  `OpenIdSettingKeys`, `Fapi2SecurityProfile`.

- **`F43` — Validate `tls_client_auth` certificate chains in the server against configured trust anchors.**
  `tls_client_auth` ([ADR-0107](docs/adr/0107-mutual-tls-client-authentication.md)) assumes the TLS terminator already
  PKI-validated the certificate; the handler only matches subject DN / SAN. That forces the proxy to
  `RequireAndVerifyClientCert` against a CA bundle, which (a) rejects `self_signed_tls_client_auth` certificates on the
  same host, (b) cannot select a per-tenant CA when tenants share a hostname (TLS options bind per SNI, before the tenant
  is known), and (c) becomes an **authentication bypass** if the proxy only requests the certificate
  (`RequireAnyClientCert`) — a self-signed certificate carrying the registered subject DN would match. Add
  tenant/client-scoped trust-anchor settings (CA bundle, optional intermediates from the forwarded chain, revocation
  policy) and build the chain with `X509Chain` (custom root trust, `clientAuth` EKU, validity) before the identity match,
  so the proxy only needs to request and forward the certificate. Found during the
  [hosting analysis](docs/hosting-analysis.md#8-traefik-mtls-and-tenant-pki). Touch points:
  `src/NCode.Identity.OpenId.Authentication/Clients/Handlers/TlsClientAuthClientAuthenticationHandler.cs`,
  `MutualTlsClientAuthenticationHandler`, `IClientCertificateResolver` (surface the chain), `OpenIdSettingKeys`. Capture
  the design in an ADR.

- **`F44` — Accept Traefik's `X-Forwarded-Tls-Client-Cert` encoding.** Traefik's `passTLSClientCert` (`pem: true`)
  forwards each certificate as base64 DER (PEM delimiters and newlines stripped) and comma-separates the chain when the
  client presents one. `MutualTlsClientCertificateFormat.Base64Der` decodes a single certificate only, so a presented
  chain fails to parse and `DefaultClientCertificateResolver` silently falls back to "no certificate". Make the base64-DER
  format list-aware (leaf first; expose the rest as intermediates for `F43`) or add a dedicated format. Touch points:
  `src/NCode.Identity.OpenId.Authentication/Tokens/ProofOfPossession/DefaultClientCertificateResolver.cs`,
  `src/NCode.Identity.OpenId.Authentication.Abstractions/Tokens/MutualTlsClientCertificateFormat.cs`.

- **`F45` — Per-tenant rate limits and quotas.** Customers run their dev, test, and prod environments as separate
  tenants on the one production platform ([hosting analysis](docs/hosting-analysis.md#environment-model)), so a
  customer's load test against a `-dev` tenant shares hardware with every other tenant's production. Nothing limits a
  tenant's request rate today. Add tenant-partitioned rate limiting (ASP.NET Core rate limiting, partitioned by the
  resolved tenant and, within it, by client) with limits as tenant settings bounded by the merge lattice, a shared store
  (Valkey) so limits hold across replicas, and RFC 6585 `429` + `Retry-After` responses; consider per-tenant quotas
  (requests/day, token issuances) for plan tiers. Capture the design in an ADR.

- **`F46` — Kubernetes deployment artifacts for the host.** `NCode.Identity.OpenId.Host` is the deployable
  ([ADR-0130](docs/adr/0130-distinct-names-for-host-local-dev-orchestrator-and-library.md)); it already trusts forwarded
  headers only from configured proxies (`ReverseProxy:KnownNetworks` — set it to the cluster's pod CIDR) and refuses
  development-only settings in Production (`AddProductionSafety`). Still missing for a rolling deployment behind
  Traefik: the [`Dockerfile`](src/NCode.Identity.OpenId.Host/Dockerfile) is development-shaped (copies the whole repo,
  exposes 80/443 although .NET listens on 8080, runs as root, amd64 only) and never builds the `apps/auth` interactive
  bundle — `InteractiveAuthentication:BundleRootPath` is set only in `launchSettings.json` — so the container cannot
  render login or consent; `/health` has no checks registered, so readiness (database, Valkey) is not separated from
  liveness; no Helm chart (probes, `preStop` drain, PodDisruptionBudget, restricted security context); and
  `Host.MigrationService` (which has no image) should run as a pre-deploy Job with expand/contract schema changes so a
  rolling update never breaks the running version. The production-safety gate should also refuse a tenant with no
  configured issuer (the [ADR-0129](docs/adr/0129-the-tenant-issuer-is-the-single-authoritative-origin.md)
  follow-up) and an unprotected Data Protection key ring (`S10`). Plus the OpenTofu modules, bootstrap stack, and GitHub
  Actions workflows in the public `NCodeGroup/identity-platform` repository
  ([ADR-0132](docs/adr/0132-platform-infrastructure-as-code-is-public-on-github.md); see the
  [hosting analysis](docs/hosting-analysis.md#11-iac-and-delivery)). Touch points:
  `src/NCode.Identity.OpenId.Host/Program.cs`, `src/NCode.Identity.OpenId.Host/Dockerfile`,
  `src/NCode.Identity.OpenId.Host.MigrationService`, `src/NCode.Identity.Server/ProductionSafety`,
  `src/frontend/apps/auth`.

- **`F47` — Route a tenant's mTLS endpoint-alias host.** `mtls_endpoint_alias_authority` publishes the RFC 8705 §5
  `mtls_endpoint_aliases` on a separate authority (for example `https://acme.mtls.example`), but `DynamicByHost` routes
  only on the host component of the tenant's issuer
  ([ADR-0129](docs/adr/0129-the-tenant-issuer-is-the-single-authoritative-origin.md)), so a request to the alias host
  resolves no tenant. Multi-tenant mTLS on per-tenant hostnames therefore cannot work today. Derive the alias host into
  the routing lookup the same way as the issuer host (never independently writable), decide whether the alias authority
  becomes a first-class tenant property like the issuer, and enforce host uniqueness across issuer and alias hosts under
  the host strategy. Found during the [hosting analysis](docs/hosting-analysis.md#domain-and-dns--nidentityio). Touch
  points: `src/NCode.Identity.OpenId.Core/Tenants/Strategies/DynamicByHostTenantStrategy.cs`, the tenant store's
  issuer-host index, `DefaultDiscoverMetadataHandler`.

- **`F48` — Publish the deployment artifacts the platform runs.** Per
  [ADR-0133](docs/adr/0133-we-ship-and-run-the-same-deployment-artifacts.md), the hosted platform consumes only the
  artifacts offered to self-hosters. Deliver: the host's Helm chart in this repository (built on `F46`), released with
  the host image as a signed OCI chart on GHCR with provenance and chart tests; in `NCodeGroup/identity-platform`, the
  layered building blocks — OpenTofu component modules (`modules/`: Cloudflare zone and email routing, AWS state backend,
  backup bucket and GitHub OIDC, Akamai LKE and Postgres, OCI OKE, Flux bootstrap), in-cluster components
  (`kubernetes/components/`: Traefik, cert-manager, Valkey, `identity`), and the `akamai` and `oci` stacks that compose
  them — with `tofu test` tests, one lockstep semver version per release (breaking = interface change or forced
  resource replacement; `moved` blocks for refactors), protected tags, signed OCI artifacts on GHCR for the modules and
  Kustomize bases, and release notes; the member-rule CI check that rejects local sources under `live/` and
  `clusters/`; and a self-hosting guide stating the support tiers. Keep releases `0.x` and unannounced until the lab
  and production run them by released version.
  _Progress:_ the first components (`tags`, `aws-state-backend`, `aws-backup-bucket`, `aws-kms-key`, `aws-github-oidc`,
  `cloudflare-zone`, `github-repository`), the `live/bootstrap` root, and the `validate` CI workflow exist; release
  `v0.2.0` is current and `live/bootstrap` consumes it, and CI enforces the member rule for `live/`. The `deploy`
  workflow plans each CI root on pull requests, applies the reviewed plan through the root's GitHub environment, and
  fails a daily run on drift; `live/global` runs through it and owns inbound email routing. The platform bootstrap is
  applied and closed out (2026-10-18): `nidentity.io` is delegated with a secure DNSSEC chain and inbound email is
  confirmed; the OCI sign-up (runbook section 5) waits for the lab. Provenance per
  [ADR-0136](docs/adr/0136-provenance-is-attested-on-workloads-and-audited-for-infrastructure.md) is in place for
  infrastructure: CI names its AWS sessions `gh-<run_id>-<run_attempt>`, and `aws-github-oidc` (v0.2.0) requires that
  form and trusts GitHub's immutable OIDC subject (owner and repository IDs, the default for repositories created after
  July 15, 2026). Next: the rest of `live/global` (DNS records — including the lab's, so the lab never holds a zone
  token — and zone settings) and `live/lab` on OCI OKE; each root that manages AWS resources adds exactly those
  permissions to its apply role, which today can reach only its state and secrets. Beyond this item, the lab needs
  `F46` and `S11`, plus `F49` to run more than one replica. Workload images and charts get
  build-provenance attestations and signatures that Flux verifies before the first production rollout.

- **`F49` — Shared `IDistributedCache` for multi-replica deployments.** The DPoP nonce service, the DPoP proof replay
  cache, and the client-assertion replay cache (RFC 7523 §3) run over `IDistributedCache`, and the server registers
  `AddDistributedMemoryCache()` by default, so every replica keeps its own: a nonce issued by one pod is rejected by
  another, and a proof or assertion replayed against a different pod is accepted. Nothing in the server composition or
  the host registers a shared provider, so a deployment must run a single replica until one exists. Add a
  configuration-bound Valkey (Redis-protocol) registration to the server composition, wire it in the host, and report
  it in readiness (`F46`). Give the replay caches an atomic set-if-absent on that store to close the check-then-set race
  their `IDistributedCache` implementations document. Touch points:
  `src/NCode.Identity.OpenId.Authentication/Tokens/DefaultRegistration.cs`,
  `src/NCode.Identity.OpenId.Authentication/Tokens/ProofOfPossession/DefaultDPoPNonceService.cs`,
  `src/NCode.Identity.OpenId.Authentication/Tokens/ProofOfPossession/DefaultDPoPProofReplayCache.cs`,
  `src/NCode.Identity.OpenId.Authentication/Clients/DefaultClientAssertionReplayCache.cs`,
  `src/NCode.Identity.OpenId.Host/Program.cs`.

- **`F50` — Serve the interactive login and consent surface under the tenant route.** The protocol endpoints are served
  under the tenant route ([ADR-0138](docs/adr/0138-protocol-endpoints-are-served-under-the-tenant-route.md)), but the
  interactive surface is still mounted at absolute routes (`/account/*`) with a fixed cookie `LoginPath`, a single
  cookie, and its bundle's static assets at the root. A static or host-selected tenant does not notice, but under
  `DynamicByPath` the surface's environment filter has no `{tenantId}` to resolve, so interactive flows fail — and
  path tenants sharing an origin would share one session cookie, which must not happen. Mount the surface's group under
  the tenant route, make the login and logout redirects tenant-relative, scope the cookie (name or path) per tenant,
  serve the bundle beneath the tenant route, and have the `apps/auth` bundle derive its API base from its own location.
  Touch points: `src/NCode.Identity.OpenId.Authentication.Interactive/InteractiveAuthenticationEndpointGroup.cs`,
  `src/NCode.Identity.OpenId.Authentication.Interactive/DefaultRegistration.cs`,
  `src/NCode.Identity.OpenId.Authentication.Interactive/InteractiveAuthenticationApplicationBuilderExtensions.cs`,
  `src/frontend/apps/auth`. Capture the design in an ADR.

- **`F51` — Validate a tenant's issuer against the tenant route in the management API.** The static tenant's configured
  issuer is checked when the host starts: an absolute `http`/`https` URL ending in the tenant path
  ([ADR-0137](docs/adr/0137-configuration-binds-strictly-and-is-validated-at-startup.md)). A tenant created or updated
  through the management API gets no such check, so it can be given an issuer that no endpoint is served under — a
  `DynamicByPath` tenant `acme` whose issuer does not end in `/acme`, or any tenant with a query or fragment in its
  issuer — and conforming clients reject its discovery document. Apply the same rules in the tenant validator, binding
  the tenant route with the tenant's own identifier for the path strategy. Touch points:
  `src/NCode.Identity.OpenId.Management/Endpoints/Tenants/DefaultTenantValidator.cs`,
  `src/NCode.Identity.OpenId.Core/Tenants/TenantResolutionOptionsValidator.cs` (share the issuer rule).

- **`F54` — Make every registered security profile un-loosenable down the cascade, not only `fapi2`.** The
  `security_profile` domain is the set of registered `ISecurityProfile` names
  ([ADR-0140](docs/adr/0140-setting-values-are-validated-against-their-domain.md)), so a deployment can add a profile by
  registering one. Its merge is not generic: `MergeSecurityProfile` keeps a parent's selection only when it is `fapi2`,
  so a tenant that mandates any other registered profile can have it cleared (or replaced by a weaker one) by a client.
  Make the merge honor every registered profile — a child may not clear or replace a parent's profile, or may only
  select one the parent's profile declares as at least as strict — and let the profile itself declare that ordering
  rather than hard-coding a name. Touch points:
  `src/NCode.Identity.OpenId.Core/Settings/DefaultSettingDescriptorDataSource.cs` (`MergeSecurityProfile`),
  `src/NCode.Identity.OpenId.Abstractions/Settings/ISecurityProfile.cs`. Capture the design in an ADR (amending
  [ADR-0108](docs/adr/0108-fapi-security-profile-merge-lattice-ceiling-injection.md)).

## Conformance & interop testing (group `T`)

- **`T1` (subsumes `T6`) — Independent OIDC/OAuth conformance validation via the OpenID Foundation Conformance Suite,
  delivered through the in-house spec-coverage harness (remaining: nightly CI).** An independent, spec-author-maintained
  oracle to catch misreadings our own integration tests share, driven by an **in-house measurement harness** (the
  spec-coverage matrix + the headless probe-runner pre-flight) **and** the committed automation
  ([`build/conformance/run-conformance.ps1`](build/conformance/run-conformance.ps1)) that drives the external certified
  suite. Everything runs **locally in Docker** against the OpenID host over `host.docker.internal:5001` — nothing is
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
- **`T9` — Ephemeral Azure dev/test environment for hosted-suite certification and topology tests.** The local `T1`
  harness cannot exercise what only a deployed, publicly reachable OP can: the OpenID Foundation's hosted conformance
  suite (official certification), the production-shaped path (L4 LB → Traefik → forwarded client certificate, multiple
  replicas sharing Valkey, networked Postgres — including the mTLS/FAPI plans and `F43` / `F44` / `F47`), k6 load
  tests for sizing, and Key Vault remote signing (`S7`). Build an OpenTofu (`azurerm`) + GitHub Actions workflow on the
  Visual Studio Professional Azure credit (dev/test only; instances may be suspended after 120 h continuous run) that
  applies, deploys, runs, uploads results, and always destroys, authenticated by GitHub OIDC federation. Costs and
  constraints are in [`docs/hosting-analysis.md`](docs/hosting-analysis.md#10-devtest-and-conformance-on-azure-visual-studio-credit).
  Depends on the CI bootstrap noted under `T1`.
- **`T6` — Spec-coverage conformance harness (folded into `T1`).** The in-house measurement harness and the committed
  `run-conformance.ps1` automation that drives the external certified suite are the mechanism by which `T1`'s
  independent-oracle goal was delivered; they are tracked together under `T1` above. See there for status, the lone
  remaining item (nightly CI), design ([ADR-0084](docs/adr/0084-spec-coverage-conformance-harness.md)), and touch points.

## Frontend presentation platform (group `U`)

The platform foundation landed under [`src/frontend`](src/frontend) with the Management console as the first surface
([ADR-0064](docs/adr/0064-frontend-presentation-platform.md)). Remaining work builds additively on it.

- **`U15` — Adopt TypeScript 7 once typescript-eslint supports it.** TypeScript 7 is the native (Go) compiler and its
  npm package no longer exposes the JavaScript compiler API (`ts.createSourceFile` and friends) that
  `typescript-eslint` parses with; `typescript-eslint` 8.72 caps its `typescript` peer at `<6.1.0`. The workspace holds
  the newest 6.x and [`.github/dependabot.yml`](.github/dependabot.yml) ignores TypeScript majors
  ([ADR-0147](docs/adr/0147-frontend-toolchain-follows-the-pinned-runtime-and-supported-upstreams.md)). When
  `typescript-eslint` widens its peer range to 7, bump every workspace's `typescript` together, drop the Dependabot
  ignore, and keep `pnpm typecheck` (`tsc --noEmit`) and `pnpm lint` green. Touch points: `src/frontend/package.json`
  and each `apps/*` / `packages/*` `package.json`, `.github/dependabot.yml`.
- **`U16` — Move the frontend toolchain runtime to the next Node LTS.** The frontend runs on Node 22
  ([`.node-version`](src/frontend/.node-version), the CI `node-version`), which is in Maintenance LTS until
  2027-04-30; Node 24 is Active LTS until 2026-10-20 and Node 26 enters Active LTS on 2026-10-28. `@types/node` tracks
  the runtime major and Dependabot ignores its majors
  ([ADR-0147](docs/adr/0147-frontend-toolchain-follows-the-pinned-runtime-and-supported-upstreams.md)), so the move is
  one deliberate change: `.node-version`, `node-version` in
  [`.github/workflows/frontend.yml`](.github/workflows/frontend.yml), `engines.node` and the `@types/node` range in
  [`src/frontend/package.json`](src/frontend/package.json) (plus `apps/conformance-runner`), the Aspire AppHost's NVS
  lookup ([ADR-0071](docs/adr/0071-aspire-local-dev-orchestrator.md)), and the Node 22 mentions in
  [`src/frontend/README.md`](src/frontend/README.md) and [`CONTRIBUTING.md`](CONTRIBUTING.md).
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
  `src/NCode.Identity.Secrets.Persistence/DefaultSecretGenerator.cs`,
  `src/NCode.Identity.Secrets.Persistence/DefaultSecretSerializer.cs`.

- **`S7` — HSM / KMS / keyless-signing `SecretKey` providers.** The biggest production-modern gap for the secrets
  library: back an asymmetric `SecretKey` with an external keystore — Azure Key Vault, AWS KMS, PKCS#11 HSM, TPM 2.0,
  or Windows CNG — so private material **never leaves the boundary** and sign/unwrap operations are delegated to the
  provider. Needs a `SecretKey` that exposes operations without exportable material (the verify-only public-key
  primitive from [ADR-0103](docs/adr/0103-public-only-asymmetric-secret-keys.md) is the shape to generalize) and a
  provider abstraction the JOSE signature/key-management algorithms call instead of in-process BCL primitives.
  Touch points: [`src/NCode.Identity.Secrets.Abstractions/Keys`](src/NCode.Identity.Secrets.Abstractions/Keys),
  [`src/NCode.Identity.Secrets`](src/NCode.Identity.Secrets),
  [`src/NCode.Identity.Jose/Algorithms`](src/NCode.Identity.Jose/Algorithms). Capture the design in an ADR.

- **`S8` — Key lifecycle / rotation primitives.** First-class signing-key rollover: overlapping validity windows,
  activation/retirement grace periods, automatic rotation, and a published JWKS that reflects active **and** retiring
  keys (so verifiers accept both across a rotation) — analogous to the ASP.NET Core Data Protection key ring. Builds on
  the delivered `F33` JWK/JWKS types and the `S1` rotate endpoint. Touch points:
  [`src/NCode.Identity.Secrets`](src/NCode.Identity.Secrets),
  `src/NCode.Identity.Secrets.Persistence`. Capture the design in an ADR.

- **`S9` — Envelope encryption / hardware-backed protection at rest.** Protect stored reversible secret material with
  envelope encryption (a KMS/HSM-held root key wrapping per-secret data keys) rather than the single data-protection
  key today, and allow hardware-backed root keys. Ties into the `S5` hash-at-rest-vs-reversible fork and the `S7`
  external-keystore providers. Touch points:
  `src/NCode.Identity.Secrets.Persistence/DefaultSecretSerializer.cs`,
  `src/NCode.Identity.Secrets.Persistence.Abstractions/DataContracts/PersistedSecret.cs`. Capture the design in an ADR.

- **`S10` — Encrypt the Data Protection key ring at rest.** The host persists the ASP.NET Core Data Protection key ring
  to the same Postgres database as the secrets it protects (`PersistKeysToDbContext<DeveloperDataProtectionDbContext>`,
  unconditionally and with no `ProtectKeysWith…`), so a database backup or dump contains both the protected secret
  material and the keys that unprotect it. Production must wrap the ring with a key held outside the database — a cloud
  KMS / HSM key (Azure Key Vault, OCI Vault) or a certificate from the secret store — the server composition should
  make that the secure default rather than an opt-in, and `AddProductionSafety` should refuse an unprotected ring.
  Complements `S9` (envelope encryption of stored secrets) and `S7` (external-keystore providers). Touch points:
  `src/NCode.Identity.OpenId.Host/Program.cs`, `src/NCode.Identity.Server/IdentityServerBuilder.cs`,
  `src/NCode.Identity.Server/ProductionSafety`, `src/NCode.Identity.Secrets/DefaultRegistration.cs`.

- **`S11` — Provision tenant signing keys in production.** A tenant's signing keys come only from its persisted secrets
  (`DefaultOpenIdTenantFactory.GetTenantSecretsAsync`), and the only seeder that creates one is the development-only
  `DeveloperSigningKeySeedHandler`. `AddProductionSafety` requires `DeveloperKeys:Mode=None` in Production, so on a
  fresh production database no tenant has a signing key: the JWKS is empty, no token can be issued, and the
  [ADR-0044](docs/adr/0044-management-api-authentication-and-globaladmin-bootstrap.md) bootstrap administrator cannot
  obtain the management token it needs to create or import one through `POST api/tenants/{tenantId}/secrets` — a
  cold-start deadlock that blocks the lab. Add production key provisioning (the open "first-class production
  key-management registration" follow-up of [ADR-0002](docs/adr/0002-ephemeral-development-keys.md)): on tenant seed,
  generate the tenant's asymmetric signing keys through the normal persistence path when none is usable — covering the
  mandatory-to-implement `RS256` and the preferred `PS256` / `ES256`. Unlike the developer seeder, never prune
  undecryptable secrets; fail loudly, since that signals a lost or changed Data Protection key ring. Rotation and
  retirement follow in `S8`, external keystores in `S7`. Touch points:
  `src/NCode.Identity.Server/DevelopmentEnvironment/DeveloperSigningKeySeedHandler.cs`,
  `src/NCode.Identity.Server/ProductionSafety/ProductionSafetyRegistration.cs`,
  `src/NCode.Identity.OpenId.Core/Tenants/DefaultOpenIdTenantFactory.cs`. Capture the design in an ADR.

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
- **`X4` — Decide the .NET coverage scope.** The merged .NET coverage report
  ([ADR-0143](docs/adr/0143-ci-presents-test-results-and-coverage-in-github.md)) measures every assembly the tests load,
  so its overall line figure (≈36% on the first isolated run) is dragged down by code that is not unit-testable library
  surface: EF Core migrations in `NCode.Identity.OpenId.Host.Database` (0%), the reference host
  `NCode.Identity.OpenId.Host` (<1%), and source-generator output under `obj/` (OpenAPI XML-comment and `Regex`
  generators — ReportGenerator also warns that these files "do not exist" because they are build intermediates). Decide
  what the headline number should measure — exclude generated code (`[GeneratedCode]`, `**/obj/**`, `**/Migrations/**`)
  and the host/composition assemblies via coverlet `ExcludeByFile`/`Exclude` or ReportGenerator filters, applied
  identically to the job summary and to Codecov (`codecov.yml` `ignore`) so the two never disagree. They already
  disagree: on the first CI run (`d02ca805`) the job summary reported 35.8% .NET line coverage over 75,871 coverable
  lines, while Codecov's `dotnet` flag reported 52.8% over ≈49,600 — Codecov silently drops every report file that is
  not in the repository (the generated `obj/` sources) and scores partially-covered lines differently. Touch points:
  [`build/coverage-report.ps1`](build/coverage-report.ps1), [`tests/Directory.Build.props`](tests/Directory.Build.props),
  [`codecov.yml`](codecov.yml). Coverage thresholds are a separate, later policy decision.
