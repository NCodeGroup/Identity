# 29. Token introspection resolves JWT access tokens and refresh-token grants

- **Status:** Accepted
- **Date:** 2026-09-30
- **Deciders:** NCode Group

## Context

<see href="https://datatracker.ietf.org/doc/html/rfc7662">RFC 7662</see> defines a token introspection endpoint that
lets a protected resource (or any authorized caller) ask the authorization server whether a token is currently active
and, if so, obtain its metadata (scope, subject, audience, expiry, …). The response is a flat JSON object with a
required boolean `active` member and any number of optional claims; an inactive token returns just `{"active": false}`
so the endpoint never leaks why a token failed.

This server issues two token shapes with different natures (see [ADR-0028](0028-token-revocation-refresh-tokens-only.md)):
an **access token** is a self-contained, signed JWT whose active state is a function of its signature, issuer, and
lifetime; a **refresh token** is a persisted grant whose active state is the `Status` of a server-side row. Introspection
must answer for both. The server already has the building blocks — `IJsonWebTokenService.ValidateJwtAsync` for the JWT
and `IPersistedGrantService` for the grant — so introspection is a composition of existing services, not new machinery.

## Decision

**The introspection endpoint (`/oauth2/introspect`) resolves active state for both token shapes and returns a
discovery-style, extensible JSON result.** The endpoint follows the established OAuth-endpoint and mediator conventions
([ADR-0001](0001-mediator-vs-dependency-injection-logic-classes.md)): a thin `IOpenIdEndpointProvider` authenticates the
client and parses the form into a typed `ITokenIntrospectionRequest`, then delegates to an `IntrospectTokenCommand`
mediator handler. A missing `token` parameter is the one hard error (`400 invalid_request`); every other authenticated
request returns `200 OK` with an `IntrospectionResult`.

Three rules shape the design:

- **Resolution order.** The default handler tries the token as a **JWT access token** first (validated against the
  tenant signing keys, issuer, and lifetime), then — if that fails to parse or validate — as a **refresh-token grant**
  (`Status == Active`). A token that matches neither yields `{"active": false}`. The JWT path swallows parse failures so
  a non-JWT value simply falls through to the grant path rather than erroring.
- **Extensible result.** `IntrospectionResult` exposes the required `active` member plus a `[JsonExtensionData]`
  dictionary that flattens arbitrary claims into the top-level response — the same shape the discovery endpoint uses for
  its metadata. The default handler copies the JWT payload claims verbatim, or populates
  `client_id`/`scope`/`sub` for a refresh-token grant. Additional `ICommandHandler<IntrospectTokenCommand>` handlers may
  enrich or override the result; the default handler runs at `High` mediator priority and no-ops once a prior handler has
  marked the token active.
- **Caller authorization.** The endpoint requires client authentication, so only a registered client (public or
  confidential) can introspect. The server does not further restrict _which_ client may introspect _which_ token — any
  authenticated client may probe any token value. This matches RFC 7662's model where the endpoint is for trusted
  protected resources; a finer-grained policy (for example, "only the token's audience may introspect it") can be added
  later as an additional mediator handler without changing the contract.

## Options considered

- **Introspect access tokens only.** Rejected: a refresh token is a first-class token a resource/confidential client may
  legitimately introspect, and the grant state is already available — omitting it would make the endpoint half a
  feature.
- **Introspect refresh tokens only (treat the JWT as opaque).** Rejected: the common introspection target is the access
  token, and the server can validate its own JWTs cheaply without a round-trip to a persisted store.
- **Return `404`/`400` for an unknown or expired token.** Rejected: it violates RFC 7662 and turns the endpoint into an
  oracle; `{"active": false}` is the specified answer.
- **Restrict introspection to the token's audience.** Deferred, not rejected: it is a reasonable hardening, but it needs
  a policy decision on how audiences map to introspecting clients. The mediator shape makes it an additive handler, so
  nothing in this contract blocks it.
- **A fixed, strongly-typed response DTO.** Rejected in favor of the `[JsonExtensionData]` dictionary: claims vary by
  token and by extension handler, and the dictionary keeps the result open for enrichment without a public-API change
  per claim — consistent with the discovery endpoint.

## Consequences

- A client can introspect an access token (getting its full claim set) or a refresh token (getting `client_id`, `scope`,
  and `sub`); anything else is reported inactive. The endpoint auto-advertises as `introspection_endpoint` in discovery.
- Introspection reflects the same lifetimes and revocation state as the rest of the server: a revoked refresh token
  (see [ADR-0028](0028-token-revocation-refresh-tokens-only.md)) reports inactive, while an unexpired access token
  reports active until it expires — the server does not consult a denylist for access tokens.
- The result is extensible: new claims or richer policies arrive as additional `ICommandHandler<IntrospectTokenCommand>`
  registrations, not as changes to the endpoint or the response type.
- Because any authenticated client may introspect any token value, operators should treat the endpoint as a trusted
  resource-server surface; tightening to an audience-scoped policy is a future additive change.

## References

- [ADR-0001](0001-mediator-vs-dependency-injection-logic-classes.md) — the mediator command/handler shape the endpoint
  follows.
- [ADR-0027](0027-access-token-audience-from-resource-servers.md) — the audience-bound access tokens introspection
  validates.
- [ADR-0028](0028-token-revocation-refresh-tokens-only.md) — the sibling client-facing endpoint whose token-shape
  analysis (stateless JWT vs persisted grant) this ADR reuses.
