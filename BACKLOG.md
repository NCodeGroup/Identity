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
| `D` — Deferred, intentionally not-yet | `D19`     | `D18`      |
| `F` — Feature gaps                    | `F85`     | `F84`      |
| `T` — Conformance & interop testing   | `T10`     | `T9`       |
| `U` — Frontend presentation platform  | `U27`     | `U26`      |
| `S` — Secrets management              | `S21`     | `S20`      |
| `X` — Tooling & agent customization   | `X12`     | `X11`      |
| `R` — Review findings                 | `R114`    | `R113`     |

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
- **`D16` — Resume Dependabot version updates.** Version updates are paused: every ecosystem in
  [`.github/dependabot.yml`](.github/dependabot.yml) except `docker` sets `open-pull-requests-limit: 0`, because
  while the family is refactored heavily ahead of its first release a weekly bump is mostly churn against code that is
  about to move. The `docker` ecosystem stays on, because the host image's base images are pinned by digest and a
  stale digest misses security fixes ([ADR-0249](docs/adr/0249-the-host-images-base-images-are-pinned-by-digest-and-tracked-by-dependabot.md)).
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
- **`D17` — Remove the retired `mtls_endpoint_alias_authority` setting.** `PromoteMtlsEndpointAliasAuthority` copies
  each tenant's alias into its `MtlsEndpointAliasAuthority` column and leaves the setting in the stored document for the
  previous release, and the setting stays registered as retired
  ([ADR-0251](docs/adr/0251-a-retired-setting-stays-registered-until-a-later-release-removes-it.md)). In the first
  release after the one that ships that migration, add a `[ContractMigration]` that removes the key from every settings
  table and gives each rewritten row a new `SettingsConcurrencyToken`, and delete the retired descriptor. Not before:
  the previous release still reads the key. Touch points: `NCode.Identity.OpenId.Host.Database` migrations,
  `DefaultSettingDescriptorDataSource`.
- **`D18` — Let configuration order and select the client-certificate revocation sources.** The default checker asks
  every registered `IClientCertificateRevocationSource` in registration order, `ocsp` then `crl`
  ([ADR-0259](docs/adr/0259-revocation-is-checked-by-registered-sources-ocsp-before-every-crl-form.md)), so a
  deployment whose OCSP responder the server cannot reach pays one `FetchTimeout` per responder in each
  `FailureCacheDuration` before the CRLs answer, and skips OCSP only by replacing the checker. A configured order
  (`OpenId:ClientCertificateRevocation:Sources`) must be validated when the host starts without constructing the
  sources, which read the options it would validate, so it needs registered source descriptors and a catalog, as
  client-authentication methods have
  ([ADR-0207](docs/adr/0207-client-authentication-methods-are-named-by-registered-descriptors.md)). Deferred until a
  deployment needs it. Touch points: `DefaultClientCertificateRevocationChecker`, `ClientCertificateRevocationOptions`,
  `AddClientCertificateServices`.

## Feature gaps — not yet implemented (group `F`)

- **`F5` — First-class claim-shaping pipeline (Auth0-Actions-equivalent).** Custom claim shaping is already possible
  today by registering an `ICommandHandler` on the subject-claims / UserInfo commands (the mediator pipeline is the
  extension seam, per [ADR-0054](docs/adr/0054-local-account-metadata-projection.md)), but there is no first-class
  surface: a strongly-typed shaping context (event/api), per-key namespaced flattening helpers, or ordering/management.
  Layer these on the existing pipeline additively. (Subject metadata already resolves fresh per issuance by default, per
  [ADR-0055](docs/adr/0055-subject-metadata-is-a-principal-level-concept-resolved-at-issuance.md).)
  Touch points: `src/NCode.Identity.OpenId.Authentication/Tokens/Handlers/`,
  `src/NCode.Identity.OpenId.Authentication/Endpoints/UserInfo/Handlers/`.
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
- **`F84` — Backend for Frontend (BFF) library: cookie sessions with server-side token custody.** The family ships no
  relying-party library, and a browser frontend can use the OP only as a public client that holds its tokens in
  script-reachable memory — the shape [RFC 10017](https://www.rfc-editor.org/rfc/rfc10017) (BCP 212) discourages. Build
  the BFF designed in [`docs/backend-for-frontend.md`](docs/backend-for-frontend.md): a confidential client
  (`private_key_jwt`, PKCE `S256`, PAR, RFC 9207, DPoP) that keeps tokens in a server-side session encrypted under a
  key derived from an opaque `__Host-Http-` cookie handle, accepts that cookie on local and allow-listed remote APIs
  behind a static anti-forgery header, CORS, and Fetch Metadata, refreshes single-flight across replicas so rotation
  never trips reuse detection ([ADR-0087](docs/adr/0087-refresh-token-reuse-detection-and-family-revocation.md)), and
  ends sessions through RP-initiated and back-channel logout. Deliver it in the design's phases — SharedState versioned
  expiring values and membership indexes, the `NCode.Identity.OpenId.Client` protocol client, the `NCode.Identity.Bff`
  core, remote APIs and the YARP package, then multiple frontends — and promote each phase's decisions to ADRs. The
  design's open questions (a `bff` client profile at the OP, refresh-token lifetime discovery) are decided along the
  way. Touch points: new `src/NCode.Identity.OpenId.Client*` and `src/NCode.Identity.Bff*` projects,
  `src/NCode.Identity.SharedState.Abstractions/`, `src/NCode.Identity.SharedState.Valkey/`.

## Conformance & interop testing (group `T`)

- **`T1` (subsumes `T6`) — Independent OIDC/OAuth conformance validation via the OpenID Foundation Conformance Suite,
  delivered through the in-house spec-coverage harness (remaining: nightly CI, the FAPI 2.0 plan, and the FAPI 1.0 Advanced plan's first end-to-end run).** An independent,
  spec-author-maintained oracle to catch misreadings our own integration tests share, driven by an **in-house
  measurement harness** (the
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
  lands (no `.github/workflows` exists yet); and the **FAPI 2.0 plan** (moved from `F16`, whose OP side is complete):
  seed the two `fapi2` / `private_key_jwt` clients the plan needs (`ConformanceClientSeed` already carries `Jwks` and
  `SecurityProfiles`, and `-Fapi1Advanced` shows how to generate their keys), add a FAPI 2.0 plan config to
  `run-conformance.ps1` (the `private_key_jwt` + DPoP variant avoids passing client certificates through Docker),
  and run it end to end and commit its baseline. The `coverage.ts` `profile-fapi2` row already classifies from discovery and
  needs no change. Also the **FAPI 1.0 Advanced plan's first end-to-end run** (moved from `F83`, whose OP side is
  complete, [ADR-0202](docs/adr/0202-fapi-1-0-advanced-is-a-second-registered-security-profile.md)). The harness is
  built: `run-conformance.ps1 -Fapi1Advanced` seeds the four `fapi1-*` key-authenticated clients from keys and
  self-signed certificates it generates into a gitignored folder, launches the OP requesting client certificates, and
  runs `fapi1-advanced-final-test-plan` with `private_key_jwt` and `mtls` client authentication, the `pushed` request
  method, and the `jarm` and `plain_response` response modes. It has not yet been run against the suite, because the
  cloud-agent sandbox that built it cannot reach `gitlab.com`. Remaining: run it on a machine with Docker, triage the
  verdicts (the suite's container must reach the OP with client certificates), and commit the plan's entries in the
  `expected-failures.json` / `expected-skips.json`
  baseline. The `profile-fapi1-advanced` coverage row already classifies from discovery. Design and phases are in
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
- **`U26` — Serve the management console through the BFF instead of as a browser public client.** The console runs
  authorization code + PKCE in JavaScript through `@ncode/auth`, keeps the access and refresh tokens in the Redux store,
  and `@ncode/store` attaches `Authorization: Bearer` to every management API call — so the family's most privileged
  surface holds tokens any injected script can take. Once `F84` lands, register the console as a confidential BFF
  client of the root tenant, serve it through the BFF so it calls the management API with the session cookie and the
  anti-forgery header, and retire the console's in-browser token custody (the Playground stays a public client on
  purpose — showing tokens is its job). Decide whether the BFF runs in the OP host process or a separate host
  ([`docs/backend-for-frontend.md`](docs/backend-for-frontend.md) §11). Planning found that the console cannot keep its
  standalone Vite dev server: proxying `/bff` and the API through it hides the browser's origin from the BFF unless the
  host trusts loopback as a reverse proxy, and the forwarded `http` scheme then trips the host's HTTPS redirection.
  Serving the console from the OP host under `/mgmt/`, as `apps/auth` is served under `/account/`
  ([ADR-0065](docs/adr/0065-interactive-login-consent-op-hosted-auth-surface.md)), keeps the console, `/bff/*`, and the
  forwarded API on one origin. Touch points:
  [`apps/mgmt/src/auth.ts`](src/frontend/apps/mgmt/src/auth.ts), `apps/mgmt/src/AuthGate.tsx`,
  `packages/store/src/baseQuery.ts`, `packages/auth/`, `src/NCode.Identity.OpenId.Host/Program.cs`.

## Secrets management (group `S`)

Follow-ups surfaced while implementing [ADR-0050](docs/adr/0050-secret-material-revealed-once-on-create.md)
(client-secret material revealed once on create).

- **`S5` — Hash client secrets that only need verifying (needs its own ADR).** Client secrets are envelope-encrypted
  under the tenant master key (ADR-0235) but still reversible. A secret used only for `client_secret_basic` /
  `client_secret_post` should be stored as a verifier: SHA-256 for server-generated secrets (≥ 256 bits of entropy),
  PBKDF2 (the `DefaultPasswordHasher` format) for operator-supplied ones. Keep a secret reversible only when its client
  is registered for `client_secret_jwt`, decided when the secret is issued; switching a client to `client_secret_jwt`
  then needs a new secret. Touch points: `DefaultSecretGenerator`, `CommonClientAuthenticationHandler`,
  `ClientSecretJwtClientAuthenticationHandler`, the client management endpoints.
  - Verification uses `CryptographicOperations.FixedTimeEquals`; the verifier format is self-describing
    (`$ncode-sha256$…` / the existing PBKDF2 format) so it can evolve.

- **`S7` — Remote (KMS-resident) signing keys.** Let a tenant opt in to signing keys that never leave a key management
  service: a public-material `SecretKey` that references the remote key, an asynchronous signing path in the JOSE
  serializer (the current `TrySign` seam is synchronous), algorithm mapping (PS256 → RSA-PSS SHA-256 with a 32-byte
  salt; ES256 → ECDSA P-256 SHA-256, converting DER signatures to the 64-byte `r‖s` form JWS requires), cached public
  keys for JWKS, retry with back-off on throttling. Providers reuse the root key provider packages' clients (ADR-0235).
  Touch points: `src/NCode.Identity.Secrets.Abstractions/Keys`, `src/NCode.Identity.Jose/Algorithms`.
  - `RemoteAsymmetricSecretKey : AsymmetricSecretKey` holds the public key and a provider reference; an
    `IRemoteSigner.SignAsync(keyReference, algorithm, signingInput, ct)` seam; the JOSE serializer's encode path awaits
    the signer before assembling the JWS (the existing synchronous `TrySign` remains for local keys).
  - Algorithm mapping: PS256 → Cloud KMS `RSA_SIGN_PSS_2048_SHA256` / AWS `RSASSA_PSS_SHA_256` / Azure `PS256` (salt =
    32 bytes); ES256 → Cloud KMS `EC_SIGN_P256_SHA256` / AWS `ECDSA_SHA_256` (both return DER — convert to the 64-byte
    IEEE P1363 `r‖s`) / Azure `ES256` (already `r‖s`). Sign a SHA-256 digest where the service accepts one; Infisical's
    PSS needs the full message.
  - Public keys cached for JWKS; per-tenant opt-in setting; throttling handled with jittered back-off; key rollover
    (`S8`) creates new key versions.

- **`S8` — Key lifecycle / rotation primitives.** First-class signing-key rollover: overlapping validity windows,
  activation/retirement grace periods, automatic rotation, and a published JWKS that reflects active **and** retiring
  keys (so verifiers accept both across a rotation) — analogous to the ASP.NET Core Data Protection key ring. Builds on
  the delivered `F33` JWK/JWKS types and the `S1` rotate endpoint; once expiry is enforced, let that endpoint keep
  the rotated client secret for a grace period
  ([ADR-0183](docs/adr/0183-a-client-secret-is-rotated-by-replacing-it-in-one-change.md)). The keys every tenant is
  provisioned with ([ADR-0169](docs/adr/0169-every-tenant-is-provisioned-with-persisted-keys.md)) carry no effective
  lifetime today —
  nothing enforces a key's expiry, so they are stamped a century out — and rollover is what gives them one; retire a
  provisioned key its tenant's settings no longer use, which provisioning keeps (ADR-0169 decision 9); record an
  audit event when the server generates or retires a key, since provisioning only logs it. Touch points:
  [`src/NCode.Identity.Secrets`](src/NCode.Identity.Secrets),
  `src/NCode.Identity.Secrets.Persistence`, `src/NCode.Identity.OpenId.Core/Tenants/Keys/`. Capture the design in an
  ADR.

- **`S9` — Envelope encryption / hardware-backed protection at rest.** Implemented, awaiting a green delivery:
  stored secrets and the Data Protection key ring are encrypted under per-owner master keys wrapped by an external
  root key (`ephemeral`, `local`, `aws-kms`) —
  [ADR-0235](docs/adr/0235-stored-secrets-are-protected-by-a-key-hierarchy-under-an-external-root-key.md),
  [`CHANGELOG.md`](CHANGELOG.md) [Unreleased]. Remaining: every GitHub Actions run for the commit that delivers it
  to `main` succeeds; then archive this entry. Provisioning the production root key is `S19`.

- **`S13` — Master key rotation, rekey and status.** Rotate a tenant's or the server's master key (new version active,
  old retired), broadcast the rotation, rewrap every data key of the owner in a background job, then destroy the
  retired key. Management API: `GET` and `POST …/encryption-keys/rekey` for tenants and the server (Auth0 parity),
  scopes `read:encryption_keys` / `update:encryption_keys`, audit events; yearly automatic rotation.
  - Seam: `IMasterKeyManager` (public, Abstractions) with `GetStatusAsync(SecretOwner, ct)` (versions, created, retired,
    provider, recovery present — never material) and `RotateAsync(SecretOwner, ct)`.
  - Rotate: in one unit of work create version n+1 (active) and set `RetiredWhen` on the old key; broadcast
    `MasterKeyRotated(owner)` through `IReplicaBroadcaster`; every replica calls `Evict(owner)` and, for the server
    owner, refreshes `ServerSecretProtectorHolder`.
  - Rewrap job (`NCode.Identity.Dispatch`, delayed about five minutes so replicas converge): for each of the owner's
    secrets whose envelope names the retired key, unwrap the data key with the old purpose key and wrap it with the new
    one; the payload is untouched because payload associated data carries an all-zero key id (ADR-0235). Owner's
    secrets: a tenant's `TenantSecrets`, its clients' and resource servers' secrets; the server's `ServerSecrets`. Write
    through the owner stores' `UpdateSecretAsync` with concurrency tokens; re-run on conflict.
  - Data Protection: existing ring keys are not re-encrypted; new ring keys use the new master key, and the retired
    server key is kept until every ring key it wrapped has expired (Data Protection default lifetime 90 days) plus a
    grace period.
  - Destroy: when a verification scan finds no reference, delete the retired row. Backups keep their own copies.
  - Management API (Auth0 parity with `POST /api/v2/keys/encryption/rekey`):
    `GET /api/tenants/{tenantId}/encryption-keys`, `POST /api/tenants/{tenantId}/encryption-keys/rekey`, and
    `/api/servers/{serverId}/…` equivalents; scopes `read:encryption_keys` / `update:encryption_keys`; audit events
    `master_key_rotated`, `master_key_destroyed`.
  - Automatic rotation: a hosted timer dispatches a daily job that rotates any active key older than
    `SecretProtection:MasterKeyRotationPeriod` (default 365 days).

- **`S14` — Rewrap and recovery tool.** A host command (run as a Kubernetes Job) that rewraps every master key under
  the configured root key provider — unwrapping with the provider recorded per row, or with the offline recovery
  private key — and can rewrap the recovery copies under a new recovery key.
  - A host verb, run once as a Kubernetes Job from the host image:
    `secret-protection rewrap [--recovery-private-key <pem>] [--owner <type:id>] [--dry-run]`.
  - For every `MasterKeys` row: unwrap with the provider named in the row, or with RSA-OAEP-SHA256 and the recovery
    private key when given; verify the key check; wrap with the configured provider; recompute the recovery wrap under
    the configured recovery public key; update with the concurrency token. Idempotent; logs counts only.
  - `secret-protection rewrap-recovery` rewraps only the recovery copies (new recovery key pair).
  - The recovery private key is read from a file mounted for the Job only, never from configuration.

- **`S15` — More root key providers.** Google Cloud KMS (`google-cloud-kms`) and Azure Key Vault (`azure-key-vault`)
  are implemented ([ADR-0260](docs/adr/0260-azure-key-vault-and-google-cloud-kms-are-root-key-providers.md)).
  Remaining: Infisical KMS, OpenBao/Vault Transit and PKCS#11 packages, each an `IRootKeyProvider` with its own
  options section.
  - Infisical KMS, OpenBao/Vault Transit and PKCS#11: Infisical: REST encrypt/decrypt with an `aes-256-gcm` KMS key;
    machine identity through Universal Auth (stored credential) or Kubernetes Auth (needs API-server reachability).
    OpenBao Transit: `encrypt`/`decrypt` with `context` on a derived key, Kubernetes auth in-cluster. PKCS#11:
    `CKM_AES_GCM` through Pkcs11Interop for on-premises HSMs.

- **`S16` — Per-tenant root keys (bring your own key).** A tenant's master key wrapped by a key the tenant controls, so
  revoking or destroying it shreds the tenant's data immediately, backups included.
  - A tenant setting names a root key (provider + key reference) that the provider validates. The tenant's master key
    is wrapped by that key instead of the deployment root, and gets no operator recovery wrap. Revoking or destroying
    the tenant's key makes its data unreadable everywhere, backups included. The ring re-validates such keys
    periodically (for example hourly) so a revocation takes effect within that period.

- **`S17` — Encrypt grant payloads.** `GrantEntity.PayloadJson` (authorization codes, refresh tokens, device and PAR
  requests) is plaintext; encrypt it under the tenant master key (purpose `grant-payload`).
  - `DefaultPersistedGrantService` protects `PayloadJson` with the tenant protector, purpose `grant-payload`, item id =
    the grant's hashed key; reads unprotect. AES-GCM costs microseconds per grant. Clean cutover (pre-release).

- **`S18` — Store the public half of private keys.** Let listing endpoints and registration responses read public keys
  without unprotecting private ones.
  - Store the SPKI of every generated asymmetric key in a new `PersistedSecret.PublicKey` (base64url) so the management
    converters and registration responses export public keys without a protector; then drop the protector plumbing from
    those paths.

- **`S19` — Provision the AWS KMS root key and the cluster's access to it.** In `identity-platform`: the root key (one
  more `aws-kms-key` module instance), and — if the cluster's service-account issuer is publicly discoverable — an IAM
  OIDC identity provider plus a role only the host's service account can assume, allowed `kms:Encrypt` / `kms:Decrypt`
  on that key with the `ncode-identity:root-context` encryption context. If the issuer is not discoverable (an
  in-cluster name), IAM Roles Anywhere instead: a trust anchor for a cluster CA, a profile and role, a short-lived
  workload certificate from cert-manager, and the chart serving it to the SDK (for example the
  `aws_signing_helper serve` sidecar with `AWS_EC2_METADATA_SERVICE_ENDPOINT`).
  - OpenTofu: the root key as one more `aws-kms-key` module instance (alias `ncode-identity-root`, role
    `secret-protection`, `deletion_protection = true`, 30-day deletion window, yearly rotation) in the platform's
    account.
  - **Publicly discoverable issuer:** `aws_iam_openid_connect_provider` for the cluster's issuer with client id
    `sts.amazonaws.com`; `aws_iam_role` whose trust policy allows `sts:AssumeRoleWithWebIdentity` only for
    `system:serviceaccount:<namespace>:<service account>` and audience `sts.amazonaws.com`; an inline policy allowing
    `kms:Encrypt` / `kms:Decrypt` on the key ARN only, conditioned on `kms:EncryptionContextKeys` =
    `ncode-identity:root-context`. Outputs: the alias ARN and role ARN for the chart values.
  - **Private issuer (an in-cluster name):** IAM Roles Anywhere — `aws_rolesanywhere_trust_anchor` for a cluster CA
    (cert-manager `CA` issuer whose key stays in the cluster), `aws_rolesanywhere_profile` and a role trusting
    `rolesanywhere.amazonaws.com` conditioned on the certificate subject; cert-manager issues a short-lived workload
    certificate; the chart gains an optional `aws_signing_helper serve` sidecar and sets
    `AWS_EC2_METADATA_SERVICE_ENDPOINT` so the SDK's default chain reads its credentials. No code change in the
    provider.
  - Infrastructure bootstrap secrets (database and Valkey passwords, DNS tokens) generated with `random_password` and
    written to Kubernetes Secrets; OpenTofu state is already encrypted with AWS KMS.

- **`S20` — Post-quantum recovery wrap.** Add a hybrid ML-KEM-768 + X25519 recovery wrap once the container images'
  OpenSSL supports ML-KEM.
  - Add recovery wrap format `r2`: hybrid ML-KEM-768 + X25519 key encapsulation feeding HKDF, then AES-256-GCM over the
    master key. Requires `System.Security.Cryptography.MLKem` support in the container images' OpenSSL (3.5+). Keep
    RSA-OAEP until then; the master layer, and the `local`, `aws-kms` and `google-cloud-kms` roots, are symmetric and
    already quantum-resistant; an `azure-key-vault` root is RSA and is not (ADR-0260), so a post-quantum root on Azure
    needs a Managed HSM AES key.

## Tooling & agent customization (group `X`)

- **`X6` — Publish the NuGet packages from a release tag.** [`GOVERNANCE.md`](GOVERNANCE.md) "Publishing" says
  packages and their `.snupkg` symbols are published to nuget.org from protected refs only, signed and RFC-3161
  timestamped, but no workflow publishes anything today. Add a workflow that a `v*` tag on a release line triggers: it
  runs the full Definition of Done, signs and timestamps the packages, verifies them with `dotnet nuget verify`, and
  pushes the packages and symbols to nuget.org through a protected environment and a trusted publisher, with no
  long-lived API key. Touch points: `.github/workflows/`, `build/dod.ps1`, `GOVERNANCE.md` "Publishing".

- **`X7` — Automate backports.** Servicing is upstream-first
  ([ADR-0263](docs/adr/0263-feature-branches-run-a-fast-gate-and-the-merge-queue-runs-the-full-gate.md)): a fix
  merges to `main`, and a backport pull request cherry-picks it with `git cherry-pick -x` onto
  `backport/<pr>-vX.Y`, cut from `release/vX.Y`. Today that is done by hand. Add a workflow that, when a pull request
  labeled `backport vX.Y` merges to `main`, opens the cherry-pick pull request against `release/vX.Y`, and comments on
  the original pull request when the cherry-pick conflicts. Touch points: `.github/workflows/`, `GOVERNANCE.md`
  "Releases, branches & tags", ADR-0263.

- **`X8` — Re-enable the Request Delegate Generator for the Interactive package.**
  `NCode.Identity.OpenId.Authentication.Interactive` binds its endpoints at runtime, because in `dotnet/aspnetcore`
  `release/10.0` the generated form binding (`TryResolveFormAsync`) reads a form without checking
  `IAntiforgeryValidationFeature`, so a POST whose antiforgery validation failed answers 500 instead of 400
  ([ADR-0262](docs/adr/0262-minimal-api-request-delegates-are-generated-at-compile-time.md)). Once upstream's
  generated form binding checks the feature, remove the opt-out and confirm that
  `LocalAccountLifecycleTests.ChangePassword_WithoutTheAntiforgeryToken_ChangesNothing` still passes. Touch points:
  `src/NCode.Identity.OpenId.Authentication.Interactive/NCode.Identity.OpenId.Authentication.Interactive.csproj`,
  `tests/NCode.Identity.OpenId.IntegrationTests/Endpoints/LocalAccountLifecycleTests.cs`, ADR-0262,
  `.github/instructions/csharp-production.instructions.md`.

- **`X11` — Adopt the project template in identity-platform.** This repository adopted project-template `v0.3.0`
  (backlog `X10`). identity-platform still generates its agents, prompts and skills with the template's installer, so
  a template change reaches it only by hand, and its executor stays on Auto (the Copilot CLI workaround is in its
  `AGENTS.md`). First change the template: `Adopt-Project.ps1` and `.template/Update-Project.ps1` set
  `core.hooksPath .githooks` whenever it is unset, which would silently turn off identity-platform's pre-commit hooks
  in `.git/hooks`. Then adopt it with `Adopt-Project.ps1` (`workflow_tools` `claude, copilot`) and run the
  `/plan-migration` request it prints. identity-platform gets the template's `ci.yml` with the `Commit attribution`
  job, the hook, and Claude Code's and VS Code's attribution settings, which closes its gap: today only a Copilot-only
  hook run by pre-commit keeps agents out of its commits. Require `Commit attribution` in its `default-branch` ruleset
  afterwards. Touch points: project-template's `Adopt-Project.ps1` and `template/.template/Update-Project.ps1`; in
  identity-platform `.github/`, `.githooks/`, `.pre-commit-config.yaml`, `.claude/settings.json`,
  `.vscode/settings.json` and its `default-branch` ruleset.

  This repository's adoption (pull request #15) and its updates found more template gaps; fix them in the same
  template change:
  - **The adoption breaks an allow-list Docker build context.** `Directory.Build.props` now imports
    `build/ProjectTemplate.props`, and this repository's `.dockerignore` re-admits only named root build files, so the
    host image failed to restore (`MSB4019`) until `!build/ProjectTemplate.props` was added. The Definition of Done
    does not build the image, so only the `Host image` check caught it. The `/plan-migration` request that
    `Adopt-Project.ps1` prints should name every allow-list build context (`.dockerignore`, a CI cache key) as a place
    to re-admit the new file. Check identity-platform's Dockerfiles for the same gap when adopting.
  - **The Claude Code executor cannot run its Done-when check.** In a session that works in a git worktree, Claude
    Code refuses to let the `plan-executor` subagent run `pwsh`, because it cannot prove the command stays in the
    worktree, so every executor reported a hand-made check instead. The coordinator's run of each Done-when is what
    verified the steps. The template's `plan-executor` agent and `execute-step` command should say that the
    coordinator runs the check when the executor cannot, rather than asking the executor to run it.
  - **`Update-Project.ps1` never hands the update over to the newer script.** It clones the template with
    `--filter=blob:none`, and in that clone `git checkout <tag> -- template/.template/Update-Project.ps1` fails
    ("unable to read sha1 file") on Git 2.56.0.windows.3, while `git show <tag>:<path>` fetches the blob. The script
    reads the failure as "no newer script" and runs the project's own copy, so every update runs the previous
    version's update logic. Updating this repository to `v0.4.0` hit it: the `v0.3.0` copy ran, without the
    `WinError 206` fix, and failed; running project-template's root `Update-Project.ps1 -Path <repository>` from a
    `v0.4.0` clone worked. `v0.7.0` still has the bug: its script matches `v0.4.0`'s, so the update to `v0.7.0`
    needed no hand-over, but the next version that changes the script will run the old copy again. Read the target
    script with `git show`, and give `Test-Template.ps1` an update from a `file://` clone, so the blob filter applies
    the way it does for a hosted template.
  - **The update's output is rough in places.** `Update-Project.ps1` commits on whatever branch is checked out, so on
    `main`, which accepts only pull requests, it leaves a local commit to undo: warn or refuse on the default branch.
    Copier prints "Make sure Git >= 2.24 is installed" on Git 2.56.0.windows.3, apparently misreading the `.windows.N`
    suffix: explain or filter it. The "project-owned files changed" listing shows the raw Jinja source
    (`{% if 'claude' in workflow_tools %}`) rather than the rendered text. The rendered `CLAUDE.md` has no final
    newline.

  More touch points: project-template's `Adopt-Project.ps1` migration request,
  `template/.claude/agents/plan-executor.md`, and the `execute-step` and `orchestrate-plan` sources.

## Review findings for the feature-gaps branch (group `R`)

A principal-level review of the `copilot/work-through-feature-gaps-frontend` branch against `main` (93 commits, 670
files) recorded these items. Severity follows the review: **P1** blocks merging the branch, **P2** should follow soon,
**P3** is hardening, and **Refactor** removes duplicated or convention-based code. Items marked _pre_ already exist on
`main`; the branch makes them matter. The pull request tracks them as a checklist; when an item is done, move it to
[`BACKLOG-COMPLETED.md`](BACKLOG-COMPLETED.md) as usual.

### P1 — before the branch merges

_None remaining._

### P2

_None remaining._

### P3

- **`R67` — Run the conformance pre-flight in the FAPI 1.0 Advanced run.** Advertised certificate binding does not
  force every client to present a certificate (`R42`), so `-Fapi1Advanced` has no reason to skip the pre-flight; update
  `build/conformance/README.md` with it. Touch points: `build/conformance/run-conformance.ps1` (`Initialize-Fapi1Run`).
### Refactor

- **`R44` — Key the conformance seed by `client_id`.** Stop passing keys by array position
  (`run-conformance.ps1:318-321`), generate both FAPI templates from one source, and derive `discoveryUrl` from
  `$IssuerUrl`.
