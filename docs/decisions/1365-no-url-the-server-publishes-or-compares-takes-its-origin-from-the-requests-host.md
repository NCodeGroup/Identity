# 1365. No URL the server publishes or compares takes its origin from the request's Host

- **Status:** Accepted
- **Date:** 2026-11-02
- **Deciders:** NCode Group
- **Refines:** [project-template ADR-0126](project-template/0126-published-urls-come-from-a-configured-origin.md)

## Context

[project-template ADR-0126](project-template/0126-published-urls-come-from-a-configured-origin.md) says that the request
contributes nothing to a URL the server publishes. A URL built from the request's `scheme://host` lets a forged or
proxied `Host` choose where it points. Five places need a URL that names the server, and each takes its origin from
configuration or from the tenant, never from the request:

- The interactive surface's login redirect (`BuildLoginRedirectUri`).
- The BFF's OAuth `redirect_uri` and `post_logout_redirect_uri`, which RFC 6749 §3.1.2 requires to be absolute.
- The URL of the endpoint that receives a client's message, which two checks compare with what the client sent: a
  client assertion's `aud` (OpenID Connect Core §9) and a DPoP proof's `htu` (RFC 9449 §4.3). A forged `Host` would
  otherwise choose which `aud` and `htu` values the server accepts, so an assertion or a proof minted for another
  server's endpoint would be accepted when the request named that server's host.

## Decision

No URL the server publishes or compares takes its origin from the request's `Host`.

1. **The interactive login redirect is root-relative.** `BuildLoginRedirectUri` produces
   `/acme/account/login?ReturnUrl=...`, like every other redirect of the surface, so the `Host` contributes nothing
   ([project-template ADR-0126](project-template/0126-published-urls-come-from-a-configured-origin.md) allows a
   same-origin relative link).
2. **Each BFF frontend has a required `Bff:Frontends:<name>:PublicOrigin`** (`BffFrontendOptions.PublicOrigin`), the
   absolute URL the frontend is published at. Startup validates it: absolute, `https` outside Development, no user
   info, query, fragment or trailing `/`, and written in canonical form. A BFF without a valid `PublicOrigin` does not
   start.
3. **`BffNavigation` builds the BFF's URLs from it.** The `redirect_uri` is `PublicOrigin + PathPrefix + "/callback"`
   and the `post_logout_redirect_uri` is `PublicOrigin + returnUrl`. The BFF's local redirects stay relative under the
   request's path base. Startup validation is the only `https` check of the callback, so the login handler has no
   runtime check and no `IHostEnvironment` dependency.
4. **The URL of the endpoint receiving a client's message is one the tenant publishes** (`TenantRequestUri`). Its
   authority is the tenant's base address (its issuer) or, when the tenant has one, its mutual-TLS endpoint alias
   authority (RFC 8705 §5). Only the path comes from the request, which routing matched to the endpoint.
   `OpenIdLinkGeneratorExtensions.GetTenantUriByName` builds discovery's endpoints the same way, so the compared URL is
   the one a client reads from discovery.
5. **A client assertion's `aud` may be** the tenant's issuer, the endpoint's URL under the base address, or its URL
   under the alias authority (`TenantRequestUri.GetPublishedUris`). A security profile may restrict it to the issuer
   only (FAPI).
6. **A DPoP proof's `htu` is compared** by `DefaultProofOfPossessionService`, at the token endpoint and at the OP's
   own protected resources, with the URL under the alias authority when the request was sent to that authority's host
   and port, and otherwise the URL under the base address (`TenantRequestUri.GetPublishedUri`). The `Host` only
   selects among the tenant's own origins and never adds one.
7. **A tenant without an issuer is not served**
   ([ADR-1165](1165-a-tenants-stored-issuer-is-its-single-origin-and-its-mtls-alias-a-routed-second-host.md)), so the
   base address is always a configured origin.

## Options considered

- Build the URLs from the request and rely on `AllowedHosts` or the reverse proxy. Rejected by
  [project-template ADR-0126](project-template/0126-published-urls-come-from-a-configured-origin.md): the request
  contributes nothing to a URL the server publishes or trusts.
- Take the BFF's URL from `Bff:Authority`. Rejected: it is the OpenID Provider's origin, not the BFF's.
- A relative `redirect_uri`. Rejected: OAuth requires an absolute one.
- Accept only the issuer as the assertion audience by default. Rejected: OpenID Connect Core §9 also allows the
  endpoint's URL, and many clients send it; FAPI's profile already narrows it to the issuer.
- Validate the `htu` against every published URL at once. Rejected: RFC 9449 binds a proof to the URL the request was
  sent to, so a proof for the alias host must not be accepted on the base host or the reverse.
- A configured origin for the BFF and the tenant's published URLs for the comparisons (chosen).

## Consequences

- A forged `Host` changes no published URL and widens no accepted `aud` or `htu`.
- A BFF or an OP that is reachable under several names publishes and accepts one origin, plus the mutual-TLS alias for
  the OP.
- A BFF without a valid `PublicOrigin` fails at startup instead of publishing a wrong callback.
- A client assertion whose `aud`, or a DPoP proof whose `htu`, names the endpoint under a host the tenant does not
  publish is rejected.

## References

- [`BffNavigation.cs`](../../src/NCode.Identity.Bff/Navigation/BffNavigation.cs),
  [`BffOptionsValidator.cs`](../../src/NCode.Identity.Bff/Options/BffOptionsValidator.cs) and
  [`DefaultRegistration.cs`](../../src/NCode.Identity.OpenId.Authentication.Interactive/DefaultRegistration.cs) of the
  interactive package.
- [`TenantRequestUri.cs`](../../src/NCode.Identity.OpenId.Authentication/Endpoints/TenantRequestUri.cs),
  [`ClientAssertionClientAuthenticationHandler.cs`](../../src/NCode.Identity.OpenId.Authentication/Clients/Handlers/ClientAssertionClientAuthenticationHandler.cs)
  and
  [`DefaultProofOfPossessionService.cs`](../../src/NCode.Identity.OpenId.Authentication/Tokens/ProofOfPossession/DefaultProofOfPossessionService.cs).
- [project-template ADR-0126](project-template/0126-published-urls-come-from-a-configured-origin.md),
  [ADR-1165](1165-a-tenants-stored-issuer-is-its-single-origin-and-its-mtls-alias-a-routed-second-host.md) and
  [ADR-1432](1432-one-bff-serves-several-frontends-each-selected-per-request-by-an-open-rule.md).
- [NCodeGroup/identity#493](https://github.com/NCodeGroup/identity/issues/493) and
  [NCodeGroup/identity#518](https://github.com/NCodeGroup/identity/issues/518).
