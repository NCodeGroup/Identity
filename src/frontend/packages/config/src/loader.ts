import {
  mergeRuntimeConfig,
  parseBootstrapDocument,
  parseOpenIdDiscovery,
  RuntimeConfigError,
} from './parse.js';
import type { RuntimeConfig } from './types.js';

/** Well-known path, relative to the serving host, of the bootstrap document. */
export const BOOTSTRAP_PATH = '/config/bootstrap.json';

/** Standard OpenID discovery path, relative to the issuer. */
export const DISCOVERY_PATH = '/.well-known/openid-configuration';

/** Minimal fetch surface so the loader is testable without a DOM/network. */
export type FetchLike = (
  url: string,
  init?: { signal?: AbortSignal },
) => Promise<{
  readonly ok: boolean;
  readonly status: number;
  json(): Promise<unknown>;
}>;

export type LoadRuntimeConfigOptions = {
  /** Origin that serves the static bundle and publishes the bootstrap document. */
  readonly origin: string;
  /** Injected fetch; defaults to global `fetch` in the browser. */
  readonly fetchFn?: FetchLike;
  readonly signal?: AbortSignal;
};

async function fetchJson(fetchFn: FetchLike, url: string, signal?: AbortSignal): Promise<unknown> {
  const response = await fetchFn(url, signal ? { signal } : undefined);
  if (!response.ok) {
    throw new RuntimeConfigError(`Failed to fetch '${url}' (HTTP ${response.status}).`);
  }
  return response.json();
}

/**
 * Discovers runtime configuration at boot. Fetches the host bootstrap document, then the OpenID
 * discovery metadata for the advertised issuer, and merges them into a validated {@link RuntimeConfig}.
 */
export async function loadRuntimeConfig(options: LoadRuntimeConfigOptions): Promise<RuntimeConfig> {
  const fetchFn = options.fetchFn ?? (globalThis.fetch as FetchLike | undefined);
  if (!fetchFn) {
    throw new RuntimeConfigError('No fetch implementation available to load runtime config.');
  }

  const origin = options.origin.endsWith('/') ? options.origin.slice(0, -1) : options.origin;
  const bootstrapRaw = await fetchJson(fetchFn, `${origin}${BOOTSTRAP_PATH}`, options.signal);
  const bootstrap = parseBootstrapDocument(bootstrapRaw);

  const discoveryRaw = await fetchJson(
    fetchFn,
    `${bootstrap.issuer}${DISCOVERY_PATH}`,
    options.signal,
  );
  const discovery = parseOpenIdDiscovery(discoveryRaw);

  return mergeRuntimeConfig(bootstrap, discovery);
}
