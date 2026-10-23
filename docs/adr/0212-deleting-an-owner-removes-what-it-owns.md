# 212. Deleting a client or a tenant removes what it owns, its end users' data included

- **Status:** Accepted
- **Date:** 2026-10-22
- **Deciders:** NCode Group

## Context

Every foreign key of the persistence model is `Restrict`, and the stores checked only some of the dependents of a row
before they removed it. Deleting a client that had grants to resource servers, consents, or issued grants failed with a
foreign-key violation, and so did deregistering any dynamically registered client, because the grant of its
registration access token references it. Deleting a tenant whose users had signed in failed the same way, because
sessions and federated principals reference it (`500` on PostgreSQL). Local accounts reference their tenant by
identifier and have no foreign key, so they outlived the tenant silently. The Entity Framework InMemory provider
enforces no foreign key, so no test of the hermetic host saw any of it (backlog `R59`).

## Decision

**Deleting an owner removes what it owns, in the same unit of work.**

1. **A client is removed with everything that exists for it.** Deleting a client removes its secrets with their key
   material, its grants to resource servers, its consents, and every grant issued to it, such as its authorization
   codes, tokens, pushed authorization requests, device and CIBA grants, and its registration access token. Nothing but
   authorization blocks the delete, and `IClientStore.HasDependentsAsync` is removed.
2. **A tenant is refused while it holds an operator's records, and is otherwise removed with everything it owns.** A
   tenant that holds a client or a non-system resource server is refused (`409 Conflict`)
   ([ADR-0169](0169-every-tenant-is-provisioned-with-persisted-keys.md) decision 11). Everything else the tenant owns is
   removed in the same unit of work, the data of its end users included: local accounts and their claims, federated
   principals and identities, sessions, and other grants. The usage ledger is kept
   ([ADR-0199](0199-tenant-usage-is-a-daily-ledger-that-replicas-add-to.md)), and the identifier and the issuer of the
   tenant are retired ([ADR-0189](0189-a-retired-tenant-identifier-or-issuer-is-never-reissued.md)).
3. **The tenant store erases every tenant-owned entity.** The Entity Framework `TenantStore` removes every row of every
   model entity that implements `ISupportTenantEntity` whose `NormalizedTenantId` is the tenant's, and the
   `SecretEntity` rows that those rows reference through `ISupportSecretEntity`. The set of entities is read from the
   model, as the tenant query filter reads it
   ([ADR-0018](0018-tenant-scoped-data-access-at-the-persistence-layer.md)), so an entity that another package
   contributes, such as a local account, is erased with its tenant without a change to the store.
4. **Rows are removed through the change tracker.** Entity Framework orders the deletes. Foreign keys stay `Restrict`,
   and no migration is needed.
5. **A deregistration relies on the store alone.** `DELETE` of a dynamically registered client
   ([ADR-0063](0063-dynamic-client-registration.md)) removes the client through `IClientStore.RemoveAsync`, which
   removes its secrets and its registration access token with it.

## Options considered

- **Report the data of the end users as dependents (`409`).** Rejected: no API removes the users or the sessions of a
  tenant in bulk, and the data of a deleted tenant is unreachable because its identifier is retired, so keeping it is a
  privacy liability (GDPR erasure).
- **Keep the data detached.** Rejected: it leaves orphaned personal data with no owner.
- **Database cascades.** Rejected: SQL Server refuses multiple cascade paths, the Entity Framework InMemory provider
  cascades only the rows that it tracks, and local accounts have no foreign key.
- **A confirmation parameter on the delete.** Rejected: the `409` for operator-created records and the confirmation of
  the console already guard it.
- **Refuse a client while it has secrets.** Rejected: the secrets of a client exist only for it, as the keys of a tenant
  do ([ADR-0169](0169-every-tenant-is-provisioned-with-persisted-keys.md)), and Auth0 and Keycloak delete them with it.

## Consequences

- Deleting a client ends every refresh token and every reference grant issued to it at once. The JWT access tokens that
  were issued run to their expiry.
- Deleting a tenant erases its users irreversibly.
- The store loads every row that it removes into one unit of work, so deleting an owner with a very large number of
  grants is a large unit of work (backlog `R76`).
- The owner role assignments of a deleted client remain (backlog `R75`).
- **Breaking:** `IClientStore.HasDependentsAsync` is removed, and `RemoveAsync` of the client store and of the tenant
  store removes what the resource owns.
- This implements backlog `R59`.

## References

- [ADR-0169](0169-every-tenant-is-provisioned-with-persisted-keys.md) — a tenant's keys and system records are removed
  with it, and a client or a resource server that an operator created blocks the delete.
- [ADR-0189](0189-a-retired-tenant-identifier-or-issuer-is-never-reissued.md) — the identifier and the issuer of a
  deleted tenant are retired.
- [ADR-0199](0199-tenant-usage-is-a-daily-ledger-that-replicas-add-to.md) — the usage ledger outlives the tenant.
- [ADR-0063](0063-dynamic-client-registration.md) — dynamic client registration and its deregistration.
- [ADR-0018](0018-tenant-scoped-data-access-at-the-persistence-layer.md) — tenant-bound data access is scoped at the
  persistence layer.
- Code: `NCode.Identity.OpenId.Persistence.EntityFramework.Stores.ClientStore`,
  `NCode.Identity.OpenId.Persistence.EntityFramework.Stores.TenantStore`,
  `NCode.Identity.OpenId.Authentication.Endpoints.Registration.DefaultClientRegistrationEndpointHandler`.
