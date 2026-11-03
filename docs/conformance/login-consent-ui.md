# Browser-automatable login + consent UI (with a seeded conformance identity)

The OpenID Provider's **interactive login and consent experience** is one the OpenID Foundation Conformance Suite's
headless `BrowserControl` can drive end-to-end: stable element locators, a deterministic signed-in session, and a
consent outcome — so a protocol-correct authorization flow completes under automation. Tracking issue:
[#125](https://github.com/NCodeGroup/Identity/issues/125). Parent design:
[`docs/conformance-testing.md`](../conformance-testing.md) (prerequisite #2 and Appendix B). Decision records:
[ADR-1062](../decisions/1062-opt-in-interactive-login-consent-ui.md) (seam + stable-locator contract),
[ADR-1065](../decisions/1065-interactive-login-consent-op-hosted-auth-surface.md) (the OP-hosted `apps/auth` surface),
[ADR-1070](../decisions/1070-interactive-login-consent-leaf-package-and-credential-verifier-seam.md) (leaf capability
package + pluggable credential verifier).

## The surface

The interactive login/consent surface is the opt-in leaf capability package
[`NCode.Identity.OpenId.Authentication.Interactive`](../../src/NCode.Identity.OpenId.Authentication.Interactive)
(+ [`.Abstractions`](../../src/NCode.Identity.OpenId.Authentication.Interactive.Abstractions)). A single
`AddInteractiveAuthentication` registration
([`DefaultRegistration`](../../src/NCode.Identity.OpenId.Authentication.Interactive/DefaultRegistration.cs)) registers
the `Identity.Application` cookie scheme (`LoginPath = /account/login`) the authorize challenge redirects into, and maps
the `/account/*` endpoints, which also serve the bundle's files, beneath the tenant route
([ADR-1193](../decisions/1193-the-interactive-surface-is-served-under-the-tenant-route.md)) through an
`IEndpointProvider`
([`InteractiveAuthenticationEndpointHandler`](../../src/NCode.Identity.OpenId.Authentication.Interactive/InteractiveAuthenticationEndpointHandler.cs)).
The `Identity.Application` name lines up with the `authorization_authenticate_scheme` /
`authorization_challenge_scheme` setting defaults, so no per-client override is needed.

- **Credential verification** is a pluggable `IInteractiveCredentialVerifier` seam. Its local-account implementation
  ([`LocalAccountInteractiveCredentialVerifier`](../../src/NCode.Identity.OpenId.Accounts.Authentication/LocalAccountInteractiveCredentialVerifier.cs),
  registered by `AddLocalAccountInteractiveAuthentication()`) reuses the resource-owner-password credential path, so
  there is one credential verifier.
- **The render surface** is the React + MUI `apps/auth` bundle ([`src/frontend/apps/auth`](../../src/frontend/apps/auth)),
  served same-origin under `/account/*`, exposing the Appendix B locators (`username`, `password`, `login`,
  `consent-allow`, `consent-deny`) as explicit ids, since MUI's generated ids are not stable selectors.
- **Progressive enhancement.** `InteractiveAuthenticationEndpointHandler` server-renders a working no-JavaScript
  `<form>` with the same locators through
  [`InteractiveAuthenticationPageRenderer`](../../src/NCode.Identity.OpenId.Authentication.Interactive/InteractiveAuthenticationPageRenderer.cs),
  injected into the bundle's `#root` so the SPA takes it over on mount. The conformance suite's no-JS `HtmlUnitDriver`
  therefore drives the surface without user-agent sniffing.
- **Login** (`GET/POST /account/login`) carries the ASP.NET Core antiforgery token, verifies the credentials, signs in
  to `Identity.Application` with the subject claim the OP expects, and redirects to the validated `ReturnUrl`; a
  `ReturnUrl` that leaves the OP is rejected (open-redirect guard).
- **Consent** (`GET/POST /account/consent`, `consent-allow` / `consent-deny`) is durable and granular, enforced through
  this same surface ([ADR-1076](../decisions/1076-consent-persistence-and-enforcement.md),
  [ADR-1081](../decisions/1081-granular-consent-and-authorized-applications.md)). The conformance `browser` config
  marks its `Consent` task `optional`, so a flow that needs no consent screen completes without it.
- **Logout confirmation** (`/account/logout/confirm`,
  [ADR-1380](../decisions/1380-a-logout-without-a-valid-id-token-hint-for-the-current-session-is-confirmed.md)) is
  served the same way and exposes `logout-confirm` and `logout-cancel`.

The host opts in ([`Program.cs`](../../src/NCode.Identity.OpenId.Host/Program.cs)), and integration and unit tests cover
the endpoints
([`InteractiveLoginAntiforgeryTests`](../../tests/NCode.Identity.OpenId.Integration.Tests/Endpoints/InteractiveLoginAntiforgeryTests.cs),
[`InteractiveLogoutTests`](../../tests/NCode.Identity.OpenId.Integration.Tests/Endpoints/InteractiveLogoutTests.cs),
[`InteractiveAuthenticationEndpointHandlerTests`](../../tests/NCode.Identity.OpenId.Authentication.Interactive.Tests/InteractiveAuthenticationEndpointHandlerTests.cs)).
Integration tests that need no browser authenticate through
[`TestSubjectAuthenticationHandler`](../../tests/NCode.Identity.OpenId.Integration.Tests/Infrastructure/TestSubjectAuthenticationHandler.cs),
which reads an `X-Test-Subject` header.

## Seeded identity

The login uses a deterministic user. That seeding is owned by [#127](https://github.com/NCodeGroup/Identity/issues/127)
([`conformance-seeding.md`](conformance-seeding.md)); the login surface **consumes** it (a fixed username and a
per-machine password in the conformance tenant) and never seeds independently.

## Acceptance criteria

- A challenge from the authorize endpoint redirects to `/account/login`, and the rendered form exposes `id=username`,
  `id=password`, and `id=login`.
- Posting valid seeded credentials establishes an `Identity.Application` cookie session and returns to the original
  authorize URL, which then completes with an authorization code.
- `/account/consent` renders `id=consent-allow` / `id=consent-deny`.
- The whole loop is drivable headlessly by the Appendix D `browser` task block.

## Dependencies & links

- **Depends on** [#127](https://github.com/NCodeGroup/Identity/issues/127) (seeded conformance identity) and
  [#126](https://github.com/NCodeGroup/Identity/issues/126) (reachable host for end-to-end verification).
- Locator contract: [`conformance-testing.md`](../conformance-testing.md) Appendix B; `browser` config: Appendix D.
