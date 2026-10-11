import type { BootstrapDocument, OpenIdDiscovery, RuntimeConfig } from './types.js';

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

  const branding =
    doc.branding && typeof doc.branding === 'object'
      ? (doc.branding as BootstrapDocument['branding'])
      : undefined;

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

/** Merges a bootstrap document and OpenID discovery metadata into the resolved runtime config. */
export function mergeRuntimeConfig(
  bootstrap: BootstrapDocument,
  discovery: OpenIdDiscovery,
): RuntimeConfig {
  if (normalizeBaseUrl(discovery.issuer) !== bootstrap.issuer) {
    throw new RuntimeConfigError(
      `Issuer mismatch: bootstrap '${bootstrap.issuer}' !== discovery '${discovery.issuer}'.`,
    );
  }
  return {
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
