import { createApi } from '@reduxjs/toolkit/query/react';
import {
  diffPatch,
  replacePatch,
  scopeApiPrefix,
  type ClientResource,
  type CollectionResource,
  type CreatedInitialAccessTokenResource,
  type CreatedSecretResource,
  type CreateClientRequest,
  type CreateInitialAccessTokenRequest,
  type CreateResourceServerRequest,
  type CreateScopeRequest,
  type CreateSecretRequest,
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
  type TenantResource,
  type UpdateClientFields,
  type UpdateResourceServerFields,
  type UpdateScopeRequest,
  type UpdateTenantFields,
} from '@ncode/api';
import { dynamicBaseQuery } from './baseQuery.js';

/**
 * The RTK Query client for the Management REST API. Co-located with the store (one RTK module
 * boundary) so its middleware/reducer types reconcile with `configureStore`. Cache tags are scoped
 * so the global and tenant boundaries invalidate independently (see docs/adr/0064): a tenant
 * mutation never invalidates the global list and vice versa.
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
        { type: 'Tenant' as const, id: 'LIST' },
        ...(result?.items ?? []).map((t) => ({ type: 'Tenant' as const, id: t.tenantId })),
      ],
    }),
    getTenant: build.query<TenantResource, string>({
      query: (tenantId) => `api/tenants/${encodeURIComponent(tenantId)}`,
      providesTags: (_result, _error, tenantId) => [{ type: 'Tenant', id: tenantId }],
    }),
    listServers: build.query<CollectionResource<ServerResource>, void>({
      query: () => 'api/servers',
      providesTags: [{ type: 'Server', id: 'LIST' }],
    }),
    listClients: build.query<CollectionResource<ClientResource>, { scope: ManagementScope }>({
      query: ({ scope }) => `${scopeApiPrefix(scope)}/clients`,
      providesTags: (result) => [
        { type: 'Client' as const, id: 'LIST' },
        ...(result?.items ?? []).map((c) => ({ type: 'Client' as const, id: c.clientId })),
      ],
    }),
    createTenant: build.mutation<TenantResource, CreateTenantRequest>({
      query: (body) => ({ url: 'api/tenants', method: 'POST', body }),
      invalidatesTags: [{ type: 'Tenant', id: 'LIST' }],
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
        { type: 'Tenant', id: 'LIST' },
        { type: 'Tenant', id: tenantId },
      ],
    }),
    deleteTenant: build.mutation<void, { tenantId: string }>({
      query: ({ tenantId }) => ({
        url: `api/tenants/${encodeURIComponent(tenantId)}`,
        method: 'DELETE',
      }),
      invalidatesTags: (_result, _error, { tenantId }) => [
        { type: 'Tenant', id: 'LIST' },
        { type: 'Tenant', id: tenantId },
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
      invalidatesTags: [{ type: 'Client', id: 'LIST' }],
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
      invalidatesTags: (_result, _error, { clientId }) => [
        { type: 'Client', id: 'LIST' },
        { type: 'Client', id: clientId },
      ],
    }),
    deleteClient: build.mutation<void, { scope: ManagementScope; clientId: string }>({
      query: ({ scope, clientId }) => ({
        url: `${scopeApiPrefix(scope)}/clients/${encodeURIComponent(clientId)}`,
        method: 'DELETE',
      }),
      invalidatesTags: (_result, _error, { clientId }) => [
        { type: 'Client', id: 'LIST' },
        { type: 'Client', id: clientId },
      ],
    }),
    listResourceServers: build.query<
      CollectionResource<ResourceServerResource>,
      { scope: ManagementScope }
    >({
      query: ({ scope }) => `${scopeApiPrefix(scope)}/resource-servers`,
      providesTags: (result) => [
        { type: 'ResourceServer' as const, id: 'LIST' },
        ...(result?.items ?? []).map((r) => ({
          type: 'ResourceServer' as const,
          id: r.resourceServerId,
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
      invalidatesTags: [{ type: 'ResourceServer', id: 'LIST' }],
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
      invalidatesTags: (_result, _error, { resourceServerId }) => [
        { type: 'ResourceServer', id: 'LIST' },
        { type: 'ResourceServer', id: resourceServerId },
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
      invalidatesTags: (_result, _error, { resourceServerId }) => [
        { type: 'ResourceServer', id: 'LIST' },
        { type: 'ResourceServer', id: resourceServerId },
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
      invalidatesTags: (_result, _error, { resourceServerId }) => [
        { type: 'ResourceServer', id: 'LIST' },
        { type: 'ResourceServer', id: resourceServerId },
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
      invalidatesTags: (_result, _error, { resourceServerId }) => [
        { type: 'ResourceServer', id: 'LIST' },
        { type: 'ResourceServer', id: resourceServerId },
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
      invalidatesTags: (_result, _error, { resourceServerId }) => [
        { type: 'ResourceServer', id: 'LIST' },
        { type: 'ResourceServer', id: resourceServerId },
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
      providesTags: (result) => [
        { type: 'Grant' as const, id: 'LIST' },
        ...(result?.items ?? []).map((g) => ({ type: 'Grant' as const, id: g.grantId })),
      ],
    }),
    revokeGrant: build.mutation<void, { tenantId: string; grantId: string }>({
      query: ({ tenantId, grantId }) => ({
        url: `api/tenants/${encodeURIComponent(tenantId)}/grants/${encodeURIComponent(grantId)}`,
        method: 'DELETE',
      }),
      invalidatesTags: (_result, _error, { grantId }) => [
        { type: 'Grant', id: 'LIST' },
        { type: 'Grant', id: grantId },
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
      invalidatesTags: [{ type: 'Grant', id: 'LIST' }],
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
      invalidatesTags: [{ type: 'Grant', id: 'LIST' }],
    }),
    listClientSecrets: build.query<SecretsResource, { scope: ManagementScope; clientId: string }>({
      query: ({ scope, clientId }) =>
        `${scopeApiPrefix(scope)}/clients/${encodeURIComponent(clientId)}/secrets`,
      providesTags: (result, _error, { clientId }) => [
        { type: 'Secret' as const, id: `client:${clientId}` },
        ...(result?.secrets ?? []).map((s) => ({ type: 'Secret' as const, id: s.secretId })),
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
      invalidatesTags: (_result, _error, { clientId }) => [
        { type: 'Secret', id: `client:${clientId}` },
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
      invalidatesTags: (_result, _error, { clientId, secretId }) => [
        { type: 'Secret', id: `client:${clientId}` },
        { type: 'Secret', id: secretId },
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
      providesTags: (_result, _error, { clientId }) => [
        { type: 'Settings', id: `client:${clientId}` },
      ],
    }),
    getClientEffectiveSettings: build.query<
      EffectiveSettingsResource,
      { scope: ManagementScope; clientId: string }
    >({
      query: ({ scope, clientId }) =>
        `${scopeApiPrefix(scope)}/clients/${encodeURIComponent(clientId)}/effective-settings`,
      providesTags: (_result, _error, { clientId }) => [
        { type: 'Settings', id: `client-effective:${clientId}` },
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
      invalidatesTags: (_result, _error, { clientId }) => [
        { type: 'Settings', id: `client:${clientId}` },
        { type: 'Settings', id: `client-effective:${clientId}` },
      ],
    }),
    listTenantSecrets: build.query<SecretsResource, { tenantId: string }>({
      query: ({ tenantId }) => `api/tenants/${encodeURIComponent(tenantId)}/secrets`,
      providesTags: (result, _error, { tenantId }) => [
        { type: 'Secret' as const, id: `tenant:${tenantId}` },
        ...(result?.secrets ?? []).map((s) => ({ type: 'Secret' as const, id: s.secretId })),
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
        { type: 'Secret', id: `tenant:${tenantId}` },
      ],
    }),
    deleteTenantSecret: build.mutation<void, { tenantId: string; secretId: string }>({
      query: ({ tenantId, secretId }) => ({
        url: `api/tenants/${encodeURIComponent(tenantId)}/secrets/${encodeURIComponent(secretId)}`,
        method: 'DELETE',
      }),
      invalidatesTags: (_result, _error, { tenantId, secretId }) => [
        { type: 'Secret', id: `tenant:${tenantId}` },
        { type: 'Secret', id: secretId },
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
        { type: 'Settings', id: `tenant:${tenantId}` },
      ],
    }),
    getTenantEffectiveSettings: build.query<EffectiveSettingsResource, { tenantId: string }>({
      query: ({ tenantId }) => `api/tenants/${encodeURIComponent(tenantId)}/effective-settings`,
      providesTags: (_result, _error, { tenantId }) => [
        { type: 'Settings', id: `tenant-effective:${tenantId}` },
      ],
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
        { type: 'Settings', id: `tenant:${tenantId}` },
        { type: 'Settings', id: `tenant-effective:${tenantId}` },
      ],
    }),
    listServerSecrets: build.query<SecretsResource, { serverId: string }>({
      query: ({ serverId }) => `api/servers/${encodeURIComponent(serverId)}/secrets`,
      providesTags: (result, _error, { serverId }) => [
        { type: 'Secret' as const, id: `server:${serverId}` },
        ...(result?.secrets ?? []).map((s) => ({ type: 'Secret' as const, id: s.secretId })),
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
        { type: 'Secret', id: `server:${serverId}` },
      ],
    }),
    deleteServerSecret: build.mutation<void, { serverId: string; secretId: string }>({
      query: ({ serverId, secretId }) => ({
        url: `api/servers/${encodeURIComponent(serverId)}/secrets/${encodeURIComponent(secretId)}`,
        method: 'DELETE',
      }),
      invalidatesTags: (_result, _error, { serverId, secretId }) => [
        { type: 'Secret', id: `server:${serverId}` },
        { type: 'Secret', id: secretId },
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
        { type: 'Settings', id: `server:${serverId}` },
      ],
    }),
    getServerEffectiveSettings: build.query<EffectiveSettingsResource, { serverId: string }>({
      query: ({ serverId }) => `api/servers/${encodeURIComponent(serverId)}/effective-settings`,
      providesTags: (_result, _error, { serverId }) => [
        { type: 'Settings', id: `server-effective:${serverId}` },
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
        { type: 'Settings', id: `server:${serverId}` },
        { type: 'Settings', id: `server-effective:${serverId}` },
      ],
    }),
    listResourceServerSecrets: build.query<
      SecretsResource,
      { scope: ManagementScope; resourceServerId: string }
    >({
      query: ({ scope, resourceServerId }) =>
        `${scopeApiPrefix(scope)}/resource-servers/${encodeURIComponent(resourceServerId)}/secrets`,
      providesTags: (result, _error, { resourceServerId }) => [
        { type: 'Secret' as const, id: `resource-server:${resourceServerId}` },
        ...(result?.secrets ?? []).map((s) => ({ type: 'Secret' as const, id: s.secretId })),
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
      invalidatesTags: (_result, _error, { resourceServerId }) => [
        { type: 'Secret', id: `resource-server:${resourceServerId}` },
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
      invalidatesTags: (_result, _error, { resourceServerId, secretId }) => [
        { type: 'Secret', id: `resource-server:${resourceServerId}` },
        { type: 'Secret', id: secretId },
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
      providesTags: (_result, _error, { resourceServerId }) => [
        { type: 'Settings', id: `resource-server:${resourceServerId}` },
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
      providesTags: (_result, _error, { resourceServerId }) => [
        { type: 'Settings', id: `resource-server-effective:${resourceServerId}` },
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
      invalidatesTags: (_result, _error, { resourceServerId }) => [
        { type: 'Settings', id: `resource-server:${resourceServerId}` },
        { type: 'Settings', id: `resource-server-effective:${resourceServerId}` },
      ],
    }),
  }),
});

export const {
  useGetCapabilitiesQuery,
  useListTenantsQuery,
  useGetTenantQuery,
  useListServersQuery,
  useListClientsQuery,
  useListResourceServersQuery,
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
