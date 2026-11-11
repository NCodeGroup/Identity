import {
  mergeRuntimeConfig,
  parseBffBootstrapDocument,
  parseBootstrapDocument,
  parseOpenIdDiscovery,
  RuntimeConfigError,
  toBffRuntimeConfig,
} from './parse.js';
import type { BffRuntimeConfig, OpenIdRuntimeConfig, RuntimeConfig, SurfaceKind } from './types.js';

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
  /**
   * Origin that serves the static bundle and publishes the bootstrap document; defaults to the page's own origin
   * (`location.origin`), which is the serving host of every bundle — a standalone dev server proxies the document.
   */
  readonly origin?: string;
  /**
   * Which presentation surface is loading. When set, it is sent as the `surface` query parameter so the host can
   * return surface-specific coordinates (for example, each SPA's own public client id) from the one bootstrap
   * endpoint. Omit it to receive the host's default (base) document.
   */
  readonly surface?: SurfaceKind;
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

/** Resolves the injected or global fetch and the serving origin (without a trailing slash) a load reads from. */
function resolveSource(options: Omit<LoadRuntimeConfigOptions, 'surface'>): {
  readonly fetchFn: FetchLike;
  readonly origin: string;
} {
  const fetchFn = options.fetchFn ?? (globalThis.fetch as FetchLike | undefined);
  if (!fetchFn) {
    throw new RuntimeConfigError('No fetch implementation available to load runtime config.');
  }

  // Typed structurally: the package also compiles without the DOM library (the headless runner).
  const servingOrigin =
    options.origin ?? (globalThis as { location?: { origin?: string } }).location?.origin;
  if (!servingOrigin) {
    throw new RuntimeConfigError('No serving origin available to load runtime config.');
  }

  const origin = servingOrigin.endsWith('/') ? servingOrigin.slice(0, -1) : servingOrigin;
  return { fetchFn, origin };
}

/**
 * Discovers runtime configuration at boot. Fetches the host bootstrap document, then the OpenID
 * discovery metadata for the advertised issuer, and merges them into a validated {@link OpenIdRuntimeConfig}.
 */
export async function loadRuntimeConfig(
  options: LoadRuntimeConfigOptions = {},
): Promise<OpenIdRuntimeConfig> {
  const { fetchFn, origin } = resolveSource(options);
  const bootstrapUrl = options.surface
    ? `${origin}${BOOTSTRAP_PATH}?surface=${encodeURIComponent(options.surface)}`
    : `${origin}${BOOTSTRAP_PATH}`;
  const bootstrapRaw = await fetchJson(fetchFn, bootstrapUrl, options.signal);
  const bootstrap = parseBootstrapDocument(bootstrapRaw);

  const discoveryRaw = await fetchJson(
    fetchFn,
    `${bootstrap.issuer}${DISCOVERY_PATH}`,
    options.signal,
  );
  const discovery = parseOpenIdDiscovery(discoveryRaw);

  return mergeRuntimeConfig(bootstrap, discovery);
}

/**
 * Loads the runtime configuration of a surface its own BFF host serves. Fetches only the host bootstrap document,
 * which names the surface's BFF, and resolves it into a validated {@link BffRuntimeConfig}: there is no surface
 * parameter and no OpenID discovery, because the browser holds no client and never talks to the issuer.
 */
export async function loadBffRuntimeConfig(
  options: Omit<LoadRuntimeConfigOptions, 'surface'> = {},
): Promise<BffRuntimeConfig> {
  const { fetchFn, origin } = resolveSource(options);
  const raw = await fetchJson(fetchFn, `${origin}${BOOTSTRAP_PATH}`, options.signal);
  return toBffRuntimeConfig(parseBffBootstrapDocument(raw));
}

/**
 * Loads one bundle's runtime configuration, abandoning the load when the signal aborts. The boot gate takes one as a
 * dependency, so a bundle binds its surface in its composition root and a test injects its own.
 */
export type RuntimeConfigLoader = (signal: AbortSignal) => Promise<RuntimeConfig>;

/** Binds {@link loadRuntimeConfig} to a bundle's options — typically just its `surface` — as a loader. */
export function createRuntimeConfigLoader(
  options: Omit<LoadRuntimeConfigOptions, 'signal'> = {},
): RuntimeConfigLoader {
  return (signal) => loadRuntimeConfig({ ...options, signal });
}

/** Binds {@link loadBffRuntimeConfig} to a BFF-served bundle's options — typically none — as a loader. */
export function createBffRuntimeConfigLoader(
  options: Omit<LoadRuntimeConfigOptions, 'surface' | 'signal'> = {},
): RuntimeConfigLoader {
  return (signal) => loadBffRuntimeConfig({ ...options, signal });
}
