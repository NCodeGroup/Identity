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
});
