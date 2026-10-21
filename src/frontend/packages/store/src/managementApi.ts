import { createApi } from '@reduxjs/toolkit/query/react';
import {
  diffPatch,
  GLOBAL_SCOPE,
  LIST_TAG,
  replacePatch,
  scopeApiPrefix,
  scopeTagId,
  type ClientResource,
  type CollectionResource,
  type CreatedInitialAccessTokenResource,
  type CreatedSecretResource,
  type CreateClientRequest,
  type CreateInitialAccessTokenRequest,
  type CreateResourceServerRequest,
  type CreateScopeRequest,
  type CreateSecretRequest,
  type RotateSecretRequest,
  type CreateTenantRequest,
  type EffectiveSettingsResource,
  type GrantResource,
  type GrantRevocationResult,
  type ManagementCapabilitiesResource,
  type ManagementScope,
  type PublicKeyResource,
  type ResourceServerResource,
  type ScopeResource,
  type SecretsResource,
  type ServerResource,
  type SettingsResource,
  type TenantAddressResource,
  type TenantResource,
  type TenantUsageResource,
  type UpdateClientFields,
  type UpdateResourceServerFields,
  type UpdateScopeRequest,
  type UpdateServerFields,
  type UpdateTenantFields,
  type CreateServerRequest,
} from '@ncode/api';
import { dynamicBaseQuery } from './baseQuery.js';

/** A cache-tag id in the global scope, which owns the tenants and the servers. */
const globalTag = (id: string): string => scopeTagId(GLOBAL_SCOPE, id);

/** A cache-tag id in a tenant's scope, which owns its grants, secrets, and settings. */
const tenantTag = (tenantId: string, id: string): string =>
  scopeTagId({ kind: 'tenant', tenantId }, id);

/**
 * The RTK Query client for the Management REST API. Co-located with the store (one RTK module
 * boundary) so its middleware/reducer types reconcile with `configureStore`. Every cache-tag id is
 * derived from the scope that owns the resource ({@link scopeTagId}), so the global and tenant
 * boundaries invalidate independently (see docs/adr/0064): a mutation in one scope never invalidates
 * the caches of the global scope or another tenant.
 */
export const managementApi = createApi({
  reducerPath: 'managementApi',
  baseQuery: dynamicBaseQuery,
  tagTypes: ['Tenant', 'Server', 'Client', 'ResourceServer', 'Grant', 'Secret', 'Settings'],
  endpoints: (build) => ({
    getCapabilities: build.query<ManagementCapabilitiesResource, void>({
      query: () => 'api/capabilities',
    }),
    listTenants: build.query<CollectionResource<TenantResource>, void>({
      query: () => 'api/tenants',
      providesTags: (result) => [
        { type: 'Tenant' as const, id: globalTag(LIST_TAG) },
        ...(result?.items ?? []).map((t) => ({
          type: 'Tenant' as const,
          id: globalTag(t.tenantId),
        })),
      ],
    }),
    getTenant: build.query<TenantResource, string>({
      query: (tenantId) => `api/tenants/${encodeURIComponent(tenantId)}`,
      providesTags: (_result, _error, tenantId) => [{ type: 'Tenant', id: globalTag(tenantId) }],
    }),
    // A preview of the address a tenant has or would have. It is never cached beyond its subscriber,
    // so it always reflects the tenants that exist when it is asked.
    getTenantAddress: build.query<
      TenantAddressResource,
      { tenantId: string; issuer?: string; mtlsEndpointAliasAuthority?: string }
    >({
      query: ({ tenantId, issuer, mtlsEndpointAliasAuthority }) => {
        const params = {
          ...(issuer ? { issuer } : {}),
          ...(mtlsEndpointAliasAuthority ? { mtlsEndpointAliasAuthority } : {}),
        };
        return {
          url: `api/tenants/${encodeURIComponent(tenantId)}/address`,
          ...(Object.keys(params).length > 0 ? { params } : {}),
        };
      },
      keepUnusedDataFor: 0,
    }),
    listServers: build.query<CollectionResource<ServerResource>, void>({
      query: () => 'api/servers',
      providesTags: (result) => [
        { type: 'Server' as const, id: globalTag(LIST_TAG) },
        ...(result?.items ?? []).map((s) => ({
          type: 'Server' as const,
          id: globalTag(s.serverId),
        })),
      ],
    }),
    createServer: build.mutation<ServerResource, CreateServerRequest>({
      query: (body) => ({ url: 'api/servers', method: 'POST', body }),
      invalidatesTags: [{ type: 'Server', id: globalTag(LIST_TAG) }],
    }),
    updateServer: build.mutation<
      void,
      { serverId: string; concurrencyToken: string; fields: UpdateServerFields }
    >({
      query: ({ serverId, concurrencyToken, fields }) => ({
        url: `api/servers/${encodeURIComponent(serverId)}`,
        method: 'PATCH',
        body: replacePatch({ ...fields }),
        headers: { 'If-Match': concurrencyToken },
      }),
      invalidatesTags: (_result, _error, { serverId }) => [
        { type: 'Server', id: globalTag(LIST_TAG) },
        { type: 'Server', id: globalTag(serverId) },
      ],
    }),
    deleteServer: build.mutation<void, { serverId: string }>({
      query: ({ serverId }) => ({
        url: `api/servers/${encodeURIComponent(serverId)}`,
        method: 'DELETE',
      }),
      invalidatesTags: (_result, _error, { serverId }) => [
        { type: 'Server', id: globalTag(LIST_TAG) },
        { type: 'Server', id: globalTag(serverId) },
      ],
    }),
    // `resourceServerId` lists only the clients that act for that resource server.
    listClients: build.query<
      CollectionResource<ClientResource>,
      { scope: ManagementScope; resourceServerId?: string }
    >({
      query: ({ scope, resourceServerId }) => ({
        url: `${scopeApiPrefix(scope)}/clients`,
        ...(resourceServerId ? { params: { resourceServerId } } : {}),
      }),
      providesTags: (result, _error, { scope }) => [
        { type: 'Client' as const, id: scopeTagId(scope, LIST_TAG) },
        ...(result?.items ?? []).map((c) => ({
          type: 'Client' as const,
          id: scopeTagId(scope, c.clientId),
        })),
      ],
    }),
    createTenant: build.mutation<TenantResource, CreateTenantRequest>({
      query: (body) => ({ url: 'api/tenants', method: 'POST', body }),
      invalidatesTags: [{ type: 'Tenant', id: globalTag(LIST_TAG) }],
    }),
    updateTenant: build.mutation<
      void,
      { tenantId: string; concurrencyToken: string; fields: UpdateTenantFields }
    >({
      query: ({ tenantId, concurrencyToken, fields }) => ({
        url: `api/tenants/${encodeURIComponent(tenantId)}`,
        method: 'PATCH',
        body: replacePatch({ ...fields }),
        headers: { 'If-Match': concurrencyToken },
      }),
      invalidatesTags: (_result, _error, { tenantId }) => [
        { type: 'Tenant', id: globalTag(LIST_TAG) },
        { type: 'Tenant', id: globalTag(tenantId) },
      ],
    }),
    deleteTenant: build.mutation<void, { tenantId: string }>({
      query: ({ tenantId }) => ({
        url: `api/tenants/${encodeURIComponent(tenantId)}`,
        method: 'DELETE',
      }),
      invalidatesTags: (_result, _error, { tenantId }) => [
        { type: 'Tenant', id: globalTag(LIST_TAG) },
        { type: 'Tenant', id: globalTag(tenantId) },
      ],
    }),
    createClient: build.mutation<
      ClientResource,
      { scope: ManagementScope; body: CreateClientRequest }
    >({
      query: ({ scope, body }) => ({
        url: `${scopeApiPrefix(scope)}/clients`,
        method: 'POST',
        body,
      }),
      invalidatesTags: (_result, _error, { scope }) => [
        { type: 'Client', id: scopeTagId(scope, LIST_TAG) },
      ],
    }),
    updateClient: build.mutation<
      void,
      {
        scope: ManagementScope;
        clientId: string;
        concurrencyToken: string;
        fields: UpdateClientFields;
      }
    >({
      query: ({ scope, clientId, concurrencyToken, fields }) => ({
        url: `${scopeApiPrefix(scope)}/clients/${encodeURIComponent(clientId)}`,
        method: 'PATCH',
        body: replacePatch({ ...fields }),
        headers: { 'If-Match': concurrencyToken },
      }),
      invalidatesTags: (_result, _error, { scope, clientId }) => [
        { type: 'Client', id: scopeTagId(scope, LIST_TAG) },
        { type: 'Client', id: scopeTagId(scope, clientId) },
      ],
    }),
    deleteClient: build.mutation<void, { scope: ManagementScope; clientId: string }>({
      query: ({ scope, clientId }) => ({
        url: `${scopeApiPrefix(scope)}/clients/${encodeURIComponent(clientId)}`,
        method: 'DELETE',
      }),
      invalidatesTags: (_result, _error, { scope, clientId }) => [
        { type: 'Client', id: scopeTagId(scope, LIST_TAG) },
        { type: 'Client', id: scopeTagId(scope, clientId) },
      ],
    }),
    listResourceServers: build.query<
      CollectionResource<ResourceServerResource>,
      { scope: ManagementScope }
    >({
      query: ({ scope }) => `${scopeApiPrefix(scope)}/resource-servers`,
      providesTags: (result, _error, { scope }) => [
        { type: 'ResourceServer' as const, id: scopeTagId(scope, LIST_TAG) },
        ...(result?.items ?? []).map((r) => ({
          type: 'ResourceServer' as const,
          id: scopeTagId(scope, r.resourceServerId),
        })),
      ],
    }),
    createResourceServer: build.mutation<
      ResourceServerResource,
      { scope: ManagementScope; body: CreateResourceServerRequest }
    >({
      query: ({ scope, body }) => ({
        url: `${scopeApiPrefix(scope)}/resource-servers`,
        method: 'POST',
        body,
      }),
      invalidatesTags: (_result, _error, { scope }) => [
        { type: 'ResourceServer', id: scopeTagId(scope, LIST_TAG) },
      ],
    }),
    updateResourceServer: build.mutation<
      void,
      {
        scope: ManagementScope;
        resourceServerId: string;
        concurrencyToken: string;
        fields: UpdateResourceServerFields;
      }
    >({
      query: ({ scope, resourceServerId, concurrencyToken, fields }) => ({
        url: `${scopeApiPrefix(scope)}/resource-servers/${encodeURIComponent(resourceServerId)}`,
        method: 'PATCH',
        body: replacePatch({ ...fields }),
        headers: { 'If-Match': concurrencyToken },
      }),
      invalidatesTags: (_result, _error, { scope, resourceServerId }) => [
        { type: 'ResourceServer', id: scopeTagId(scope, LIST_TAG) },
        { type: 'ResourceServer', id: scopeTagId(scope, resourceServerId) },
      ],
    }),
    deleteResourceServer: build.mutation<
      void,
      { scope: ManagementScope; resourceServerId: string }
    >({
      query: ({ scope, resourceServerId }) => ({
        url: `${scopeApiPrefix(scope)}/resource-servers/${encodeURIComponent(resourceServerId)}`,
        method: 'DELETE',
      }),
      invalidatesTags: (_result, _error, { scope, resourceServerId }) => [
        { type: 'ResourceServer', id: scopeTagId(scope, LIST_TAG) },
        { type: 'ResourceServer', id: scopeTagId(scope, resourceServerId) },
      ],
    }),
    createScope: build.mutation<
      ScopeResource,
      { scope: ManagementScope; resourceServerId: string; body: CreateScopeRequest }
    >({
      query: ({ scope, resourceServerId, body }) => ({
        url: `${scopeApiPrefix(scope)}/resource-servers/${encodeURIComponent(
          resourceServerId,
        )}/scopes`,
        method: 'POST',
        body,
      }),
      invalidatesTags: (_result, _error, { scope, resourceServerId }) => [
        { type: 'ResourceServer', id: scopeTagId(scope, LIST_TAG) },
        { type: 'ResourceServer', id: scopeTagId(scope, resourceServerId) },
      ],
    }),
    updateScope: build.mutation<
      void,
      {
        scope: ManagementScope;
        resourceServerId: string;
        scopeValue: string;
        body: UpdateScopeRequest;
      }
    >({
      query: ({ scope, resourceServerId, scopeValue, body }) => ({
        url: `${scopeApiPrefix(scope)}/resource-servers/${encodeURIComponent(
          resourceServerId,
        )}/scopes/${encodeURIComponent(scopeValue)}`,
        method: 'PUT',
        body,
      }),
      invalidatesTags: (_result, _error, { scope, resourceServerId }) => [
        { type: 'ResourceServer', id: scopeTagId(scope, LIST_TAG) },
        { type: 'ResourceServer', id: scopeTagId(scope, resourceServerId) },
      ],
    }),
    deleteScope: build.mutation<
      void,
      { scope: ManagementScope; resourceServerId: string; scopeValue: string }
    >({
      query: ({ scope, resourceServerId, scopeValue }) => ({
        url: `${scopeApiPrefix(scope)}/resource-servers/${encodeURIComponent(
          resourceServerId,
        )}/scopes/${encodeURIComponent(scopeValue)}`,
        method: 'DELETE',
      }),
      invalidatesTags: (_result, _error, { scope, resourceServerId }) => [
        { type: 'ResourceServer', id: scopeTagId(scope, LIST_TAG) },
        { type: 'ResourceServer', id: scopeTagId(scope, resourceServerId) },
      ],
    }),
    listGrants: build.query<
      CollectionResource<GrantResource>,
      { tenantId: string; subjectId?: string; clientId?: string }
    >({
      query: ({ tenantId, subjectId, clientId }) => ({
        url: `api/tenants/${encodeURIComponent(tenantId)}/grants`,
        params: {
          ...(subjectId ? { subjectId } : {}),
          ...(clientId ? { clientId } : {}),
        },
      }),
      providesTags: (result, _error, { tenantId }) => [
        { type: 'Grant' as const, id: tenantTag(tenantId, LIST_TAG) },
        ...(result?.items ?? []).map((g) => ({
          type: 'Grant' as const,
          id: tenantTag(tenantId, g.grantId),
        })),
      ],
    }),
    revokeGrant: build.mutation<void, { tenantId: string; grantId: string }>({
      query: ({ tenantId, grantId }) => ({
        url: `api/tenants/${encodeURIComponent(tenantId)}/grants/${encodeURIComponent(grantId)}`,
        method: 'DELETE',
      }),
      invalidatesTags: (_result, _error, { tenantId, grantId }) => [
        { type: 'Grant', id: tenantTag(tenantId, LIST_TAG) },
        { type: 'Grant', id: tenantTag(tenantId, grantId) },
      ],
    }),
    // Bulk revoke requires at least one filter server-side; the caller must supply a subject and/or client.
    revokeGrants: build.mutation<
      GrantRevocationResult,
      { tenantId: string; subjectId?: string; clientId?: string }
    >({
      query: ({ tenantId, subjectId, clientId }) => ({
        url: `api/tenants/${encodeURIComponent(tenantId)}/grants`,
        method: 'DELETE',
        params: {
          ...(subjectId ? { subjectId } : {}),
          ...(clientId ? { clientId } : {}),
        },
      }),
      invalidatesTags: (_result, _error, { tenantId }) => [
        { type: 'Grant', id: tenantTag(tenantId, LIST_TAG) },
      ],
    }),
    // Mints an RFC 7591 initial access token; the plaintext value is revealed once and the backing grant is listed.
    createInitialAccessToken: build.mutation<
      CreatedInitialAccessTokenResource,
      { tenantId: string; body: CreateInitialAccessTokenRequest }
    >({
      query: ({ tenantId, body }) => ({
        url: `api/tenants/${encodeURIComponent(tenantId)}/grants/initial-access-tokens`,
        method: 'POST',
        body,
      }),
      invalidatesTags: (_result, _error, { tenantId }) => [
        { type: 'Grant', id: tenantTag(tenantId, LIST_TAG) },
      ],
    }),
    listClientSecrets: build.query<SecretsResource, { scope: ManagementScope; clientId: string }>({
      query: ({ scope, clientId }) =>
        `${scopeApiPrefix(scope)}/clients/${encodeURIComponent(clientId)}/secrets`,
      providesTags: (result, _error, { scope, clientId }) => [
        { type: 'Secret' as const, id: scopeTagId(scope, `client:${clientId}`) },
        ...(result?.secrets ?? []).map((s) => ({
          type: 'Secret' as const,
          id: scopeTagId(scope, s.secretId),
        })),
      ],
    }),
    createClientSecret: build.mutation<
      CreatedSecretResource,
      { scope: ManagementScope; clientId: string; body: CreateSecretRequest }
    >({
      query: ({ scope, clientId, body }) => ({
        url: `${scopeApiPrefix(scope)}/clients/${encodeURIComponent(clientId)}/secrets`,
        method: 'POST',
        body,
      }),
      invalidatesTags: (_result, _error, { scope, clientId }) => [
        { type: 'Secret', id: scopeTagId(scope, `client:${clientId}`) },
      ],
    }),
    deleteClientSecret: build.mutation<
      void,
      { scope: ManagementScope; clientId: string; secretId: string }
    >({
      query: ({ scope, clientId, secretId }) => ({
        url: `${scopeApiPrefix(scope)}/clients/${encodeURIComponent(
          clientId,
        )}/secrets/${encodeURIComponent(secretId)}`,
        method: 'DELETE',
      }),
      invalidatesTags: (_result, _error, { scope, clientId, secretId }) => [
        { type: 'Secret', id: scopeTagId(scope, `client:${clientId}`) },
        { type: 'Secret', id: scopeTagId(scope, secretId) },
      ],
    }),
    rotateClientSecret: build.mutation<
      CreatedSecretResource,
      { scope: ManagementScope; clientId: string; secretId: string; body: RotateSecretRequest }
    >({
      query: ({ scope, clientId, secretId, body }) => ({
        url: `${scopeApiPrefix(scope)}/clients/${encodeURIComponent(
          clientId,
        )}/secrets/${encodeURIComponent(secretId)}/rotate`,
        method: 'POST',
        body,
      }),
      invalidatesTags: (_result, _error, { scope, clientId, secretId }) => [
        { type: 'Secret', id: scopeTagId(scope, `client:${clientId}`) },
        { type: 'Secret', id: scopeTagId(scope, secretId) },
      ],
    }),
    getClientSecretPublicKey: build.query<
      PublicKeyResource,
      { scope: ManagementScope; clientId: string; secretId: string }
    >({
      query: ({ scope, clientId, secretId }) =>
        `${scopeApiPrefix(scope)}/clients/${encodeURIComponent(
          clientId,
        )}/secrets/${encodeURIComponent(secretId)}/public-key`,
    }),
    getClientSettings: build.query<SettingsResource, { scope: ManagementScope; clientId: string }>({
      query: ({ scope, clientId }) =>
        `${scopeApiPrefix(scope)}/clients/${encodeURIComponent(clientId)}/settings`,
      providesTags: (_result, _error, { scope, clientId }) => [
        { type: 'Settings', id: scopeTagId(scope, `client:${clientId}`) },
      ],
    }),
    getClientEffectiveSettings: build.query<
      EffectiveSettingsResource,
      { scope: ManagementScope; clientId: string }
    >({
      query: ({ scope, clientId }) =>
        `${scopeApiPrefix(scope)}/clients/${encodeURIComponent(clientId)}/effective-settings`,
      providesTags: (_result, _error, { scope, clientId }) => [
        { type: 'Settings', id: scopeTagId(scope, `client-effective:${clientId}`) },
      ],
    }),
    // Settings are an opaque object, so updates are a top-level add/remove/replace diff (not replace-only).
    updateClientSettings: build.mutation<
      void,
      {
        scope: ManagementScope;
        clientId: string;
        concurrencyToken: string;
        original: Readonly<Record<string, unknown>>;
        desired: Readonly<Record<string, unknown>>;
      }
    >({
      query: ({ scope, clientId, concurrencyToken, original, desired }) => ({
        url: `${scopeApiPrefix(scope)}/clients/${encodeURIComponent(clientId)}/settings`,
        method: 'PATCH',
        body: diffPatch(original, desired),
        headers: { 'If-Match': concurrencyToken },
      }),
      invalidatesTags: (_result, _error, { scope, clientId }) => [
        { type: 'Settings', id: scopeTagId(scope, `client:${clientId}`) },
        { type: 'Settings', id: scopeTagId(scope, `client-effective:${clientId}`) },
      ],
    }),
    listTenantSecrets: build.query<SecretsResource, { tenantId: string }>({
      query: ({ tenantId }) => `api/tenants/${encodeURIComponent(tenantId)}/secrets`,
      providesTags: (result, _error, { tenantId }) => [
        { type: 'Secret' as const, id: tenantTag(tenantId, 'tenant') },
        ...(result?.secrets ?? []).map((s) => ({
          type: 'Secret' as const,
          id: tenantTag(tenantId, s.secretId),
        })),
      ],
    }),
    createTenantSecret: build.mutation<
      CreatedSecretResource,
      { tenantId: string; body: CreateSecretRequest }
    >({
      query: ({ tenantId, body }) => ({
        url: `api/tenants/${encodeURIComponent(tenantId)}/secrets`,
        method: 'POST',
        body,
      }),
      invalidatesTags: (_result, _error, { tenantId }) => [
        { type: 'Secret', id: tenantTag(tenantId, 'tenant') },
      ],
    }),
    deleteTenantSecret: build.mutation<void, { tenantId: string; secretId: string }>({
      query: ({ tenantId, secretId }) => ({
        url: `api/tenants/${encodeURIComponent(tenantId)}/secrets/${encodeURIComponent(secretId)}`,
        method: 'DELETE',
      }),
      invalidatesTags: (_result, _error, { tenantId, secretId }) => [
        { type: 'Secret', id: tenantTag(tenantId, 'tenant') },
        { type: 'Secret', id: tenantTag(tenantId, secretId) },
      ],
    }),
    getTenantSecretPublicKey: build.query<
      PublicKeyResource,
      { tenantId: string; secretId: string }
    >({
      query: ({ tenantId, secretId }) =>
        `api/tenants/${encodeURIComponent(tenantId)}/secrets/${encodeURIComponent(
          secretId,
        )}/public-key`,
    }),
    getTenantSettings: build.query<SettingsResource, { tenantId: string }>({
      query: ({ tenantId }) => `api/tenants/${encodeURIComponent(tenantId)}/settings`,
      providesTags: (_result, _error, { tenantId }) => [
        { type: 'Settings', id: tenantTag(tenantId, 'tenant') },
      ],
    }),
    getTenantEffectiveSettings: build.query<EffectiveSettingsResource, { tenantId: string }>({
      query: ({ tenantId }) => `api/tenants/${encodeURIComponent(tenantId)}/effective-settings`,
      providesTags: (_result, _error, { tenantId }) => [
        { type: 'Settings', id: tenantTag(tenantId, 'tenant-effective') },
      ],
    }),
    // The usage ledger is append-only and written by the server's periodic flush (ADR-0199), so it is not tagged:
    // every mount reads the current ledger.
    getTenantUsage: build.query<
      TenantUsageResource,
      { tenantId: string; from?: string | undefined; to?: string | undefined }
    >({
      query: ({ tenantId, from, to }) => {
        const params = new URLSearchParams();
        if (from) params.set('from', from);
        if (to) params.set('to', to);
        const search = params.toString();
        return `api/tenants/${encodeURIComponent(tenantId)}/usage${search ? `?${search}` : ''}`;
      },
    }),
    // Settings are an opaque object, so updates are a top-level add/remove/replace diff (not replace-only).
    updateTenantSettings: build.mutation<
      void,
      {
        tenantId: string;
        concurrencyToken: string;
        original: Readonly<Record<string, unknown>>;
        desired: Readonly<Record<string, unknown>>;
      }
    >({
      query: ({ tenantId, concurrencyToken, original, desired }) => ({
        url: `api/tenants/${encodeURIComponent(tenantId)}/settings`,
        method: 'PATCH',
        body: diffPatch(original, desired),
        headers: { 'If-Match': concurrencyToken },
      }),
      invalidatesTags: (_result, _error, { tenantId }) => [
        { type: 'Settings', id: tenantTag(tenantId, 'tenant') },
        { type: 'Settings', id: tenantTag(tenantId, 'tenant-effective') },
      ],
    }),
    listServerSecrets: build.query<SecretsResource, { serverId: string }>({
      query: ({ serverId }) => `api/servers/${encodeURIComponent(serverId)}/secrets`,
      providesTags: (result, _error, { serverId }) => [
        { type: 'Secret' as const, id: globalTag(`server:${serverId}`) },
        ...(result?.secrets ?? []).map((s) => ({
          type: 'Secret' as const,
          id: globalTag(s.secretId),
        })),
      ],
    }),
    createServerSecret: build.mutation<
      CreatedSecretResource,
      { serverId: string; body: CreateSecretRequest }
    >({
      query: ({ serverId, body }) => ({
        url: `api/servers/${encodeURIComponent(serverId)}/secrets`,
        method: 'POST',
        body,
      }),
      invalidatesTags: (_result, _error, { serverId }) => [
        { type: 'Secret', id: globalTag(`server:${serverId}`) },
      ],
    }),
    deleteServerSecret: build.mutation<void, { serverId: string; secretId: string }>({
      query: ({ serverId, secretId }) => ({
        url: `api/servers/${encodeURIComponent(serverId)}/secrets/${encodeURIComponent(secretId)}`,
        method: 'DELETE',
      }),
      invalidatesTags: (_result, _error, { serverId, secretId }) => [
        { type: 'Secret', id: globalTag(`server:${serverId}`) },
        { type: 'Secret', id: globalTag(secretId) },
      ],
    }),
    getServerSecretPublicKey: build.query<
      PublicKeyResource,
      { serverId: string; secretId: string }
    >({
      query: ({ serverId, secretId }) =>
        `api/servers/${encodeURIComponent(serverId)}/secrets/${encodeURIComponent(
          secretId,
        )}/public-key`,
    }),
    getServerSettings: build.query<SettingsResource, { serverId: string }>({
      query: ({ serverId }) => `api/servers/${encodeURIComponent(serverId)}/settings`,
      providesTags: (_result, _error, { serverId }) => [
        { type: 'Settings', id: globalTag(`server:${serverId}`) },
      ],
    }),
    getServerEffectiveSettings: build.query<EffectiveSettingsResource, { serverId: string }>({
      query: ({ serverId }) => `api/servers/${encodeURIComponent(serverId)}/effective-settings`,
      providesTags: (_result, _error, { serverId }) => [
        { type: 'Settings', id: globalTag(`server-effective:${serverId}`) },
      ],
    }),
    // Settings are an opaque object, so updates are a top-level add/remove/replace diff (not replace-only).
    updateServerSettings: build.mutation<
      void,
      {
        serverId: string;
        concurrencyToken: string;
        original: Readonly<Record<string, unknown>>;
        desired: Readonly<Record<string, unknown>>;
      }
    >({
      query: ({ serverId, concurrencyToken, original, desired }) => ({
        url: `api/servers/${encodeURIComponent(serverId)}/settings`,
        method: 'PATCH',
        body: diffPatch(original, desired),
        headers: { 'If-Match': concurrencyToken },
      }),
      invalidatesTags: (_result, _error, { serverId }) => [
        { type: 'Settings', id: globalTag(`server:${serverId}`) },
        { type: 'Settings', id: globalTag(`server-effective:${serverId}`) },
      ],
    }),
    listResourceServerSecrets: build.query<
      SecretsResource,
      { scope: ManagementScope; resourceServerId: string }
    >({
      query: ({ scope, resourceServerId }) =>
        `${scopeApiPrefix(scope)}/resource-servers/${encodeURIComponent(resourceServerId)}/secrets`,
      providesTags: (result, _error, { scope, resourceServerId }) => [
        { type: 'Secret' as const, id: scopeTagId(scope, `resource-server:${resourceServerId}`) },
        ...(result?.secrets ?? []).map((s) => ({
          type: 'Secret' as const,
          id: scopeTagId(scope, s.secretId),
        })),
      ],
    }),
    createResourceServerSecret: build.mutation<
      CreatedSecretResource,
      { scope: ManagementScope; resourceServerId: string; body: CreateSecretRequest }
    >({
      query: ({ scope, resourceServerId, body }) => ({
        url: `${scopeApiPrefix(scope)}/resource-servers/${encodeURIComponent(
          resourceServerId,
        )}/secrets`,
        method: 'POST',
        body,
      }),
      invalidatesTags: (_result, _error, { scope, resourceServerId }) => [
        { type: 'Secret', id: scopeTagId(scope, `resource-server:${resourceServerId}`) },
      ],
    }),
    deleteResourceServerSecret: build.mutation<
      void,
      { scope: ManagementScope; resourceServerId: string; secretId: string }
    >({
      query: ({ scope, resourceServerId, secretId }) => ({
        url: `${scopeApiPrefix(scope)}/resource-servers/${encodeURIComponent(
          resourceServerId,
        )}/secrets/${encodeURIComponent(secretId)}`,
        method: 'DELETE',
      }),
      invalidatesTags: (_result, _error, { scope, resourceServerId, secretId }) => [
        { type: 'Secret', id: scopeTagId(scope, `resource-server:${resourceServerId}`) },
        { type: 'Secret', id: scopeTagId(scope, secretId) },
      ],
    }),
    getResourceServerSecretPublicKey: build.query<
      PublicKeyResource,
      { scope: ManagementScope; resourceServerId: string; secretId: string }
    >({
      query: ({ scope, resourceServerId, secretId }) =>
        `${scopeApiPrefix(scope)}/resource-servers/${encodeURIComponent(
          resourceServerId,
        )}/secrets/${encodeURIComponent(secretId)}/public-key`,
    }),
    getResourceServerSettings: build.query<
      SettingsResource,
      { scope: ManagementScope; resourceServerId: string }
    >({
      query: ({ scope, resourceServerId }) =>
        `${scopeApiPrefix(scope)}/resource-servers/${encodeURIComponent(
          resourceServerId,
        )}/settings`,
      providesTags: (_result, _error, { scope, resourceServerId }) => [
        { type: 'Settings', id: scopeTagId(scope, `resource-server:${resourceServerId}`) },
      ],
    }),
    getResourceServerEffectiveSettings: build.query<
      EffectiveSettingsResource,
      { scope: ManagementScope; resourceServerId: string }
    >({
      query: ({ scope, resourceServerId }) =>
        `${scopeApiPrefix(scope)}/resource-servers/${encodeURIComponent(
          resourceServerId,
        )}/effective-settings`,
      providesTags: (_result, _error, { scope, resourceServerId }) => [
        {
          type: 'Settings',
          id: scopeTagId(scope, `resource-server-effective:${resourceServerId}`),
        },
      ],
    }),
    // Settings are an opaque object, so updates are a top-level add/remove/replace diff (not replace-only).
    updateResourceServerSettings: build.mutation<
      void,
      {
        scope: ManagementScope;
        resourceServerId: string;
        concurrencyToken: string;
        original: Readonly<Record<string, unknown>>;
        desired: Readonly<Record<string, unknown>>;
      }
    >({
      query: ({ scope, resourceServerId, concurrencyToken, original, desired }) => ({
        url: `${scopeApiPrefix(scope)}/resource-servers/${encodeURIComponent(
          resourceServerId,
        )}/settings`,
        method: 'PATCH',
        body: diffPatch(original, desired),
        headers: { 'If-Match': concurrencyToken },
      }),
      invalidatesTags: (_result, _error, { scope, resourceServerId }) => [
        { type: 'Settings', id: scopeTagId(scope, `resource-server:${resourceServerId}`) },
        {
          type: 'Settings',
          id: scopeTagId(scope, `resource-server-effective:${resourceServerId}`),
        },
      ],
    }),
  }),
});

export const {
  useGetCapabilitiesQuery,
  useListTenantsQuery,
  useGetTenantQuery,
  useGetTenantAddressQuery,
  useListServersQuery,
  useListClientsQuery,
  useListResourceServersQuery,
  useCreateServerMutation,
  useUpdateServerMutation,
  useDeleteServerMutation,
  useCreateTenantMutation,
  useUpdateTenantMutation,
  useDeleteTenantMutation,
  useCreateClientMutation,
  useUpdateClientMutation,
  useDeleteClientMutation,
  useCreateResourceServerMutation,
  useUpdateResourceServerMutation,
  useDeleteResourceServerMutation,
  useCreateScopeMutation,
  useUpdateScopeMutation,
  useDeleteScopeMutation,
  useListGrantsQuery,
  useRevokeGrantMutation,
  useRevokeGrantsMutation,
  useCreateInitialAccessTokenMutation,
  useListClientSecretsQuery,
  useCreateClientSecretMutation,
  useDeleteClientSecretMutation,
  useRotateClientSecretMutation,
  useLazyGetClientSecretPublicKeyQuery,
  useGetClientSettingsQuery,
  useGetClientEffectiveSettingsQuery,
  useUpdateClientSettingsMutation,
  useListTenantSecretsQuery,
  useCreateTenantSecretMutation,
  useDeleteTenantSecretMutation,
  useLazyGetTenantSecretPublicKeyQuery,
  useGetTenantSettingsQuery,
  useGetTenantEffectiveSettingsQuery,
  useGetTenantUsageQuery,
  useUpdateTenantSettingsMutation,
  useListServerSecretsQuery,
  useCreateServerSecretMutation,
  useDeleteServerSecretMutation,
  useLazyGetServerSecretPublicKeyQuery,
  useGetServerSettingsQuery,
  useGetServerEffectiveSettingsQuery,
  useUpdateServerSettingsMutation,
  useListResourceServerSecretsQuery,
  useCreateResourceServerSecretMutation,
  useDeleteResourceServerSecretMutation,
  useLazyGetResourceServerSecretPublicKeyQuery,
  useGetResourceServerSettingsQuery,
  useGetResourceServerEffectiveSettingsQuery,
  useUpdateResourceServerSettingsMutation,
} = managementApi;
