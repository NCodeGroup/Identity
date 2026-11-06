import type {
  BffBootstrapDocument,
  BffRuntimeConfig,
  BootstrapDocument,
  BrandingConfig,
  EnvironmentConfig,
  OpenIdDiscovery,
  OpenIdRuntimeConfig,
} from './types.js';

/** Thrown when runtime configuration cannot be discovered or is invalid. */
export class RuntimeConfigError extends Error {
  constructor(message: string) {
    super(message);
    this.name = 'RuntimeConfigError';
  }
}

/** Removes a single trailing slash so base URLs join predictably. */
export function normalizeBaseUrl(value: string): string {
  return value.endsWith('/') ? value.slice(0, -1) : value;
}

function requireNonEmpty(value: unknown, field: string): string {
  if (typeof value !== 'string' || value.trim().length === 0) {
    throw new RuntimeConfigError(`Bootstrap document is missing required field '${field}'.`);
  }
  return value;
}

/** Default login scopes: `openid` plus one management scope that binds the `urn:ncode:management` audience. */
export const DEFAULT_SCOPES: readonly string[] = ['openid', 'read:clients'];

/**
 * Reads a tenant's non-production environment from an untrusted value. Returns `undefined` unless the value is an
 * object with a non-blank string `type`; the `name` is kept only when it is a non-blank string.
 */
function parseEnvironmentConfig(raw: unknown): EnvironmentConfig | undefined {
  if (raw === null || typeof raw !== 'object' || Array.isArray(raw)) {
    return undefined;
  }
  const source = raw as Record<string, unknown>;
  const text = (value: unknown): string | undefined =>
    typeof value === 'string' && value.trim() ? value : undefined;

  const type = text(source.type);
  if (!type) {
    return undefined;
  }
  const name = text(source.name);
  return { type, ...(name ? { name } : {}) };
}

/**
 * Reads the brandable theme tokens from an untrusted value, keeping each token only when it has the expected shape.
 * Returns `undefined` when the value is not an object or sets no usable token, so the default theme applies.
 */
export function parseBrandingConfig(raw: unknown): BrandingConfig | undefined {
  if (raw === null || typeof raw !== 'object' || Array.isArray(raw)) {
    return undefined;
  }
  const source = raw as Record<string, unknown>;
  const text = (value: unknown): string | undefined =>
    typeof value === 'string' && value.trim() ? value : undefined;

  const displayName = text(source.displayName);
  const logoUrl = text(source.logoUrl);
  const primaryColor = text(source.primaryColor);
  const secondaryColor = text(source.secondaryColor);
  const mode = source.mode === 'light' || source.mode === 'dark' ? source.mode : undefined;
  const environment = parseEnvironmentConfig(source.environment);

  const branding: BrandingConfig = {
    ...(displayName ? { displayName } : {}),
    ...(logoUrl ? { logoUrl } : {}),
    ...(primaryColor ? { primaryColor } : {}),
    ...(secondaryColor ? { secondaryColor } : {}),
    ...(mode ? { mode } : {}),
    ...(environment ? { environment } : {}),
  };
  return Object.keys(branding).length > 0 ? branding : undefined;
}

/** Validates and normalizes the host-published bootstrap document. */
export function parseBootstrapDocument(raw: unknown): BootstrapDocument {
  if (raw === null || typeof raw !== 'object') {
    throw new RuntimeConfigError('Bootstrap document must be a JSON object.');
  }
  const doc = raw as Record<string, unknown>;
  const apiBaseUrl = normalizeBaseUrl(requireNonEmpty(doc.apiBaseUrl, 'apiBaseUrl'));
  const issuer = normalizeBaseUrl(requireNonEmpty(doc.issuer, 'issuer'));

  const features =
    doc.features && typeof doc.features === 'object'
      ? (doc.features as Record<string, boolean>)
      : {};

  const branding = parseBrandingConfig(doc.branding);

  const scopes =
    Array.isArray(doc.scopes) && doc.scopes.every((s) => typeof s === 'string')
      ? (doc.scopes as string[])
      : undefined;

  return {
    apiBaseUrl,
    issuer,
    features,
    ...(typeof doc.clientId === 'string' && doc.clientId ? { clientId: doc.clientId } : {}),
    ...(scopes ? { scopes } : {}),
    ...(branding ? { branding } : {}),
  };
}

/** Validates and normalizes the bootstrap document a BFF-served surface's host publishes. */
export function parseBffBootstrapDocument(raw: unknown): BffBootstrapDocument {
  if (raw === null || typeof raw !== 'object') {
    throw new RuntimeConfigError('Bootstrap document must be a JSON object.');
  }
  const doc = raw as Record<string, unknown>;
  const apiBaseUrl = normalizeBaseUrl(requireNonEmpty(doc.apiBaseUrl, 'apiBaseUrl'));

  if (doc.bff === null || typeof doc.bff !== 'object' || Array.isArray(doc.bff)) {
    throw new RuntimeConfigError("Bootstrap document is missing required field 'bff'.");
  }
  const bff = doc.bff as Record<string, unknown>;
  const basePath = normalizeBaseUrl(requireNonEmpty(bff.basePath, 'bff.basePath'));
  if (!basePath.startsWith('/')) {
    throw new RuntimeConfigError("Bootstrap document field 'bff.basePath' must start with '/'.");
  }

  const header =
    bff.antiforgeryHeader !== null &&
    typeof bff.antiforgeryHeader === 'object' &&
    !Array.isArray(bff.antiforgeryHeader)
      ? (bff.antiforgeryHeader as Record<string, unknown>)
      : {};
  const name = requireNonEmpty(header.name, 'bff.antiforgeryHeader.name');
  const value = requireNonEmpty(header.value, 'bff.antiforgeryHeader.value');

  const features =
    doc.features && typeof doc.features === 'object'
      ? (doc.features as Record<string, boolean>)
      : {};

  const branding = parseBrandingConfig(doc.branding);

  return {
    apiBaseUrl,
    bff: { basePath, antiforgeryHeader: { name, value } },
    features,
    ...(branding ? { branding } : {}),
  };
}

/** Validates the OpenID discovery metadata the frontend depends on. */
export function parseOpenIdDiscovery(raw: unknown): OpenIdDiscovery {
  if (raw === null || typeof raw !== 'object') {
    throw new RuntimeConfigError('Discovery document must be a JSON object.');
  }
  const doc = raw as Record<string, unknown>;
  return {
    issuer: requireNonEmpty(doc.issuer, 'issuer'),
    authorization_endpoint: requireNonEmpty(doc.authorization_endpoint, 'authorization_endpoint'),
    token_endpoint: requireNonEmpty(doc.token_endpoint, 'token_endpoint'),
    ...(typeof doc.end_session_endpoint === 'string'
      ? { end_session_endpoint: doc.end_session_endpoint }
      : {}),
    ...(typeof doc.userinfo_endpoint === 'string'
      ? { userinfo_endpoint: doc.userinfo_endpoint }
      : {}),
    ...(typeof doc.jwks_uri === 'string' ? { jwks_uri: doc.jwks_uri } : {}),
  };
}

/** Merges a bootstrap document and OpenID discovery metadata into the resolved runtime config of an OpenID surface. */
export function mergeRuntimeConfig(
  bootstrap: BootstrapDocument,
  discovery: OpenIdDiscovery,
): OpenIdRuntimeConfig {
  if (normalizeBaseUrl(discovery.issuer) !== bootstrap.issuer) {
    throw new RuntimeConfigError(
      `Issuer mismatch: bootstrap '${bootstrap.issuer}' !== discovery '${discovery.issuer}'.`,
    );
  }
  return {
    kind: 'openid',
    apiBaseUrl: bootstrap.apiBaseUrl,
    issuer: bootstrap.issuer,
    authorizationEndpoint: discovery.authorization_endpoint,
    tokenEndpoint: discovery.token_endpoint,
    scopes: bootstrap.scopes && bootstrap.scopes.length > 0 ? bootstrap.scopes : DEFAULT_SCOPES,
    features: bootstrap.features ?? {},
    ...(bootstrap.clientId ? { clientId: bootstrap.clientId } : {}),
    ...(discovery.end_session_endpoint
      ? { endSessionEndpoint: discovery.end_session_endpoint }
      : {}),
    ...(discovery.userinfo_endpoint ? { userInfoEndpoint: discovery.userinfo_endpoint } : {}),
    ...(bootstrap.branding ? { branding: bootstrap.branding } : {}),
  };
}

/** Resolves a BFF-served surface's bootstrap document into its runtime config. */
export function toBffRuntimeConfig(document: BffBootstrapDocument): BffRuntimeConfig {
  return {
    kind: 'bff',
    apiBaseUrl: document.apiBaseUrl,
    bff: document.bff,
    features: document.features ?? {},
    ...(document.branding ? { branding: document.branding } : {}),
  };
}
