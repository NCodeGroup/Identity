# 70. Interactive login/consent is a leaf capability package with a pluggable credential verifier

- **Status:** Accepted
- **Date:** 2026-10-11
- **Deciders:** NCode Group

## Context

[ADR-0065](0065-interactive-login-consent-op-hosted-auth-surface.md) realized the interactive
login/consent surface as an opt-in module — a cookie scheme, four `/account/*` endpoints, antiforgery
wiring, and a static-file server for the `apps/auth` bundle — and described it as "a reusable opt-in
module … surfaced from `NCode.Identity.Server`." The code was inlined under
`NCode.Identity.Server/InteractiveAuthentication/`. But `NCode.Identity.Server` is the **composition
root** of the family (`AddIdentityServer()`, `DefaultRegistration`, the `IdentityServerBuilder`) — it
wires capability packages together; it is not itself the home of a capability. Every other capability
(Accounts authentication, Management, Authentication) is a leaf package; this one was the exception.

Two consequences followed. First, the capability could not be referenced without pulling in the whole
composition root. Second, the surface hard-wired its credential backend: the endpoint handler called
`ILocalAccountStore.VerifyCredentialAsync` and built the `ClaimsPrincipal` directly, so an LDAP,
external-IdP, or passwordless backend had no seam to replace — even though the bundle (via
`BundleRootPath`) and the render surface (via the cookie `LoginPath`/scheme) were already swappable.

## Decision

**The interactive login/consent surface is its own leaf capability package pair —
`NCode.Identity.OpenId.Authentication.Interactive` and its `.Abstractions` — and its credential backend
is inverted behind an `IInteractiveCredentialVerifier` seam owned by the `.Abstractions` package. The
surface package is backend-agnostic; the local-account implementation lives in the local-account
capability package (`NCode.Identity.OpenId.Accounts.Authentication`) and is registered through its own
opt-in, exactly like the resource-owner password grant
([ADR-0060](0060-local-account-one-store-seam-and-dependency-inverted-authentication.md)).**

- **`…Interactive.Abstractions`** holds the stable contract: `InteractiveAuthenticationDefaults` (paths
  and cookie scheme name), `InteractiveAuthenticationOptions`, and the `IInteractiveCredentialVerifier`
  seam (`VerifyCredentialAsync(OpenIdContext, username, password, ct) -> ClaimsPrincipal?`).
- **`…Interactive`** holds the registration, the request-pipeline and endpoint-route extensions, and the
  endpoint handler. The handler depends only on `IInteractiveCredentialVerifier` — it no longer
  references the account store, persistence, JOSE, or secure-buffer types. The base registration wires
  **no** verifier, mirroring how the base Authentication library reports the password grant as
  unsupported until a capability supplies it.
- **The local-account verifier** (`LocalAccountInteractiveCredentialVerifier`) lives in
  `NCode.Identity.OpenId.Accounts.Authentication` and is registered by
  `AddLocalAccountInteractiveAuthentication()`. It owns the local-account coupling (the secure-buffer
  credential verification and the self-issued `ClaimsPrincipal` construction). A host fronting a
  different backend registers its own `IInteractiveCredentialVerifier` instead.
- **Registration follows the marker pattern** ([ADR-0069](0069-registration-library-compile-time-marker-nesting.md)):
  `AddInteractiveAuthentication()` and `AddLocalAccountInteractiveAuthentication()` extend
  `IServiceBuilder<OpenIdAuthenticationLibrary>`, so the surface cannot be added without the OpenId
  Authentication library present.

The three UI extensibility seams are now explicit and symmetric: the **bundle** (`BundleRootPath`), the
**render surface** (cookie `LoginPath`/scheme + whatever is served at `/account/*`), and the
**credential backend** (`IInteractiveCredentialVerifier`).

## Options considered

- **Leave it inlined in `NCode.Identity.Server`.** Rejected: the composition root is not a capability
  home; it made the module unreferenceable without the whole server and left the backend hard-wired.
- **Extract the package but keep the local-account verifier inside it.** Rejected: it would couple the
  backend-agnostic surface to the account store and persistence, defeating the extensibility seam and
  violating the dependency-inversion already established for the password grant (ADR-0060).
- **Extract the package and invert the verifier behind an interface owned by `.Abstractions` (chosen).**
  The surface is reusable and backend-agnostic; the local-account backend is one opt-in implementation
  among possible others, registered by the package that owns the local-account coupling.
- **Name it in the `Server` family (`NCode.Identity.Server.*`).** Rejected: it is an OpenID
  authentication capability, so it joins the `NCode.Identity.OpenId.Authentication.*` family.

## Consequences

- The capability is referenceable on its own; `NCode.Identity.Server` only references it like any other
  capability. The old `NCode.Identity.Server.InteractiveAuthentication.*` public surface is removed and
  reintroduced under `NCode.Identity.OpenId.Authentication.Interactive.*` — breaking `PublicAPI` churn,
  acceptable pre-release ([ADR-0025](0025-pre-release-posture-and-auth0-parity-plus.md)).
- A second credential backend (LDAP, external IdP, passwordless) is now a matter of implementing
  `IInteractiveCredentialVerifier`; no fork of the surface is required.
- Host wiring moves onto the authentication-library builder:
  `identityServer.OpenIdAuthenticationLibraryBuilder.AddLocalAccountAuthentication().AddInteractiveAuthentication(…).AddLocalAccountInteractiveAuthentication()`.

## References

- [ADR-0065](0065-interactive-login-consent-op-hosted-auth-surface.md) — the interactive surface decision this extracts.
- [ADR-0060](0060-local-account-one-store-seam-and-dependency-inverted-authentication.md) — the dependency-inverted local-account authentication pattern the verifier seam follows.
- [ADR-0069](0069-registration-library-compile-time-marker-nesting.md) — the registration marker pattern the capability's opt-ins use.
- [ADR-0016](0016-implementation-packages-depend-only-on-abstractions.md) — why the seam lives in `.Abstractions`.
