import { loadRuntimeConfig, type RuntimeConfig } from '@ncode/config';
import { fetchRawDiscovery, type FetchLike, type RawDiscovery } from '@ncode/conformance-core';

/** Resolves the origin that serves this bundle and its bootstrap document. */
export function currentOrigin(): string {
  return window.location.origin;
}

/**
 * Loads the tier-agnostic runtime configuration for this bundle, declaring the `conformance` surface. The dashboard
 * is read-only and performs no interactive login, so the host returns no client id for this surface.
 */
export function bootstrapRuntimeConfig(signal?: AbortSignal): Promise<RuntimeConfig> {
  return loadRuntimeConfig({
    origin: currentOrigin(),
    surface: 'conformance',
    ...(signal ? { signal } : {}),
  });
}

/** Fetches the OpenID Provider's raw discovery metadata for the discovered issuer using the global fetch. */
export function loadDiscovery(issuer: string, signal?: AbortSignal): Promise<RawDiscovery> {
  return fetchRawDiscovery(issuer, fetch as FetchLike, signal);
}
