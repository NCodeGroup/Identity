import { describe, expect, it } from 'vitest';
import type { ProbeFetch, ProbeResponse, RawDiscovery } from '@ncode/conformance-core';
import {
  probeBackChannelLogout,
  probeCiba,
  probeClientCredentials,
  probeDeviceAuthorization,
  probeDpop,
  probeIntrospection,
  probeMutualTls,
  probePrivateKeyJwt,
  probeRevocation,
  runProbeRunner,
  type ProbeRunnerContext,
} from './probeRunner.js';

function jsonResponse(
  body: unknown,
  status = 200,
  headers: Record<string, string> = {},
): ProbeResponse {
  return {
    ok: status >= 200 && status < 300,
    status,
    headers: { get: (name) => headers[name.toLowerCase()] ?? null },
    json: () => Promise.resolve(body),
    text: () => Promise.resolve(JSON.stringify(body)),
  };
}

/** Builds a fetch stub that routes each URL to a scripted response and records the requests it receives. */
function routedFetch(routes: Record<string, ProbeResponse>): {
  fetchFn: ProbeFetch;
  requests: { url: string; init?: Parameters<ProbeFetch>[1] }[];
} {
  const requests: { url: string; init?: Parameters<ProbeFetch>[1] }[] = [];
  const fetchFn: ProbeFetch = (url, init) => {
    requests.push({ url, ...(init ? { init } : {}) });
    const response = routes[url];
    if (!response) {
      return Promise.reject(new Error(`no route for ${url}`));
    }
    return Promise.resolve(response);
  };
  return { fetchFn, requests };
}

const discovery: RawDiscovery = {
  issuer: 'https://op.test',
  token_endpoint: 'https://op.test/token',
  introspection_endpoint: 'https://op.test/introspect',
  revocation_endpoint: 'https://op.test/revoke',
  device_authorization_endpoint: 'https://op.test/device',
};

const confidential: ProbeRunnerContext = {
  client: { clientId: 'svc', clientSecret: 's3cr3t', authMethod: 'client_secret_basic' },
};

describe('probeClientCredentials', () => {
  it('verifies when an access token is issued', async () => {
    const { fetchFn } = routedFetch({
      'https://op.test/token': jsonResponse({ access_token: 'abc', token_type: 'Bearer' }),
    });
    const result = await probeClientCredentials(discovery, fetchFn, confidential);
    expect(result.outcome).toBe('verified');
  });

  it('fails when the client credentials are rejected', async () => {
    const { fetchFn } = routedFetch({
      'https://op.test/token': jsonResponse({ error: 'invalid_client' }, 401),
    });
    const result = await probeClientCredentials(discovery, fetchFn, confidential);
    expect(result.outcome).toBe('failed');
  });

  it('skips when the grant is unsupported', async () => {
    const { fetchFn } = routedFetch({
      'https://op.test/token': jsonResponse({ error: 'unsupported_grant_type' }, 400),
    });
    const result = await probeClientCredentials(discovery, fetchFn, confidential);
    expect(result.outcome).toBe('skipped');
  });

  it('sends Basic auth for client_secret_basic', async () => {
    const { fetchFn, requests } = routedFetch({
      'https://op.test/token': jsonResponse({ access_token: 'abc' }),
    });
    await probeClientCredentials(discovery, fetchFn, confidential);
    const auth = requests[0]?.init?.headers?.authorization;
    expect(auth).toBe(`Basic ${Buffer.from('svc:s3cr3t').toString('base64')}`);
  });

  it('skips without a confidential client', async () => {
    const { fetchFn } = routedFetch({});
    const result = await probeClientCredentials(discovery, fetchFn, {});
    expect(result.outcome).toBe('skipped');
  });
});

describe('probeIntrospection', () => {
  it('verifies an unknown token is reported inactive', async () => {
    const { fetchFn } = routedFetch({
      'https://op.test/introspect': jsonResponse({ active: false }),
    });
    const result = await probeIntrospection(discovery, fetchFn, confidential);
    expect(result.outcome).toBe('verified');
  });

  it('fails when an unknown token is reported active', async () => {
    const { fetchFn } = routedFetch({
      'https://op.test/introspect': jsonResponse({ active: true }),
    });
    const result = await probeIntrospection(discovery, fetchFn, confidential);
    expect(result.outcome).toBe('failed');
  });

  it('skips without a confidential client', async () => {
    const result = await probeIntrospection(discovery, routedFetch({}).fetchFn, {});
    expect(result.outcome).toBe('skipped');
  });
});

describe('probeRevocation', () => {
  it('verifies a 200 for an unknown token', async () => {
    const { fetchFn } = routedFetch({
      'https://op.test/revoke': jsonResponse({}, 200),
    });
    const result = await probeRevocation(discovery, fetchFn, confidential);
    expect(result.outcome).toBe('verified');
  });

  it('fails on a non-200 response', async () => {
    const { fetchFn } = routedFetch({
      'https://op.test/revoke': jsonResponse({ error: 'invalid_request' }, 400),
    });
    const result = await probeRevocation(discovery, fetchFn, confidential);
    expect(result.outcome).toBe('failed');
  });
});

describe('probeDeviceAuthorization', () => {
  it('verifies a device_code and a pending poll', async () => {
    const { fetchFn } = routedFetch({
      'https://op.test/device': jsonResponse({
        device_code: 'dev',
        user_code: 'ABCD-EFGH',
        verification_uri: 'https://op.test/activate',
      }),
      'https://op.test/token': jsonResponse({ error: 'authorization_pending' }, 400),
    });
    const result = await probeDeviceAuthorization(discovery, fetchFn, confidential);
    expect(result.outcome).toBe('verified');
    expect(result.detail).toContain('authorization_pending');
  });

  it('skips when the endpoint is not advertised', async () => {
    const result = await probeDeviceAuthorization({}, routedFetch({}).fetchFn, confidential);
    expect(result.outcome).toBe('skipped');
  });
});

describe('probeCiba', () => {
  it('skips when the endpoint is not advertised (gap F3)', async () => {
    const result = await probeCiba(discovery, routedFetch({}).fetchFn, confidential);
    expect(result.outcome).toBe('skipped');
    expect(result.detail).toContain('F3');
  });

  it('verifies when an auth_req_id is issued', async () => {
    const raw = { ...discovery, backchannel_authentication_endpoint: 'https://op.test/ciba' };
    const { fetchFn } = routedFetch({
      'https://op.test/ciba': jsonResponse({ auth_req_id: 'req-123', expires_in: 120 }),
    });
    const result = await probeCiba(raw, fetchFn, confidential);
    expect(result.outcome).toBe('verified');
  });
});

describe('probePrivateKeyJwt', () => {
  it('skips when private_key_jwt is not advertised', async () => {
    const result = await probePrivateKeyJwt(discovery, routedFetch({}).fetchFn, {
      publicClientId: 'app',
      signClientAssertion: () => Promise.resolve('jwt'),
    });
    expect(result.outcome).toBe('skipped');
  });

  it('verifies with a signed assertion when a token is issued', async () => {
    const raw = {
      ...discovery,
      token_endpoint_auth_methods_supported: ['private_key_jwt'],
    };
    const { fetchFn, requests } = routedFetch({
      'https://op.test/token': jsonResponse({ access_token: 'abc' }),
    });
    const result = await probePrivateKeyJwt(raw, fetchFn, {
      publicClientId: 'app',
      signClientAssertion: () => Promise.resolve('signed.jwt.value'),
    });
    expect(result.outcome).toBe('verified');
    expect(requests[0]?.init?.body).toContain('client_assertion=signed.jwt.value');
  });
});

describe('probeDpop', () => {
  it('skips when DPoP is not advertised (gap F7)', async () => {
    const result = await probeDpop(discovery, routedFetch({}).fetchFn, {
      ...confidential,
      createDpopProof: () => Promise.resolve('proof'),
    });
    expect(result.outcome).toBe('skipped');
  });

  it('verifies when the endpoint issues a DPoP-bound token', async () => {
    const raw = { ...discovery, dpop_signing_alg_values_supported: ['ES256'] };
    const { fetchFn, requests } = routedFetch({
      'https://op.test/token': jsonResponse({ access_token: 'abc', token_type: 'DPoP' }),
    });
    const result = await probeDpop(raw, fetchFn, {
      ...confidential,
      createDpopProof: () => Promise.resolve('dpop-proof'),
    });
    expect(result.outcome).toBe('verified');
    expect(requests[0]?.init?.headers?.dpop).toBe('dpop-proof');
  });

  it('verifies a use_dpop_nonce challenge', async () => {
    const raw = { ...discovery, dpop_signing_alg_values_supported: ['ES256'] };
    const { fetchFn } = routedFetch({
      'https://op.test/token': jsonResponse({ error: 'use_dpop_nonce' }, 400, {
        'dpop-nonce': 'n-1',
      }),
    });
    const result = await probeDpop(raw, fetchFn, {
      ...confidential,
      createDpopProof: () => Promise.resolve('dpop-proof'),
    });
    expect(result.outcome).toBe('verified');
  });
});

describe('probeMutualTls', () => {
  it('skips when no mTLS capability is advertised', () => {
    expect(probeMutualTls(discovery).outcome).toBe('skipped');
  });

  it('records advertised certificate-bound tokens', () => {
    const result = probeMutualTls({ tls_client_certificate_bound_access_tokens: true });
    expect(result.outcome).toBe('skipped');
    expect(result.detail).toContain('certificate-bound');
  });
});

describe('probeBackChannelLogout', () => {
  it('skips when not advertised', () => {
    expect(probeBackChannelLogout(discovery).outcome).toBe('skipped');
  });

  it('records advertised support with session detail', () => {
    const result = probeBackChannelLogout({
      backchannel_logout_supported: true,
      backchannel_logout_session_supported: true,
    });
    expect(result.detail).toContain('session support');
  });
});

describe('runProbeRunner', () => {
  it('runs every probe and tallies the outcomes', async () => {
    const { fetchFn } = routedFetch({
      'https://op.test/token': jsonResponse({ access_token: 'abc' }),
      'https://op.test/introspect': jsonResponse({ active: false }),
      'https://op.test/revoke': jsonResponse({}, 200),
      'https://op.test/device': jsonResponse({
        device_code: 'dev',
        user_code: 'ABCD',
        verification_uri: 'https://op.test/activate',
      }),
    });
    const report = await runProbeRunner(discovery, fetchFn, confidential);
    expect(report.results).toHaveLength(9);
    expect(report.totals.verified).toBeGreaterThanOrEqual(4);
    expect(report.totals.failed).toBe(0);
  });
});
