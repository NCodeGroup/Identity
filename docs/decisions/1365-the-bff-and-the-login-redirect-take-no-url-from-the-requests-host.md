# 1365. The BFF and the login redirect take no URL from the request's Host

- **Status:** Accepted
- **Date:** 2026-11-02
- **Deciders:** NCode Group
- **Refines:** [project-template ADR-0126](project-template/0126-published-urls-come-from-a-configured-origin.md)

## Context

Three URLs the server published were built from the request's `scheme://host`, so a forged or proxied `Host` chose where
they pointed:

- `DefaultRegistration.BuildLoginRedirectUri` built the interactive surface's login redirect as
  `{request.Scheme}://{request.Host}{loginPath}{query}`.
- `BffNavigation.CallbackUri` built the BFF's OAuth `redirect_uri` the same way.
- `BffNavigation.AbsoluteUrl` built the BFF's `post_logout_redirect_uri` the same way.

[project-template ADR-0126](project-template/0126-published-urls-come-from-a-configured-origin.md) says that the request
contributes nothing to a URL the server publishes.

## Decision

1. The interactive surface's login redirect is root-relative (`/acme/account/login?ReturnUrl=...`). It targets the same
   origin, and every other redirect of the surface is already relative, so the `Host` contributes nothing
   ([project-template ADR-0126](project-template/0126-published-urls-come-from-a-configured-origin.md) decision 2 allows
   a relative same-origin link).
2. The BFF needs an absolute `redirect_uri` (RFC 6749 §3.1.2), so it takes a required `Bff:PublicOrigin`: the absolute URL
   the BFF is published at — scheme, host, optional port and path base. Startup validates it: absolute, `https` outside
   Development (`http` is allowed in Development), no user info, query, fragment or trailing `/`, and written in
   canonical form. The `redirect_uri` is `PublicOrigin + PathPrefix + "/callback"` and the `post_logout_redirect_uri` is
   `PublicOrigin + returnUrl`. The BFF's local redirects stay relative under the request's path base.
3. Startup validation replaces the login handler's runtime check that the callback URI uses `https`, so the check and the
   handler's `IHostEnvironment` dependency are removed.

## Options considered

- Build the URLs from the request and rely on `AllowedHosts`. Rejected by
  [project-template ADR-0126](project-template/0126-published-urls-come-from-a-configured-origin.md).
- Take the URL from `Bff:Authority`. Rejected: it is the OpenID Provider's origin, not the BFF's.
- A relative `redirect_uri`. Rejected: OAuth requires an absolute one.
- A configured origin (chosen).

## Consequences

- **Breaking:** every BFF configures `Bff:PublicOrigin`; the host fails to start without a valid one.
- A forged `Host` changes no URL the login redirect or the BFF publishes.
- A BFF that is reachable under several names publishes one of them.
- Comparisons that still read the request's `Host` (the client-assertion `aud` and the DPoP `htu`) compare and do not
  publish, so they are left to a follow-up of
  [NCodeGroup/Identity#493](https://github.com/NCodeGroup/Identity/issues/493).

## References

- [`BffNavigation.cs`](../../src/NCode.Identity.Bff/Navigation/BffNavigation.cs),
  [`BffOptionsValidator.cs`](../../src/NCode.Identity.Bff/Options/BffOptionsValidator.cs) and
  [`DefaultRegistration.cs`](../../src/NCode.Identity.OpenId.Authentication.Interactive/DefaultRegistration.cs) of the
  interactive package.
- [NCodeGroup/Identity#493](https://github.com/NCodeGroup/Identity/issues/493).
