# 78. Resource indicators (RFC 8707) bind and narrow the access-token audience

- **Status:** Accepted
- **Date:** 2026-10-12
- **Deciders:** NCode.Identity maintainers

## Context

[ADR-0027](0027-access-token-audience-from-resource-servers.md) derives an access token's `aud` claim
**entirely from the token's effective scopes**: `IClientScopeService.ResolveAudiencesAsync` (and, for
credential material, `IResourceServerCredentialResolver`) maps the effective scopes to the enabled
resource servers that own them, and the token targets **all** of them. A client that holds scopes for
several APIs therefore always receives a token audienced to **every** matching resource server, with no
way to ask for a token scoped to just one of them.

[RFC 8707 (Resource Indicators for OAuth 2.0)](https://datatracker.ietf.org/doc/html/rfc8707) defines the
`resource` request parameter precisely for this: a client names the protected resource(s) a token is for,
the authorization server validates them, and the issued token's audience is **narrowed** to the requested
resources. This is also a prerequisite for FAPI-grade deployments (issue [#54](https://github.com/NCodeGroup/Identity/issues/54)) where tokens must be
tightly audience-bound.

## Decision

Model `resource` as a first-class, multi-valued request parameter on both the authorization and token
requests, and make it **narrow** the scope-derived audience rather than replace the scope model.

- **Parameter.** `resource` is a repeated string parameter (`OpenIdCommonParameters.Resources`,
  `OpenIdConstants.Parameters.Resource`) carried on `IAuthorizationRequest`/`ITokenRequest`. Its values
  are matched (ordinal) against resource-server **identifiers** (audiences); resource identifiers are the
  authority, so no separate absolute-URI syntactic constraint is imposed.
- **Validation is layered and centralized.**
    - At the **authorization endpoint**, each requested resource must be a known, **enabled** resource
      server for the tenant (`IClientScopeService.GetEnabledResourceServerIdentifiersAsync`); otherwise
      the request is rejected with `invalid_target`.
    - At the **token endpoint** for the authorization-code grant, a requested resource must be **within**
      the resources authorized at the authorization endpoint (`invalid_target` otherwise).
    - The single **choke point** for every grant is `IResourceServerCredentialResolver.ResolveAsync`,
      which now also takes the requested resources. When non-empty it keeps only the resource servers in
      that set, and rejects (`invalid_target`) any requested resource that does not resolve to an enabled
      resource server owning one of the token's effective scopes. Because every access-token path funnels
      through `DefaultTokenService` → the resolver, all grants (authorization code, refresh, client
      credentials, password, device code) get resource-narrowing and `invalid_target` enforcement for
      free.
- **Audience binding.** `CreateSecurityTokenRequest.Resources` carries the effective resources into
  `DefaultTokenService`, which threads them into the resolver so the emitted `aud` is the narrowed set.
  Absent any `resource`, behavior is exactly ADR-0027 (audience = every scope-owning resource server).
- **Error code.** A new `invalid_target` error code (`OpenIdConstants.ErrorCodes.InvalidTarget`) and
  `IOpenIdErrorFactory.InvalidTarget(...)` extension carry RFC 8707 rejections.

No discovery-metadata field is advertised for resource indicators: RFC 8707 and RFC 8414 define none.

## Consequences

- A client can obtain narrowly-audienced tokens by naming a `resource`, improving least-privilege and
  unblocking FAPI audience-binding work.
- Resource validation is uniform across grant types because it lives at the credential-resolver choke
  point rather than being duplicated per grant handler.
- The scope model is unchanged: `resource` can only **narrow** within what the granted scopes already
  authorize — it never grants access to a resource the client has no scope for.
- The resolver narrows the tenant's resource servers by resource identifier in addition to scope; both
  filters are pushed into the DBMS by the discrete `IResourceServerStore.FindEnabledByScopesAsync` query
  ([ADR-0080](0080-store-query-methods-push-filtering-to-dbms.md)) rather than scanning the full set in memory.
