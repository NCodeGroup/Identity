# 27. An access token's audience is the resource servers that own its scopes

- **Status:** Accepted
- **Date:** 2026-09-30
- **Deciders:** NCode Group

## Context

An access token's `aud` claim tells a resource server "this token is for you." A resource server validates an incoming
token by checking that its own identifier is in `aud`; a token whose audience is something else must be rejected. So the
audience is the load-bearing link between a token and the API it may call.

The runtime already resolves scopes against the resource-server / client-grant model
([ADR-0026](0026-scope-enforcement-via-resource-servers-and-client-grants.md)): each scope is owned by exactly one
resource server, and a resource server is identified by its audience `Identifier`
([ADR-0023](0023-scopes-and-resources-management-model.md)). The access token's effective scopes therefore already
determine, unambiguously, which resource servers the token targets — but the token's `aud` does not reflect that.

## Decision

**An access token's `aud` is the set of distinct identifiers of the (enabled) resource servers that own its effective
scopes.** `IClientScopeService.ResolveAudiencesAsync` maps the token's effective scopes to those resource-server
identifiers, and the access-token encoder sets `aud` to them (the `aud` claim is a single string for one audience, an
array for several). When no resource-server audience applies — for example a scopeless token — `aud` falls back to the
client id, so a token is never audience-less.

This applies to the **access token only**. The **ID token's** `aud` remains the client id, as OpenID Connect requires.
No `resource` request parameter (RFC 8707) is introduced: the audience is derived from scopes, consistent with the
scope-only validation of [ADR-0026](0026-scope-enforcement-via-resource-servers-and-client-grants.md). Because the
OpenID Connect identity scopes live on the system identity resource server, a token carrying `openid`/`profile`/… is
audienced to that resource server's identifier (its UserInfo audience), which falls out of the same rule rather than
needing a special case.

## Options considered

- **Keep `aud` = client id (the prior behavior).** Rejected: it conflates the party that *requested* the token with the
  party that must *accept* it; a resource server cannot distinguish a token minted for it from any other token the
  client holds, defeating audience validation.
- **Require an explicit `resource` parameter (RFC 8707) to set the audience.** Rejected for now: it adds request surface
  and a second way to say what the scopes already say under Archetype A (one scope, one resource server); it can be
  added later for clients that want to narrow the audience within a single request.
- **Derive `aud` from the effective scopes' resource servers (chosen).** The token's audience is exactly the APIs its
  scopes authorize, with no extra request surface and no drift from the scope model.

## Consequences

- A resource server can validate tokens by checking its `Identifier` against `aud`; a token issued for API *A* is not
  accepted by API *B*.
- Minting an access token performs a tenant-scoped resource-server read to resolve audiences (via the same
  `IClientScopeService` used for scope validation); this per-request read is a caching opportunity left as future work,
  as noted in [ADR-0026](0026-scope-enforcement-via-resource-servers-and-client-grants.md).
- A token spanning scopes from several resource servers carries a multi-valued `aud`; resource servers must accept a
  JSON array `aud` (already required by RFC 7519).
- The scopeless-token fallback to the client id keeps a token from ever having an empty audience; whether a scopeless
  access token should be issued at all is a separate validation concern.

## References

- [ADR-0023](0023-scopes-and-resources-management-model.md) — resource servers are identified by an audience
  `Identifier` that owns its scopes.
- [ADR-0026](0026-scope-enforcement-via-resource-servers-and-client-grants.md) — scope resolution against the
  resource-server / client-grant model, which this decision reuses to resolve audiences.
- [ADR-0025](0025-pre-release-posture-and-auth0-parity-plus.md) — the parity-plus posture under which the audience is
  derived from scopes rather than a separate request parameter.
