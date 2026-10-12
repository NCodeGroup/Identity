import { loadRuntimeConfig, type RuntimeConfig } from '@ncode/config';
import type { FetchLike } from '@ncode/auth';
import type { FlowDeps } from './flow.js';

/** Resolves the origin that serves this bundle and its bootstrap document. */
export function currentOrigin(): string {
  return window.location.origin;
}

/**
 * Loads the tier-agnostic runtime configuration for this bundle, declaring the `playground` surface so the host
 * returns this surface's own coordinates (its seeded public client id and scopes). Kept separate from React so the
 * boot sequence is testable.
 */
export function bootstrapRuntimeConfig(signal?: AbortSignal): Promise<RuntimeConfig> {
  return loadRuntimeConfig({
    origin: currentOrigin(),
    surface: 'playground',
    ...(signal ? { signal } : {}),
  });
}

/** The runtime dependencies for the flow explorer: global fetch and session-scoped transaction storage. */
export function createFlowDeps(): FlowDeps {
  return {
    // Omit an empty body: the browser `fetch` rejects a GET/HEAD request that carries one, and the UserInfo call is a
    // bodyless GET while the token/refresh calls carry a form body.
    fetchFn: ((url, init) =>
      fetch(url, {
        method: init.method,
        headers: init.headers,
        ...(init.body ? { body: init.body } : {}),
      })) as FetchLike,
    storage: window.sessionStorage,
  };
}
