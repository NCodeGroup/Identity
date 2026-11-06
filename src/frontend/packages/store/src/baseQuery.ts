import {
  fetchBaseQuery,
  type BaseQueryFn,
  type FetchArgs,
  type FetchBaseQueryError,
} from '@reduxjs/toolkit/query';
import type { RuntimeConfig } from '@ncode/config';
import { sessionEnded } from './sessionSlice.js';

/**
 * The slice of root state the API client depends on. The client reads the runtime configuration so
 * it can be tier-agnostic (base URL and anti-forgery header from the BFF-served bootstrap document)
 * without baking anything at build time.
 */
export type ApiRootState = {
  readonly config: { readonly runtime: RuntimeConfig | null };
};

/**
 * A base query whose base URL is resolved at request time from the loaded {@link RuntimeConfig}. The
 * session cookie and the BFF's anti-forgery header authenticate each call; the browser holds no token
 * and sends no `Authorization` header. A `401` answer means the BFF session has ended, so the query
 * dispatches `sessionEnded()`.
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

  if (runtime.kind !== 'bff') {
    return {
      error: {
        status: 'CUSTOM_ERROR',
        error: 'The management API is called only from a surface its BFF serves.',
      } satisfies FetchBaseQueryError,
    };
  }

  const { name, value } = runtime.bff.antiforgeryHeader;
  const rawBaseQuery = fetchBaseQuery({
    baseUrl: `${runtime.apiBaseUrl}/`,
    credentials: 'same-origin',
    prepareHeaders: (headers) => {
      headers.set(name, value);
      return headers;
    },
  });

  const result = await rawBaseQuery(args, api, extraOptions);
  if (result.error?.status === 401) {
    api.dispatch(sessionEnded());
  }
  return result;
};
