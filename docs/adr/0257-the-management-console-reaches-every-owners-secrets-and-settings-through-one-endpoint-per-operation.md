# 257. The management console reaches every owner's secrets and settings through one endpoint per operation

- **Status:** Accepted
- **Date:** 2026-10-24
- **Deciders:** NCode Group

## Context

The Management API serves the same secrets and settings operations for four kinds of owner: the server, a tenant, a
client and a resource server. A console that addresses each owner through its own endpoints repeats the same code once
per owner: 29 endpoints, each with its own hook and its own cache-tag scheme, and four copies of the secrets and
settings tabs. A fix to one copy, such as the cache tags that keep one scope's data apart from another's
([ADR-0064](0064-frontend-presentation-platform.md)), reaches one owner and misses the other three (backlog `R45`).

The operations are the same for every owner except one: the Management API maps the one-call rotation of a secret
(`/secrets/{secretId}/rotate`) for clients alone
([ADR-0183](0183-a-client-secret-is-rotated-by-replacing-it-in-one-change.md)).

## Decision

**The console reaches the secrets and settings of every owner through one endpoint per operation, each of which takes a
`ManagementOwner`, and renders them through one pair of tab containers.**

1. **A `ManagementOwner` names the owner.** `@ncode/api` defines it in `src/owner.ts` as one of
   `{ kind: 'server'; serverId }`, `{ kind: 'tenant'; tenantId }`, `{ kind: 'client'; scope; clientId }` and
   `{ kind: 'resource-server'; scope; resourceServerId }`, every member `readonly`. `ClientOwner` is the client member.
   `ownerApiPath(owner)` is the owner's API path, with every id URI-encoded, under which `secrets` and `settings` live.
   `ownerScope(owner)` is the scope the owner is administered in: global for a server, the tenant's own scope for a
   tenant, and the given scope for a client or a resource server. `ownerTagId(owner)` and `ownerEffectiveTagId(owner)`
   are the owner's cache-tag ids within that scope. The owner's id is URI-encoded inside its tag key, so the key
   `client:x%3Aeffective` never equals the effective-settings tag of client `x`.
2. **`managementApi` has one endpoint per operation.** `listSecrets(owner)`, `createSecret({ owner, body })`,
   `deleteSecret({ owner, secretId })`, `rotateSecret({ owner, secretId, body })`,
   `getSecretPublicKey({ owner, secretId })`, `getSettings(owner)`, `getEffectiveSettings(owner)` and
   `updateSettings({ owner, concurrencyToken, original, desired })` serve every owner. `rotateSecret` takes a
   `ClientOwner`, so the type checker refuses a rotation for any other owner. `@ncode/store` exports the matching hooks:
   `useListSecretsQuery`, `useCreateSecretMutation`, `useDeleteSecretMutation`, `useRotateSecretMutation`,
   `useLazyGetSecretPublicKeyQuery`, `useGetSettingsQuery`, `useGetEffectiveSettingsQuery` and
   `useUpdateSettingsMutation`.
3. **Cache tags stay isolated per scope.** A secrets list provides the `Secret` tag `ownerTagId(owner)` and, for each
   secret, `scopeTagId(ownerScope(owner), secretId)`. The settings of an owner provide the `Settings` tag
   `ownerTagId(owner)`, and its effective settings provide the `Settings` tag `ownerEffectiveTagId(owner)`. A mutation
   invalidates the tags of its owner, so it refetches that owner's data and no other owner's, in its scope or another.
4. **One pair of containers renders them.** `OwnerSecretsTab` and `OwnerSettingsTab` in `apps/mgmt/src/containers/`
   take a `ManagementOwner`, wire the endpoints above to the shared `SecretsManager` and `SettingsManager`, and serve
   the server, tenant, client and resource server detail pages. `OwnerSecretsTab` offers rotation only when the owner is
   a client.

## Options considered

- **One set of endpoints per owner kind.** Rejected: it is the duplication that the decision removes, and every fix has
  to be repeated four times.
- **A factory that generates a named endpoint set for each owner kind.** Rejected: the code is written once, but each
  owner kind still has its own hook names, and generated names cannot be found by searching for them.
- **Callers build the path themselves.** Rejected: each caller then repeats the scope rules (the global `api` prefix,
  the tenant-nested prefix, the URI-encoding of ids) that `ownerApiPath` holds once, and a cache tag built by hand can
  collide with another owner's.

## Consequences

- A fix to the secrets or settings flow reaches every owner, because there is one endpoint and one container for it.
- An owner kind is added with one `ManagementOwner` member and its cases in `owner.ts` (`ownerApiPath`, `ownerScope` and
  the tag key); no endpoint or container changes.
- Tag ids carry the URI-encoded owner key, so ids that contain `:` cannot collide, and one owner's caches never
  invalidate another's.
- Rotation stays a client operation, selected by type: `rotateSecret` accepts only a `ClientOwner`.
- A consumer of `@ncode/store` calls the owner-generic hooks with a `ManagementOwner`; there is no hook per owner type.

## References

- [ADR-0064](0064-frontend-presentation-platform.md): a single cohesive frontend presentation platform; the global and
  tenant scopes are distinct browsing contexts.
- [ADR-0174](0174-the-management-consoles-url-is-its-navigation-state.md): the management console's URL is its
  navigation state; the scope derives from the route.
- [ADR-0183](0183-a-client-secret-is-rotated-by-replacing-it-in-one-change.md): a client secret is rotated by replacing
  it in one change.
- Code: `src/frontend/packages/api/src/owner.ts`, `src/frontend/packages/api/src/scope.ts`,
  `src/frontend/packages/store/src/managementApi.ts`, `src/frontend/apps/mgmt/src/containers/OwnerSecretsTab.tsx`,
  `src/frontend/apps/mgmt/src/containers/OwnerSettingsTab.tsx`,
  `src/frontend/apps/mgmt/src/containers/ServerDetailPage.tsx`,
  `src/frontend/apps/mgmt/src/containers/TenantDetailPage.tsx`,
  `src/frontend/apps/mgmt/src/containers/ClientDetailPage.tsx` and
  `src/frontend/apps/mgmt/src/containers/ResourceServerDetailPage.tsx`.
