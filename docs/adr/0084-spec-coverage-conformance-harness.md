# 84. Spec-coverage conformance harness (full protocol catalog + browser/probe-runner boundary)

- **Status:** Accepted
- **Date:** 2026-10-12
- **Deciders:** NCode Group

## Context

The conformance surface (`apps/conformance`, [ADR-0082](0082-playground-and-conformance-surfaces.md)) audits the
provider's published discovery metadata against modern OAuth/OIDC security guidance and reports Basic/Config **OP
profile readiness**. That audit answers "is what the OP advertises *secure and well-formed*?" — but only over the
subset of the protocol the OP already implements and advertises. It does not answer the operator's other question:
**"of the entire published OAuth/OIDC spec surface, what does this OP support, what is missing, and what is
non-conformant?"** — so a reviewer cannot see, in one place, that (for example) CIBA, DPoP, PAR, token exchange, or the
FAPI profiles are absent and track them as explicit gaps.

The Playground flow explorer (`apps/playground`) is the complementary **happy-path** tool: it walks the one flow the OP
implements and the security-practice default recommends (authorization-code + PKCE). It is deliberately *not* a
coverage tool — it cannot represent features the OP lacks.

The request is a **spec-coverage harness**: measure the OP against the full published spec catalog, independent of what
is implemented, so missing functionality (explicitly CIBA and FAPI) surfaces as first-class gap rows. Two constraints
shape the design:

- **A browser SPA cannot exercise the whole protocol.** The confidential and server-initiated legs are structurally out
  of a browser's reach: `client_credentials`, introspection/revocation *with client authentication*, `private_key_jwt`,
  device-code polling, back-channel logout receipt, DPoP proof signing, mTLS client certificates, and CIBA ping/push
  all require a secret, a client certificate, or a server-to-server call that CORS and the browser secret model forbid.
- **An authoritative oracle already exists.** The OpenID Foundation Conformance Suite ([#124](https://github.com/NCodeGroup/Identity/issues/124),
  [`docs/conformance-testing.md`](../conformance-testing.md)) is the certified "tests all published specs" harness. The
  in-house harness must **complement** it — a fast, always-on, self-hosted gap dashboard — not re-implement a certified
  suite's judgment.

## Decision

**Evolve `apps/conformance` from a discovery *security audit* into a discovery-driven *spec-coverage matrix*, and model
the exercise boundary explicitly as `browser` vs `probe-runner` vs `external-suite` so the harness grows from passive
classification to active probing without re-architecture.** Concretely:

- **A data-first spec catalog (`coverage.ts`, UI-free and unit-tested).** An implementation-agnostic catalog enumerates
  the published protocol surface — endpoints, grant types, client-authentication methods, sender-constraining and
  request-integrity features, OIDC subject/claims/prompt/ACR features, response modes, and the FAPI profile — each
  entry carrying its authoritative `reference` (RFC / OIDC section), the `channel` that would exercise it, and, where
  known, the tracking `issue` number (e.g. [#41](https://github.com/NCodeGroup/Identity/issues/41), [#45](https://github.com/NCodeGroup/Identity/issues/45), [#46](https://github.com/NCodeGroup/Identity/issues/46), [#54](https://github.com/NCodeGroup/Identity/issues/54)). A classifier maps each entry over the published
  discovery document to one of four statuses: **`implemented`** (advertised), **`partial`**, **`missing`** (a published
  feature the OP does not advertise), or **`non-conformant`** (advertised but insecure/deprecated — implicit/hybrid,
  `plain` PKCE, symmetric/`none` signing). The catalog is the single source of truth for "what exists in the specs";
  adding a newly published spec is a catalog row, not a code change.
- **Phase 1 is discovery-driven ("advertised") classification.** Classification derives entirely from the published
  `/.well-known/openid-configuration`, so the matrix is read-only and needs no OAuth client — the same posture that let
  the security audit ship clientless. The matrix column semantics are therefore **advertised**, and the UI states this,
  because a secure-by-default OP ([ADR-0083](0083-secure-by-default-discovery-metadata.md)) may implement but not
  advertise a capability (for example the opt-in `password` grant). Active probes refine *advertised* into *verified* in
  later phases.
- **An explicit exercise boundary, encoded now.** Every catalog entry is tagged `browser`, `probe-runner`, or
  `external-suite`, recording which actor can actually drive it. This makes the browser's limits legible in the matrix
  itself and defines the seam for Phase 3's headless **probe runner** (a Node/backend actor for the confidential and
  server-initiated legs) without reshaping the model.
- **Phased delivery.** (1) catalog + coverage matrix UI (this ADR's shipped slice); (2) browser-drivable active probes
  (PAR construction, response-mode matrix, `prompt`/`max_age`/`acr`/`claims` behavior, negative/error paths);
  (3) a Node/backend probe runner for the confidential and back-channel legs, including CIBA poll/ping once [#41](https://github.com/NCodeGroup/Identity/issues/41) lands;
  (4) wire the external OpenID Foundation Conformance Suite ([#124](https://github.com/NCodeGroup/Identity/issues/124)) as the certified oracle, with this matrix as the fast
  pre-flight. The harness is a measurement surface: it tracks backend gaps ([#41](https://github.com/NCodeGroup/Identity/issues/41) CIBA, [#45](https://github.com/NCodeGroup/Identity/issues/45) DPoP/mTLS, [#46](https://github.com/NCodeGroup/Identity/issues/46) PAR, [#54](https://github.com/NCodeGroup/Identity/issues/54)
  FAPI, token exchange) but does not itself implement them.

  **Status:** Phase 1 (catalog + matrix), Phase 2 (browser-drivable probes), and Phase 3 (the headless probe runner)
  are delivered. The framework-free conformance logic lives in the shared `@ncode/conformance-core` package; the
  browser dashboard is `apps/conformance` and the Phase-3 runner is the `@ncode/app-conformance-runner` CLI, which
  exercises `client_credentials`, introspection/revocation with client auth, device-code polling, CIBA, `private_key_jwt`,
  DPoP/mTLS, and back-channel logout, exiting non-zero on any `failed` probe so it is a usable CI pre-flight. The
  runner's asymmetric legs (`private_key_jwt`, DPoP) are driven by a real Node-`crypto` signer ([#130](https://github.com/NCodeGroup/Identity/issues/130)) that loads a
  PEM/JWK private key (algorithm inferred from the key, overridable) via `--private-key` / `--dpop-key` and builds the
  compact-JWS client assertion (OIDC Core §9) and DPoP proof (RFC 9449 §4). Phase 4 (wiring the external certified suite
  as the oracle behind this pre-flight) is
  now committed as the runnable harness under [`build/conformance/`](../../build/conformance) — `run-conformance.ps1`
  launches the OP, runs the probe-runner pre-flight, brings up the prebuilt-image suite, and drives the Config/Basic OP
  plans gated on committed baselines (see [`docs/conformance/external-suite.md`](../conformance/external-suite.md)); only
  its nightly/opt-in CI pipeline remains, deferred to [#124](https://github.com/NCodeGroup/Identity/issues/124) until the repo's CI bootstrap lands.

## Options considered

- **Extend `apps/playground` to drive every flow.** Rejected: the Playground is the security-practice-default happy
  path and cannot represent absent features; an explorer that emitted implicit/hybrid or ROPC would normalize insecure
  patterns ([ADR-0082](0082-playground-and-conformance-surfaces.md)).
- **One browser SPA that exercises the entire protocol.** Rejected as structurally impossible: confidential-client,
  sender-constraining, and server-initiated legs cannot run in a browser (secrets, client certificates, CORS). The
  `browser` / `probe-runner` boundary is intrinsic, so the model encodes it rather than pretending it away.
- **Re-implement the OpenID Foundation Conformance Suite in-house.** Rejected: duplicating a certified oracle is high
  cost and low trust. The in-house matrix is the fast local pre-flight; the external suite ([#124](https://github.com/NCodeGroup/Identity/issues/124)) remains the
  authoritative certification.
- **Replace the security audit with the coverage matrix.** Rejected: the two answer different questions ("is the
  advertised config secure?" vs "how much of the spec surface exists?"). They are complementary panels on the same
  discovery fetch, so the matrix is added alongside the audit, not in place of it.
- **A hardcoded gap checklist in docs instead of a computed matrix.** Rejected: a static list drifts from the running
  OP. Deriving status from live discovery keeps the matrix honest and makes a newly implemented capability light up
  without a doc edit.

## Consequences

- The conformance surface now answers protocol **coverage** as well as **security posture**: CIBA, DPoP, PAR, token
  exchange, pairwise subjects, JAR/JARM, and the FAPI profile render as explicit `missing` rows linked to their RFC and
  their tracking issue, so missing functionality is visible and attributable at a glance.
- Because every row is classified from **live discovery metadata**, the matrix is also a **regression** detector, not
  only a gap finder: a capability that silently stops being advertised (or an advertised value that flips to
  non-conformant) flips its row, so the harness guards *shipped* features as well as tracking *missing* ones — which is
  why the classifier is pure over discovery and fully unit-tested, so a status change is a signal rather than noise.
- Phase 1 classification is **advertised**, not **verified** — a deliberate, documented limitation. A secure-by-default
  OP may implement but not advertise a capability; the matrix says so, and later-phase active probes close the gap. The
  UI labels the semantics so the distinction is not mistaken for a false negative.
- The `browser` / `probe-runner` / `external-suite` boundary is now a first-class field, establishing the seam for the
  future headless probe runner (Phase 3) and the external-suite wiring (Phase 4, [#124](https://github.com/NCodeGroup/Identity/issues/124)) without a model change.
- The catalog is the durable, versioned home for "the published spec surface we measure against"; extending coverage to
  a newly published spec is a catalog row plus a classifier, keeping the knowledge in the repository
  ([ADR-0008](0008-conventions-and-lessons-live-in-the-repository.md)).
- This complements, and does not replace, the external OpenID Foundation Conformance Suite ([#124](https://github.com/NCodeGroup/Identity/issues/124)); that suite remains
  the authoritative oracle and this matrix the fast, local pre-flight.

## References

- Issues: [#129](https://github.com/NCodeGroup/Identity/issues/129) (the spec-coverage harness epic and its phases), the backend
  gaps it measures — [#41](https://github.com/NCodeGroup/Identity/issues/41) (CIBA), [#45](https://github.com/NCodeGroup/Identity/issues/45) (DPoP/mTLS), [#46](https://github.com/NCodeGroup/Identity/issues/46) (PAR), [#47](https://github.com/NCodeGroup/Identity/issues/47) (pairwise subjects), [#49](https://github.com/NCodeGroup/Identity/issues/49) (`acr`), [#54](https://github.com/NCodeGroup/Identity/issues/54) (FAPI) —
  and group `U` ([#135](https://github.com/NCodeGroup/Identity/issues/135), the surface this extends).
- Surfaces: [ADR-0082](0082-playground-and-conformance-surfaces.md) (the Playground and conformance surfaces this
  builds on), [ADR-0064](0064-frontend-presentation-platform.md) (the presentation platform and its invariants).
- Discovery: [ADR-0083](0083-secure-by-default-discovery-metadata.md) (why the OP advertises a secure-by-default subset,
  which makes Phase 1 semantics "advertised").
- Conformance oracle: [`docs/conformance-testing.md`](../conformance-testing.md) (the external OpenID Foundation
  Conformance Suite this matrix pre-flights).
- Posture: [ADR-0025](0025-pre-release-posture-and-auth0-parity-plus.md) and the security-practice default in
  [`.github/instructions/repository.instructions.md`](../../.github/instructions/repository.instructions.md).
