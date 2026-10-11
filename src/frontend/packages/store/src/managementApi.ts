import { createApi } from '@reduxjs/toolkit/query/react';
import {
  replacePatch,
  scopeApiPrefix,
  type ClientResource,
  type CollectionResource,
  type CreateClientRequest,
  type CreateTenantRequest,
  type ManagementScope,
  type ServerResource,
  type TenantResource,
  type UpdateClientFields,
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
  tagTypes: ['Tenant', 'Server', 'Client'],
  endpoints: (build) => ({
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
  }),
});

export const {
  useListTenantsQuery,
  useGetTenantQuery,
  useListServersQuery,
  useListClientsQuery,
  useCreateTenantMutation,
  useUpdateTenantMutation,
  useDeleteTenantMutation,
  useCreateClientMutation,
  useUpdateClientMutation,
  useDeleteClientMutation,
} = managementApi;
