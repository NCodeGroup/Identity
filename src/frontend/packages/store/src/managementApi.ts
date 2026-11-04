import { createApi } from '@reduxjs/toolkit/query/react';
import {
  diffPatch,
  PLATFORM_SCOPE,
  LIST_TAG,
  ownerApiPath,
  ownerEffectiveTagId,
  ownerScope,
  ownerTagId,
  replacePatch,
  scopeApiPrefix,
  scopeTagId,
  teamScope,
  type ClientOwner,
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
  type CreateTeamRequest,
  type CreateTenantRequest,
  type EffectiveSettingsResource,
  type GrantResource,
  type GrantRevocationResult,
  type ManagementCapabilitiesResource,
  type ManagementOwner,
  type ManagementScope,
  type PublicKeyResource,
  type ResourceServerResource,
  type ScopeResource,
  type SecretsResource,
  type SecretOwner,
  type SettingsResource,
  type TeamResource,
  type TeamUsageResource,
  type TenantAddressResource,
  type TenantResource,
  type TenantUsageResource,
  type UpdateClientFields,
  type UpdateResourceServerFields,
  type UpdateScopeRequest,
  type UpdateTeamFields,
  type UpdateTenantFields,
} from '@ncode/api';
import { dynamicBaseQuery } from './baseQuery.js';

/** A cache-tag id in the platform scope, which owns the teams and identifies every tenant. */
const platformTag = (id: string): string => scopeTagId(PLATFORM_SCOPE, id);

/** A cache-tag id in a team's scope, which owns the list of its tenants. */
const teamTag = (teamId: string, id: string): string => scopeTagId(teamScope(teamId), id);

/** A cache-tag id in a tenant's scope, which owns its grants, secrets, and settings. */
const tenantTag = (tenantId: string, id: string): string =>
  scopeTagId({ kind: 'tenant', tenantId }, id);

/**
 * The RTK Query client for the Management REST API. Co-located with the store (one RTK module
 * boundary) so its middleware/reducer types reconcile with `configureStore`. Every cache-tag id is
 * derived from the scope that owns the resource ({@link scopeTagId}), so the platform, team and
 * tenant boundaries invalidate independently (see docs/decisions/1064): a mutation in one scope never
 * invalidates the caches of the platform scope, another team or another tenant.
 */
export const managementApi = createApi({
  reducerPath: 'managementApi',
  baseQuery: dynamicBaseQuery,
  tagTypes: ['Tenant', 'Team', 'Client', 'ResourceServer', 'Grant', 'Secret', 'Settings'],
  endpoints: (build) => ({
    getCapabilities: build.query<ManagementCapabilitiesResource, void>({
      query: () => 'api/capabilities',
    }),
    listTeams: build.query<CollectionResource<TeamResource>, void>({
      query: () => 'api/teams',
      providesTags: (result) => [
        { type: 'Team' as const, id: platformTag(LIST_TAG) },
        ...(result?.items ?? []).map((t) => ({
          type: 'Team' as const,
          id: platformTag(t.teamId),
        })),
      ],
    }),
    getTeam: build.query<TeamResource, string>({
      query: (teamId) => `api/teams/${encodeURIComponent(teamId)}`,
      providesTags: (_result, _error, teamId) => [{ type: 'Team', id: platformTag(teamId) }],
    }),
    createTeam: build.mutation<TeamResource, CreateTeamRequest>({
      query: (body) => ({ url: 'api/teams', method: 'POST', body }),
      invalidatesTags: [{ type: 'Team', id: platformTag(LIST_TAG) }],
    }),
    updateTeam: build.mutation<
      void,
      { teamId: string; concurrencyToken: string; fields: UpdateTeamFields }
    >({
      query: ({ teamId, concurrencyToken, fields }) => ({
        url: `api/teams/${encodeURIComponent(teamId)}`,
        method: 'PATCH',
        body: replacePatch({ ...fields }),
        headers: { 'If-Match': concurrencyToken },
      }),
      invalidatesTags: (_result, _error, { teamId }) => [
        { type: 'Team', id: platformTag(LIST_TAG) },
        { type: 'Team', id: platformTag(teamId) },
      ],
    }),
    deleteTeam: build.mutation<void, { teamId: string }>({
      query: ({ teamId }) => ({
        url: `api/teams/${encodeURIComponent(teamId)}`,
        method: 'DELETE',
      }),
      invalidatesTags: (_result, _error, { teamId }) => [
        { type: 'Team', id: platformTag(LIST_TAG) },
        { type: 'Team', id: platformTag(teamId) },
      ],
    }),
    // The usage ledger of a team's tenants grows with every flush of the server (ADR-1199), so nothing is cached once
    // unused: every mount reads the current ledger.
    getTeamUsage: build.query<
      TeamUsageResource,
      { teamId: string; from?: string | undefined; to?: string | undefined }
    >({
      query: ({ teamId, from, to }) => {
        const params = new URLSearchParams();
        if (from) params.set('from', from);
        if (to) params.set('to', to);
        const search = params.toString();
        return `api/teams/${encodeURIComponent(teamId)}/usage${search ? `?${search}` : ''}`;
      },
      keepUnusedDataFor: 0,
    }),
    // The tenants of one team; each tenant's own tag lives in the platform scope, where its id is unique.
    listTenants: build.query<CollectionResource<TenantResource>, { teamId: string }>({
      query: ({ teamId }) => `api/teams/${encodeURIComponent(teamId)}/tenants`,
      providesTags: (result, _error, { teamId }) => [
        { type: 'Tenant' as const, id: teamTag(teamId, LIST_TAG) },
        ...(result?.items ?? []).map((t) => ({
          type: 'Tenant' as const,
          id: platformTag(t.tenantId),
        })),
      ],
    }),
    getTenant: build.query<TenantResource, string>({
      query: (tenantId) => `api/tenants/${encodeURIComponent(tenantId)}`,
      providesTags: (_result, _error, tenantId) => [{ type: 'Tenant', id: platformTag(tenantId) }],
    }),
    // A preview of the address an existing tenant has, or would have with a proposed issuer or alias. It
    // is never cached beyond its subscriber, so it always reflects the tenants that exist when it is asked.
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
    // A preview of the address a tenant that does not exist yet would have in a team. It is never cached
    // beyond its subscriber, so it always reflects the tenants that exist when it is asked.
    getNewTenantAddress: build.query<
      TenantAddressResource,
      { teamId: string; tenantId: string; issuer?: string; mtlsEndpointAliasAuthority?: string }
    >({
      query: ({ teamId, tenantId, issuer, mtlsEndpointAliasAuthority }) => {
        const params = {
          ...(issuer ? { issuer } : {}),
          ...(mtlsEndpointAliasAuthority ? { mtlsEndpointAliasAuthority } : {}),
        };
        return {
          url: `api/teams/${encodeURIComponent(teamId)}/tenants/${encodeURIComponent(tenantId)}/address`,
          ...(Object.keys(params).length > 0 ? { params } : {}),
        };
      },
      keepUnusedDataFor: 0,
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
    createTenant: build.mutation<TenantResource, { teamId: string; body: CreateTenantRequest }>({
      query: ({ teamId, body }) => ({
        url: `api/teams/${encodeURIComponent(teamId)}/tenants`,
        method: 'POST',
        body,
      }),
      invalidatesTags: (_result, _error, { teamId }) => [
        { type: 'Tenant', id: teamTag(teamId, LIST_TAG) },
      ],
    }),
    updateTenant: build.mutation<
      void,
      { teamId: string; tenantId: string; concurrencyToken: string; fields: UpdateTenantFields }
    >({
      query: ({ tenantId, concurrencyToken, fields }) => ({
        url: `api/tenants/${encodeURIComponent(tenantId)}`,
        method: 'PATCH',
        body: replacePatch({ ...fields }),
        headers: { 'If-Match': concurrencyToken },
      }),
      invalidatesTags: (_result, _error, { teamId, tenantId }) => [
        { type: 'Tenant', id: teamTag(teamId, LIST_TAG) },
        { type: 'Tenant', id: platformTag(tenantId) },
      ],
    }),
    deleteTenant: build.mutation<void, { teamId: string; tenantId: string }>({
      query: ({ tenantId }) => ({
        url: `api/tenants/${encodeURIComponent(tenantId)}`,
        method: 'DELETE',
      }),
      invalidatesTags: (_result, _error, { teamId, tenantId }) => [
        { type: 'Tenant', id: teamTag(teamId, LIST_TAG) },
        { type: 'Tenant', id: platformTag(tenantId) },
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
    // The usage ledger grows with every flush of the server (ADR-1199), so nothing is cached once unused: every mount
    // reads the current ledger.
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
      keepUnusedDataFor: 0,
    }),
    // The secrets of a tenant, or of a client or resource server in a scope, and the settings of any owner: the
    // platform, a team, a tenant, or a client or resource server (docs/decisions/1257). Every tag is scoped by the
    // owner's scope, so one owner's caches never invalidate another's.
    listSecrets: build.query<SecretsResource, SecretOwner>({
      query: (owner) => `${ownerApiPath(owner)}/secrets`,
      providesTags: (result, _error, owner) => [
        { type: 'Secret' as const, id: ownerTagId(owner) },
        ...(result?.secrets ?? []).map((s) => ({
          type: 'Secret' as const,
          id: scopeTagId(ownerScope(owner), s.secretId),
        })),
      ],
    }),
    createSecret: build.mutation<
      CreatedSecretResource,
      { owner: SecretOwner; body: CreateSecretRequest }
    >({
      query: ({ owner, body }) => ({
        url: `${ownerApiPath(owner)}/secrets`,
        method: 'POST',
        body,
      }),
      invalidatesTags: (_result, _error, { owner }) => [{ type: 'Secret', id: ownerTagId(owner) }],
    }),
    deleteSecret: build.mutation<void, { owner: SecretOwner; secretId: string }>({
      query: ({ owner, secretId }) => ({
        url: `${ownerApiPath(owner)}/secrets/${encodeURIComponent(secretId)}`,
        method: 'DELETE',
      }),
      invalidatesTags: (_result, _error, { owner, secretId }) => [
        { type: 'Secret', id: ownerTagId(owner) },
        { type: 'Secret', id: scopeTagId(ownerScope(owner), secretId) },
      ],
    }),
    // Only a client's secrets rotate in one call, so the owner is a client by type.
    rotateSecret: build.mutation<
      CreatedSecretResource,
      { owner: ClientOwner; secretId: string; body: RotateSecretRequest }
    >({
      query: ({ owner, secretId, body }) => ({
        url: `${ownerApiPath(owner)}/secrets/${encodeURIComponent(secretId)}/rotate`,
        method: 'POST',
        body,
      }),
      invalidatesTags: (_result, _error, { owner, secretId }) => [
        { type: 'Secret', id: ownerTagId(owner) },
        { type: 'Secret', id: scopeTagId(ownerScope(owner), secretId) },
      ],
    }),
    getSecretPublicKey: build.query<PublicKeyResource, { owner: SecretOwner; secretId: string }>({
      query: ({ owner, secretId }) =>
        `${ownerApiPath(owner)}/secrets/${encodeURIComponent(secretId)}/public-key`,
    }),
    getSettings: build.query<SettingsResource, ManagementOwner>({
      query: (owner) => `${ownerApiPath(owner)}/settings`,
      providesTags: (_result, _error, owner) => [{ type: 'Settings', id: ownerTagId(owner) }],
    }),
    getEffectiveSettings: build.query<EffectiveSettingsResource, ManagementOwner>({
      query: (owner) => `${ownerApiPath(owner)}/effective-settings`,
      providesTags: (_result, _error, owner) => [
        { type: 'Settings', id: ownerEffectiveTagId(owner) },
      ],
    }),
    // Settings are an opaque object, so updates are a top-level add/remove/replace diff (not replace-only). A client's
    // settings change can issue the secret its client-authentication method needs, which the response carries once
    // (docs/decisions/1391); every other change answers 204 with no body, which resolves to null.
    updateSettings: build.mutation<
      CreatedSecretResource | null,
      {
        owner: ManagementOwner;
        concurrencyToken: string;
        original: Readonly<Record<string, unknown>>;
        desired: Readonly<Record<string, unknown>>;
      }
    >({
      query: ({ owner, concurrencyToken, original, desired }) => ({
        url: `${ownerApiPath(owner)}/settings`,
        method: 'PATCH',
        body: diffPatch(original, desired),
        headers: { 'If-Match': concurrencyToken },
      }),
      invalidatesTags: (_result, _error, { owner }) => [
        { type: 'Settings', id: ownerTagId(owner) },
        { type: 'Settings', id: ownerEffectiveTagId(owner) },
        { type: 'Secret', id: ownerTagId(owner) },
      ],
    }),
  }),
});

export const {
  useGetCapabilitiesQuery,
  useListTenantsQuery,
  useGetTenantQuery,
  useGetTenantAddressQuery,
  useGetNewTenantAddressQuery,
  useListTeamsQuery,
  useGetTeamQuery,
  useGetTeamUsageQuery,
  useListClientsQuery,
  useListResourceServersQuery,
  useCreateTeamMutation,
  useUpdateTeamMutation,
  useDeleteTeamMutation,
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
  useGetTenantUsageQuery,
  useListSecretsQuery,
  useCreateSecretMutation,
  useDeleteSecretMutation,
  useRotateSecretMutation,
  useLazyGetSecretPublicKeyQuery,
  useGetSettingsQuery,
  useGetEffectiveSettingsQuery,
  useUpdateSettingsMutation,
} = managementApi;
