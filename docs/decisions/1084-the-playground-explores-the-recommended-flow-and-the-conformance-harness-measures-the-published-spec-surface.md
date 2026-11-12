# 1084. The Playground explores the recommended flow and the conformance harness measures the published spec surface

- **Status:** Accepted
- **Date:** 2026-10-12
- **Deciders:** NCode Group

## Context

The frontend platform ([ADR-1064](1064-one-frontend-platform-of-shared-packages-and-thin-app-shells-serves-every-web-surface.md))
serves two developer and operator surfaces beside the management console and the interactive sign-in and consent
surface: `apps/playground` and `apps/conformance`. Each answers a different question.

- **How does the recommended flow work, step by step?** A developer needs to see the artifacts of an OAuth 2.1 /
  OpenID Connect sign-in: the PKCE verifier and challenge, `state`, `nonce`, the authorize URL, the token response and
  the decoded JWT. An explorer that could emit every flow would normalize insecure patterns.
- **Is what the provider advertises secure and well-formed?** Audited over the discovery metadata, against current
  OAuth and OpenID Connect guidance.
- **Of the whole published spec surface, what does the provider support, what is missing and what is
  non-conformant?** The audit sees only the subset the provider already implements and advertises, so a reviewer
  cannot see in one place which published capabilities, such as CIBA, DPoP, PAR, token exchange or the FAPI profiles,
  the provider lacks, and track them as gaps.

Two constraints shape the harness:

- **A browser SPA cannot exercise the whole protocol.** `client_credentials`, introspection and revocation with client
  authentication, `private_key_jwt`, device-code polling, back-channel logout receipt, DPoP proof signing, mTLS client
  certificates and CIBA ping and push need a secret, a client certificate or a server-to-server call that CORS and the
  browser secret model forbid.
- **An authoritative oracle exists.** The OpenID Foundation Conformance Suite
  ([#124](https://github.com/NCodeGroup/identity/issues/124),
  [`docs/conformance-testing.md`](../conformance-testing.md)) is the certified harness. The in-house tools complement
  it: a fast, always-on, self-hosted pre-flight, not a re-implementation of a certified suite's judgment.

The Selenium-driven sign-in and consent surface that the external suite automates is `apps/auth`
([#125](https://github.com/NCodeGroup/identity/issues/125)), so the conformance dashboard does not duplicate it.

## Decision

**The Playground explores only the recommended authorization-code flow, and the conformance harness measures the
provider against the whole published spec surface through a data-first catalog whose entries name the actor that can
exercise them.** Both are standalone Vite SPAs on the shared `@ncode/*` platform.

### The Playground

`apps/playground` is an OAuth 2.1 / OpenID Connect flow explorer. It discovers the provider and walks authorization
code with PKCE `S256` end to end, shows every artifact (the verifier and challenge, `state`, `nonce`, the authorize
URL, the token response and the decoded JWT header and claims), and exercises UserInfo, refresh and RP-Initiated
Logout. The flow orchestration is the UI-free, unit-tested `flow.ts`, composed from the shared `@ncode/auth`
primitives. It offers only the authorization-code flow: implicit, hybrid front-channel-token and password flows cannot
be selected, matching the security-practice default.

### Serving and clients

The Playground is a public client, `playground-spa`, with its port-5174 callback and a developer CORS origin. The
conformance dashboard is read-only and discovery-driven, so it needs no client, only a CORS origin for its discovery
read. Each gets its client id from the surface-aware bootstrap at run time, never from a build-time value (the
platform's "one bundle, every tier" invariant, [ADR-1064](1064-one-frontend-platform-of-shared-packages-and-thin-app-shells-serves-every-web-surface.md)).
Both join the Aspire AppHost graph health-gated on the API
([ADR-1071](1071-the-aspire-apphost-runs-the-local-stack-in-health-gated-order-after-checking-the-frontend-toolchain.md)).

### The conformance dashboard

`apps/conformance` runs two panels over one discovery fetch:

- **A security audit (`audit.ts`)** against OAuth 2.1, RFC 9700, OpenID Connect Core and Discovery, and RFC
  7636, 8414, 7009, 7662 and 9126. It flags insecure or deprecated configuration, derives Basic and Config OP
  **profile readiness**, and shows the external suite's configuration.
- **A spec-coverage matrix.** It answers how much of the published surface exists, and complements the audit rather
  than replacing it, because the two answer different questions.

The framework-free logic lives in `@ncode/conformance-core` (`audit.ts`, `coverage.ts`, `discovery.ts`,
`probes.ts`), pure and unit-tested.

### The coverage catalog

The catalog is data. Each entry carries its RFC or OpenID Connect `reference`, its tracking `issue` and the
`CoverageChannel` that exercises it: `browser`, `probe-runner` or `external-suite`. Adding a published spec is a
catalog row, not a code change. The channel makes the browser's limits visible in the matrix and is the seam between
the dashboard and the headless runner.

The classifier maps each entry over the live discovery metadata to `implemented`, `partial`, `missing` or
`non-conformant`. Because every row is computed from live discovery, a capability that stops being advertised, or an
advertised value that turns insecure, changes its row: the matrix detects regressions as well as gaps.

The matrix states that its status is **advertised**, not **verified**, because a secure-by-default provider
([ADR-1010](1010-settings-merge-as-ceilings-and-floors-unset-lists-restrict-nothing-and-a-secure-baseline-supplies-defaults.md))
may implement a capability without advertising it. Browser-drivable probes refine advertised into **verified** for
PAR, the response-mode matrix, `prompt`, `max_age`, `acr` and `claims`, and the error paths.

### The probe runner and the external suite

The headless `@ncode/app-conformance-runner` CLI drives the legs a browser cannot: `client_credentials`,
introspection and revocation with client authentication, device code, CIBA, `private_key_jwt`, DPoP, mTLS and
back-channel logout. It signs with a Node `crypto` key loaded through `--private-key` and `--dpop-key`, infers the
algorithm from the key (overridable), builds the compact-JWS client assertion (OpenID Connect Core §9) and the DPoP
proof (RFC 9449 §4), and exits non-zero on any failed probe.

The OpenID Foundation Conformance Suite is the authoritative oracle.
`build/conformance/run-conformance.ps1` launches the provider, runs the runner as a pre-flight, brings up the
prebuilt-image suite and drives its OP plans against committed baselines
([`docs/conformance/external-suite.md`](../conformance/external-suite.md)). No CI pipeline runs it
([#124](https://github.com/NCodeGroup/identity/issues/124)).

The harness is a measurement surface: it measures the provider's protocol capabilities and implements none of them.

## Options considered

- **A Playground that offers every response type and flow.** Rejected: an explorer that can emit implicit, hybrid or
  password flows normalizes insecure patterns, and it cannot represent features the provider lacks.
- **A conformance surface that duplicates the sign-in and consent locator surface.** Rejected: that is `apps/auth`, and
  duplicating it would fork the stable-locator contract. A read-only posture and coverage dashboard is the
  non-overlapping value.
- **One browser SPA that exercises the entire protocol.** Rejected as structurally impossible: confidential-client,
  sender-constraining and server-initiated legs cannot run in a browser. The `browser` and `probe-runner` boundary is
  intrinsic, so the model encodes it.
- **Re-implementing the OpenID Foundation Conformance Suite in-house.** Rejected: duplicating a certified oracle is
  high cost and low trust. The in-house matrix is the fast local pre-flight.
- **Replacing the audit with the matrix.** Rejected: the audit asks whether the advertised configuration is secure,
  the matrix how much of the spec surface exists. Two panels over one discovery fetch keep both answers.
- **A hard-coded gap checklist in docs.** Rejected: a static list drifts from the running provider. Deriving status
  from live discovery keeps the matrix honest and lets a newly implemented capability appear without a doc edit.
- **Backend-served bundles for these surfaces.** Rejected: both are operator and developer tools with no reason to be
  served by the provider, and standalone dev servers keep the provider host focused.
- **A per-app build-time client id.** Rejected: it violates the "one bundle, every tier" invariant; the client id is a
  deployment coordinate discovered at run time.
- **A bootstrap endpoint per surface.** Rejected: one surface-aware endpoint means fewer routes.

The chosen design keeps the Playground to the recommended flow, computes the matrix from live discovery over a data
catalog, and splits exercise between browser, headless runner and the certified suite.

## Consequences

- The family has a developer flow explorer and a conformance dashboard on the shared platform, inheriting its theme,
  discovery, store and testing conventions.
- Every published capability, CIBA, DPoP, PAR, token exchange, pairwise subjects, JAR, JARM and the FAPI profile among
  them, has a row linked to its reference and, where one exists, its tracking issue, and the row shows `missing`
  whenever the provider does not advertise it, so a gap is visible and attributable.
- The classifier is pure over discovery and unit-tested, so a status change is a signal rather than noise.
- Advertised is not verified, a documented limitation: a provider that implements a capability without advertising it
  shows `missing` until a probe verifies it. The UI labels the semantics so the distinction is not mistaken for a
  false negative.
- The dashboard encodes this repository's reading of the security specs in `audit.ts`. It complements the external
  suite, which stays the authority; the dashboard is the fast, local pre-flight.
- Extending coverage to a newly published spec is a catalog row plus, where needed, a classifier rule, keeping the
  knowledge in the repository
  ([project-template ADR-0001](project-template/0001-conventions-and-lessons-live-in-the-repository.md)).

## References

- Issues: [#129](https://github.com/NCodeGroup/identity/issues/129) (the spec-coverage harness),
  [#135](https://github.com/NCodeGroup/identity/issues/135) (the surfaces), [#124](https://github.com/NCodeGroup/identity/issues/124)
  (the external suite), [#125](https://github.com/NCodeGroup/identity/issues/125) (the sign-in and consent surface),
  [#130](https://github.com/NCodeGroup/identity/issues/130) (the runner's signer).
- Code: `frontend/apps/playground` (`flow.ts`), `frontend/apps/conformance`, `frontend/apps/conformance-runner`,
  `frontend/packages/conformance-core`, `build/conformance/run-conformance.ps1`.
- Platform: [ADR-1064](1064-one-frontend-platform-of-shared-packages-and-thin-app-shells-serves-every-web-surface.md),
  [ADR-1071](1071-the-aspire-apphost-runs-the-local-stack-in-health-gated-order-after-checking-the-frontend-toolchain.md),
  [ADR-1062](1062-the-op-serves-an-opt-in-interactive-sign-in-and-consent-surface-with-a-pluggable-credential-verifier.md)
  (the `apps/auth` surface this does not duplicate).
- Discovery: [ADR-1010](1010-settings-merge-as-ceilings-and-floors-unset-lists-restrict-nothing-and-a-secure-baseline-supplies-defaults.md).
- Plans and guides: [`docs/conformance-testing.md`](../conformance-testing.md),
  [`docs/conformance/external-suite.md`](../conformance/external-suite.md),
  [`frontend/README.md`](../../frontend/README.md).
- Posture: [ADR-1025](1025-the-family-is-greenfield-and-pre-release-tracks-its-public-api-and-models-auth0-parity-plus.md)
  and the security-practice default in
  [`.github/instructions/repository.instructions.md`](../../.github/instructions/repository.instructions.md).
