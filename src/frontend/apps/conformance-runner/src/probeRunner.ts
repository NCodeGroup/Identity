/**
 * Probe-runner probes (UI-free, unit-tested) — the confidential and server-initiated legs a browser cannot drive.
 *
 * The browser dashboard's active probes ([@ncode/conformance-core](../../../packages/conformance-core/src/probes.ts))
 * verify the CORS-reachable, credential-free endpoints. These probes complete the picture for the legs that need a
 * client secret, an asymmetric client key, a client certificate, or a server-to-server call the browser secret model
 * and CORS forbid: `client_credentials`, introspection/revocation with client authentication, `private_key_jwt`,
 * device-code polling, CIBA, DPoP/mTLS, and back-channel logout. Every probe is a plain async function over an injected
 * {@link ProbeFetch} (and, for the asymmetric legs, an injected signer) so the runner stays deterministic and testable
 * without a network or real keys. Phase 3 of [ADR-0084](../../../../docs/adr/0084-spec-coverage-conformance-harness.md).
 */

import type { ProbeFetch, ProbeReport, ProbeResult, RawDiscovery } from '@ncode/conformance-core';

/** How the runner authenticates a confidential client at the token/introspection/revocation endpoints. */
export type ClientAuthMethod = 'client_secret_basic' | 'client_secret_post';

/** A confidential client the runner authenticates as for the server-to-server legs. */
export type ConfidentialClient = {
  readonly clientId: string;
  readonly clientSecret: string;
  readonly authMethod: ClientAuthMethod;
};

/** Signs a `private_key_jwt` client assertion from the given claims, returning a compact JWS. */
export type ClientAssertionSigner = (claims: Readonly<Record<string, unknown>>) => Promise<string>;

/** Builds a DPoP proof JWT bound to an HTTP method + URI (RFC 9449 §4), returning a compact JWS. */
export type DpopProofFactory = (htm: string, htu: string) => Promise<string>;

/** Everything the runner needs to exercise the confidential and server-initiated legs. */
export type ProbeRunnerContext = {
  /** The confidential client used for `client_credentials`, introspection, and revocation. */
  readonly client?: ConfidentialClient;
  /** A public client id for the device-authorization and CIBA legs that accept an unauthenticated client. */
  readonly publicClientId?: string;
  /** The scope requested by the token-issuing probes (defaults to none / the provider default). */
  readonly scope?: string;
  /** A `login_hint` for the CIBA backchannel-authentication probe. */
  readonly loginHint?: string;
  /** Signs a `private_key_jwt` client assertion; absent when no asymmetric client key is configured. */
  readonly signClientAssertion?: ClientAssertionSigner;
  /** Builds a DPoP proof; absent when no DPoP key is configured. */
  readonly createDpopProof?: DpopProofFactory;
  readonly signal?: AbortSignal;
};

const CLIENT_CREDENTIALS = 'client_credentials';
const DEVICE_CODE_GRANT = 'urn:ietf:params:oauth:grant-type:device_code';
const CLIENT_ASSERTION_TYPE = 'urn:ietf:params:oauth:client-assertion-type:jwt-bearer';

function asString(raw: RawDiscovery, key: string): string | undefined {
  const value = raw[key];
  return typeof value === 'string' && value.length > 0 ? value : undefined;
}

function asStringArray(raw: RawDiscovery, key: string): string[] {
  const value = raw[key];
  return Array.isArray(value)
    ? value.filter((item): item is string => typeof item === 'string')
    : [];
}

function isObject(value: unknown): value is Record<string, unknown> {
  return value !== null && typeof value === 'object' && !Array.isArray(value);
}

function base64(value: string): string {
  return Buffer.from(value, 'utf8').toString('base64');
}

function skip(id: string, feature: string, reference: string, detail: string): ProbeResult {
  return { id, feature, outcome: 'skipped', detail, reference };
}

function networkDetail(cause: unknown): string {
  return cause instanceof Error ? `Request failed: ${cause.message}.` : 'Request failed.';
}

/**
 * Applies the confidential client's authentication to a form POST: `client_secret_basic` adds the RFC 6749 §2.3.1
 * Authorization header (over percent-encoded credentials), `client_secret_post` carries the credentials in the body.
 */
function applyClientAuth(
  client: ConfidentialClient,
  params: URLSearchParams,
): Record<string, string> {
  const headers: Record<string, string> = {
    'content-type': 'application/x-www-form-urlencoded',
    accept: 'application/json',
  };
  if (client.authMethod === 'client_secret_basic') {
    const credentials = `${encodeURIComponent(client.clientId)}:${encodeURIComponent(client.clientSecret)}`;
    headers.authorization = `Basic ${base64(credentials)}`;
  } else {
    params.set('client_id', client.clientId);
    params.set('client_secret', client.clientSecret);
  }
  return headers;
}

/** Reads a JSON body without throwing on a non-JSON response. */
async function readJson(response: {
  json(): Promise<unknown>;
}): Promise<Record<string, unknown> | null> {
  const body = await response.json().catch(() => null);
  return isObject(body) ? body : null;
}

/**
 * Verifies the `client_credentials` grant (RFC 6749 §4.4): a confidential client exchanges its credentials for an
 * access token. A structured OAuth error still proves the endpoint processed the grant; `unsupported_grant_type`
 * skips (the grant is simply not offered), and `invalid_client` fails (the configured credentials were rejected).
 */
export async function probeClientCredentials(
  raw: RawDiscovery,
  fetchFn: ProbeFetch,
  context: ProbeRunnerContext,
): Promise<ProbeResult> {
  const id = 'runner-client-credentials';
  const feature = 'client_credentials grant issues an access token';
  const reference = 'RFC 6749 §4.4';
  const url = asString(raw, 'token_endpoint');
  if (!url) {
    return skip(id, feature, reference, "'token_endpoint' is not advertised.");
  }
  if (!context.client) {
    return skip(id, feature, reference, 'No confidential client configured.');
  }
  const params = new URLSearchParams({ grant_type: CLIENT_CREDENTIALS });
  if (context.scope) {
    params.set('scope', context.scope);
  }
  const headers = applyClientAuth(context.client, params);
  try {
    const response = await fetchFn(url, {
      method: 'POST',
      headers,
      body: params.toString(),
      ...(context.signal ? { signal: context.signal } : {}),
    });
    const body = await readJson(response);
    if (response.ok && body && typeof body.access_token === 'string') {
      return { id, feature, outcome: 'verified', detail: 'Issued an access token.', reference };
    }
    return classifyTokenError(id, feature, reference, response.status, body);
  } catch (cause) {
    return { id, feature, outcome: 'failed', detail: networkDetail(cause), reference };
  }
}

/** Shared classification for a token-endpoint error response across the token-issuing probes. */
function classifyTokenError(
  id: string,
  feature: string,
  reference: string,
  status: number,
  body: Record<string, unknown> | null,
): ProbeResult {
  const error = body && typeof body.error === 'string' ? body.error : undefined;
  if (error === 'invalid_client') {
    return {
      id,
      feature,
      outcome: 'failed',
      detail: 'Client authentication was rejected (invalid_client).',
      reference,
    };
  }
  if (error === 'unsupported_grant_type') {
    return skip(id, feature, reference, 'The grant is not supported (unsupported_grant_type).');
  }
  if (error) {
    return {
      id,
      feature,
      outcome: 'verified',
      detail: `Endpoint processed the grant; rejected this probe with '${error}'.`,
      reference,
    };
  }
  return {
    id,
    feature,
    outcome: 'failed',
    detail: `HTTP ${status} without a JSON 'error' field.`,
    reference,
  };
}

/**
 * Verifies token introspection (RFC 7662): an authenticated client introspecting an unknown token must receive
 * `200 { "active": false }`. A token that reports `active: true` for a bogus value, or a non-200 response, fails.
 */
export async function probeIntrospection(
  raw: RawDiscovery,
  fetchFn: ProbeFetch,
  context: ProbeRunnerContext,
): Promise<ProbeResult> {
  const id = 'runner-introspection';
  const feature = 'Introspection reports an unknown token as inactive';
  const reference = 'RFC 7662 §2.2';
  const url = asString(raw, 'introspection_endpoint');
  if (!url) {
    return skip(id, feature, reference, "'introspection_endpoint' is not advertised.");
  }
  if (!context.client) {
    return skip(
      id,
      feature,
      reference,
      'Introspection requires client authentication; none configured.',
    );
  }
  const params = new URLSearchParams({ token: 'ncode-conformance-probe-unknown-token' });
  const headers = applyClientAuth(context.client, params);
  try {
    const response = await fetchFn(url, {
      method: 'POST',
      headers,
      body: params.toString(),
      ...(context.signal ? { signal: context.signal } : {}),
    });
    const body = await readJson(response);
    if (response.status === 401 || (body && body.error === 'invalid_client')) {
      return {
        id,
        feature,
        outcome: 'failed',
        detail: 'Client authentication was rejected.',
        reference,
      };
    }
    if (response.ok && body && body.active === false) {
      return {
        id,
        feature,
        outcome: 'verified',
        detail: 'An unknown token was reported as inactive.',
        reference,
      };
    }
    if (response.ok && body && body.active === true) {
      return {
        id,
        feature,
        outcome: 'failed',
        detail: 'An unknown token was reported as ACTIVE.',
        reference,
      };
    }
    return {
      id,
      feature,
      outcome: 'failed',
      detail: `Unexpected introspection response (HTTP ${response.status}).`,
      reference,
    };
  } catch (cause) {
    return { id, feature, outcome: 'failed', detail: networkDetail(cause), reference };
  }
}

/**
 * Verifies token revocation (RFC 7009): revoking an unknown token must still return `200` — the endpoint does not
 * leak whether the token existed. Client authentication is required for a confidential client.
 */
export async function probeRevocation(
  raw: RawDiscovery,
  fetchFn: ProbeFetch,
  context: ProbeRunnerContext,
): Promise<ProbeResult> {
  const id = 'runner-revocation';
  const feature = 'Revocation returns 200 for an unknown token';
  const reference = 'RFC 7009 §2.2';
  const url = asString(raw, 'revocation_endpoint');
  if (!url) {
    return skip(id, feature, reference, "'revocation_endpoint' is not advertised.");
  }
  if (!context.client) {
    return skip(
      id,
      feature,
      reference,
      'Revocation requires client authentication; none configured.',
    );
  }
  const params = new URLSearchParams({ token: 'ncode-conformance-probe-unknown-token' });
  const headers = applyClientAuth(context.client, params);
  try {
    const response = await fetchFn(url, {
      method: 'POST',
      headers,
      body: params.toString(),
      ...(context.signal ? { signal: context.signal } : {}),
    });
    if (response.status === 200) {
      return {
        id,
        feature,
        outcome: 'verified',
        detail: 'Revoking an unknown token returned 200.',
        reference,
      };
    }
    const body = await readJson(response);
    if (response.status === 401 || (body && body.error === 'invalid_client')) {
      return {
        id,
        feature,
        outcome: 'failed',
        detail: 'Client authentication was rejected.',
        reference,
      };
    }
    return {
      id,
      feature,
      outcome: 'failed',
      detail: `Expected 200, got HTTP ${response.status}.`,
      reference,
    };
  } catch (cause) {
    return { id, feature, outcome: 'failed', detail: networkDetail(cause), reference };
  }
}

/**
 * Verifies the Device Authorization grant (RFC 8628): a device-code request returns a `device_code`, `user_code`, and
 * verification URI, and a single immediate token poll is rejected with `authorization_pending` (or `slow_down`). Uses
 * the confidential client when configured, otherwise the public client id.
 */
export async function probeDeviceAuthorization(
  raw: RawDiscovery,
  fetchFn: ProbeFetch,
  context: ProbeRunnerContext,
): Promise<ProbeResult> {
  const id = 'runner-device-code';
  const feature = 'Device authorization issues a device_code and polls';
  const reference = 'RFC 8628 §3.2 / §3.5';
  const url = asString(raw, 'device_authorization_endpoint');
  if (!url) {
    return skip(id, feature, reference, "'device_authorization_endpoint' is not advertised.");
  }
  const clientId = context.client?.clientId ?? context.publicClientId;
  if (!clientId) {
    return skip(id, feature, reference, 'No client id configured for the device-code request.');
  }
  const params = new URLSearchParams({ client_id: clientId });
  if (context.scope) {
    params.set('scope', context.scope);
  }
  const headers = context.client
    ? applyClientAuth(context.client, params)
    : { 'content-type': 'application/x-www-form-urlencoded', accept: 'application/json' };
  try {
    const response = await fetchFn(url, {
      method: 'POST',
      headers,
      body: params.toString(),
      ...(context.signal ? { signal: context.signal } : {}),
    });
    const body = await readJson(response);
    if (
      response.ok &&
      body &&
      typeof body.device_code === 'string' &&
      typeof body.user_code === 'string' &&
      (typeof body.verification_uri === 'string' ||
        typeof body.verification_uri_complete === 'string')
    ) {
      // `return await` keeps a rejected token poll inside this try/catch, so it fails the probe, not the whole run.
      return await pollDeviceToken(
        raw,
        fetchFn,
        context,
        clientId,
        body.device_code,
        id,
        feature,
        reference,
      );
    }
    if (body && typeof body.error === 'string') {
      return {
        id,
        feature,
        outcome: 'verified',
        detail: `Endpoint is live; rejected this probe with '${body.error}'.`,
        reference,
      };
    }
    return {
      id,
      feature,
      outcome: 'failed',
      detail: `Unexpected device-authorization response (HTTP ${response.status}).`,
      reference,
    };
  } catch (cause) {
    return { id, feature, outcome: 'failed', detail: networkDetail(cause), reference };
  }
}

/** Issues a single device-code token poll and verifies the RFC 8628 §3.5 pending/slow-down contract. */
async function pollDeviceToken(
  raw: RawDiscovery,
  fetchFn: ProbeFetch,
  context: ProbeRunnerContext,
  clientId: string,
  deviceCode: string,
  id: string,
  feature: string,
  reference: string,
): Promise<ProbeResult> {
  const tokenUrl = asString(raw, 'token_endpoint');
  if (!tokenUrl) {
    return {
      id,
      feature,
      outcome: 'verified',
      detail: 'Device authorization issued a device_code (token endpoint not advertised to poll).',
      reference,
    };
  }
  const params = new URLSearchParams({ grant_type: DEVICE_CODE_GRANT, device_code: deviceCode });
  const headers = context.client
    ? applyClientAuth(context.client, params)
    : (() => {
        params.set('client_id', clientId);
        return { 'content-type': 'application/x-www-form-urlencoded', accept: 'application/json' };
      })();
  const response = await fetchFn(tokenUrl, {
    method: 'POST',
    headers,
    body: params.toString(),
    ...(context.signal ? { signal: context.signal } : {}),
  });
  const body = await readJson(response);
  const error = body && typeof body.error === 'string' ? body.error : undefined;
  if (error === 'authorization_pending' || error === 'slow_down') {
    return {
      id,
      feature,
      outcome: 'verified',
      detail: `Issued a device_code; the immediate poll returned '${error}'.`,
      reference,
    };
  }
  return {
    id,
    feature,
    outcome: 'verified',
    detail: `Issued a device_code; the poll returned '${error ?? `HTTP ${response.status}`}'.`,
    reference,
  };
}

/**
 * Verifies the CIBA backchannel-authentication endpoint (OpenID Connect CIBA Core §7): a request returns an
 * `auth_req_id`, or a structured error proving the endpoint is live. Skipped until the endpoint is advertised
 * (backlog `F3`).
 */
export async function probeCiba(
  raw: RawDiscovery,
  fetchFn: ProbeFetch,
  context: ProbeRunnerContext,
): Promise<ProbeResult> {
  const id = 'runner-ciba';
  const feature = 'CIBA backchannel authentication issues an auth_req_id';
  const reference = 'OpenID Connect CIBA Core §7.3';
  const url = asString(raw, 'backchannel_authentication_endpoint');
  if (!url) {
    return skip(
      id,
      feature,
      reference,
      "'backchannel_authentication_endpoint' is not advertised (gap: F3).",
    );
  }
  const clientId = context.client?.clientId ?? context.publicClientId;
  if (!clientId) {
    return skip(id, feature, reference, 'No client id configured for the CIBA request.');
  }
  const params = new URLSearchParams({
    scope: context.scope ?? 'openid',
    login_hint: context.loginHint ?? 'ncode-conformance-probe@example.test',
  });
  const headers = context.client
    ? applyClientAuth(context.client, params)
    : (() => {
        params.set('client_id', clientId);
        return { 'content-type': 'application/x-www-form-urlencoded', accept: 'application/json' };
      })();
  try {
    const response = await fetchFn(url, {
      method: 'POST',
      headers,
      body: params.toString(),
      ...(context.signal ? { signal: context.signal } : {}),
    });
    const body = await readJson(response);
    if (response.ok && body && typeof body.auth_req_id === 'string') {
      return { id, feature, outcome: 'verified', detail: 'Issued an auth_req_id.', reference };
    }
    if (body && typeof body.error === 'string') {
      return {
        id,
        feature,
        outcome: 'verified',
        detail: `Endpoint is live; rejected this probe with '${body.error}'.`,
        reference,
      };
    }
    return {
      id,
      feature,
      outcome: 'failed',
      detail: `Unexpected CIBA response (HTTP ${response.status}).`,
      reference,
    };
  } catch (cause) {
    return { id, feature, outcome: 'failed', detail: networkDetail(cause), reference };
  }
}

/**
 * Verifies `private_key_jwt` client authentication (OIDC Core §9): the client authenticates a `client_credentials`
 * request with a signed JWT assertion instead of a shared secret. Skipped when the method is not advertised, or when
 * no asymmetric client key (and therefore no assertion signer) is configured.
 */
export async function probePrivateKeyJwt(
  raw: RawDiscovery,
  fetchFn: ProbeFetch,
  context: ProbeRunnerContext,
): Promise<ProbeResult> {
  const id = 'runner-private-key-jwt';
  const feature = 'private_key_jwt client authentication';
  const reference = 'OpenID Connect Core §9';
  const url = asString(raw, 'token_endpoint');
  if (!url) {
    return skip(id, feature, reference, "'token_endpoint' is not advertised.");
  }
  if (!asStringArray(raw, 'token_endpoint_auth_methods_supported').includes('private_key_jwt')) {
    return skip(id, feature, reference, "'private_key_jwt' is not advertised (gap: F16 / S3).");
  }
  if (!context.signClientAssertion || !context.publicClientId) {
    return skip(
      id,
      feature,
      reference,
      'No client key configured (supply a private key and client id).',
    );
  }
  const now = Math.floor(Date.now() / 1000);
  let assertion: string;
  try {
    assertion = await context.signClientAssertion({
      iss: context.publicClientId,
      sub: context.publicClientId,
      aud: url,
      jti: `ncode-conformance-probe-${now}`,
      iat: now,
      exp: now + 60,
    });
  } catch (cause) {
    return {
      id,
      feature,
      outcome: 'failed',
      detail: `Assertion signing failed: ${networkDetail(cause)}`,
      reference,
    };
  }
  const params = new URLSearchParams({
    grant_type: CLIENT_CREDENTIALS,
    client_assertion_type: CLIENT_ASSERTION_TYPE,
    client_assertion: assertion,
  });
  if (context.scope) {
    params.set('scope', context.scope);
  }
  try {
    const response = await fetchFn(url, {
      method: 'POST',
      headers: { 'content-type': 'application/x-www-form-urlencoded', accept: 'application/json' },
      body: params.toString(),
      ...(context.signal ? { signal: context.signal } : {}),
    });
    const body = await readJson(response);
    if (response.ok && body && typeof body.access_token === 'string') {
      return {
        id,
        feature,
        outcome: 'verified',
        detail: 'Authenticated with a signed JWT assertion and issued a token.',
        reference,
      };
    }
    return classifyTokenError(id, feature, reference, response.status, body);
  } catch (cause) {
    return { id, feature, outcome: 'failed', detail: networkDetail(cause), reference };
  }
}

/**
 * Verifies DPoP sender-constrained tokens (RFC 9449): a token request carrying a DPoP proof yields a `DPoP`-typed
 * token, or a `use_dpop_nonce` challenge (both prove the endpoint honors the proof). Skipped when DPoP is not
 * advertised (backlog `F7`) or no DPoP key is configured.
 */
export async function probeDpop(
  raw: RawDiscovery,
  fetchFn: ProbeFetch,
  context: ProbeRunnerContext,
): Promise<ProbeResult> {
  const id = 'runner-dpop';
  const feature = 'DPoP sender-constrained token request';
  const reference = 'RFC 9449 §5';
  const url = asString(raw, 'token_endpoint');
  if (!url) {
    return skip(id, feature, reference, "'token_endpoint' is not advertised.");
  }
  if (asStringArray(raw, 'dpop_signing_alg_values_supported').length === 0) {
    return skip(
      id,
      feature,
      reference,
      "'dpop_signing_alg_values_supported' is not advertised (gap: F7).",
    );
  }
  if (!context.createDpopProof || !context.client) {
    return skip(id, feature, reference, 'No DPoP key / confidential client configured.');
  }
  let proof: string;
  try {
    proof = await context.createDpopProof('POST', url);
  } catch (cause) {
    return {
      id,
      feature,
      outcome: 'failed',
      detail: `DPoP proof signing failed: ${networkDetail(cause)}`,
      reference,
    };
  }
  const params = new URLSearchParams({ grant_type: CLIENT_CREDENTIALS });
  if (context.scope) {
    params.set('scope', context.scope);
  }
  const headers = { ...applyClientAuth(context.client, params), dpop: proof };
  try {
    const response = await fetchFn(url, {
      method: 'POST',
      headers,
      body: params.toString(),
      ...(context.signal ? { signal: context.signal } : {}),
    });
    const body = await readJson(response);
    if (
      response.ok &&
      body &&
      typeof body.token_type === 'string' &&
      body.token_type.toLowerCase() === 'dpop'
    ) {
      return {
        id,
        feature,
        outcome: 'verified',
        detail: 'Issued a DPoP-bound access token.',
        reference,
      };
    }
    const challenge = response.headers?.get('dpop-nonce');
    if (challenge || (body && body.error === 'use_dpop_nonce')) {
      return {
        id,
        feature,
        outcome: 'verified',
        detail: 'Endpoint honored the proof and issued a DPoP nonce challenge.',
        reference,
      };
    }
    return classifyTokenError(id, feature, reference, response.status, body);
  } catch (cause) {
    return { id, feature, outcome: 'failed', detail: networkDetail(cause), reference };
  }
}

/**
 * Reports mutual-TLS client authentication / certificate-bound tokens (RFC 8705). A plain HTTP client cannot present
 * a client certificate on the TLS handshake, so active verification is deferred to the external certified suite
 * (Phase 4 / `T1`); this probe records whether the capability is advertised. Skipped when it is not (gap: `F7`).
 */
export function probeMutualTls(raw: RawDiscovery): ProbeResult {
  const id = 'runner-mtls';
  const feature = 'Mutual-TLS client authentication / bound tokens';
  const reference = 'RFC 8705 §2-3';
  const methods = asStringArray(raw, 'token_endpoint_auth_methods_supported').filter(
    (method) => method === 'tls_client_auth' || method === 'self_signed_tls_client_auth',
  );
  const boundTokens = raw.tls_client_certificate_bound_access_tokens === true;
  if (methods.length === 0 && !boundTokens) {
    return skip(id, feature, reference, 'No mutual-TLS capability is advertised (gap: F7).');
  }
  const advertised = [...methods, ...(boundTokens ? ['certificate-bound access tokens'] : [])];
  return skip(
    id,
    feature,
    reference,
    `Advertised (${advertised.join(', ')}); active verification needs a client certificate and is deferred to the external suite (T1).`,
  );
}

/**
 * Reports Back-Channel Logout (OpenID Connect Back-Channel Logout §2). Receiving a `logout_token` requires hosting an
 * RP logout endpoint and an authenticated session, which a stateless CLI cannot drive, so active receipt is deferred
 * to the external certified suite (Phase 4 / `T1`); this probe records whether the capability is advertised.
 */
export function probeBackChannelLogout(raw: RawDiscovery): ProbeResult {
  const id = 'runner-backchannel-logout';
  const feature = 'Back-Channel Logout';
  const reference = 'OpenID Connect Back-Channel Logout §2';
  if (raw.backchannel_logout_supported !== true) {
    return skip(id, feature, reference, "'backchannel_logout_supported' is not advertised.");
  }
  const session = raw.backchannel_logout_session_supported === true ? ' with session support' : '';
  return skip(
    id,
    feature,
    reference,
    `Advertised${session}; active logout-token receipt needs a hosted RP endpoint and is deferred to the external suite (T1).`,
  );
}

/** Every probe-runner probe, in the order the report renders them. */
const PROBES: readonly ((
  raw: RawDiscovery,
  fetchFn: ProbeFetch,
  context: ProbeRunnerContext,
) => Promise<ProbeResult> | ProbeResult)[] = [
  probeClientCredentials,
  probeIntrospection,
  probeRevocation,
  probeDeviceAuthorization,
  probeCiba,
  probePrivateKeyJwt,
  probeDpop,
  (raw) => probeMutualTls(raw),
  (raw) => probeBackChannelLogout(raw),
];

/** Runs every probe-runner probe against the live provider and aggregates the outcomes into a report. */
export async function runProbeRunner(
  raw: RawDiscovery,
  fetchFn: ProbeFetch,
  context: ProbeRunnerContext = {},
): Promise<ProbeReport> {
  const results = await Promise.all(PROBES.map((probe) => probe(raw, fetchFn, context)));
  const totals = results.reduce<Record<ProbeResult['outcome'], number>>(
    (acc, result) => {
      acc[result.outcome] += 1;
      return acc;
    },
    { verified: 0, failed: 0, skipped: 0 },
  );
  return { results, totals };
}
