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
code comments reference it long after the entry is gone. Read the ledger at the moment you allocate, not from an earlier
read: a change merged or rebased in the meantime may already have taken the number — fetch `origin/main` first,
re-check the bound when you rebase, and renumber your new entries rather than collide. The ledger is the quick
reference; the authoritative upper bound for a group is the **maximum** of its high-water mark, the highest pending ID
here, and the highest archived ID in [`BACKLOG-COMPLETED.md`](BACKLOG-COMPLETED.md) — allocate strictly above that.

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

**Extensibility is the goal.** The family is built to be extended easily and without restriction — by registering, never
by editing the library — so every vocabulary is open by default and a closed construct (an `enum`, a `switch` or bit
mask over a vocabulary) must be provably closed
([ADR-0150](docs/adr/0150-vocabularies-are-open-unless-provably-closed.md)). A design that would make a consumer or
sibling package edit this library to add a member is the minimal patch, not the comprehensive model.

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
| `D` — Deferred, intentionally not-yet | `D17`     | `D16`      |
| `F` — Feature gaps                    | `F78`     | `F77`      |
| `T` — Conformance & interop testing   | `T10`     | `T9`       |
| `U` — Frontend presentation platform  | `U24`     | `U23`      |
| `S` — Secrets management              | `S13`     | `S12`      |
| `X` — Tooling & agent customization   | `X6`      | `X5`       |

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
- **`D15` — Re-evaluate the existing public enums against the open-vocabulary rule.** Vocabularies are open unless
  provably closed ([ADR-0150](docs/adr/0150-vocabularies-are-open-unless-provably-closed.md)), and the public enums
  predate the three-part test. `SessionTerminationReason` is already converted to the `SessionTerminationReasons`
  names ([ADR-0098](docs/adr/0098-session-lifecycle-termination-seam.md)). Several others look open:
  `SecretKeyImportFormat` (PKCS #12, OpenSSH, or a key-vault reference), `PersistedGrantStatus`
  (persisted; a lifecycle a grant type may extend), and `HashAlgorithmType` / `BinaryEncodingType` (closed only because
  the implementation is a `switch`). `AlgorithmType`, `RequestObjectSource`, `TenantPlane`, `SerializationFormat`,
  `DeveloperKeyMode`, the `ObjectJson*Handling` mirrors, and the operation outcomes (`PasswordVerificationResult`,
  `OutboundHttpReadResult`, `OwnerRemovalResult`) appear to pass. Apply the test to each and convert the ones that fail
  to names plus registered strategies, with data migrations for any persisted value. Touch points: the enum files under
  `src/**/` and their `switch` consumers.
- **`D16` — Resume Dependabot version updates.** Version updates are paused: every ecosystem in
  [`.github/dependabot.yml`](.github/dependabot.yml) sets `open-pull-requests-limit: 0`, because while the family is
  refactored heavily ahead of its first release a weekly bump is mostly churn against code that is about to move.
  Vulnerabilities are still handled. Dependabot alerts are on, and the "Dependabot security updates" repository
  setting opens a pull request, grouped per ecosystem, whenever a published advisory affects a dependency. Security
  updates bypass both the npm cooldown and pnpm's `minimumReleaseAge`
  ([ADR-0153](docs/adr/0153-one-release-age-window-gates-frontend-dependencies.md)). What the pause costs is drift, so
  resume no later than the first published release, and before then if the drift starts to block work — for example a
  toolchain the code needs, or a NuGet advisory the full Definition of Done fails on with no security update to fix it.
  To resume, set each `open-pull-requests-limit` back to 5. Expect each group's first pull request to be a large
  catch-up: land the frontend toolchain the way
  [ADR-0147](docs/adr/0147-frontend-toolchain-follows-the-pinned-runtime-and-supported-upstreams.md) describes, and
  hold any major that cannot land yet with an `ignore` rule and a backlog item. Touch point: `.github/dependabot.yml`.

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
    constraints are enforced via a **named security-profile mechanism** (a `security_profiles` setting that tightens the
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
    **The FAPI security-profile mechanism** is **done**: a `security_profiles` setting selecting `fapi2`, resolved through an
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
      FAPI-2.0 seed client (`security_profiles` = `[fapi2]`, `private_key_jwt`) to `AddConformanceSeed` (this first needs the
      conformance seed model extended — `ConformanceClientSeed` is secret-only today, with no asymmetric-key /
      `security_profiles` fields); and wire the OpenID Foundation FAPI 2.0 test plan into `T1` and the `T6` matrix.
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

- **`F45` — Per-tenant rate limits and quotas.** Customers run their dev, test, and prod environments as separate
  tenants on the one production platform ([hosting analysis](docs/hosting-analysis.md#environment-model)), so a
  customer's load test against a `-dev` tenant shares hardware with every other tenant's production. Nothing limits a
  tenant's request rate today. Add tenant-partitioned rate limiting (ASP.NET Core rate limiting, partitioned by the
  resolved tenant and, within it, by client) with limits as tenant settings bounded by the merge lattice, a shared store
  (Valkey) so limits hold across replicas, and RFC 6585 `429` + `Retry-After` responses; consider per-tenant quotas
  (requests/day, token issuances) for plan tiers. Capture the design in an ADR.

- **`F47` — Route a tenant's mTLS endpoint-alias host.** `mtls_endpoint_alias_authority` publishes the RFC 8705 §5
  `mtls_endpoint_aliases` on a separate authority (for example `https://acme.mtls.example`), but `DynamicByHost` routes
  only on the host component of the tenant's issuer
  ([ADR-0129](docs/adr/0129-the-tenant-issuer-is-the-single-authoritative-origin.md)), so a request to the alias host
  resolves no tenant. Multi-tenant mTLS on per-tenant hostnames therefore cannot work today. Derive the alias host into
  the routing lookup the same way as the issuer host (never independently writable), decide whether the alias authority
  becomes a first-class tenant property like the issuer — if so, give the host strategy an alias template beside its
  issuer template (`https://{tenantId}.mtls.example`) whose expansion is persisted and judged like the issuer
  ([ADR-0165](docs/adr/0165-every-served-tenant-has-an-issuer-and-its-strategy-supplies-the-default.md)) — and enforce
  host uniqueness across issuer and alias hosts under
  the host strategy. Found during the [hosting analysis](docs/hosting-analysis.md#domain-and-dns--nidentityio). Touch
  points: `src/NCode.Identity.OpenId.Core/Tenants/Strategies/DynamicByHostTenantStrategy.cs`, the tenant store's
  issuer-host index, `DefaultDiscoverMetadataHandler`, `TenantAddressRules`.

- **`F48` — Publish the deployment artifacts the platform runs.** Per
  [ADR-0133](docs/adr/0133-we-ship-and-run-the-same-deployment-artifacts.md), the hosted platform consumes only the
  artifacts offered to self-hosters. Deliver: the host's Helm chart in this repository
  ([`charts/ncode-identity-host`](charts/ncode-identity-host), built on `F46`), released with the host image as a signed
  OCI chart on GHCR with provenance and chart tests, both tagged with the version Nerdbank.GitVersioning computes and
  stamped into `Chart.yaml` (the image build has no `.git`, so the assembly version inside comes from `version.json`
  alone); in `NCodeGroup/identity-platform`, the
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
  a Valkey instance (the shared-state adapter `F49` delivered) to run more than one replica; tenant signing
  keys are provisioned on first use ([ADR-0169](docs/adr/0169-every-tenant-is-provisioned-with-persisted-keys.md)).
  Workload images and charts get build-provenance attestations and signatures that Flux verifies before the first
  production rollout.

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

- **`F68` — Check a client's registered response algorithms when its settings are written, and let a security profile
  supply compliant defaults.** A client's `id_token_signed_response_alg` and its other registered response algorithms
  must be in its effective `*_values_supported` lists
  ([ADR-0155](docs/adr/0155-jwts-exchanged-with-a-client-follow-its-registered-metadata.md)). Dynamic client
  registration checks them, but the management API, the seeders, and configuration do not, so a mismatch surfaces only
  when issuance fails with `server_error`. The common case is a security profile: the FAPI 2.0 ceiling excludes the
  registration default `RS256`, so every client under it needs an explicit `id_token_signed_response_alg`, and a profile
  can contribute only lattice-merged ceilings, not a default
  ([ADR-0148](docs/adr/0148-security-profiles-accumulate-down-the-settings-cascade.md)). Validate the registered values
  against the client's effective ceilings wherever a client's or its tenant's settings are written, and let a profile
  contribute overridable defaults (for example `PS256`) that its own ceiling then bounds. Touch points:
  `src/NCode.Identity.OpenId.Abstractions/Settings/ISecurityProfile.cs`,
  `src/NCode.Identity.OpenId.Authentication/Settings/Fapi2SecurityProfile.cs`, the management client and tenant
  endpoints.

- **`F75` — Authenticate the management API against the control plane, not the tenant the request resolves.** The
  management `/api` surface materializes its OpenID context through `CreateForRequestAsync`, so the tenant that
  validates the caller's access token is whichever tenant the protocol strategy resolves from the request. That holds
  under `StaticSingle`, but under `DynamicByPath` a collection route such as `POST /api/tenants` has no `tenantId`
  route value and fails, and `/api/tenants/{tenantId}/…` authenticates the caller against the target tenant rather
  than the tenant that issued the token; under `DynamicByHost` the management host `manage.<apex>`
  ([ADR-0131](docs/adr/0131-hosted-platform-environment-model-and-hostname-scheme.md)) is no tenant's host, so every
  call is `404`. The
  management plane should authenticate against a configured authenticating tenant — the root (control-plane) tenant
  by default ([ADR-0024](docs/adr/0024-control-plane-and-per-tenant-planes.md),
  [ADR-0042](docs/adr/0042-management-api-route-driven-tenant-scope.md)) — independent of the protocol strategy, and
  the route's `tenantId` should only scope the data. Capture the design in an ADR and cover every strategy with
  integration tests. The access token itself is validated through `IIssuedTokenValidator` against the `OpenIdContext`
  it is given ([ADR-0167](docs/adr/0167-the-server-validates-the-tokens-it-issued-under-one-policy.md)), so passing the
  authenticating tenant's context is enough; the seam needs no change. Touch points:
  `src/NCode.Identity.OpenId.Management/Endpoints/ManagementEnvironmentEndpointFilter.cs`, `IOpenIdContextFactory`,
  `TenantScopeEndpointFilter`, `OwnershipHandler`,
  `src/NCode.Identity.OpenId.Authentication/Tokens/Handlers/DefaultAuthenticateAccessTokenHandler.cs`.

- **`F77` — Register a resource-server client through dynamic client registration.** A resource server introspects
  through a client bound to it, and the binding is set only through the management API, by a caller allowed to update
  that resource server ([ADR-0168](docs/adr/0168-introspection-discloses-a-token-only-to-the-party-that-consumes-it.md)).
  RFC 9701 §3 allows the authorization server to register resource servers dynamically, which a deployment that
  provisions APIs automatically would need. Define how a registration request names its resource server and how the
  registrant proves it may act for it — an initial access token scoped to that resource server is the likely shape —
  register the `introspection_*_response_alg` metadata, and report the binding in the client information response.
  Touch points: `src/NCode.Identity.OpenId.Authentication/Endpoints/Registration/DefaultClientRegistrationEndpointHandler.cs`,
  `src/NCode.Identity.OpenId.Authentication.Abstractions/Endpoints/Registration/`.

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
- **`U21` — Move pnpm to its current major once Dependabot supports it.** The workspace pins pnpm 10 in the
  `packageManager` field, the newest major Dependabot officially supports
  ([ADR-0152](docs/adr/0152-frontend-node-and-pnpm-are-pinned-once-and-provisioned-without-corepack.md)). pnpm 12 is
  current. It is a native executable and can also provision Node itself (`devEngines.runtime`, the `pnpm/setup`
  action), locking both versions with checksums in `pnpm-lock.yaml`. That would replace NVS, `actions/setup-node`, and
  the AppHost's NVS lookup with one pin. It cannot land yet: Dependabot reads pnpm 12's multi-document lockfile as an
  empty dependency graph and stops raising security updates (dependabot-core#15904), does not officially support
  pnpm 11 or 12 (dependabot-core#16397), and ignores `devEngines` (dependabot-core#13722). When it supports them, bump
  `packageManager` by hand (Dependabot never does, dependabot-core#4830), regenerate the lockfile, decide whether
  `devEngines.runtime` replaces `.node-version` as the single Node pin, and keep `strictDepBuilds` with the reviewed
  `allowBuilds` list. Touch points: [`src/frontend/package.json`](src/frontend/package.json),
  [`pnpm-workspace.yaml`](src/frontend/pnpm-workspace.yaml), `pnpm-lock.yaml`,
  [`.github/workflows/frontend.yml`](.github/workflows/frontend.yml),
  [`.github/workflows/copilot-setup-steps.yml`](.github/workflows/copilot-setup-steps.yml),
  [`build/frontend-install.ps1`](build/frontend-install.ps1), the AppHost's `EnsurePinnedNodeOnPath`.
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
  the delivered `F33` JWK/JWKS types and the `S1` rotate endpoint; once expiry is enforced, let that endpoint keep
  the rotated client secret for a grace period
  ([ADR-0183](docs/adr/0183-a-client-secret-is-rotated-by-replacing-it-in-one-change.md)). The keys every tenant is
  provisioned with ([ADR-0169](docs/adr/0169-every-tenant-is-provisioned-with-persisted-keys.md)) carry no effective
  lifetime today —
  nothing enforces a key's expiry, so they are stamped a century out — and rollover is what gives them one; record an
  audit event when the server generates or retires a key, since provisioning only logs it. Touch points:
  [`src/NCode.Identity.Secrets`](src/NCode.Identity.Secrets),
  `src/NCode.Identity.Secrets.Persistence`, `src/NCode.Identity.OpenId.Core/Tenants/Keys/`. Capture the design in an
  ADR.

- **`S9` — Envelope encryption / hardware-backed protection at rest.** Protect stored reversible secret material with
  envelope encryption (a KMS/HSM-held root key wrapping per-secret data keys) rather than the single data-protection
  key today, and allow hardware-backed root keys. Ties into the `S5` hash-at-rest-vs-reversible fork and the `S7`
  external-keystore providers. Touch points:
  `src/NCode.Identity.Secrets.Persistence/DefaultSecretSerializer.cs`,
  `src/NCode.Identity.Secrets.Persistence.Abstractions/DataContracts/PersistedSecret.cs`. Capture the design in an ADR.

- **`S12` — Tailor each tenant's provisioned keys to the algorithms it can sign with.** Every tenant is provisioned with a
  persisted key for each algorithm `OpenId:Tenant:KeyProvisioning:Keys` configures — `RS256`, `PS256`, and `ES256` by
  default ([ADR-0169](docs/adr/0169-every-tenant-is-provisioned-with-persisted-keys.md)). The set is server-wide, so a
  tenant whose effective signing lists exclude an algorithm still holds and publishes a key for it: a `fapi2` tenant
  publishes an RS256 key it never signs with. Conversely, a client that registers an algorithm outside the set, such as
  `ES384`, fails at issuance until a key is imported. Provision per tenant from what its effective
  `*_signing_alg_values_supported` lists and security profile permit, intersected with the configured set. That needs the
  tenant's merged settings while it is seeded, which today materialize only after the seed. Decide whether keys a tenant
  no longer permits are retired, which is `S8`'s lifecycle. Touch points:
  `src/NCode.Identity.OpenId.Core/Tenants/Keys/` (`TenantKeySeedHandler`, `DefaultTenantKeyPolicy`),
  `DefaultOpenIdTenantFactory` (the settings it materializes), `ISecurityProfile`.

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
