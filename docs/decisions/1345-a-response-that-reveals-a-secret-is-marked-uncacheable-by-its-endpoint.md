# 1345. A response that reveals a secret is marked uncacheable by its endpoint

- **Status:** Accepted
- **Date:** 2026-11-01
- **Deciders:** NCode Group
- **Refines:** [project-template ADR-0146](project-template/0146-secrets-are-never-logged-compared-in-constant-time-and-shown-once.md)

## Context

Template ADR-0146 rule 5 says a response that reveals a secret is never cached. A browser, a proxy or a gateway that
keeps a copy of a one-time secret defeats showing it once ([ADR-1050](1050-secret-material-revealed-once-on-create.md)).

Six routes reveal a secret:

- `POST api/tenants/{tenantId}/clients/{clientId}/secrets` and
  `POST api/tenants/{tenantId}/clients/{clientId}/secrets/{secretId}/rotate` (`ClientApiEndpointHandler`) return the new
  secret's `CreatedSecretResource`, material included.
- `POST api/tenants/{tenantId}/resource-servers/{resourceServerId}/secrets` (`ResourceServerApiEndpointHandler`) returns
  a `CreatedSecretResource`.
- `POST api/tenants/{tenantId}/grants/initial-access-tokens` (`GrantApiEndpointHandler`) returns the initial access
  token.
- `POST /oauth2/register` returns the `client_secret` and the `registration_access_token`, and
  `GET|PUT|DELETE /oauth2/register/{client_id}` (`DefaultClientRegistrationEndpointHandler`) answer with the client's
  registration, which carries the same credentials. RFC 7591 §3.2.1 and RFC 7592 §3 show these responses with
  `Cache-Control: no-store` and `Pragma: no-cache`.

The token endpoint and the authorization response send `Cache-Control: no-store, no-cache, max-age=0` and
`Pragma: no-cache` (RFC 6749 §5.1). Without `no-store`, RFC 9111 §3 leaves storing a response to the cache, so a route
that reveals a secret carries the directive itself.

The tenant and server secret creates, and every import, read and list, return metadata only: a tenant's or a server's
key never crosses the API, and the other routes never repeat material
([ADR-1050](1050-secret-material-revealed-once-on-create.md)). They carry no secret to protect.

## Decision

An endpoint whose response can carry secret material is mapped `.WithNoStore()`
(`NCode.Identity.OpenId.Authentication.Endpoints`). Its endpoint filter calls `HttpResponse.SetNoStore()` before the
handler runs, so success and error responses alike carry `Cache-Control: no-store, no-cache, max-age=0` and
`Pragma: no-cache`. The six routes above are mapped this way.

- **Responses written before the filter carry no secret.** A `400` for a body that fails to bind, or a `401` or `403`
  from an outer filter, is produced before the filter runs and is not marked.
- **`no-store` is not alone.** `no-cache, max-age=0` and `Pragma: no-cache` accompany it for HTTP/1.0 caches and for
  caches that ignore `no-store`, as they do on the token endpoint.
- **A new secret-revealing endpoint opts in the same way**, whether it lives in this family or in an extending package.
  `.WithNoStore()`, its filter and `HttpResponse.SetNoStore()` live in
  `NCode.Identity.OpenId.Authentication.Abstractions` so that the management package, which does not reference the
  authentication package, uses the one mechanism.

## Options considered

- **Write the headers in each handler.** Rejected: a new route or an early return can forget them, and the mapping no
  longer shows which routes reveal a secret.
- **A filter on the whole management route group.** Rejected: it misses the registration endpoint, and it marks routes
  that other packages map into the group.
- **A middleware that reads endpoint metadata.** Rejected: it needs host middleware ordering, which a filter does not.
- **`.WithNoStore()` on each endpoint.** Chosen: the mapping lists the routes that reveal a secret, and no handler path
  can skip it.

## Consequences

- The secret-revealing responses are never cached by a client or an intermediary.
- `HttpResponse.SetNoStore()` is public in `NCode.Identity.OpenId.Authentication.Abstractions`; the token endpoint and
  the authorization response writer call it too.
- Smell: a route that returns a `CreatedSecretResource`, a token or a `client_secret` without `.WithNoStore()`.
- Marking the pushed authorization, device authorization and backchannel authentication endpoints, whose responses carry
  one-time values, is tracked in [#488](https://github.com/NCodeGroup/Identity/issues/488).

## References

- [`NoStoreEndpointConventionBuilderExtensions`](../../src/NCode.Identity.OpenId.Authentication.Abstractions/Endpoints/NoStoreEndpointConventionBuilderExtensions.cs)
- [`NoStoreResponseExtensions`](../../src/NCode.Identity.OpenId.Authentication.Abstractions/Endpoints/NoStoreResponseExtensions.cs)
- [`NoStoreEndpointFilter`](../../src/NCode.Identity.OpenId.Authentication.Abstractions/Endpoints/NoStoreEndpointFilter.cs)
- [`ClientApiEndpointHandler`](../../src/NCode.Identity.OpenId.Management/Endpoints/Clients/ClientApiEndpointHandler.cs)
- [`ResourceServerApiEndpointHandler`](../../src/NCode.Identity.OpenId.Management/Endpoints/ResourceServers/ResourceServerApiEndpointHandler.cs)
- [`GrantApiEndpointHandler`](../../src/NCode.Identity.OpenId.Management/Endpoints/Grants/GrantApiEndpointHandler.cs)
- [`DefaultClientRegistrationEndpointHandler`](../../src/NCode.Identity.OpenId.Authentication/Endpoints/Registration/DefaultClientRegistrationEndpointHandler.cs)
- [`SecretRevealingManagementResponseTests`](../../tests/NCode.Identity.OpenId.Integration.Tests/Endpoints/SecretRevealingManagementResponseTests.cs)
- [`ClientRegistrationEndpointTests`](../../tests/NCode.Identity.OpenId.Integration.Tests/Endpoints/ClientRegistrationEndpointTests.cs)
- [ADR-1050](1050-secret-material-revealed-once-on-create.md),
  [ADR-1320](1320-a-rotated-client-secret-overlaps-its-replacement-until-it-expires-or-is-deleted.md)
- Issue [#473](https://github.com/NCodeGroup/Identity/issues/473)
- RFC [7591](https://www.rfc-editor.org/rfc/rfc7591), [7592](https://www.rfc-editor.org/rfc/rfc7592) and
  [9111](https://www.rfc-editor.org/rfc/rfc9111)
