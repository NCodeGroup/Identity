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
| `F` — Feature gaps                    | `F85`     | `F84`      |
| `T` — Conformance & interop testing   | `T10`     | `T9`       |
| `U` — Frontend presentation platform  | `U27`     | `U26`      |
| `S` — Secrets management              | `S13`     | `S12`      |
| `X` — Tooling & agent customization   | `X6`      | `X5`       |
| `R` — Review findings                 | `R96`     | `R95`      |

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
  nothing enforces a key's expiry, so they are stamped a century out — and rollover is what gives them one; retire a
  provisioned key its tenant's settings no longer use, which provisioning keeps (ADR-0169 decision 9); record an
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

## Tooling & agent customization (group `X`)

_No pending items. Completed `X` items are archived in [`BACKLOG-COMPLETED.md`](BACKLOG-COMPLETED.md)._

## Review findings for the feature-gaps branch (group `R`)

A principal-level review of the `copilot/work-through-feature-gaps-frontend` branch against `main` (93 commits, 670
files) recorded these items. Severity follows the review: **P1** blocks merging the branch, **P2** should follow soon,
**P3** is hardening, and **Refactor** removes duplicated or convention-based code. Items marked _pre_ already exist on
`main`; the branch makes them matter. The pull request tracks them as a checklist; when an item is done, move it to
[`BACKLOG-COMPLETED.md`](BACKLOG-COMPLETED.md) as usual.

### P1 — before the branch merges

_None remaining._

### P2

- **`R14` — Close the gaps in `ExpandOnlyRule`.** It ignores raw SQL, allows nullable→required with a default (a test
  asserts it), and misses new unique indexes, foreign keys, and check constraints on existing tables. Touch points:
  `ExpandOnlyRule.cs:30, 98`, `ExpandOnlyRuleTests.cs:195, 326`.
- **`R15` — Make `PromoteMtlsEndpointAliasAuthority` safe for the previous release and rollback.** Its SQL removes the
  settings key the previous release reads, and `Down()` does not restore it. Copy now and remove in a later contract
  step. Touch point: `20261021021124_PromoteMtlsEndpointAliasAuthority.cs:41-77`.
- **`R20` — Mount the login bundle without waiting for branding.** The mount waits on a fetch with no timeout, then
  replaces the server-rendered form and erases typed or autofilled credentials. Touch point:
  `src/frontend/apps/auth/src/main.tsx:12-21`.
- **`R89` — End a deleted or disabled account's sessions and grants.** Deleting a local account removes the federated
  principal with its last identity, and the principal's role assignments with it (ADR-0212), but the subject's
  sessions, refresh tokens and consents remain, and disabling an account keeps them as well.
  `DefaultValidateSubjectAuthenticationHandler` does not check that a subject still exists or is enabled, so a refresh
  token of a deleted or disabled account is redeemed until it expires. Terminate the subject's sessions through
  `ISessionService.RevokeAllAsync` (back-channel logout included), revoke its grants and consents when its account is
  deleted or disabled, and refuse a token request for a subject that is gone or disabled. Touch points:
  `LocalAccountApiEndpointHandler` (`DeleteLocalAccountAsync`, `SetEnabledAsync`), `ISessionService`,
  `ValidateSubjectAuthenticationCommand`.
- **`R92` — Validate the subject at the device-code, CIBA and token-exchange grants.** These grants issue tokens for
  the subject that their approval or `subject_token` names without sending `ValidateSubjectAuthenticationCommand`, so a
  disabled local account is not refused there (ADR-0224). The device verification endpoint approves with an
  authentication cookie whose server-side session it does not validate. Validate the subject at each grant and the
  session at the endpoint. Touch points: `DefaultDeviceCodeGrantHandler`, `DefaultCibaGrantHandler`,
  `DefaultValidateTokenExchangeGrantHandler`, `DefaultDeviceVerificationEndpointHandler`.
- **`R94` — End a local account's sessions when an administrator resets its password.** The reset rotates a security
  stamp that nothing reads, so the sessions and refresh tokens of a compromised account keep working (OWASP ASVS
  V3.3.3). Touch points: `LocalAccountApiEndpointHandler.ResetPasswordAsync`, `SessionTerminationReasons`.

### P3

- **`R30` — Serve interactive bundle assets without resolving the tenant.** Touch point:
  `InteractiveAuthenticationEndpointGroup.cs:55`.
- **`R35` — Pin container base images by digest and let Dependabot track them.** Touch points:
  `src/NCode.Identity.OpenId.Host/Dockerfile:17, 28, 60`, `.github/dependabot.yml`.
- **`R36` — Test the bundle-shell split against the built bundle's real `index.html`.** Touch point:
  `InteractiveBundleShell.cs:51-100`.
- **`R48` — Refuse to start while a live Data Protection key cannot be unwrapped.** Split from R7, which documented
  hand rotation of the key-protection certificate. A certificate renewed in place leaves the ring unreadable after a
  restart. A readiness check is the wrong shape: Data Protection's hosted service runs at startup, finds no default
  key it can unwrap, and writes a new key (live for 90 days) wrapped by the wrong certificate. Once the certificate is
  fixed, that key keeps every correctly configured replica unready, because from one replica's view an unreadable key
  written by a misconfigured pod looks the same as an unreadable key the ring depends on. A first attempt was reverted
  for this reason. Instead, run the check as a start gate (`ValidateOnStart`, which runs before hosted services) that
  fails while any live, unrevoked key cannot be unwrapped, so a misconfigured replica stops before it writes a key.
  Also add a supported way to revoke a key by ID (`IKeyManager.RevokeKey`) for recovery, and document both in
  ADR-0177 and the chart README. Touch points: `NCode.Identity.Server` (`ProductionSafetyRegistration` pattern),
  `DataProtection:KeyProtection`, ADR-0177.
- **`R64` — Check client-certificate revocation beyond complete CRLs.** The default checker refuses, as Unknown, an
  OCSP-only PKI, delta, partitioned and indirect CRLs, a CRL signed by a key other than the issuing certificate, and
  RSA-PSS or SHA-1 signatures. It also retries an unreachable distribution point on every request (no negative cache,
  no single-flight). Touch points: `DefaultClientCertificateRevocationChecker`, `CertificateRevocationList`, ADR-0173.
- **`R67` — Run the conformance pre-flight in the FAPI 1.0 Advanced run.** Advertised certificate binding does not
  force every client to present a certificate (`R42`), so `-Fapi1Advanced` has no reason to skip the pre-flight; update
  `build/conformance/README.md` with it. Touch points: `build/conformance/run-conformance.ps1` (`Initialize-Fapi1Run`).
- **`R71` — Report a client value that a `Min` or `Max` ceiling overrides.** `RegisteredValueCoherenceRule` reports a
  listed value an `Intersect` ceiling drops and a `true` an `And` ceiling turns off, but a client's own lifetime above
  its tenant's `Min` ceiling is clamped silently. It needs the merge kind of a descriptor, which `SettingDescriptor`
  exposes only as `HasLatticeMerge`. Touch points: `RegisteredValueCoherenceRule`, `SettingDescriptor`, `SettingMerge`.
- **`R74` — Write one-line JSON console logs in container deployments.** The reference hosts log only to standard
  output (ADR-0210), in the console provider's default `simple` format. That format spreads one entry, and the stack
  trace of its exception, over several lines, so a container log collector splits it into separate records. Select the
  `json` formatter (`Logging__Console__FormatterName`) in the container image or the chart's default environment, and
  keep `simple` for development. Touch points: `src/NCode.Identity.OpenId.Host/Dockerfile`,
  `charts/ncode-identity-host/values.yaml`, ADR-0210.
- **`R85` — Run the in-process dispatcher's jobs with bounded concurrency.** `JobDispatchWorker` runs one job at a
  time, so a delivery that waits on an unreachable endpoint (a CIBA notification or a back-channel logout) delays every
  job queued behind it. Run jobs in parallel up to a limit from `InProcessJobDispatcherOptions`. Touch points:
  `JobDispatchWorker`, `InProcessJobQueue`, `InProcessJobDispatcherOptions`.
- **`R87` — List the incoherent clients in the management API.** The reconciliation of the server's settings logs each
  stored client that the settings make incoherent (ADR-0219), so an operator without the logs cannot find them. Add a
  read-only report per tenant. Touch points: `ClientApiEndpointHandler`, `IClientCoherenceValidator`,
  `ClientCoherenceReportHandler`.
- **`R88` — Reconcile only the tenants and clients a server settings change affects.** The reconciliation reads the
  keys of every tenant that uses a configured algorithm and checks every client on each change, because only a
  fingerprint of the settings it last reconciled is stored. Storing those settings would let it skip a tenant whose
  effective settings did not change in a setting it reads (`TenantKeyUsage`,
  `IClientCoherenceValidator.ReadsChangedSetting`). Touch points: `PersistedServer.ReconciledSettingsFingerprint`,
  `TenantKeyReconciliationHandler`, `ClientCoherenceReportHandler`.
- **`R90` — Terminate an end-user session that ends while no request uses it.** A session's grant expires at the end
  of its idle window or its absolute lifetime (ADR-0220), and a request that finds an ended session terminates it
  through `OnSessionTerminatedCommand`. A session that ends with no request is only pruned: the grants issued under it
  are not revoked, its relying parties receive no back-channel logout (Auth0's `session-expired` initiator), and the
  check-session iframe reports no change. A session that the previous release stored without an expiry during a
  rollout is kept until a request finds it. Terminate ended, unrevoked sessions in a background pass that runs on one
  replica at a time; it needs an `OpenIdContext` per tenant, which `IOpenIdContextFactory.CreateForTenantAsync` builds
  only from an `HttpContext`. Touch points: `DefaultSessionService`, `GrantPruneService`, `OnSessionTerminatedCommand`,
  `IGrantStore`.
- **`R91` — Make the session touch and the addition of a client lose no concurrent write.**
  `DefaultSessionService.ValidateAndTouchAsync` and `AddClientAsync` compute a payload from the grant they read.
  `IPersistedGrantService.UpdatePayloadAsync` and `UpdatePayloadAndExpirationAsync` then read the grant again and
  replace its payload, so the concurrency token only guards the time between that second read and the save. A write that
  lands between the two reads is overwritten: the touch, which the authorize request that resolves a session performs at
  most once a minute (ADR-0220), removes a client that a concurrent authorize request added to the session, and two
  concurrent additions lose one. A client that is missing from the session receives no back-channel logout when the
  session is terminated. Apply the change to the payload that the service read (a read-modify-write inside the service,
  retried on `StoreConcurrencyException`), or pass the concurrency token that the caller read. Touch points:
  `DefaultSessionService`, `IPersistedGrantService`, `DefaultPersistedGrantService`.
- **`R93` — Let an administrator block any end user.** Only a local account has a status, so a principal whose
  connections are all external cannot be blocked (ADR-0224); Auth0 blocks a user whatever its connections. Touch
  points: `PersistedFederatedPrincipal`, `DefaultValidateLocalAccountSubjectHandler`, the management API.
- **`R95` — Terminate a session at most once on every termination path.** `RevokeAsync`, `RevokeAllExceptAsync` and the
  concurrent-session eviction dispatch `OnSessionTerminatedCommand` without the claim of ADR-0223, so a session that two
  paths terminate at once notifies its relying parties twice. Touch point: `DefaultSessionService`.

### Refactor

- **`R41` — Rate-limit policy as data.** One descriptor table for partitions, quotas, and meters, a batched Valkey
  round trip, and one metrics wrapper in the Valkey adapters.
- **`R44` — Key the conformance seed by `client_id`.** Stop passing keys by array position
  (`run-conformance.ps1:318-321`), generate both FAPI templates from one source, and derive `discoveryUrl` from
  `$IssuerUrl`.
- **`R45` — Deduplicate the frontend data and routing code.** An owner-generic builder for `managementApi.ts`'s
  secrets and settings endpoints, one `fetchSurfaceJson` helper for the 401 redirect, and tenant detail tabs in the URL.
