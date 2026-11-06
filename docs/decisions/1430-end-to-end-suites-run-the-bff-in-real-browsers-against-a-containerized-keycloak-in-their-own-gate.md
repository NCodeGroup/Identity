# 1430. End-to-end suites run the BFF in real browsers against a containerized Keycloak in their own gate

- **Status:** Accepted (amends
  [ADR-1263](1263-feature-branches-run-a-fast-gate-and-the-merge-queue-runs-the-full-gate.md); decision 1
  amended by [ADR-1441](1441-a-bff-connection-ends-with-its-session-even-when-idle-and-a-blazor-web-app-proves-it.md): a
  project under tests/ whose name does not end in .Tests is a support project that no tier runs)
- **Date:** 2026-11-05
- **Deciders:** NCode Group
- **Refines:** [project-template ADR-0008](project-template/0008-external-inputs-are-pinned-and-kept-current.md), [ADR-1249](1249-the-host-images-base-images-are-pinned-by-digest-and-tracked-by-dependabot.md)

## Context

The backend-for-frontend (BFF) design (§10) asks for a browser suite and for an OpenID Provider (OP) that is not NCode
(goal G3). The browser suite checks what only a browser enforces: the session cookie's attributes, `SameSite` on
cross-site navigation and form posts, CORS preflight denial and Fetch Metadata rejection. The interop suite checks
login (PAR, PKCE `S256`, `private_key_jwt`, RFC 9207, PS256 ID tokens), DPoP, refresh-token rotation, RP-initiated
logout and back-channel logout against an OP built by someone else, using only its discovery metadata.

Both need Docker and real browsers, and a browser download is roughly 500 MB. The Definition of Done
([`build/dod.ps1`](../../build/dod.ps1)) is the one gate that developers, agents and CI run, and a container runtime
must not become a prerequisite of it, for the reason given in
[ADR-1071](1071-aspire-local-dev-orchestrator.md). The suites therefore run in their own gate, which has its own check
([ADR-1261](1261-main-accepts-changes-only-through-pull-requests-that-pass-the-gate.md) decision 5).

## Decision

**End-to-end suites are test projects marked by name, run by one entrypoint in their own required CI check, and they
run the BFF in Chromium, Firefox and WebKit against a containerized Keycloak.**

1. **A tier, marked by name.** An end-to-end suite is a test project under `tests/` whose name ends in
   `.EndToEnd.Tests`. [`build/test-projects.ps1`](../../build/test-projects.ps1) is the one place that holds this rule.
   The solution compiles these projects. `./build/dod.ps1` tests only the other test projects, through
   `artifacts/tests.slnf`, in full and in `-Affected` mode alike. A suite under `tests/` keeps every test convention,
   the editorconfig `[tests/**/*.cs]` block and `tests/Directory.Build.props`.

2. **One entrypoint, `./build/e2e.ps1`, for CI and developers.** It checks Docker, builds the suites, installs the
   Playwright browsers and runs `dotnet test`. With `-Affected` it runs only the suites in
   [`build/affected-tests.ps1`](../../build/affected-tests.ps1)'s `EndToEndProjects` output. It writes JUnit reports to
   `artifacts/e2e-results` and ends with `End-to-end: PASSED`. The Definition of Done never runs it.

3. **A CI job, `End-to-end`, is a required check.** [`.github/workflows/e2e.yml`](../../.github/workflows/e2e.yml) has
   the triggers and tiers of `ci.yml` ([ADR-1263](1263-feature-branches-run-a-fast-gate-and-the-merge-queue-runs-the-full-gate.md)):
   the affected suites on a pull request into `main`, every suite in the merge queue, on `main` and on the release
   lines. It reports on every pull request. The `End-to-end` check is the sixth required check of the `main` and
   `release-lines` rulesets, beside the five of ADR-1263 decision 7.

4. **Playwright for .NET, driven by xUnit.** The BFF, the origin pages and the fixtures run in the test process, in
   one language and one runner, with the existing JUnit logger. The suites do not use Node Playwright in
   `src/frontend`, which is the console's workspace.

5. **Browsers.** Tests are theories over `BrowserNames.All`: Chromium and Firefox on every operating system, and WebKit
   except on Windows. Playwright's Windows WebKit port does not use Safari's cookie stack and mishandles `SameSite` and
   `Secure` (microsoft/playwright#20370). CI runs on Linux, so it covers all three engines. The interop suite uses
   Chromium only, because what it tests is the OP, not the browser.

6. **The OP is Keycloak 26.8.1 for both suites.** The image is pinned by digest in a `Dockerfile` beside a checked-in
   realm, and Dependabot's docker ecosystem keeps it current, ignoring majors
   ([ADR-1249](1249-the-host-images-base-images-are-pinned-by-digest-and-tracked-by-dependabot.md),
   [project-template ADR-0008](project-template/0008-external-inputs-are-pinned-and-kept-current.md)). Keycloak
   implements every standard the BFF uses and can require PAR, PKCE, DPoP and the ID-token algorithm per client, so a
   login that succeeds proves the BFF used them. The browser suite's assertions are about the BFF, so it needs no NCode
   OP. The NCode OP stays the in-process oracle of `NCode.Identity.OpenId.Integration.Tests`, and no shared host
   library is extracted.

7. **Testcontainers starts Keycloak.** The fixture owns the container's lifecycle and the port wiring. The realm uses
   Keycloak's `${ENV}` placeholders for ports and per-run credentials, so the repository holds no static secret.
   `TestcontainersSettings.ExposeHostPortsAsync` lets Keycloak reach the BFF at `host.testcontainers.internal` for
   `jwks_uri` and back-channel logout, without binding the BFF beyond loopback.

8. **Sites and TLS.** The BFFs listen on `https://localhost:<port>`. Keycloak and the cross-site pages are reached at
   `https://127.0.0.1:<port>`, and the same-site sibling pages at other `localhost` ports. One self-signed certificate
   per run covers `localhost`, `host.testcontainers.internal`, `127.0.0.1` and `::1`. Kestrel serves it, Keycloak
   serves it and trusts it, the BFF's provider `HttpClient` trusts exactly it, and browsers ignore certificate errors.
   Every host is HTTPS, because `__Host-` cookies need `Secure`.

9. **Two BFF instances share one realm.** `bff-strict` uses the default `SameSite=Strict` and `bff-lax` uses `Lax`.
   Each has the same deliberately broad CORS policy: it admits the two same-site siblings and one cross-site origin,
   with credentials and the `X-CSRF` header. Only the trusted sibling is in `Bff:TrustedOrigins`, so the tests show that
   Fetch Metadata refuses what CORS admits.

10. **Every token request refreshes.** The realm's 60-second access-token lifespan and the BFF's default 60-second
    `Bff:Tokens:RefreshBeforeExpiry` make every `/api/token` call refresh. The realm rotates refresh tokens and
    revokes on reuse, so two calls with different token fingerprints prove rotation.

11. **An imported user is granted its default roles.** Keycloak gives a user imported from a realm file no default
    roles, so the realm grants `alice` `default-roles-bff-e2e` explicitly. She then holds what a registered user
    holds, and a login that requests the `offline_access` scope succeeds.

12. **A form post waits for the response, not a navigation.** `BrowserFlows.SubmitFormAsync` waits for the response to
    its `POST`. Firefox turns an error response with an empty body into a failed navigation, so a navigation wait
    cannot observe a refusal in every browser.

13. **The login requests `offline_access` only when the deployment lists it.** The interop suite runs the BFF as
    [ADR-1431](1431-the-bff-requests-offline-access-only-when-its-deployment-lists-it.md) describes, so Keycloak's
    logout at the OP reaches the BFF as a back-channel logout token.

## Options considered

- **A trait filter in the Definition of Done.** Rejected: the gate loads the end-to-end assembly even when a filter
  excludes its tests.
- **Running the suites in the Definition of Done.** Rejected: Docker and about 500 MB of browsers become prerequisites
  of the gate every developer, agent and CI run uses.
- **Node Playwright in `src/frontend`.** Rejected: it splits the suites across two languages and two runners, and the
  BFF host, the origin pages and the container fixtures would have to start from Node.
- **The NCode OP in the tier, through a shared host library.** Rejected: it needs a host library extracted from the
  integration tests, and an NCode OP proves the BFF only against its own implementation of the standards.
- **Docker Compose.** Rejected: the fixture owns the lifecycle and the port wiring, which Testcontainers expresses in
  code.
- **Other OPs.**
  - node-oidc-provider is a library, with no official server image.
  - Ory Hydra needs a separate login app.
  - Dex has no PAR or DPoP.
  - ZITADEL needs a database.
- **Keycloak in a container (chosen).** It has an official image, needs no database in development mode, and implements
  PAR, PKCE, DPoP, `private_key_jwt`, RFC 9207, RP-initiated logout and back-channel logout.

## Consequences

- The `End-to-end` check gates merges, and a change to a suite, to the BFF's projects or to a global build input runs it
  in a pull request.
- A Keycloak bump arrives as a Dependabot pull request that the check validates.
- WebKit is proven only off Windows; CI covers it on Linux.
- Adding the check to the `main` and `release-lines` rulesets is an administrator's action on the repository.
- The `main` and `release-lines` rulesets enforce the check once they list `End-to-end`, tracked in
  [#591](https://github.com/NCodeGroup/Identity/issues/591).
- A test that shows the BFF failing against Keycloak or a browser is a finding about the BFF.

## References

- [ADR-1261](1261-main-accepts-changes-only-through-pull-requests-that-pass-the-gate.md),
  [ADR-1263](1263-feature-branches-run-a-fast-gate-and-the-merge-queue-runs-the-full-gate.md),
  [ADR-1249](1249-the-host-images-base-images-are-pinned-by-digest-and-tracked-by-dependabot.md),
  [ADR-1431](1431-the-bff-requests-offline-access-only-when-its-deployment-lists-it.md),
  [project-template ADR-0008](project-template/0008-external-inputs-are-pinned-and-kept-current.md)
- Issues [#418](https://github.com/NCodeGroup/Identity/issues/418) and
  [#419](https://github.com/NCodeGroup/Identity/issues/419)
- `tests/NCode.Identity.Bff.EndToEnd.Tests/`, `build/e2e.ps1`, `build/test-projects.ps1`,
  `.github/workflows/e2e.yml`
