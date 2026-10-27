# 189. A retired tenant identifier or issuer is never reissued

- **Status:** Accepted
- **Date:** 2026-10-20
- **Deciders:** NCode Group

## Context

A tenant is named by its creator ([ADR-0159](./0159-a-tenant-is-named-by-its-creator.md)). Its issuer is its identity
in every token it mints ([ADR-0129](./0129-the-tenant-issuer-is-the-single-authoritative-origin.md)). Before this
decision, deleting a tenant freed both. A new tenant could then take the name `acme`, and with it an issuer that relying
parties had configured to trust the old tenant. Those relying parties would fetch the new tenant's JWKS and accept its
tokens. Changing a tenant's issuer freed the old issuer in the same way.

Auth0 never reuses a deleted tenant's name. A relying party's trust is keyed by the issuer string, not by a database
row, so the server must remember an identity after the tenant that held it is gone.

## Decision

- **The tenant store keeps two tombstone tables.**
  - `RetiredTenants` holds the identifier of every deleted tenant, with its normalized form.
  - `RetiredIssuers` holds every issuer a tenant gave up, with its normalized form and the identifier of the tenant
    that held it.

  Each normalized column has a unique index. The tables are separate because they answer different questions: whether
  an identifier is free, and who may take an issuer back.
- **The store retires an identity in the same unit of work as the change that frees it.**
  - `TenantStore.RemoveAsync` retires the tenant's identifier and, if it has one, its issuer.
  - `TenantStore.UpdateAsync` retires the previous issuer when the normalized issuer changes, including when it is
    cleared. A change of letter case only is not a new issuer, so it retires nothing.

  No caller of the store can delete a tenant or move its issuer without the tombstone, because the store writes it.
- **`ITenantResolver.ValidateTenantAsync` rejects a retired identity as a conflict.**
  - A tenant identifier in `RetiredTenants` is a conflict ("never reissued").
  - An issuer in `RetiredIssuers` is a conflict unless the tenant being validated is the one that gave it up.

  The management API maps a conflict to `409 Conflict` on create and update. The address preview
  (`GET /api/tenants/{tenantId}/address`) runs the same validation, so it reports a retired issuer too, and so does the
  issuer a strategy derives from its template ([ADR-0165](./0165-every-served-tenant-has-an-issuer-and-its-strategy-supplies-the-default.md)).
- **Only the tenant that gave up an issuer can take it back.** Tenant identifiers are never reissued, so the former
  holder is always the same tenant, and relying parties that trusted the issuer trusted that tenant. The retired row
  stays when the tenant takes the issuer back, so retiring it again adds nothing.
- **`ITenantStore` exposes the lookups** as `IsTenantIdRetiredAsync` and `GetRetiredIssuerHolderOrDefaultAsync`.
- **A tenant the deployment requires cannot be deleted.** The root tenant is created on demand under every strategy,
  and the static single-tenant strategy also creates its configured tenant. Because a deleted identifier is never
  reissued, deleting either would make every later attempt to create it fail, and with it every request. The strategy
  reports these tenants through `ITenantStrategy.IsRequiredTenant`, which `ITenantResolver` delegates to, so a custom
  strategy can name its own. The management API answers `409 Conflict` to a delete of a required tenant, before it
  checks for dependents.

## Options considered

- **One tombstone table for identifiers and issuers.** Rejected. An issuer row needs its former holder and an
  identifier row does not, and a single table would mix two meanings in one column.
- **Retire identities in the management endpoint instead of the store.** Rejected. A seeder, an importer, or a
  consumer's own code that deletes a tenant through `ITenantStore` would bypass it.
- **An operator API to release a retired issuer, with an audit record.** Not provided. Auth0 never reuses a name, and a
  release is the one action that lets a relying party accept a stranger's tokens. An operator who must release an issuer
  deletes its `RetiredIssuers` row in the database, outside the product.
- **Record when each identity was retired.** Not needed. The rows' identifiers come from the IdGen generator, which
  encodes a timestamp, and the management audit log records the delete or update that retired it.

## Consequences

- A tenant identifier, once deleted, can never be used again, and an issuer can never move to another tenant. An
  operator who deletes a tenant by mistake must create it under a new identifier.
- Every tenant validation reads `RetiredTenants`, and a validation with an issuer may also read `RetiredIssuers`. Both
  are single-row lookups on a unique index.
- The tombstone tables grow with every delete and every issuer change. They hold no secrets, so they are not pruned.
- The `RetireTenantIdentifiersAndIssuers` migration only adds the two tables and their indexes.

## References

- `src/NCode.Identity.OpenId.Persistence.EntityFramework.Core/Entities/RetiredTenantEntity.cs`
- `src/NCode.Identity.OpenId.Persistence.EntityFramework.Core/Entities/RetiredIssuerEntity.cs`
- `src/NCode.Identity.OpenId.Persistence.EntityFramework.Core/Stores/TenantStore.cs`
- `src/NCode.Identity.OpenId.Core/Tenants/DefaultTenantResolver.cs`
- [ADR-0129](./0129-the-tenant-issuer-is-the-single-authoritative-origin.md)
- [ADR-0159](./0159-a-tenant-is-named-by-its-creator.md)
- [ADR-0165](./0165-every-served-tenant-has-an-issuer-and-its-strategy-supplies-the-default.md)
- Issue [#109](https://github.com/NCodeGroup/Identity/issues/109)
