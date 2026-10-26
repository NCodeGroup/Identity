# 65. Interactive login/consent as the OP-hosted `apps/auth` surface

- **Status:** Accepted (packaging superseded by
  [ADR-0070](0070-interactive-login-consent-leaf-package-and-credential-verifier-seam.md); amended by
  [ADR-0193](0193-the-interactive-surface-is-served-under-the-tenant-route.md): the surface and its bundle are served
  beneath the tenant route by the surface's own endpoints, and `UseInteractiveAuthentication()` is removed)
- **Date:** 2026-10-10
- **Deciders:** NCode Group

## Context

The OpenID Provider authenticates and challenges through standard ASP.NET Core schemes and expects the
host to supply the interactive surface
([`DefaultAuthenticateHandler`](../../src/NCode.Identity.OpenId.Authentication/Endpoints/Authorization/Handlers/DefaultAuthenticateHandler.cs)
calls `AuthenticateAsync(scheme)`,
[`DefaultChallengeHandler`](../../src/NCode.Identity.OpenId.Authentication/Endpoints/Authorization/Handlers/DefaultChallengeHandler.cs)
calls `ChallengeAsync(scheme)`, where `scheme` resolves from the `authorization_authenticate_scheme` /
`authorization_challenge_scheme` settings; the default is `IdentityConstants.ApplicationScheme`,
`"Identity.Application"`). No host in the family registers that scheme, so a challenge has nowhere to
go. Two converging needs make this a concrete gap rather than a host's problem:

- **Management end-to-end sign-in ([#133](https://github.com/NCodeGroup/Identity/issues/133)/[#139](https://github.com/NCodeGroup/Identity/issues/139)).** The Management console (`apps/mgmt`) performs the
  browser authorization-code + PKCE flow, but the flow cannot complete because the authorize endpoint's
  challenge has no login surface to redirect to, and no public SPA client is seeded.
- **Conformance ([#125](https://github.com/NCodeGroup/Identity/issues/125)/[ADR-0062](0062-opt-in-interactive-login-consent-ui.md)).** Independent OIDC
  conformance drives the interactive flow with a headless Selenium `BrowserControl` that locates fields
  by stable id/name; a UI-less OP cannot complete an authorization-code flow under automation even when
  the wire protocol is correct.

[ADR-0062](0062-opt-in-interactive-login-consent-ui.md) ratified the **seam and the stable-locator
contract** (`id=username` / `password` / `login` / `consent-allow` / `consent-deny`) and deliberately
left two things open: the **rendering technology** (server-rendered HTML vs. React + MUI) and whether the
module is **dev-only** or a first-class surface. [ADR-0064](0064-frontend-presentation-platform.md)
established the cohesive React + Material UI frontend platform under [`src/frontend/`](../../src/frontend/)
and already names **`apps/auth`** as one of its planned surfaces. This ADR closes the two open questions
by binding them to that platform.

The hard constraint shaping the hosting choice: the cookie challenge redirects the browser to the scheme's
`LoginPath` **on the OpenID Provider's own origin**, and the login POST must set the session cookie and
honor a server-validated `ReturnUrl` **same-origin**. A login surface served from a different origin (the
Vite dev server, a separate CDN host) cannot carry the cookie/antiforgery/`ReturnUrl` round-trip cleanly.

## Decision

**Realize the interactive login/consent surface as `apps/auth` — a React + Material UI app on the shared
frontend platform ([ADR-0064](0064-frontend-presentation-platform.md)) — served by the OpenID Provider
host as same-origin static assets under `/account/*`, backed by minimal-API endpoints that reuse the
local-account credential path and issue the `Identity.Application` cookie. The server-driven cookie
challenge seam and the stable-locator contract from [ADR-0062](0062-opt-in-interactive-login-consent-ui.md)
are preserved unchanged; this ADR only resolves ADR-0062's two deferred choices — rendering is React +
MUI on the platform, and the surface is a first-class opt-in capability rather than a dev-only fake.**
Concretely:

- **Rendering is `apps/auth` (React + MUI).** The login and consent forms are the `apps/auth` surface of
  the platform, reusing `@ncode/ui`, `@ncode/config`, and `@ncode/auth`. The rendered DOM sets the
  Appendix B ids **explicitly** (MUI's generated ids are not stable selectors). This is the real
  end-user login/consent surface, not a conformance-only stopgap.
- **The OP host serves it same-origin.** The reference host serves the built `apps/auth` bundle as static
  files under `/account/*` so the cookie `LoginPath` redirect, the antiforgery-protected POST, and the
  `ReturnUrl` round-trip are all same-origin. The forms submit as native HTML form POSTs (not `fetch`) so
  the browser follows the resulting `Set-Cookie` + redirect, which is also what Selenium drives.
- **The backend is a reusable opt-in module.** A single registration
  (`AddInteractiveAuthentication()` / `UseInteractiveAuthentication()`) surfaced from
  [`NCode.Identity.Server`](../../src/NCode.Identity.Server) registers the cookie scheme
  (`Identity.Application`, `LoginPath = "/account/login"`, `LogoutPath = "/account/logout"`) and maps the
  endpoints: `GET /account/login` and `GET /account/consent` serve the SPA shell; `POST /account/login`
  verifies credentials and signs in; `POST /account/consent` records the allow/deny outcome;
  `GET /account/logout` clears the session. The host opts in with one call; it is never wired by default.
- **Credential verification reuses the local-account path.** `POST /account/login` verifies through the
  same `ILocalAccountStore.VerifyCredentialAsync` the resource-owner password grant uses
  ([ADR-0060](0060-local-account-one-store-seam-and-dependency-inverted-authentication.md)); it introduces
  no second credential verifier. On success it builds a `ClaimsPrincipal` carrying the subject claim the
  authorize flow reads (`sub` = the account's self-issued id, per the tenant's `subject_claim_types`),
  calls `SignInAsync("Identity.Application", principal)`, and redirects to a **validated local**
  `ReturnUrl` (open-redirect guard).
- **The public SPA clients are seeded through persistence.** The Management console authenticates as a
  **public** client (empty secret set, PKCE-only, with `redirect_uris`), seeded idempotently through the
  normal persistence path on the tenant seed fan-out (the bootstrap-admin seeder pattern,
  [ADR-0045](0045-tenant-provisioning-and-seeding-pipeline.md)). A deterministic admin **user** is seeded
  the same way so the login has a credential to verify.
- **Consent is a decoupled stopgap (unchanged from ADR-0062).** The seeded clients auto-approve by
  default; `/account/consent` still exposes `consent-allow` / `consent-deny` for negative-path tests.
  This remains independent of durable consent persistence ([#40](https://github.com/NCodeGroup/Identity/issues/40)); when [#40](https://github.com/NCodeGroup/Identity/issues/40) lands, the screen becomes a
  thin front-end over the persisted decision.
- **Build boundary.** `apps/auth` is built by the frontend toolchain, which stays **out of the hermetic
  `dod.ps1` inner loop** ([ADR-0064](0064-frontend-presentation-platform.md)); the host serves whatever
  bundle is present under its static root. The C# module (cookie scheme + endpoints + seeders) is in
  `dod.ps1` and is independently testable without the bundle.

## Options considered

- **Server-rendered HTML forms in the OP host (ADR-0062's lowest-cost option).** Simplest, no build
  pipeline, in `dod.ps1`, and fully satisfies the locator contract. Rejected as the surface because it
  forks rendering away from the React + MUI platform ([ADR-0064](0064-frontend-presentation-platform.md)):
  the login/consent screen is a genuine, long-lived **end-user** surface (every interactive sign-in and
  the future [#40](https://github.com/NCodeGroup/Identity/issues/40) consent screen render here), so it belongs on the shared design system, not as a second,
  parallel rendering stack the platform would eventually have to absorb or replace.
- **React + MUI served from a separate origin (Vite dev server / CDN).** Keeps hot-reload for the auth
  app but breaks the same-origin cookie/antiforgery/`ReturnUrl` round-trip the challenge seam requires.
  Rejected: the login surface must be same-origin with the OP that issues the cookie.
- **React + MUI served by the OP host under `/account/*` (chosen).** Same-origin for the cookie flow,
  cohesive with the platform and `@ncode/ui`, and closes `apps/auth` from
  [ADR-0064](0064-frontend-presentation-platform.md). Cost: the host serves a built bundle, so the auth
  app's dev inner loop is a build step rather than hot reload — acceptable for a small, stable surface.
- **Dev-only fake (status quo in tests).** `TestSubjectAuthenticationHandler` is header-driven, not
  browser-drivable, and carries no credential check; fine for integration tests, unusable as the real
  surface.
- **Full production ASP.NET Core Identity UI.** Over-scoped and duplicative — the local-account store
  already owns credentials ([ADR-0060](0060-local-account-one-store-seam-and-dependency-inverted-authentication.md)).

## Consequences

- The OpenID Provider gains its first interactive surface, cohesive with the frontend platform and the
  Management console. [#133](https://github.com/NCodeGroup/Identity/issues/133)/[#139](https://github.com/NCodeGroup/Identity/issues/139) end-to-end sign-in and [#125](https://github.com/NCodeGroup/Identity/issues/125) conformance automation both become reachable
  through one surface rather than two.
- The stable-locator contract remains a maintenance invariant coupled to the conformance `browser` config
  ([`conformance-testing.md`](../conformance-testing.md) Appendices B/D): the ids are set explicitly in
  `apps/auth` and reviewed with the config.
- The OP host now has a static-file root and a hard same-origin dependency on the `apps/auth` bundle: an
  interactive sign-in requires the bundle to be built and present. The C# module is independently
  testable, but a full browser round-trip needs the built surface.
- Consent remains a stopgap until [#40](https://github.com/NCodeGroup/Identity/issues/40); the auto-approve default must converge onto the persisted-consent
  decision when [#40](https://github.com/NCodeGroup/Identity/issues/40) lands, or the two will drift.
- Full OIDC-conformance automation additionally depends on a reachable HTTPS host with a byte-matched
  `issuer` ([#126](https://github.com/NCodeGroup/Identity/issues/126)/[`reachable-host.md`](../conformance/reachable-host.md)) and deterministic conformance
  tenant/client/user seeding ([#127](https://github.com/NCodeGroup/Identity/issues/127)/[`conformance-seeding.md`](../conformance/conformance-seeding.md));
  those remain separate items. The admin user and public SPA client seeded here cover local Management
  sign-in, not the full conformance identity set.

## References

- Supersedes the two deferred choices (rendering; dev-only vs. first-class) left open by
  [ADR-0062](0062-opt-in-interactive-login-consent-ui.md); preserves its seam and stable-locator contract.
- Builds on [ADR-0064](0064-frontend-presentation-platform.md) (frontend platform; names `apps/auth`),
  [ADR-0060](0060-local-account-one-store-seam-and-dependency-inverted-authentication.md) (local-account
  credential path), [ADR-0045](0045-tenant-provisioning-and-seeding-pipeline.md) (tenant seed fan-out),
  [ADR-0030](0030-userinfo-endpoint-and-subject-authentication-seam.md) (subject-authentication seam).
- Issues: [#133](https://github.com/NCodeGroup/Identity/issues/133), [#139](https://github.com/NCodeGroup/Identity/issues/139), [#125](https://github.com/NCodeGroup/Identity/issues/125), [#135](https://github.com/NCodeGroup/Identity/issues/135); plan
  [`login-consent-ui.md`](../conformance/login-consent-ui.md).
- Code: [`Host/Program.cs`](../../src/NCode.Identity.OpenId.Host/Program.cs),
  [`BootstrapAdminSeedHandler`](../../src/NCode.Identity.Server/BootstrapAdminSeedHandler.cs).
