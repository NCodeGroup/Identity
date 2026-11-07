# 1421. The management console addresses a client and a resource server through their tenant

- **Status:** Accepted
  (amended by [ADR-1466](1466-a-machine-client-belongs-to-the-platform-or-a-team-and-acts-only-within-it.md):
  `ClientDetailPage` takes the client and addresses it through `client.tenantId`, the control-plane tenant for a
  machine client)
- **Date:** 2026-11-05
- **Deciders:** NCode Group
- **Amends:** [ADR-1257](1257-the-management-console-reaches-every-owners-secrets-and-settings-through-one-endpoint-per-operation.md),
  decision 1 (a client and a resource server owner name their tenant)

## Context

The Management API serves clients, resource servers and their scopes only under `/api/tenants/{tenantId}`. No platform
or team route lists, creates or changes them. A client the platform itself uses is registered in a tenant too, the
control-plane tenant
([ADR-1191](1191-the-management-api-authenticates-against-the-control-plane-tenant.md)). A console address that carries
a platform or team scope for a client therefore names a route the API answers with 404, and a type that accepts it
hides the mistake until run time.

## Decision

**A client and a resource server are tenant resources: the console addresses them by the tenant id alone.**

1. **The owner shapes carry the tenant.** In `ManagementOwner`, the `client` member is
   `{ kind: 'client'; tenantId; clientId }` and the `resource-server` member is
   `{ kind: 'resource-server'; tenantId; resourceServerId }`. `ownerScope(owner)` is the tenant's own scope for a
   tenant, a client and a resource server.
2. **Every client, resource-server and scope endpoint of `managementApi` takes `tenantId`.** The list, create, update
   and delete endpoints of clients, resource servers and scopes take `tenantId` as the grants endpoints do, build the
   path under `/api/tenants/{tenantId}` and tag the data in the tenant's scope.
3. **The containers take `tenantId`.** `ClientsPage`, `ClientDetailPage`, `ResourceServersPage`,
   `ResourceServerDetailPage` and `ResourceServerScopesPage` take a `tenantId` prop and pass it down.
4. **No helper builds an API prefix from a scope.** Each path names its owner's route.

## Options considered

- **Keep a `ManagementScope` on the owners.** Rejected: the type checker accepts a platform or team address that the
  API answers with 404.
- **A `TenantScope` type for the tenant-only owners.** Rejected: the tenant id is the whole address, as it is for
  grants, so a wrapper type adds a name without a distinction.

## Consequences

- An address outside a tenant does not type-check.
- A client or resource-server page cannot be rendered without the tenant it belongs to.
- A platform-level client family would be a new owner kind with its own route, not a scope on the existing one.

## References

- [ADR-1257](1257-the-management-console-reaches-every-owners-secrets-and-settings-through-one-endpoint-per-operation.md),
  [ADR-1191](1191-the-management-api-authenticates-against-the-control-plane-tenant.md)
- `src/frontend/packages/api/src/owner.ts`, `src/frontend/packages/store/src/managementApi.ts`
- Issue [#568](https://github.com/NCodeGroup/Identity/issues/568)
