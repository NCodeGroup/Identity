import {
  fetchBaseQuery,
  type BaseQueryFn,
  type FetchArgs,
  type FetchBaseQueryError,
} from '@reduxjs/toolkit/query';
import type { RuntimeConfig } from '@ncode/config';

/**
 * The slice of root state the API client depends on. The client reads these slices so it can be
 * tier-agnostic (base URL from runtime discovery) and authenticated (bearer token from the auth
 * slice) without baking anything at build time.
 */
export type ApiRootState = {
  readonly config: { readonly runtime: RuntimeConfig | null };
  readonly auth: { readonly accessToken: string | null };
};

/**
 * A base query whose base URL is resolved at request time from the discovered {@link RuntimeConfig},
 * and which attaches the management bearer token from the auth slice.
 */
export const dynamicBaseQuery: BaseQueryFn<
  string | FetchArgs,
  unknown,
  FetchBaseQueryError
> = async (args, api, extraOptions) => {
  const state = api.getState() as ApiRootState;
  const runtime = state.config.runtime;
  if (!runtime) {
    return {
      error: {
        status: 'CUSTOM_ERROR',
        error: 'Runtime configuration has not been loaded.',
      } satisfies FetchBaseQueryError,
    };
  }

  const rawBaseQuery = fetchBaseQuery({
    baseUrl: `${runtime.apiBaseUrl}/`,
    prepareHeaders: (headers) => {
      const token = state.auth.accessToken;
      if (token) {
        headers.set('authorization', `Bearer ${token}`);
      }
      return headers;
    },
  });

  return rawBaseQuery(args, api, extraOptions);
};
