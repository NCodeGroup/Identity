# 187. A signed request object is short-lived and addressed to the issuer

- **Status:** Accepted
- **Date:** 2026-10-20
- **Deciders:** NCode Group

## Context

A client sends a signed request object in two places. The authorization endpoint takes one inline as `request` or by
reference as `request_uri` (RFC 9101, JAR). The CIBA backchannel authentication endpoint takes a signed authentication
request (CIBA Core §7.1.1). Both loaders validated `nbf`, `iat`, and `exp` and required `exp`, but neither bounded the
lifetime. A pre-generated request object therefore stayed usable until whatever `exp` its signer chose.

[ADR-0124](./0124-jwt-lifetime-cap-and-client-assertion-max-lifetime.md) added `TokenLifetimeRequirement.MaxLifetime`
for client assertions and left request objects to this decision. The specifications set these rules for request
objects:

- **FAPI 1.0 Advanced §5.2.2** and **FAPI 2.0 Message Signing §5.3.1** require `nbf` and `exp`, with `exp` no more than
  60 minutes after `nbf` and `nbf` no more than 60 minutes in the past. They also require `aud` to be the issuer.
- **RFC 9101 §4** and **OpenID Connect Core §6.1** say the `aud` of a request object should be the authorization
  server's issuer.
- **CIBA Core §7.1.1** requires `aud`, `iss`, `exp`, `iat`, `nbf`, and `jti` on a signed authentication request.
- **RFC 9101 §10.8** recommends the explicit `typ` `oauth-authz-req+jwt`, but OpenID Connect Core clients, the OpenID
  conformance suite, and FAPI 2.0 Message Signing do not send or require it.

The authorization endpoint checked `aud` only when `request_object_expected_audience` was set. The CIBA loader also
validated with the JWT library's default 5-minute clock skew rather than the client's `clock_skew` setting.

## Decision

- **The JWT library can require `nbf` and `iat`.** `TokenLifetimeRequirement` and `ValidateTokenLifeTimeOptions` gain
  `RequireNotBefore` and `RequireIssuedAt`. A token without a required claim fails with a message that names it.
- **`request_object_max_lifetime` bounds a request object's lifetime.** It is a client-scoped `TimeSpan` setting with a
  `Min` merge, so a tenant or a security profile sets a ceiling no client can raise. It defaults to 60 minutes, the
  FAPI bound, and the FAPI 2.0 profile pins its ceiling at 60 minutes. Both loaders apply it as `MaxLifetime`. The
  lifetime is measured from the earlier of `iat` and `nbf`, so with `exp` not yet passed it also bounds how far in the
  past `nbf` can be.
- **`request_object_nbf_required` makes `nbf` mandatory at the authorization endpoint.** It is a client-scoped `bool`
  with an `Or` merge. It defaults to `false`, because OpenID Connect Core does not require `nbf`, and the FAPI 2.0
  profile pins it to `true`.
- **The authorization endpoint always validates `aud`.** It expects `request_object_expected_audience` when that is
  set and the tenant's issuer otherwise. A request object without the issuer in `aud` is refused with
  `invalid_request_object`.
- **A CIBA signed authentication request carries every claim CIBA requires.** The loader requires `iat`, `nbf`, and
  `exp`, applies `request_object_max_lifetime`, requires a non-empty `jti`, and validates with the client's
  `clock_skew`, the same as the authorization endpoint.
- **The explicit `typ` is not required.** A request object is bound to its purpose by `iss` equal to the client,
  `aud` equal to the issuer, and the client's signature. Requiring `oauth-authz-req+jwt` would refuse every OpenID
  Connect Core client and the conformance suite.

## Options considered

- **Require `typ` `oauth-authz-req+jwt`.** Rejected as the default. It breaks interoperability with OpenID Connect Core
  clients, and the audience and issuer checks already keep a request object from being confused with another JWT
  the client signs.
- **Refuse a request object whose `typ` is present but different.** Not implemented. It would catch a client that
  signs another typed JWT with the same key, but no profile requires it. It can be added as a setting later.
- **Default `request_object_max_lifetime` lower than 60 minutes.** Rejected. Clients pre-generate request objects and
  host them at a `request_uri`. Sixty minutes is the bound FAPI sets, and a tenant can lower it.
- **Keep validating `aud` only when configured.** Rejected. RFC 9101 and FAPI expect the issuer, and an unchecked `aud`
  lets a request object meant for another authorization server be replayed here.

## Consequences

- A request object without the issuer in `aud`, or valid for more than 60 minutes, is now refused. A client that
  omitted `aud` must add it. The family is pre-release, so this is a clean cutover.
- Under the FAPI 2.0 profile, a request object without `nbf` is refused, and no client can raise the 60-minute ceiling.
- A CIBA signed authentication request without `iat`, `nbf`, or `jti` is refused, and the CIBA loader uses the
  client's clock skew.
- The remaining request-object hardening listed in [ADR-0124](./0124-jwt-lifetime-cap-and-client-assertion-max-lifetime.md)
  is complete.

## References

- `src/NCode.Identity.Jwt/Validation/Handlers/TokenLifetimeRequirementHandler.cs`
- `src/NCode.Identity.OpenId.Authentication/Endpoints/Authorization/Handlers/DefaultLoadAuthorizationRequestHandler.cs`
- `src/NCode.Identity.OpenId.Authentication/Endpoints/BackchannelAuthentication/Handlers/DefaultLoadBackchannelAuthenticationRequestHandler.cs`
- `src/NCode.Identity.OpenId.Authentication/Settings/Fapi2SecurityProfile.cs`
- [RFC 9101 — JWT-Secured Authorization Request](https://datatracker.ietf.org/doc/html/rfc9101)
- [FAPI 2.0 Message Signing](https://openid.net/specs/fapi-2_0-message-signing.html)
- [FAPI 1.0 Advanced §5.2.2](https://openid.net/specs/openid-financial-api-part-2-1_0.html)
- [OpenID Connect CIBA Core §7.1.1](https://openid.net/specs/openid-client-initiated-backchannel-authentication-core-1_0.html)
- Backlog `F40` ([`BACKLOG-COMPLETED.md`](../../BACKLOG-COMPLETED.md))
