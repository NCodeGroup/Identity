import { createApi } from '@reduxjs/toolkit/query/react';
import {
  scopeApiPrefix,
  type ClientResource,
  type CollectionResource,
  type ManagementScope,
  type ServerResource,
  type TenantResource,
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
  }),
});

export const { useListTenantsQuery, useGetTenantQuery, useListServersQuery, useListClientsQuery } =
  managementApi;
