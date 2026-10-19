import { fetchRawDiscovery, type FetchLike, type RawDiscovery } from '@ncode/conformance-core';

/** Fetches the OpenID Provider's raw discovery metadata for the discovered issuer using the global fetch. */
export function loadDiscovery(issuer: string, signal?: AbortSignal): Promise<RawDiscovery> {
  return fetchRawDiscovery(issuer, fetch as FetchLike, signal);
}
