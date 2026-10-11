import { combineReducers, configureStore, type Middleware, type Reducer } from '@reduxjs/toolkit';
import { authSlice } from './authSlice.js';
import { configSlice } from './configSlice.js';
import { managementApi } from './managementApi.js';

/**
 * The combined root reducer. RTK's `@reduxjs/toolkit/query/react` subpath bundles its own copies of
 * the core `Reducer`/`Middleware` declarations, which TypeScript treats as nominally distinct from
 * the root entry used by `configureStore`; casting the API's reducer/middleware to the root-entry
 * types keeps the map uniform so state inference works. The API's own cache state is internal and
 * read through the generated hooks, not `RootState`, so nothing user-facing loses typing.
 */
const rootReducer = combineReducers({
  config: configSlice.reducer,
  auth: authSlice.reducer,
  [managementApi.reducerPath]: managementApi.reducer as Reducer,
});

export type RootState = ReturnType<typeof rootReducer>;

/**
 * Assembles the shared Redux store. Each app shell calls this once at the top level; all surfaces
 * share the same config/auth slices and the Management API cache. `preloadedState` keeps the store
 * testable (hydrate with a known runtime config / token).
 */
export function createAppStore(preloadedState?: Partial<RootState>) {
  return configureStore({
    reducer: rootReducer,
    middleware: (getDefaultMiddleware) =>
      getDefaultMiddleware().concat(managementApi.middleware as Middleware),
    // `preloadedState` is spread in only when present: passing it unconditionally defeats
    // configureStore's inference of the middleware tuple, collapsing it to the default.
    ...(preloadedState !== undefined ? { preloadedState } : {}),
  });
}

export type AppStore = ReturnType<typeof createAppStore>;
export type AppDispatch = AppStore['dispatch'];
