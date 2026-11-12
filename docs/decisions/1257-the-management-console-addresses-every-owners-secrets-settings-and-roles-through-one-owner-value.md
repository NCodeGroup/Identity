# 1257. The management console addresses every owner's secrets, settings and roles through one owner value

- **Status:** Accepted
- **Date:** 2026-11-05
- **Deciders:** NCode Group

## Context

The Management API serves the same secrets, settings, encryption-key and role operations for several kinds of owner:
the platform, a team, a tenant, a client and a resource server. A console that addresses each owner through its own
endpoints repeats the same code once per owner: one endpoint, one hook and one cache-tag scheme per owner and
operation, and one copy of each tab. A fix to one copy, such as the cache tags that keep one scope's data apart from
another's ([ADR-1064](1064-one-frontend-platform-of-shared-packages-and-thin-app-shells-serves-every-web-surface.md)), reaches one owner and misses the others.

The operations are the same for every owner except one: the Management API maps the rotation of a secret
(`/secrets/{secretId}/rotate`) for clients alone, and a rotation keeps the rotated secret beside its replacement
([ADR-1320](1320-a-rotated-client-secret-overlaps-its-replacement-until-it-expires-or-is-deleted.md)).

The API serves the operations of a client, a resource server and their scopes only under `/api/tenants/{tenantId}`.
The platform and team routes only create and list the machine clients they own
([ADR-1466](1466-a-client-is-its-own-principal-and-a-machine-client-acts-only-within-its-owning-node.md)), and every
client is registered in a tenant: a machine client in the control-plane tenant
([ADR-1191](1191-a-management-request-is-authenticated-by-the-control-plane-tenant-and-scoped-by-its-routes-tenant.md)).
A console address that carries a platform or team scope for a client therefore names a route the API answers with
`404`, and a type that accepts it hides the mistake until run time.

## Decision

**The console reaches the secrets, settings, encryption keys and roles of every owner through one endpoint per
operation, each of which takes a `ManagementOwner`, and renders them through one set of owner containers. A client and
a resource server are tenant resources, addressed by their tenant alone.**

1. **A `ManagementOwner` names the owner.** `@ncode/api` defines it in `src/owner.ts` as one of five readonly shapes:
   `{ kind: 'platform' }`, `{ kind: 'team'; teamId }`, `{ kind: 'tenant'; tenantId }`,
   `{ kind: 'client'; tenantId; clientId }` and `{ kind: 'resource-server'; tenantId; resourceServerId }`. Types derived
   from it state which owners an operation accepts: `ClientOwner`; `SecretOwner`, every owner but the platform and a
   team, which hold settings only; `EncryptionKeyOwner`, the platform or a tenant; `MachineClientOwner`, the platform
   or a team ([ADR-1466](1466-a-client-is-its-own-principal-and-a-machine-client-acts-only-within-its-owning-node.md));
   `RoleOwner`; `AdministeredOwner`, a team or a tenant; and `RoleList`.
2. **The owner builds its path and scope.** `ownerApiPath(owner)` builds `api/platform`, `api/teams/{teamId}`,
   `api/tenants/{tenantId}`, and the client and resource-server paths under `api/tenants/{tenantId}`, with every id
   URI-encoded. `ownerScope(owner)` is the platform scope, the team's scope, or the tenant's own scope for a tenant, a
   client and a resource server. No helper builds an API prefix from a scope: each path names its owner's route.
3. **A client and a resource server are addressed through their tenant.** Every client, resource-server and scope
   endpoint of `managementApi` takes `tenantId`, builds its path under `/api/tenants/{tenantId}` and tags its data in
   the tenant's scope, except `listMachineClients` and `createMachineClient`, which take a `MachineClientOwner` and
   build their path with `ownerApiPath`. `ClientsPage`, `ResourceServersPage`, `ResourceServerDetailPage` and
   `ResourceServerScopesPage` take `tenantId`; `ClientDetailPage` takes the client and addresses it through
   `client.tenantId`, which for a machine client is the control-plane tenant.
4. **`managementApi` in `@ncode/store` has one endpoint per operation across all owners,** with matching hooks:
   `listSecrets`, `createSecret`, `deleteSecret`, `rotateSecret`, `getSecretPublicKey`, `getSettings`,
   `getEffectiveSettings`, `getSettingLimits`, `updateSettings`, `listEncryptionKeys`, `rekeyEncryptionKeys` and
   `listRoleAssignments`. `rotateSecret` takes only a `ClientOwner`, so the type checker refuses a rotation for any
   other owner.
5. **Cache tags stay isolated per owner.** They come from `ownerTagId`, `ownerEffectiveTagId`, `ownerLimitsTagId` and
   `roleListTagId` within the owner's scope. The owner key is URI-encoded inside the tag, so ids that contain `:` never
   collide, and a mutation invalidates only its own owner's tags.
6. **One set of owner containers renders every owner.** `OwnerSecretsTab` (offering rotation only for a client),
   `OwnerSettingsTab`, `OwnerEncryptionKeysTab` and `OwnerAccessTab` in `apps/mgmt/src/containers/` each take a
   `ManagementOwner` and wire the endpoints above to the shared managers, such as `SecretsManager` and
   `SettingsManager`.

## Options considered

- **One endpoint set per owner kind.** Rejected: it is the duplication the decision removes, and every fix has to be
  repeated once per owner kind.
- **A factory that generates a named endpoint set for each owner kind.** Rejected: the code is written once, but each
  owner kind still has its own hook names, and generated names cannot be found by searching for them.
- **Callers build the path and the tags themselves.** Rejected: each caller then repeats the scope rules and the
  URI-encoding of ids that `ownerApiPath` and the tag helpers hold once, and a tag built by hand can collide with
  another owner's.
- **A `ManagementScope` on the client and resource-server owners.** Rejected: the type checker accepts a platform or
  team address that the API answers with `404`.
- **A `TenantScope` wrapper type for the tenant-only owners.** Rejected: the tenant id is the whole address, as it is
  for grants, so a wrapper adds a name without a distinction.
- **One owner value with one endpoint per operation, and tenant-addressed clients and resource servers (chosen).**

## Consequences

- A fix to the secrets, settings, encryption-key or role flow reaches every owner, because there is one endpoint and
  one container for it.
- A new owner kind is one `ManagementOwner` member and its cases in `owner.ts`; no endpoint or container changes. A
  platform-level client family would be a new owner kind with its own route, not a scope on an existing one.
- An address outside a tenant for a client or resource server does not type-check, and a client or resource-server page
  cannot be rendered without the tenant it belongs to.
- Rotation stays a client operation, selected by type: `rotateSecret` accepts only a `ClientOwner`.
- Tag ids carry the URI-encoded owner key, so one owner's caches never invalidate another's.
- A consumer of `@ncode/store` calls the owner-generic hooks with a `ManagementOwner`; there is no hook per owner type.

## References

- [ADR-1064](1064-one-frontend-platform-of-shared-packages-and-thin-app-shells-serves-every-web-surface.md): a single cohesive frontend presentation platform; the scopes are
  distinct browsing contexts.
- [ADR-1255](1255-the-management-consoles-url-is-its-navigation-state-and-every-scopes-surface-has-its-own-address.md): the console's URL is its navigation state;
  the scope derives from the route.
- [ADR-1191](1191-a-management-request-is-authenticated-by-the-control-plane-tenant-and-scoped-by-its-routes-tenant.md):
  the control-plane tenant and the route's tenant.
- [ADR-1320](1320-a-rotated-client-secret-overlaps-its-replacement-until-it-expires-or-is-deleted.md): a rotated
  client secret stays beside its replacement.
- [ADR-1411](1411-the-platform-is-the-deployment-root-a-team-groups-tenants-and-tenants-share-one-database.md): the
  platform and the team.
- [ADR-1466](1466-a-client-is-its-own-principal-and-a-machine-client-acts-only-within-its-owning-node.md): a machine client.
- Code: `frontend/packages/api/src/owner.ts`, `frontend/packages/store/src/managementApi.ts` and
  `frontend/apps/mgmt/src/containers/` (`OwnerSecretsTab.tsx`, `OwnerSettingsTab.tsx`,
  `OwnerEncryptionKeysTab.tsx`, `OwnerAccessTab.tsx`).
- Issue [#568](https://github.com/NCodeGroup/identity/issues/568).
