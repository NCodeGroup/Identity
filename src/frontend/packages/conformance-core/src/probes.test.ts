import { describe, expect, it, vi } from 'vitest';
import type { RawDiscovery } from './audit.js';
import {
  probeJwks,
  probePar,
  probeTokenErrorShape,
  probeUserInfoUnauthorized,
  runProbes,
  type ProbeFetch,
  type ProbeResponse,
} from './probes.js';

const discovery: RawDiscovery = {
  issuer: 'https://op.test',
  authorization_endpoint: 'https://op.test/authorize',
  token_endpoint: 'https://op.test/token',
  userinfo_endpoint: 'https://op.test/userinfo',
  jwks_uri: 'https://op.test/jwks',
};

const withParDiscovery: RawDiscovery = {
  ...discovery,
  pushed_authorization_request_endpoint: 'https://op.test/par',
};

function response(
  partial: Partial<ProbeResponse> & { status: number; ok: boolean },
): ProbeResponse {
  return {
    json: () => Promise.resolve({}),
    text: () => Promise.resolve(''),
    ...partial,
  };
}

describe('probeJwks', () => {
  it('verifies a non-empty, well-formed key set', async () => {
    const fetchFn: ProbeFetch = vi.fn(() =>
      Promise.resolve(
        response({
          ok: true,
          status: 200,
          json: () => Promise.resolve({ keys: [{ kty: 'RSA', kid: '1' }] }),
        }),
      ),
    );
    const result = await probeJwks(discovery, fetchFn);
    expect(result.outcome).toBe('verified');
  });

  it('fails when the key set is empty', async () => {
    const fetchFn: ProbeFetch = vi.fn(() =>
      Promise.resolve(
        response({ ok: true, status: 200, json: () => Promise.resolve({ keys: [] }) }),
      ),
    );
    const result = await probeJwks(discovery, fetchFn);
    expect(result.outcome).toBe('failed');
  });

  it('skips when no jwks_uri is advertised', async () => {
    const result = await probeJwks({ issuer: 'https://op.test' }, vi.fn());
    expect(result.outcome).toBe('skipped');
  });

  it('skips when jwks_uri is an empty string', async () => {
    const fetchFn = vi.fn<ProbeFetch>();
    const result = await probeJwks({ ...discovery, jwks_uri: '' }, fetchFn);
    expect(result.outcome).toBe('skipped');
    expect(fetchFn).not.toHaveBeenCalled();
  });

  it('issues a GET to the jwks_uri forwarding the abort signal', async () => {
    const signal = new AbortController().signal;
    const fetchFn = vi.fn<ProbeFetch>(() =>
      Promise.resolve(
        response({ ok: true, status: 200, json: () => Promise.resolve({ keys: [{ kty: 'EC' }] }) }),
      ),
    );
    await probeJwks(discovery, fetchFn, signal);
    expect(fetchFn).toHaveBeenCalledWith('https://op.test/jwks', { method: 'GET', signal });
  });

  it('fails on a non-2xx JWKS response', async () => {
    const fetchFn: ProbeFetch = vi.fn(() => Promise.resolve(response({ ok: false, status: 503 })));
    const result = await probeJwks(discovery, fetchFn);
    expect(result.outcome).toBe('failed');
    expect(result.detail).toBe('JWKS request failed (HTTP 503).');
  });

  it('fails when the body is not a key-set object', async () => {
    const fetchFn: ProbeFetch = vi.fn(() =>
      Promise.resolve(response({ ok: true, status: 200, json: () => Promise.resolve([]) })),
    );
    const result = await probeJwks(discovery, fetchFn);
    expect(result.outcome).toBe('failed');
    expect(result.detail).toContain('no well-formed keys');
  });

  it('fails when "keys" is not an array', async () => {
    const fetchFn: ProbeFetch = vi.fn(() =>
      Promise.resolve(
        response({ ok: true, status: 200, json: () => Promise.resolve({ keys: 'nope' }) }),
      ),
    );
    const result = await probeJwks(discovery, fetchFn);
    expect(result.outcome).toBe('failed');
  });

  it('counts only keys that are objects carrying a string kty', async () => {
    const fetchFn: ProbeFetch = vi.fn(() =>
      Promise.resolve(
        response({
          ok: true,
          status: 200,
          json: () =>
            Promise.resolve({
              keys: [null, 'RSA', ['kty'], { kid: 'no-kty' }, { kty: 7 }, { kty: 'OKP' }],
            }),
        }),
      ),
    );
    const result = await probeJwks(discovery, fetchFn);
    expect(result.outcome).toBe('verified');
    expect(result.detail).toBe("JWKS advertises 1 key(s) with a 'kty'.");
  });

  it('fails when every key lacks a kty', async () => {
    const fetchFn: ProbeFetch = vi.fn(() =>
      Promise.resolve(
        response({
          ok: true,
          status: 200,
          json: () => Promise.resolve({ keys: [{ kid: '1' }] }),
        }),
      ),
    );
    const result = await probeJwks(discovery, fetchFn);
    expect(result.outcome).toBe('failed');
  });

  it('fails with the error message when the request throws', async () => {
    const fetchFn: ProbeFetch = vi.fn(() => Promise.reject(new Error('ECONNREFUSED')));
    const result = await probeJwks(discovery, fetchFn);
    expect(result.outcome).toBe('failed');
    expect(result.detail).toBe('Request failed: ECONNREFUSED.');
  });

  it('fails with a generic detail when the request throws a non-Error', async () => {
    const fetchFn: ProbeFetch = vi.fn(() => Promise.reject('opaque'));
    const result = await probeJwks(discovery, fetchFn);
    expect(result.outcome).toBe('failed');
    expect(result.detail).toBe('Request failed.');
  });
});

describe('probeTokenErrorShape', () => {
  it('verifies an RFC 6749 §5.2 error for an invalid grant', async () => {
    const fetchFn: ProbeFetch = vi.fn(() =>
      Promise.resolve(
        response({
          ok: false,
          status: 400,
          json: () => Promise.resolve({ error: 'invalid_grant' }),
        }),
      ),
    );
    const result = await probeTokenErrorShape(discovery, fetchFn, 'playground-spa');
    expect(result.outcome).toBe('verified');
    expect(result.detail).toContain('invalid_grant');
  });

  it('fails when the error body is not conformant', async () => {
    const fetchFn: ProbeFetch = vi.fn(() =>
      Promise.resolve(
        response({ ok: false, status: 500, json: () => Promise.reject(new Error('html')) }),
      ),
    );
    const result = await probeTokenErrorShape(discovery, fetchFn, undefined);
    expect(result.outcome).toBe('failed');
  });

  it('skips when no token_endpoint is advertised', async () => {
    const fetchFn = vi.fn<ProbeFetch>();
    const result = await probeTokenErrorShape({ issuer: 'https://op.test' }, fetchFn, 'spa');
    expect(result.outcome).toBe('skipped');
    expect(result.detail).toContain('token_endpoint');
    expect(fetchFn).not.toHaveBeenCalled();
  });

  it('posts a bogus authorization-code grant carrying the client id', async () => {
    const signal = new AbortController().signal;
    const fetchFn = vi.fn<ProbeFetch>(() =>
      Promise.resolve(
        response({
          ok: false,
          status: 400,
          json: () => Promise.resolve({ error: 'invalid_grant' }),
        }),
      ),
    );
    await probeTokenErrorShape(discovery, fetchFn, 'playground-spa', signal);
    const [url, init] = fetchFn.mock.calls[0] ?? [];
    expect(url).toBe('https://op.test/token');
    expect(init?.method).toBe('POST');
    expect(init?.signal).toBe(signal);
    expect(init?.headers).toEqual({
      'content-type': 'application/x-www-form-urlencoded',
      accept: 'application/json',
    });
    const body = new URLSearchParams(init?.body);
    expect(body.get('grant_type')).toBe('authorization_code');
    expect(body.get('code')).toBe('ncode-conformance-probe-invalid-code');
    expect(body.get('client_id')).toBe('playground-spa');
  });

  it('omits client_id when no client id is supplied', async () => {
    const fetchFn = vi.fn<ProbeFetch>(() =>
      Promise.resolve(
        response({
          ok: false,
          status: 400,
          json: () => Promise.resolve({ error: 'invalid_client' }),
        }),
      ),
    );
    await probeTokenErrorShape(discovery, fetchFn, undefined);
    const body = new URLSearchParams(fetchFn.mock.calls[0]?.[1]?.body);
    expect(body.has('client_id')).toBe(false);
  });

  it('fails when the invalid grant unexpectedly succeeds', async () => {
    const fetchFn: ProbeFetch = vi.fn(() => Promise.resolve(response({ ok: true, status: 200 })));
    const result = await probeTokenErrorShape(discovery, fetchFn, 'spa');
    expect(result.outcome).toBe('failed');
    expect(result.detail).toBe('Invalid grant unexpectedly succeeded (HTTP 200).');
  });

  it('fails when the JSON error field is not a string', async () => {
    const fetchFn: ProbeFetch = vi.fn(() =>
      Promise.resolve(
        response({ ok: false, status: 400, json: () => Promise.resolve({ error: 42 }) }),
      ),
    );
    const result = await probeTokenErrorShape(discovery, fetchFn, 'spa');
    expect(result.outcome).toBe('failed');
    expect(result.detail).toBe("HTTP 400 without a JSON 'error' field.");
  });

  it('fails when the request throws', async () => {
    const fetchFn: ProbeFetch = vi.fn(() => Promise.reject(new Error('timeout')));
    const result = await probeTokenErrorShape(discovery, fetchFn, 'spa');
    expect(result.outcome).toBe('failed');
    expect(result.detail).toBe('Request failed: timeout.');
  });
});

describe('probeUserInfoUnauthorized', () => {
  it('verifies a 401 for an unauthenticated request', async () => {
    const fetchFn: ProbeFetch = vi.fn(() => Promise.resolve(response({ ok: false, status: 401 })));
    const result = await probeUserInfoUnauthorized(discovery, fetchFn);
    expect(result.outcome).toBe('verified');
  });

  it('fails when a non-401 status is returned', async () => {
    const fetchFn: ProbeFetch = vi.fn(() => Promise.resolve(response({ ok: true, status: 200 })));
    const result = await probeUserInfoUnauthorized(discovery, fetchFn);
    expect(result.outcome).toBe('failed');
  });

  it('skips when no userinfo_endpoint is advertised', async () => {
    const fetchFn = vi.fn<ProbeFetch>();
    const result = await probeUserInfoUnauthorized({ issuer: 'https://op.test' }, fetchFn);
    expect(result.outcome).toBe('skipped');
    expect(result.detail).toContain('userinfo_endpoint');
    expect(fetchFn).not.toHaveBeenCalled();
  });

  it('issues an unauthenticated GET forwarding the abort signal', async () => {
    const signal = new AbortController().signal;
    const fetchFn = vi.fn<ProbeFetch>(() => Promise.resolve(response({ ok: false, status: 401 })));
    await probeUserInfoUnauthorized(discovery, fetchFn, signal);
    expect(fetchFn).toHaveBeenCalledWith('https://op.test/userinfo', {
      method: 'GET',
      headers: { accept: 'application/json' },
      signal,
    });
  });

  it('reports the unexpected status in the failure detail', async () => {
    const fetchFn: ProbeFetch = vi.fn(() => Promise.resolve(response({ ok: false, status: 403 })));
    const result = await probeUserInfoUnauthorized(discovery, fetchFn);
    expect(result.outcome).toBe('failed');
    expect(result.detail).toBe('Expected 401, got HTTP 403.');
  });

  it('fails when the request throws', async () => {
    const fetchFn: ProbeFetch = vi.fn(() => Promise.reject(new Error('reset')));
    const result = await probeUserInfoUnauthorized(discovery, fetchFn);
    expect(result.outcome).toBe('failed');
    expect(result.detail).toBe('Request failed: reset.');
  });
});

describe('probePar', () => {
  it('skips when no PAR endpoint is advertised (the common gap)', async () => {
    const result = await probePar(discovery, vi.fn(), 'playground-spa');
    expect(result.outcome).toBe('skipped');
    expect(result.detail).toContain('F8');
  });

  it('verifies a 201 with a request_uri when PAR is advertised', async () => {
    const withPar: RawDiscovery = {
      ...discovery,
      pushed_authorization_request_endpoint: 'https://op.test/par',
    };
    const fetchFn: ProbeFetch = vi.fn(() =>
      Promise.resolve(
        response({
          ok: true,
          status: 201,
          json: () => Promise.resolve({ request_uri: 'urn:req:1' }),
        }),
      ),
    );
    const result = await probePar(withPar, fetchFn, 'playground-spa');
    expect(result.outcome).toBe('verified');
  });

  it('skips when no client id is provided', async () => {
    const fetchFn = vi.fn<ProbeFetch>();
    const result = await probePar(withParDiscovery, fetchFn, undefined);
    expect(result.outcome).toBe('skipped');
    expect(result.detail).toBe('No client id provided for the PAR request.');
    expect(fetchFn).not.toHaveBeenCalled();
  });

  it('pushes a PKCE S256 authorization request for the client', async () => {
    const signal = new AbortController().signal;
    const fetchFn = vi.fn<ProbeFetch>(() =>
      Promise.resolve(
        response({ ok: true, status: 201, json: () => Promise.resolve({ request_uri: 'urn:1' }) }),
      ),
    );
    await probePar(withParDiscovery, fetchFn, 'playground-spa', signal);
    const [url, init] = fetchFn.mock.calls[0] ?? [];
    expect(url).toBe('https://op.test/par');
    expect(init?.method).toBe('POST');
    expect(init?.signal).toBe(signal);
    const body = new URLSearchParams(init?.body);
    expect(body.get('client_id')).toBe('playground-spa');
    expect(body.get('response_type')).toBe('code');
    expect(body.get('code_challenge_method')).toBe('S256');
    expect(body.get('scope')).toBe('openid');
  });

  it('verifies a live endpoint that rejects the probe with a structured OAuth error', async () => {
    const fetchFn: ProbeFetch = vi.fn(() =>
      Promise.resolve(
        response({
          ok: false,
          status: 400,
          json: () => Promise.resolve({ error: 'invalid_redirect_uri' }),
        }),
      ),
    );
    const result = await probePar(withParDiscovery, fetchFn, 'playground-spa');
    expect(result.outcome).toBe('verified');
    expect(result.detail).toBe(
      "PAR is live; rejected this probe with error 'invalid_redirect_uri'.",
    );
  });

  it('fails a 201 that carries no request_uri', async () => {
    const fetchFn: ProbeFetch = vi.fn(() =>
      Promise.resolve(response({ ok: true, status: 201, json: () => Promise.resolve({}) })),
    );
    const result = await probePar(withParDiscovery, fetchFn, 'playground-spa');
    expect(result.outcome).toBe('failed');
    expect(result.detail).toBe('Unexpected PAR response (HTTP 201).');
  });

  it('fails a 200 even when it carries a request_uri (RFC 9126 requires 201)', async () => {
    const fetchFn: ProbeFetch = vi.fn(() =>
      Promise.resolve(
        response({ ok: true, status: 200, json: () => Promise.resolve({ request_uri: 'urn:1' }) }),
      ),
    );
    const result = await probePar(withParDiscovery, fetchFn, 'playground-spa');
    expect(result.outcome).toBe('failed');
  });

  it('fails a 2xx error body (an error only proves liveness on a non-2xx)', async () => {
    const fetchFn: ProbeFetch = vi.fn(() =>
      Promise.resolve(
        response({ ok: true, status: 200, json: () => Promise.resolve({ error: 'oops' }) }),
      ),
    );
    const result = await probePar(withParDiscovery, fetchFn, 'playground-spa');
    expect(result.outcome).toBe('failed');
  });

  it('fails a non-2xx response whose body is not JSON', async () => {
    const fetchFn: ProbeFetch = vi.fn(() =>
      Promise.resolve(
        response({ ok: false, status: 502, json: () => Promise.reject(new Error('html')) }),
      ),
    );
    const result = await probePar(withParDiscovery, fetchFn, 'playground-spa');
    expect(result.outcome).toBe('failed');
    expect(result.detail).toBe('Unexpected PAR response (HTTP 502).');
  });

  it('fails a non-2xx response whose error field is not a string', async () => {
    const fetchFn: ProbeFetch = vi.fn(() =>
      Promise.resolve(
        response({ ok: false, status: 400, json: () => Promise.resolve({ error: ['x'] }) }),
      ),
    );
    const result = await probePar(withParDiscovery, fetchFn, 'playground-spa');
    expect(result.outcome).toBe('failed');
  });

  it('fails when the request throws', async () => {
    const fetchFn: ProbeFetch = vi.fn(() => Promise.reject(new Error('dns')));
    const result = await probePar(withParDiscovery, fetchFn, 'playground-spa');
    expect(result.outcome).toBe('failed');
    expect(result.detail).toBe('Request failed: dns.');
  });
});

describe('runProbes', () => {
  it('aggregates outcomes with per-outcome totals', async () => {
    const fetchFn: ProbeFetch = vi.fn((url: string) => {
      if (url.endsWith('/jwks')) {
        return Promise.resolve(
          response({
            ok: true,
            status: 200,
            json: () => Promise.resolve({ keys: [{ kty: 'RSA' }] }),
          }),
        );
      }
      if (url.endsWith('/token')) {
        return Promise.resolve(
          response({
            ok: false,
            status: 400,
            json: () => Promise.resolve({ error: 'invalid_grant' }),
          }),
        );
      }
      // userinfo
      return Promise.resolve(response({ ok: false, status: 401 }));
    });
    const report = await runProbes(discovery, fetchFn, { clientId: 'playground-spa' });
    expect(report.totals.verified).toBe(3);
    expect(report.totals.skipped).toBe(1); // PAR is not advertised
    expect(report.results).toHaveLength(4);
  });

  it('skips every probe without options when nothing is advertised', async () => {
    const fetchFn = vi.fn<ProbeFetch>();
    const report = await runProbes({ issuer: 'https://op.test' }, fetchFn);
    expect(report.results.map((result) => result.id)).toEqual([
      'probe-jwks',
      'probe-token-error',
      'probe-userinfo-401',
      'probe-par',
    ]);
    expect(report.totals).toEqual({ verified: 0, failed: 0, skipped: 4 });
    expect(fetchFn).not.toHaveBeenCalled();
  });

  it('tallies failures and forwards the abort signal to every probe', async () => {
    const signal = new AbortController().signal;
    const fetchFn = vi.fn<ProbeFetch>(() => Promise.reject(new Error('offline')));
    const report = await runProbes(withParDiscovery, fetchFn, { clientId: 'spa', signal });
    expect(report.totals).toEqual({ verified: 0, failed: 4, skipped: 0 });
    expect(fetchFn).toHaveBeenCalledTimes(4);
    for (const [, init] of fetchFn.mock.calls) {
      expect(init?.signal).toBe(signal);
    }
  });
});
