# 1387. The client-assertion audience and the DPoP `htu` are compared with the tenant's published URLs

- **Status:** Accepted
- **Date:** 2026-11-02
- **Deciders:** NCode Group
- **Refines:** [project-template ADR-0126](project-template/0126-published-urls-come-from-a-configured-origin.md),
  [ADR-1365](1365-the-bff-and-the-login-redirect-take-no-url-from-the-requests-host.md)

## Context

Two checks bind a client's message to the endpoint that receives it, and both built that endpoint's URL from the
request's `scheme://host`:

- `ClientAssertionClientAuthenticationHandler.GetValidAudiences` accepted, besides the tenant's issuer, the URL
  `{request.Scheme}://{request.Host}{PathBase}{Path}` as a client assertion's `aud` (OpenID Connect Core §9).
- `DefaultProofOfPossessionService` compared a DPoP proof's `htu` (RFC 9449 §4.3) with the same request-built URL, at
  the token endpoint and at the OP's own protected resources.

A forged or proxied `Host` therefore chose which `aud` and `htu` values the server accepted: an assertion or a proof
minted for another server's endpoint was accepted here when the request named that server's host.
[ADR-1365](1365-the-bff-and-the-login-redirect-take-no-url-from-the-requests-host.md) removed the request's `Host` from
the URLs the server publishes and left these comparisons to a follow-up.

## Decision

1. The URL of the endpoint receiving the request is one the tenant publishes: its authority comes from the tenant's base
   address (its issuer) or, when the tenant has one, its mutual-TLS endpoint-alias authority (RFC 8705 §5), and only
   its path comes from the request, which routing matched to the endpoint. This is how
   `OpenIdLinkGeneratorExtensions.GetTenantUriByName` builds the endpoints the discovery document publishes, so the
   compared URL is the one a client reads from discovery.
2. A client assertion's `aud` may be the tenant's issuer, the endpoint's URL under the tenant's base address, or its URL
   under the mutual-TLS alias authority. A security profile may still restrict it further (FAPI's issuer-only
   audience).
3. A DPoP proof's `htu` is compared with one of those URLs: the one under the mutual-TLS alias authority when the
   request was sent to that authority's host and port, otherwise the one under the tenant's base address. The request's
   `Host` only selects among the tenant's own published origins; it never adds one.

## Options considered

- Keep the request's URL and rely on `AllowedHosts` or the reverse proxy. Rejected by
  [project-template ADR-0126](project-template/0126-published-urls-come-from-a-configured-origin.md): the request
  contributes nothing to a URL the server trusts.
- Accept only the issuer as the assertion audience. Rejected as the default: OpenID Connect Core §9 also allows the
  token endpoint's URL, and many clients send it; FAPI's profile already narrows it to the issuer.
- Validate the `htu` against every published URL at once. Rejected: RFC 9449 binds a proof to the URL the request was
  sent to, so a proof for the alias host must not be accepted on the base host or the reverse.
- The tenant's published URLs (chosen).

## Consequences

- **Breaking:** a client assertion whose `aud` names the endpoint under a host the tenant does not publish, and a DPoP
  proof whose `htu` does, are rejected. A deployment reachable under several names accepts only its issuer's origin and
  its mutual-TLS alias.
- A forged `Host` no longer widens the `aud` or `htu` values the server accepts.
- A tenant without an issuer is not served
  ([ADR-1165](1165-a-tenants-stored-issuer-is-its-single-origin-and-its-mtls-alias-a-routed-second-host.md)), so the base address is always the
  configured origin.

## References

- [`TenantRequestUri.cs`](../../src/NCode.Identity.OpenId.Authentication/Endpoints/TenantRequestUri.cs),
  [`ClientAssertionClientAuthenticationHandler.cs`](../../src/NCode.Identity.OpenId.Authentication/Clients/Handlers/ClientAssertionClientAuthenticationHandler.cs)
  and
  [`DefaultProofOfPossessionService.cs`](../../src/NCode.Identity.OpenId.Authentication/Tokens/ProofOfPossession/DefaultProofOfPossessionService.cs).
- [NCodeGroup/identity#518](https://github.com/NCodeGroup/identity/issues/518).
