# 192. Dynamic registration binds a client to the resource server its initial access token names

- **Status:** Accepted
- **Date:** 2026-10-20
- **Deciders:** NCode Group

## Context

A resource server introspects through a client bound to it, a *resource-server client*
([ADR-0168](0168-introspection-discloses-a-token-only-to-the-party-that-consumes-it.md)). Such a client reads every
access token issued for its resource server, so binding one is a change to the resource server. The caller must be
authorized to update the resource server, which must exist in the client's tenant and must not be a system resource
server. Only the management API could set the binding, through `CreateClientRequest.resourceServerId`.

[RFC 9701](https://www.rfc-editor.org/rfc/rfc9701) §3 lets the authorization server register resource servers
dynamically. A deployment that provisions its APIs automatically needs that: a pipeline creates the resource server and
then has the API register its own introspection client, without holding a management credential at run time. Dynamic
client registration ([RFC 7591](https://www.rfc-editor.org/rfc/rfc7591)) has no standard member that names a resource
server. The registrant must also prove it may act for the resource server, and an open registration request proves
nothing.

## Decision

**The initial access token names the resource server, and the client it registers is bound to it.**

1. **Scoped initial access tokens.**
   - `CreateInitialAccessTokenRequest.resourceServerId` scopes a token to a resource server.
   - Minting a scoped token makes the same checks as binding a client through the management API: the resource server
     must exist in the tenant and must not be a system resource server (`400`), and the caller must be authorized to
     update it (`401` / `403`).
   - The binding is persisted on `InitialAccessTokenGrant.ResourceServerId` and reported on
     `CreatedInitialAccessTokenResource.resourceServerId`.
   - An unscoped token registers ordinary clients, as before.
2. **Registration binds.**
   - `IInitialAccessTokenValidator.ValidateAsync` returns the `InitialAccessTokenGrant` the token carries, or `null`
     when the token does not authorize registration. A replacement validator states the binding the same way.
   - A client registered with a scoped token is bound to the token's resource server. The resource server is checked
     again when the client registers, because it can be deleted or replaced by a system resource server between minting
     and registration. A token whose resource server no longer qualifies is answered `invalid_token` (`401`).
   - A presented token is validated even where anonymous registration is allowed. A scoped token is therefore never
     silently ignored, and an invalid one is refused (`401`).
3. **A bound client is confidential.** Registering a bound client with `token_endpoint_auth_method` `none` is refused
   with `invalid_client_metadata`, and so is an update (RFC 7592) that would make a bound client public. Introspection
   already refuses a public bound client; registration now refuses to create one.
4. **A bound client registers its introspection response protection.** Registration accepts
   `introspection_signed_response_alg`, `introspection_encrypted_response_alg`, and
   `introspection_encrypted_response_enc` (RFC 9701 §6), validated like the `userinfo_*` metadata: an `*_enc` needs its
   `*_alg`, an encrypted response needs the client's `jwks` or `jwks_uri`, and each value must be among the tenant's
   `introspection_*_values_supported`. The client information response reports them. Only a bound client receives a
   JWT introspection response, so an ordinary client that registers them is refused with `invalid_client_metadata`,
   just as introspection refuses an ordinary client a JWT.
5. **The binding is reported and never changes.**
   - The client information response carries `resource_server_id` for a bound client.
   - The member is an extension the server sets. A registration or update request may carry it, and the server ignores
     it like any unrecognized metadata (RFC 7591 §2), so a client that echoes its read response back in an update does
     not fail.
6. **A token registers one client.**
   - Every initial access token, scoped or not, is single-use and expires. Minting bounds the lifetime:
     `lifetimeSeconds` defaults to one day and is at most thirty days (`400` otherwise), so
     `CreatedInitialAccessTokenResource.expiresWhen` always has a value.
   - Registration spends the token with `IInitialAccessTokenValidator.TryConsumeAsync`, after every check has passed
     (the resource server qualifies and the metadata is valid) and immediately before the client is stored.
     `ValidateAsync` peeks first, and answers `null` for a grant whose `PersistedGrant<T>.ConsumedWhen` is set, so a
     spent token authorizes nothing. `ValidateAsync` also refuses a grant without an expiry: every minted token
     carries one, so such a grant was minted by an earlier release without a lifetime.
   - A token that cannot be spent (already spent, expired or revoked) is answered `invalid_token` (`401`) and nothing
     is stored. A registration refused by an earlier check leaves the token unspent, so the caller can correct the
     request and present the token again. A failure storing the client after the spend leaves the token spent: the
     registration fails closed, and the administrator mints another token.
   - The default spends the grant through `IPersistedGrantService.ConsumeOnceOrDefault`, not through `IExpiringKeySet`
     ([ADR-0164](0164-replicas-share-single-use-state-through-an-atomic-expiring-key-set.md)). `IExpiringKeySet` records
     single-use state that has no durable row of its own, such as a proof's `jti`. The token has one in the grant
     store, where it is hash-keyed, expires, and is listed and revoked, and a second record in the key set would have
     to agree with it. The grant row's concurrency token makes the spend atomic across replicas without a shared-state
     adapter: of two registrations that present one token at the same moment, at most one succeeds, and the save of
     the other fails on the concurrency token (mapping that failure to a protocol error is backlog `R66`).

## Options considered

- **Name the resource server in the registration request.** Rejected: the request carries no proof that its sender
  may act for the resource server. The token must carry the authority anyway, and once it does, a request member can
  only agree with it or conflict with it.
- **Scope the token by the resource server's identifier (its audience URI).** Rejected: every other binding in the
  management API names the resource server by `resourceServerId`. The identifier is the audience written into tokens,
  not the key the binding is stored under.
- **Bind through a software statement (RFC 7591 §2.3).** Deferred: a signed software statement could carry the
  resource server, but it needs a trust anchor per publisher. The initial access token is already the authority the
  server issues and revokes.
- **Ignore a presented token when anonymous registration is allowed.** Rejected: a scoped token would then register an
  ordinary client, and nobody would be told.

## Consequences

- A deployment provisions an API end to end: it creates the resource server, mints a token scoped to it, and the API
  registers its introspection client with that token.
- Revoking an unused scoped token, or deleting its resource server, withdraws the authority it carried.
- **Breaking:**
  - `IInitialAccessTokenValidator.ValidateAsync` returns `InitialAccessTokenGrant?` instead of `bool`.
  - A presented initial access token is validated even when anonymous registration is allowed.
  - `IInitialAccessTokenValidator` declares `TryConsumeAsync`, so a replacement validator implements it and returns
    `false` when the token no longer authorizes registration.
  - `CreatedInitialAccessTokenResource.ExpiresWhen` is a `DateTimeOffset`, not a `DateTimeOffset?`, and a minted token
    always carries a lifetime: a token minted without `lifetimeSeconds` expires after one day, and a lifetime beyond
    thirty days is refused (`400`).
  - A token that an earlier release minted without a lifetime authorizes no registration; an administrator mints a new
    one.
- A dynamically registered resource-server client chooses how its introspection responses are signed and encrypted,
  as a client created through the management API does.
- The Management console's mint dialog scopes an initial access token to one of the tenant's non-system resource
  servers, and its one-time reveal names that resource server.

## References

- [ADR-0168](0168-introspection-discloses-a-token-only-to-the-party-that-consumes-it.md) — resource-server clients and
  the disclosure rule.
- [RFC 7591](https://www.rfc-editor.org/rfc/rfc7591) — OAuth 2.0 Dynamic Client Registration.
- [RFC 7592](https://www.rfc-editor.org/rfc/rfc7592) — OAuth 2.0 Dynamic Client Registration Management.
- [RFC 9701](https://www.rfc-editor.org/rfc/rfc9701) — JWT Response for OAuth Token Introspection.
