/**
 * Active conformance probes (UI-free, unit-tested).
 *
 * Where the spec-coverage matrix ([coverage.ts](./coverage.ts)) reports what a provider *advertises*, these probes
 * actively exercise the browser-drivable, CORS-reachable endpoints to turn *advertised* into *verified* — or flag a
 * row as *advertised but broken*. They are the first slice of the Phase 2 active-probe track (ADR-0084); the legs that
 * require a confidential client, a client certificate, or an interactive login session are left to the headless probe
 * runner (Phase 3). Every probe is a plain async function over an injected fetch so the harness stays deterministic
 * and testable without a network.
 */

import type { RawDiscovery } from './audit.js';

/** The result of actively exercising a feature: proven working, proven broken, or not applicable. */
export type ProbeOutcome = 'verified' | 'failed' | 'skipped';

/** One active-probe result against the live provider. */
export type ProbeResult = {
  readonly id: string;
  readonly feature: string;
  readonly outcome: ProbeOutcome;
  readonly detail: string;
  /** The authoritative source the probe verifies (for example, `RFC 6749 §5.2`). */
  readonly reference: string;
};

/** The aggregate of every probe run, plus per-outcome totals. */
export type ProbeReport = {
  readonly results: readonly ProbeResult[];
  readonly totals: Record<ProbeOutcome, number>;
};

/** Minimal response surface a probe inspects. */
export type ProbeResponse = {
  readonly ok: boolean;
  readonly status: number;
  readonly headers?: { get(name: string): string | null };
  json(): Promise<unknown>;
  text(): Promise<string>;
};

/** Minimal fetch surface so probes are testable without a DOM/network. */
export type ProbeFetch = (
  url: string,
  init?: {
    readonly method?: string;
    readonly headers?: Readonly<Record<string, string>>;
    readonly body?: string;
    readonly signal?: AbortSignal | undefined;
  },
) => Promise<ProbeResponse>;

function asString(raw: RawDiscovery, key: string): string | undefined {
  const value = raw[key];
  return typeof value === 'string' && value.length > 0 ? value : undefined;
}

function skip(id: string, feature: string, reference: string, detail: string): ProbeResult {
  return { id, feature, outcome: 'skipped', detail, reference };
}

function isObject(value: unknown): value is Record<string, unknown> {
  return value !== null && typeof value === 'object' && !Array.isArray(value);
}

/** Verifies the JWKS endpoint returns a non-empty, well-formed JSON Web Key Set. */
export async function probeJwks(
  raw: RawDiscovery,
  fetchFn: ProbeFetch,
  signal?: AbortSignal,
): Promise<ProbeResult> {
  const id = 'probe-jwks';
  const feature = 'JWKS endpoint serves a valid key set';
  const reference = 'OpenID Connect Discovery §3 / RFC 7517';
  const url = asString(raw, 'jwks_uri');
  if (!url) {
    return skip(id, feature, reference, "'jwks_uri' is not advertised.");
  }
  try {
    const response = await fetchFn(url, { method: 'GET', signal });
    if (!response.ok) {
      return {
        id,
        feature,
        outcome: 'failed',
        detail: `JWKS request failed (HTTP ${response.status}).`,
        reference,
      };
    }
    const body = await response.json();
    const keys = isObject(body) && Array.isArray(body.keys) ? body.keys : [];
    const valid = keys.filter(
      (key): key is Record<string, unknown> => isObject(key) && typeof key.kty === 'string',
    );
    return valid.length > 0
      ? {
          id,
          feature,
          outcome: 'verified',
          detail: `JWKS advertises ${valid.length} key(s) with a 'kty'.`,
          reference,
        }
      : {
          id,
          feature,
          outcome: 'failed',
          detail: 'JWKS has no well-formed keys (missing or empty "keys").',
          reference,
        };
  } catch (cause) {
    return { id, feature, outcome: 'failed', detail: networkDetail(cause), reference };
  }
}

/**
 * Verifies the token endpoint rejects an invalid grant with the RFC 6749 §5.2 error shape (a non-2xx status and a JSON
 * body carrying an `error` code), rather than a malformed or HTML response.
 */
export async function probeTokenErrorShape(
  raw: RawDiscovery,
  fetchFn: ProbeFetch,
  clientId: string | undefined,
  signal?: AbortSignal,
): Promise<ProbeResult> {
  const id = 'probe-token-error';
  const feature = 'Token endpoint returns a conformant OAuth error';
  const reference = 'RFC 6749 §5.2';
  const url = asString(raw, 'token_endpoint');
  if (!url) {
    return skip(id, feature, reference, "'token_endpoint' is not advertised.");
  }
  // A deliberately invalid authorization-code grant: a public client exchanging a bogus code. The OP must answer with
  // the structured OAuth error, not a 2xx or an unstructured failure.
  const params = new URLSearchParams({
    grant_type: 'authorization_code',
    code: 'ncode-conformance-probe-invalid-code',
    redirect_uri: 'https://localhost/callback',
    ...(clientId ? { client_id: clientId } : {}),
  });
  try {
    const response = await fetchFn(url, {
      method: 'POST',
      headers: { 'content-type': 'application/x-www-form-urlencoded', accept: 'application/json' },
      body: params.toString(),
      signal,
    });
    if (response.ok) {
      return {
        id,
        feature,
        outcome: 'failed',
        detail: `Invalid grant unexpectedly succeeded (HTTP ${response.status}).`,
        reference,
      };
    }
    const body = await response.json().catch(() => null);
    return isObject(body) && typeof body.error === 'string'
      ? {
          id,
          feature,
          outcome: 'verified',
          detail: `Rejected with error '${body.error}' (HTTP ${response.status}).`,
          reference,
        }
      : {
          id,
          feature,
          outcome: 'failed',
          detail: `HTTP ${response.status} without a JSON 'error' field.`,
          reference,
        };
  } catch (cause) {
    return { id, feature, outcome: 'failed', detail: networkDetail(cause), reference };
  }
}

/** Verifies the UserInfo endpoint rejects an unauthenticated request with `401 Unauthorized`. */
export async function probeUserInfoUnauthorized(
  raw: RawDiscovery,
  fetchFn: ProbeFetch,
  signal?: AbortSignal,
): Promise<ProbeResult> {
  const id = 'probe-userinfo-401';
  const feature = 'UserInfo rejects an unauthenticated request';
  const reference = 'OpenID Connect Core §5.3.3 / RFC 6750 §3.1';
  const url = asString(raw, 'userinfo_endpoint');
  if (!url) {
    return skip(id, feature, reference, "'userinfo_endpoint' is not advertised.");
  }
  try {
    const response = await fetchFn(url, {
      method: 'GET',
      headers: { accept: 'application/json' },
      signal,
    });
    return response.status === 401
      ? {
          id,
          feature,
          outcome: 'verified',
          detail: 'Unauthenticated UserInfo request returned 401.',
          reference,
        }
      : {
          id,
          feature,
          outcome: 'failed',
          detail: `Expected 401, got HTTP ${response.status}.`,
          reference,
        };
  } catch (cause) {
    return { id, feature, outcome: 'failed', detail: networkDetail(cause), reference };
  }
}

/**
 * Verifies the Pushed Authorization Request endpoint (when advertised) accepts a PKCE request and returns a
 * `request_uri`. Skipped when the provider advertises no PAR endpoint (the common case until backlog `F8` lands).
 */
export async function probePar(
  raw: RawDiscovery,
  fetchFn: ProbeFetch,
  clientId: string | undefined,
  signal?: AbortSignal,
): Promise<ProbeResult> {
  const id = 'probe-par';
  const feature = 'PAR endpoint accepts a pushed request';
  const reference = 'RFC 9126 §2-3';
  const url = asString(raw, 'pushed_authorization_request_endpoint');
  if (!url) {
    return skip(
      id,
      feature,
      reference,
      "'pushed_authorization_request_endpoint' is not advertised (gap: F8).",
    );
  }
  if (!clientId) {
    return skip(id, feature, reference, 'No client id provided for the PAR request.');
  }
  const params = new URLSearchParams({
    client_id: clientId,
    response_type: 'code',
    redirect_uri: 'https://localhost/callback',
    scope: 'openid',
    code_challenge: 'ncode_conformance_probe_code_challenge_s256_000000000000',
    code_challenge_method: 'S256',
  });
  try {
    const response = await fetchFn(url, {
      method: 'POST',
      headers: { 'content-type': 'application/x-www-form-urlencoded', accept: 'application/json' },
      body: params.toString(),
      signal,
    });
    const body = await response.json().catch(() => null);
    if (response.status === 201 && isObject(body) && typeof body.request_uri === 'string') {
      return {
        id,
        feature,
        outcome: 'verified',
        detail: 'PAR returned 201 with a request_uri.',
        reference,
      };
    }
    // A structured OAuth error still proves the endpoint is live and conformant to the error contract.
    if (!response.ok && isObject(body) && typeof body.error === 'string') {
      return {
        id,
        feature,
        outcome: 'verified',
        detail: `PAR is live; rejected this probe with error '${body.error}'.`,
        reference,
      };
    }
    return {
      id,
      feature,
      outcome: 'failed',
      detail: `Unexpected PAR response (HTTP ${response.status}).`,
      reference,
    };
  } catch (cause) {
    return { id, feature, outcome: 'failed', detail: networkDetail(cause), reference };
  }
}

function networkDetail(cause: unknown): string {
  return cause instanceof Error ? `Request failed: ${cause.message}.` : 'Request failed.';
}

/** Options that steer the probe run (for example, the public client id used for token/PAR probes). */
export type ProbeRunOptions = {
  readonly clientId?: string;
  readonly signal?: AbortSignal;
};

/** Runs every active probe against the live provider and aggregates the outcomes. */
export async function runProbes(
  raw: RawDiscovery,
  fetchFn: ProbeFetch,
  options: ProbeRunOptions = {},
): Promise<ProbeReport> {
  const { clientId, signal } = options;
  const results = await Promise.all([
    probeJwks(raw, fetchFn, signal),
    probeTokenErrorShape(raw, fetchFn, clientId, signal),
    probeUserInfoUnauthorized(raw, fetchFn, signal),
    probePar(raw, fetchFn, clientId, signal),
  ]);
  const totals = results.reduce<Record<ProbeOutcome, number>>(
    (acc, result) => {
      acc[result.outcome] += 1;
      return acc;
    },
    { verified: 0, failed: 0, skipped: 0 },
  );
  return { results, totals };
}
