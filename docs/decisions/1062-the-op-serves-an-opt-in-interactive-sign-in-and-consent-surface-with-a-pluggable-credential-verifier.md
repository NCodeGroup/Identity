# 1062. The OP serves an opt-in interactive sign-in and consent surface with a pluggable credential verifier

- **Status:** Accepted
- **Date:** 2026-10-11
- **Deciders:** NCode Group

## Context

The OpenID Provider authenticates and challenges through standard ASP.NET Core authentication schemes.
[`DefaultAuthenticateHandler`](../../src/NCode.Identity.OpenId.Authentication/Endpoints/Authorization/Handlers/DefaultAuthenticateHandler.cs)
calls `AuthenticateAsync(scheme)` and
[`DefaultChallengeHandler`](../../src/NCode.Identity.OpenId.Authentication/Endpoints/Authorization/Handlers/DefaultChallengeHandler.cs)
calls `ChallengeAsync(scheme)`, where `scheme` resolves from the `authorization_authenticate_scheme` and
`authorization_challenge_scheme` settings, which default to `IdentityConstants.ApplicationScheme`
(`"Identity.Application"`). A challenge needs a login page to redirect to, so a reference OP that is certifiable out
of the box and usable by the management console needs a real, scriptable interactive surface.

Two needs converge on it:

- **Conformance.** The OpenID Foundation Conformance Suite drives the interactive flow with a headless Selenium
  `BrowserControl` that locates fields by stable id or name. A header-driven fake subject is not browser-drivable, and
  a UI-less OP cannot complete an authorization-code flow under automation even when the wire protocol is correct.
- **Sign-in for browser apps.** The browser authorization-code and PKCE flow needs a login surface on the OP.

The hard constraint on hosting is that the cookie challenge redirects the browser to the scheme's `LoginPath` on the
OP's own origin, and the login POST must set the session cookie and honor a server-validated `ReturnUrl` on that same
origin. A login surface served from another origin, such as a Vite dev server or a CDN, cannot carry the cookie,
antiforgery and `ReturnUrl` round trip cleanly.

The forces in tension: the surface must be driveable and deterministic; it must not fork the existing authentication
seam or the local-account credential path; it must not pull a production-grade identity UI into a library whose job
is the protocol; and the capability must be referenceable without the composition root, whose job is wiring
capabilities together, not hosting one. Every other capability is a leaf package, and the credential backend must be
replaceable by an LDAP, external-IdP or passwordless backend.

## Decision

**The OP serves an opt-in interactive sign-in and consent surface from a leaf package pair, and the credential backend
sits behind an `IInteractiveCredentialVerifier` seam that the surface package owns. The surface satisfies the OP's
existing challenge and authenticate seam; it is never wired by default and it adds no authentication model.**

### The surface

- It covers login, consent, logout and its confirmation, and the account, sessions, applications and password pages.
- The host renders no-JavaScript pages, and the `apps/auth` bundle (React and MUI on `@ncode/ui`, from the frontend
  platform of [ADR-1064](1064-frontend-presentation-platform.md)) mounts over them.
- Everything is same-origin with the OP, so the cookie, antiforgery token and `ReturnUrl` round-trip work. Forms post
  as native HTML POSTs, not `fetch` calls.
- The challenge seam is unchanged. `DefaultChallengeHandler` challenges into login. The login POST verifies the
  credential, resolves the principal ([ADR-1035](1035-an-end-user-is-one-tenant-scoped-principal-that-every-authentication-entry-point-resolves-to.md)),
  validates its standing ([ADR-1229](1229-a-refused-end-user-cannot-sign-in-and-the-client-is-answered-access-denied.md)),
  creates the session, calls `SignInAsync` and redirects to a validated local `ReturnUrl`.
- The consent page is a front end over persisted consent
  ([ADR-1081](1081-consent-is-a-durable-record-of-decided-and-granted-scopes-enforced-at-the-authorization-endpoint.md)):
  allow and deny post to it, and deny answers `access_denied`.
- The ids `username`, `password`, `login`, `consent-allow` and `consent-deny` are the stable locators. They are set
  explicitly in both the server pages and the bundle, because MUI's generated ids are not stable selectors, and they
  match the conformance `browser` config (`docs/conformance-testing.md`, Appendices B and D).

### The packages

The surface is the leaf package pair `NCode.Identity.OpenId.Authentication.Interactive` and
`NCode.Identity.OpenId.Authentication.Interactive.Abstractions`, named for the capability under the
`NCode.Identity.OpenId.Authentication.*` family
([ADR-1500](1500-every-package-names-one-capability-under-the-ncode-identity-prefix-as-an-abstractions-and-implementation-pair.md)).

- `.Abstractions` holds `InteractiveAuthenticationDefaults` (paths, `CookieSchemeName`, cookie names),
  `InteractiveAuthenticationOptions` (`BundleRootPath`) and the seam
  `IInteractiveCredentialVerifier.VerifyCredentialAsync(OpenIdContext, username, password, ct)`, which returns a
  `ClaimsPrincipal?`.
- The implementation package holds the registration, the endpoint groups, the handler and the renderer. It references
  no account store and no persistence.
- `NCode.Identity.Server` only composes the capability.

### Registration

- `AddInteractiveAuthentication(IConfiguration, IHostEnvironment, Action<InteractiveAuthenticationOptions>?)` extends
  `IServiceBuilder<OpenIdAuthenticationLibrary>`, so the surface cannot be added without the authentication library
  ([ADR-1069](1069-di-registration-proves-every-prerequisite-at-compile-time-through-marker-builders.md)). It binds
  the `InteractiveAuthentication` section strictly, validates on start and resolves `BundleRootPath` against the
  content root.
- It registers a named cookie scheme, `IdentityConstants.ApplicationScheme`, which matches the
  `authorization_authenticate_scheme` and `authorization_challenge_scheme` defaults and is never the host's default
  scheme. Its `LoginPath` is `/account/login` and its `LogoutPath` is `/account/logout`.
- It wires no verifier, as the base authentication library reports the password grant unsupported until a capability
  supplies it, and it is never enabled by default.

### The credential backend

- The local backend is `AddLocalAccountInteractiveAuthentication(IServiceBuilder<LocalAccountLibrary>)` in
  `NCode.Identity.OpenId.Accounts.Authentication`, registered like the password grant
  ([ADR-1051](1051-a-local-account-is-a-self-issued-connection-behind-one-pluggable-store-seam.md)). It registers
  `LocalAccountInteractiveCredentialVerifier`, which verifies through secure buffers and issues a self-issued
  principal, and `LocalAccountInteractivePasswordChanger`
  ([ADR-1230](1230-an-end-user-changes-their-password-and-keeps-only-the-current-session.md)).
- An LDAP, external-IdP or passwordless backend registers its own `IInteractiveCredentialVerifier`.

### Extensibility seams

Three seams are explicit and symmetric: the bundle (`BundleRootPath`), the render surface (the scheme's `LoginPath`
and what `/account/*` serves) and the credential backend (the verifier).

### Build

The frontend toolchain builds `apps/auth` outside `./build/dod.ps1`. The C# module is testable without the bundle, and
a full browser round trip needs the bundle built.

## Options considered

- **The header-driven `TestSubjectAuthenticationHandler`.** Rejected: it serves the integration tests only and is not
  browser-drivable.
- **No UI.** Rejected: the OP cannot complete an authorization-code flow under automation or for a browser app.
- **The full ASP.NET Core Identity UI.** Rejected: it is a production identity UI that a protocol library should not
  carry, and it would fork the local-account credential path.
- **Server-rendered-only pages.** Rejected: the pages cannot grow into the real end-user surface of the frontend
  platform. They remain as the no-JavaScript layer under the bundle.
- **A separate origin (Vite or a CDN).** Rejected: the cookie, antiforgery and `ReturnUrl` round trip cannot cross it.
- **Code inlined in `NCode.Identity.Server`.** Rejected: the composition root is not a capability home, so the module
  would be unreferenceable without the whole server and its backend would be hard-wired.
- **The local verifier inside the surface package.** Rejected: it couples the backend-agnostic surface to the account
  store and persistence and breaks the dependency inversion of the password grant.
- **A `Server.*` package name.** Rejected: the surface is an OpenID authentication capability.
- **A leaf package pair with the verifier inverted behind an interface of `.Abstractions` (chosen).** The surface is
  reusable and backend-agnostic, and the local-account backend is one opt-in implementation among others.

## Consequences

- The capability is referenceable on its own, and a second credential backend is a matter of implementing
  `IInteractiveCredentialVerifier` without forking the surface.
- The reference OP is certifiable out of the box: the stable ids let the conformance suite and browser apps complete
  login and consent.
- The surface adds no authentication model, so the challenge pipeline, the cookie scheme defaults and the local-account
  path stay the single route to a session.
- Because the surface is opt-in and wires no verifier, a host that enables it names its backend.
- The C# module is covered by tests without the bundle, and the browser round trip is exercised once the bundle is
  built.

## References

- [`DefaultAuthenticateHandler`](../../src/NCode.Identity.OpenId.Authentication/Endpoints/Authorization/Handlers/DefaultAuthenticateHandler.cs)
  and
  [`DefaultChallengeHandler`](../../src/NCode.Identity.OpenId.Authentication/Endpoints/Authorization/Handlers/DefaultChallengeHandler.cs):
  the seam the surface satisfies.
- [`docs/conformance-testing.md`](../conformance-testing.md): the `browser` config and its Appendices B and D.
- [ADR-1064](1064-frontend-presentation-platform.md): the frontend platform that `apps/auth` belongs to.
- [ADR-1035](1035-an-end-user-is-one-tenant-scoped-principal-that-every-authentication-entry-point-resolves-to.md),
  [ADR-1229](1229-a-refused-end-user-cannot-sign-in-and-the-client-is-answered-access-denied.md) and
  [ADR-1081](1081-consent-is-a-durable-record-of-decided-and-granted-scopes-enforced-at-the-authorization-endpoint.md):
  the principal, the standing check and the consent the login and consent pages use.
- [ADR-1051](1051-a-local-account-is-a-self-issued-connection-behind-one-pluggable-store-seam.md) and
  [ADR-1230](1230-an-end-user-changes-their-password-and-keeps-only-the-current-session.md): the local-account backend
  and the password changer.
- [ADR-1069](1069-di-registration-proves-every-prerequisite-at-compile-time-through-marker-builders.md) and
  [ADR-1500](1500-every-package-names-one-capability-under-the-ncode-identity-prefix-as-an-abstractions-and-implementation-pair.md):
  the registration pattern and the package naming.
