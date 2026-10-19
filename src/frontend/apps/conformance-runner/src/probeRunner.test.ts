import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
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
  type ClientAssertionSigner,
  type DpopProofFactory,
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

/** A response whose body is not JSON (for example, an HTML error page). */
function nonJsonResponse(status: number): ProbeResponse {
  return {
    ok: status >= 200 && status < 300,
    status,
    json: () => Promise.reject(new SyntaxError('Unexpected token <')),
    text: () => Promise.resolve('<html></html>'),
  };
}

/** Parses the form-encoded body of the n-th recorded request. */
function formBody(requests: { init?: Parameters<ProbeFetch>[1] }[], index = 0): URLSearchParams {
  return new URLSearchParams(requests[index]?.init?.body);
}

/** A fetch that rejects with a non-`Error` value (exercising the generic network-failure detail). */
const opaqueRejectingFetch: ProbeFetch = () => Promise.reject('opaque failure');

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

const confidentialPost: ProbeRunnerContext = {
  client: { clientId: 'svc', clientSecret: 's3cr3t', authMethod: 'client_secret_post' },
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

  it('skips when the token endpoint is not advertised', async () => {
    const { fetchFn, requests } = routedFetch({});
    const result = await probeClientCredentials({}, fetchFn, confidential);
    expect(result.outcome).toBe('skipped');
    expect(result.detail).toBe("'token_endpoint' is not advertised.");
    expect(requests).toHaveLength(0);
  });

  it('percent-encodes the Basic credentials (RFC 6749 §2.3.1)', async () => {
    const { fetchFn, requests } = routedFetch({
      'https://op.test/token': jsonResponse({ access_token: 'abc' }),
    });
    await probeClientCredentials(discovery, fetchFn, {
      client: { clientId: 'svc:1', clientSecret: 'p@ss word', authMethod: 'client_secret_basic' },
    });
    const auth = requests[0]?.init?.headers?.authorization;
    expect(auth).toBe(`Basic ${Buffer.from('svc%3A1:p%40ss%20word').toString('base64')}`);
    expect(formBody(requests).has('client_secret')).toBe(false);
  });

  it('carries the credentials in the body for client_secret_post, with scope and signal', async () => {
    const signal = new AbortController().signal;
    const { fetchFn, requests } = routedFetch({
      'https://op.test/token': jsonResponse({ access_token: 'abc' }),
    });
    const result = await probeClientCredentials(discovery, fetchFn, {
      ...confidentialPost,
      scope: 'api.read',
      signal,
    });
    expect(result.outcome).toBe('verified');
    const init = requests[0]?.init;
    expect(init?.method).toBe('POST');
    expect(init?.signal).toBe(signal);
    expect(init?.headers?.authorization).toBeUndefined();
    const body = formBody(requests);
    expect(body.get('grant_type')).toBe('client_credentials');
    expect(body.get('client_id')).toBe('svc');
    expect(body.get('client_secret')).toBe('s3cr3t');
    expect(body.get('scope')).toBe('api.read');
  });

  it('omits the scope and signal when none are configured', async () => {
    const { fetchFn, requests } = routedFetch({
      'https://op.test/token': jsonResponse({ access_token: 'abc' }),
    });
    await probeClientCredentials(discovery, fetchFn, confidential);
    expect(formBody(requests).has('scope')).toBe(false);
    expect(requests[0]?.init).not.toHaveProperty('signal');
  });

  it('verifies a structured OAuth error as proof the grant was processed', async () => {
    const { fetchFn } = routedFetch({
      'https://op.test/token': jsonResponse({ error: 'invalid_scope' }, 400),
    });
    const result = await probeClientCredentials(discovery, fetchFn, confidential);
    expect(result.outcome).toBe('verified');
    expect(result.detail).toBe(
      "Endpoint processed the grant; rejected this probe with 'invalid_scope'.",
    );
  });

  it('fails a 2xx response without an access token or error', async () => {
    const { fetchFn } = routedFetch({
      'https://op.test/token': jsonResponse({ token_type: 'Bearer' }),
    });
    const result = await probeClientCredentials(discovery, fetchFn, confidential);
    expect(result.outcome).toBe('failed');
    expect(result.detail).toBe("HTTP 200 without a JSON 'error' field.");
  });

  it('fails a non-JSON error response', async () => {
    const { fetchFn } = routedFetch({ 'https://op.test/token': nonJsonResponse(502) });
    const result = await probeClientCredentials(discovery, fetchFn, confidential);
    expect(result.outcome).toBe('failed');
    expect(result.detail).toBe("HTTP 502 without a JSON 'error' field.");
  });

  it('treats a JSON array body and a non-string error as unstructured', async () => {
    const arrayBody = routedFetch({ 'https://op.test/token': jsonResponse([{ error: 'x' }], 400) });
    const numericError = routedFetch({ 'https://op.test/token': jsonResponse({ error: 1 }, 400) });
    const results = await Promise.all([
      probeClientCredentials(discovery, arrayBody.fetchFn, confidential),
      probeClientCredentials(discovery, numericError.fetchFn, confidential),
    ]);
    expect(results.map((result) => result.outcome)).toEqual(['failed', 'failed']);
  });

  it('fails with the error message when the request throws', async () => {
    const result = await probeClientCredentials(discovery, routedFetch({}).fetchFn, confidential);
    expect(result.outcome).toBe('failed');
    expect(result.detail).toBe('Request failed: no route for https://op.test/token.');
  });

  it('fails with a generic detail when the request throws a non-Error', async () => {
    const result = await probeClientCredentials(discovery, opaqueRejectingFetch, confidential);
    expect(result.outcome).toBe('failed');
    expect(result.detail).toBe('Request failed.');
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

  it('skips when the introspection endpoint is not advertised', async () => {
    const result = await probeIntrospection({}, routedFetch({}).fetchFn, confidential);
    expect(result.outcome).toBe('skipped');
    expect(result.detail).toBe("'introspection_endpoint' is not advertised.");
  });

  it('posts the unknown token with client authentication and the signal', async () => {
    const signal = new AbortController().signal;
    const { fetchFn, requests } = routedFetch({
      'https://op.test/introspect': jsonResponse({ active: false }),
    });
    await probeIntrospection(discovery, fetchFn, { ...confidentialPost, signal });
    expect(requests[0]?.init?.signal).toBe(signal);
    const body = formBody(requests);
    expect(body.get('token')).toBe('ncode-conformance-probe-unknown-token');
    expect(body.get('client_id')).toBe('svc');
  });

  it('fails when the client is rejected with a 401', async () => {
    const { fetchFn } = routedFetch({ 'https://op.test/introspect': nonJsonResponse(401) });
    const result = await probeIntrospection(discovery, fetchFn, confidential);
    expect(result.outcome).toBe('failed');
    expect(result.detail).toBe('Client authentication was rejected.');
  });

  it('fails when the client is rejected with invalid_client on another status', async () => {
    const { fetchFn } = routedFetch({
      'https://op.test/introspect': jsonResponse({ error: 'invalid_client' }, 400),
    });
    const result = await probeIntrospection(discovery, fetchFn, confidential);
    expect(result.outcome).toBe('failed');
    expect(result.detail).toBe('Client authentication was rejected.');
  });

  it('fails a 200 without an active member', async () => {
    const { fetchFn } = routedFetch({ 'https://op.test/introspect': jsonResponse({}) });
    const result = await probeIntrospection(discovery, fetchFn, confidential);
    expect(result.outcome).toBe('failed');
    expect(result.detail).toBe('Unexpected introspection response (HTTP 200).');
  });

  it('fails a non-JSON server error', async () => {
    const { fetchFn } = routedFetch({ 'https://op.test/introspect': nonJsonResponse(500) });
    const result = await probeIntrospection(discovery, fetchFn, confidential);
    expect(result.outcome).toBe('failed');
    expect(result.detail).toBe('Unexpected introspection response (HTTP 500).');
  });

  it('fails a non-2xx response even when it claims the token is inactive', async () => {
    const { fetchFn } = routedFetch({
      'https://op.test/introspect': jsonResponse({ active: false }, 400),
    });
    const result = await probeIntrospection(discovery, fetchFn, confidential);
    expect(result.outcome).toBe('failed');
  });

  it('fails when the request throws', async () => {
    const result = await probeIntrospection(discovery, routedFetch({}).fetchFn, confidential);
    expect(result.outcome).toBe('failed');
    expect(result.detail).toContain('Request failed');
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
    expect(result.detail).toBe('Expected 200, got HTTP 400.');
  });

  it('skips when the revocation endpoint is not advertised', async () => {
    const result = await probeRevocation({}, routedFetch({}).fetchFn, confidential);
    expect(result.outcome).toBe('skipped');
    expect(result.detail).toBe("'revocation_endpoint' is not advertised.");
  });

  it('skips without a confidential client', async () => {
    const result = await probeRevocation(discovery, routedFetch({}).fetchFn, {});
    expect(result.outcome).toBe('skipped');
    expect(result.detail).toContain('requires client authentication');
  });

  it('posts the unknown token with client authentication and the signal', async () => {
    const signal = new AbortController().signal;
    const { fetchFn, requests } = routedFetch({ 'https://op.test/revoke': jsonResponse({}) });
    await probeRevocation(discovery, fetchFn, { ...confidentialPost, signal });
    expect(requests[0]?.init?.signal).toBe(signal);
    const body = formBody(requests);
    expect(body.get('token')).toBe('ncode-conformance-probe-unknown-token');
    expect(body.get('client_secret')).toBe('s3cr3t');
  });

  it('fails when the client is rejected with a 401', async () => {
    const { fetchFn } = routedFetch({ 'https://op.test/revoke': nonJsonResponse(401) });
    const result = await probeRevocation(discovery, fetchFn, confidential);
    expect(result.outcome).toBe('failed');
    expect(result.detail).toBe('Client authentication was rejected.');
  });

  it('fails when the client is rejected with invalid_client on another status', async () => {
    const { fetchFn } = routedFetch({
      'https://op.test/revoke': jsonResponse({ error: 'invalid_client' }, 400),
    });
    const result = await probeRevocation(discovery, fetchFn, confidential);
    expect(result.detail).toBe('Client authentication was rejected.');
  });

  it('fails a 204 (RFC 7009 requires 200)', async () => {
    const { fetchFn } = routedFetch({ 'https://op.test/revoke': nonJsonResponse(204) });
    const result = await probeRevocation(discovery, fetchFn, confidential);
    expect(result.outcome).toBe('failed');
    expect(result.detail).toBe('Expected 200, got HTTP 204.');
  });

  it('fails when the request throws', async () => {
    const result = await probeRevocation(discovery, opaqueRejectingFetch, confidential);
    expect(result.outcome).toBe('failed');
    expect(result.detail).toBe('Request failed.');
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

  const deviceCodeResponse = jsonResponse({
    device_code: 'dev',
    user_code: 'ABCD-EFGH',
    verification_uri: 'https://op.test/activate',
  });

  it('skips when no client id is configured', async () => {
    const { fetchFn, requests } = routedFetch({});
    const result = await probeDeviceAuthorization(discovery, fetchFn, {});
    expect(result.outcome).toBe('skipped');
    expect(result.detail).toBe('No client id configured for the device-code request.');
    expect(requests).toHaveLength(0);
  });

  it('uses the public client id on both legs, with scope and signal', async () => {
    const signal = new AbortController().signal;
    const { fetchFn, requests } = routedFetch({
      'https://op.test/device': deviceCodeResponse,
      'https://op.test/token': jsonResponse({ error: 'slow_down' }, 400),
    });
    const result = await probeDeviceAuthorization(discovery, fetchFn, {
      publicClientId: 'tv-app',
      scope: 'openid offline_access',
      signal,
    });
    expect(result.outcome).toBe('verified');
    expect(result.detail).toBe("Issued a device_code; the immediate poll returned 'slow_down'.");
    expect(requests.map((request) => request.url)).toEqual([
      'https://op.test/device',
      'https://op.test/token',
    ]);
    expect(requests.every((request) => request.init?.signal === signal)).toBe(true);
    expect(requests[0]?.init?.headers?.authorization).toBeUndefined();
    expect(formBody(requests, 0).get('client_id')).toBe('tv-app');
    expect(formBody(requests, 0).get('scope')).toBe('openid offline_access');
    const poll = formBody(requests, 1);
    expect(poll.get('grant_type')).toBe('urn:ietf:params:oauth:grant-type:device_code');
    expect(poll.get('device_code')).toBe('dev');
    expect(poll.get('client_id')).toBe('tv-app');
    expect(requests[1]?.init?.headers?.authorization).toBeUndefined();
  });

  it('authenticates both legs as the confidential client when configured', async () => {
    const { fetchFn, requests } = routedFetch({
      'https://op.test/device': deviceCodeResponse,
      'https://op.test/token': jsonResponse({ error: 'authorization_pending' }, 400),
    });
    await probeDeviceAuthorization(discovery, fetchFn, { ...confidential, publicClientId: 'tv' });
    expect(formBody(requests, 0).get('client_id')).toBe('svc');
    expect(requests[0]?.init?.headers?.authorization).toMatch(/^Basic /);
    expect(requests[1]?.init?.headers?.authorization).toMatch(/^Basic /);
    expect(formBody(requests, 0).has('scope')).toBe(false);
    expect(requests[0]?.init).not.toHaveProperty('signal');
  });

  it('accepts verification_uri_complete in place of verification_uri', async () => {
    const { fetchFn } = routedFetch({
      'https://op.test/device': jsonResponse({
        device_code: 'dev',
        user_code: 'ABCD',
        verification_uri_complete: 'https://op.test/activate?user_code=ABCD',
      }),
      'https://op.test/token': jsonResponse({ error: 'authorization_pending' }, 400),
    });
    const result = await probeDeviceAuthorization(discovery, fetchFn, confidential);
    expect(result.outcome).toBe('verified');
    expect(result.detail).toContain('authorization_pending');
  });

  it('verifies the device_code without polling when no token endpoint is advertised', async () => {
    const { fetchFn, requests } = routedFetch({ 'https://op.test/device': deviceCodeResponse });
    const raw: RawDiscovery = { device_authorization_endpoint: 'https://op.test/device' };
    const result = await probeDeviceAuthorization(raw, fetchFn, confidential);
    expect(result.outcome).toBe('verified');
    expect(result.detail).toContain('token endpoint not advertised to poll');
    expect(requests).toHaveLength(1);
  });

  it('reports the poll error code when it is neither pending nor slow_down', async () => {
    const { fetchFn } = routedFetch({
      'https://op.test/device': deviceCodeResponse,
      'https://op.test/token': jsonResponse({ error: 'expired_token' }, 400),
    });
    const result = await probeDeviceAuthorization(discovery, fetchFn, confidential);
    expect(result.outcome).toBe('verified');
    expect(result.detail).toBe("Issued a device_code; the poll returned 'expired_token'.");
  });

  it('reports the poll HTTP status when the poll carries no error code', async () => {
    const { fetchFn } = routedFetch({
      'https://op.test/device': deviceCodeResponse,
      'https://op.test/token': nonJsonResponse(500),
    });
    const result = await probeDeviceAuthorization(discovery, fetchFn, confidential);
    expect(result.detail).toBe("Issued a device_code; the poll returned 'HTTP 500'.");
  });

  it('fails the probe (not the run) when the token poll throws', async () => {
    const { fetchFn } = routedFetch({ 'https://op.test/device': deviceCodeResponse });
    const result = await probeDeviceAuthorization(discovery, fetchFn, confidential);
    expect(result.outcome).toBe('failed');
    expect(result.detail).toBe('Request failed: no route for https://op.test/token.');
  });

  it('verifies a live endpoint that rejects the probe with a structured error', async () => {
    const { fetchFn } = routedFetch({
      'https://op.test/device': jsonResponse({ error: 'invalid_client' }, 401),
    });
    const result = await probeDeviceAuthorization(discovery, fetchFn, confidential);
    expect(result.outcome).toBe('verified');
    expect(result.detail).toBe("Endpoint is live; rejected this probe with 'invalid_client'.");
  });

  it.each([
    ['a missing user_code', { device_code: 'dev', verification_uri: 'https://op.test/a' }],
    ['a missing verification URI', { device_code: 'dev', user_code: 'ABCD' }],
    ['a non-string device_code', { device_code: 1, user_code: 'A', verification_uri: 'u' }],
  ])('fails a 2xx response with %s', async (_label, body) => {
    const { fetchFn } = routedFetch({ 'https://op.test/device': jsonResponse(body) });
    const result = await probeDeviceAuthorization(discovery, fetchFn, confidential);
    expect(result.outcome).toBe('failed');
    expect(result.detail).toBe('Unexpected device-authorization response (HTTP 200).');
  });

  it('fails a non-JSON error response', async () => {
    const { fetchFn } = routedFetch({ 'https://op.test/device': nonJsonResponse(503) });
    const result = await probeDeviceAuthorization(discovery, fetchFn, confidential);
    expect(result.outcome).toBe('failed');
    expect(result.detail).toBe('Unexpected device-authorization response (HTTP 503).');
  });

  it('fails a well-formed body on a non-2xx status', async () => {
    const { fetchFn } = routedFetch({
      'https://op.test/device': jsonResponse(
        { device_code: 'dev', user_code: 'A', verification_uri: 'u' },
        500,
      ),
    });
    const result = await probeDeviceAuthorization(discovery, fetchFn, confidential);
    expect(result.outcome).toBe('failed');
  });

  it('fails when the device-authorization request throws', async () => {
    const result = await probeDeviceAuthorization(discovery, opaqueRejectingFetch, confidential);
    expect(result.outcome).toBe('failed');
    expect(result.detail).toBe('Request failed.');
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

  const cibaDiscovery: RawDiscovery = {
    ...discovery,
    backchannel_authentication_endpoint: 'https://op.test/ciba',
  };

  it('skips when no client id is configured', async () => {
    const result = await probeCiba(cibaDiscovery, routedFetch({}).fetchFn, {});
    expect(result.outcome).toBe('skipped');
    expect(result.detail).toBe('No client id configured for the CIBA request.');
  });

  it('sends the default scope and login_hint for a public client', async () => {
    const { fetchFn, requests } = routedFetch({
      'https://op.test/ciba': jsonResponse({ auth_req_id: 'req-1' }),
    });
    await probeCiba(cibaDiscovery, fetchFn, { publicClientId: 'app' });
    const body = formBody(requests);
    expect(body.get('scope')).toBe('openid');
    expect(body.get('login_hint')).toBe('ncode-conformance-probe@example.test');
    expect(body.get('client_id')).toBe('app');
    expect(requests[0]?.init?.headers?.authorization).toBeUndefined();
    expect(requests[0]?.init).not.toHaveProperty('signal');
  });

  it('sends the configured scope, login_hint, and signal for a confidential client', async () => {
    const signal = new AbortController().signal;
    const { fetchFn, requests } = routedFetch({
      'https://op.test/ciba': jsonResponse({ auth_req_id: 'req-1' }),
    });
    await probeCiba(cibaDiscovery, fetchFn, {
      ...confidential,
      scope: 'openid payments',
      loginHint: 'alice@example.test',
      signal,
    });
    const body = formBody(requests);
    expect(body.get('scope')).toBe('openid payments');
    expect(body.get('login_hint')).toBe('alice@example.test');
    expect(body.has('client_id')).toBe(false);
    expect(requests[0]?.init?.headers?.authorization).toMatch(/^Basic /);
    expect(requests[0]?.init?.signal).toBe(signal);
  });

  it('verifies a live endpoint that rejects the probe with a structured error', async () => {
    const { fetchFn } = routedFetch({
      'https://op.test/ciba': jsonResponse({ error: 'unknown_user_id' }, 400),
    });
    const result = await probeCiba(cibaDiscovery, fetchFn, confidential);
    expect(result.outcome).toBe('verified');
    expect(result.detail).toBe("Endpoint is live; rejected this probe with 'unknown_user_id'.");
  });

  it('fails a 2xx response without an auth_req_id', async () => {
    const { fetchFn } = routedFetch({ 'https://op.test/ciba': jsonResponse({ expires_in: 1 }) });
    const result = await probeCiba(cibaDiscovery, fetchFn, confidential);
    expect(result.outcome).toBe('failed');
    expect(result.detail).toBe('Unexpected CIBA response (HTTP 200).');
  });

  it('fails a non-JSON error response', async () => {
    const { fetchFn } = routedFetch({ 'https://op.test/ciba': nonJsonResponse(500) });
    const result = await probeCiba(cibaDiscovery, fetchFn, confidential);
    expect(result.outcome).toBe('failed');
    expect(result.detail).toBe('Unexpected CIBA response (HTTP 500).');
  });

  it('fails an auth_req_id delivered on a non-2xx status', async () => {
    const { fetchFn } = routedFetch({
      'https://op.test/ciba': jsonResponse({ auth_req_id: 'req-1' }, 500),
    });
    const result = await probeCiba(cibaDiscovery, fetchFn, confidential);
    expect(result.outcome).toBe('failed');
  });

  it('fails when the request throws', async () => {
    const result = await probeCiba(cibaDiscovery, routedFetch({}).fetchFn, confidential);
    expect(result.outcome).toBe('failed');
    expect(result.detail).toBe('Request failed: no route for https://op.test/ciba.');
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

  const pkjDiscovery: RawDiscovery = {
    ...discovery,
    token_endpoint_auth_methods_supported: ['client_secret_basic', 'private_key_jwt'],
  };
  const signer = { publicClientId: 'app', signClientAssertion: () => Promise.resolve('jwt') };

  it('skips when the token endpoint is not advertised', async () => {
    const raw = { token_endpoint_auth_methods_supported: ['private_key_jwt'] };
    const result = await probePrivateKeyJwt(raw, routedFetch({}).fetchFn, signer);
    expect(result.outcome).toBe('skipped');
    expect(result.detail).toBe("'token_endpoint' is not advertised.");
  });

  it.each<[string, ProbeRunnerContext]>([
    ['no assertion signer', { publicClientId: 'app' }],
    ['no client id', { signClientAssertion: () => Promise.resolve('jwt') }],
  ])('skips with %s', async (_label, context) => {
    const result = await probePrivateKeyJwt(pkjDiscovery, routedFetch({}).fetchFn, context);
    expect(result.outcome).toBe('skipped');
    expect(result.detail).toContain('No client key configured');
  });

  describe('with a fixed clock', () => {
    beforeEach(() => {
      vi.useFakeTimers();
      vi.setSystemTime(new Date('2026-01-01T00:00:00.000Z'));
    });

    afterEach(() => {
      vi.useRealTimers();
    });

    it('signs RFC 7523 client-assertion claims audienced to the token endpoint', async () => {
      const signClientAssertion = vi.fn<ClientAssertionSigner>(() => Promise.resolve('jwt'));
      const { fetchFn } = routedFetch({
        'https://op.test/token': jsonResponse({ access_token: 'abc' }),
      });
      await probePrivateKeyJwt(pkjDiscovery, fetchFn, {
        publicClientId: 'app',
        signClientAssertion,
      });
      const now = Date.UTC(2026, 0, 1) / 1000;
      expect(signClientAssertion).toHaveBeenCalledWith({
        iss: 'app',
        sub: 'app',
        aud: 'https://op.test/token',
        jti: `ncode-conformance-probe-${now}`,
        iat: now,
        exp: now + 60,
      });
    });
  });

  it('posts the assertion with its type, scope, and signal and no client secret', async () => {
    const signal = new AbortController().signal;
    const { fetchFn, requests } = routedFetch({
      'https://op.test/token': jsonResponse({ access_token: 'abc' }),
    });
    await probePrivateKeyJwt(pkjDiscovery, fetchFn, { ...signer, scope: 'api', signal });
    const body = formBody(requests);
    expect(body.get('grant_type')).toBe('client_credentials');
    expect(body.get('client_assertion_type')).toBe(
      'urn:ietf:params:oauth:client-assertion-type:jwt-bearer',
    );
    expect(body.get('scope')).toBe('api');
    expect(body.has('client_secret')).toBe(false);
    expect(requests[0]?.init?.headers?.authorization).toBeUndefined();
    expect(requests[0]?.init?.signal).toBe(signal);
  });

  it('fails when the assertion signer throws', async () => {
    const { fetchFn, requests } = routedFetch({});
    const result = await probePrivateKeyJwt(pkjDiscovery, fetchFn, {
      publicClientId: 'app',
      signClientAssertion: () => Promise.reject(new Error('bad key')),
    });
    expect(result.outcome).toBe('failed');
    expect(result.detail).toBe('Assertion signing failed: Request failed: bad key.');
    expect(requests).toHaveLength(0);
  });

  it('fails when the assertion is rejected (invalid_client)', async () => {
    const { fetchFn } = routedFetch({
      'https://op.test/token': jsonResponse({ error: 'invalid_client' }, 401),
    });
    const result = await probePrivateKeyJwt(pkjDiscovery, fetchFn, signer);
    expect(result.outcome).toBe('failed');
    expect(result.detail).toBe('Client authentication was rejected (invalid_client).');
  });

  it('skips when the provider rejects the grant as unsupported', async () => {
    const { fetchFn } = routedFetch({
      'https://op.test/token': jsonResponse({ error: 'unsupported_grant_type' }, 400),
    });
    const result = await probePrivateKeyJwt(pkjDiscovery, fetchFn, signer);
    expect(result.outcome).toBe('skipped');
  });

  it('fails when the token request throws', async () => {
    const result = await probePrivateKeyJwt(pkjDiscovery, routedFetch({}).fetchFn, signer);
    expect(result.outcome).toBe('failed');
    expect(result.detail).toBe('Request failed: no route for https://op.test/token.');
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

  const dpopDiscovery: RawDiscovery = {
    ...discovery,
    dpop_signing_alg_values_supported: ['ES256'],
  };
  const dpopContext: ProbeRunnerContext = {
    ...confidential,
    createDpopProof: () => Promise.resolve('dpop-proof'),
  };

  it('skips when the token endpoint is not advertised', async () => {
    const raw = { dpop_signing_alg_values_supported: ['ES256'] };
    const result = await probeDpop(raw, routedFetch({}).fetchFn, dpopContext);
    expect(result.outcome).toBe('skipped');
    expect(result.detail).toBe("'token_endpoint' is not advertised.");
  });

  it.each<[string, ProbeRunnerContext]>([
    ['no DPoP proof factory', confidential],
    ['no confidential client', { createDpopProof: () => Promise.resolve('proof') }],
  ])('skips with %s', async (_label, context) => {
    const result = await probeDpop(dpopDiscovery, routedFetch({}).fetchFn, context);
    expect(result.outcome).toBe('skipped');
    expect(result.detail).toBe('No DPoP key / confidential client configured.');
  });

  it('binds the proof to POST and the token endpoint, sending scope and signal', async () => {
    const signal = new AbortController().signal;
    const createDpopProof = vi.fn<DpopProofFactory>(() => Promise.resolve('dpop-proof'));
    const { fetchFn, requests } = routedFetch({
      'https://op.test/token': jsonResponse({ access_token: 'abc', token_type: 'dpop' }),
    });
    const result = await probeDpop(dpopDiscovery, fetchFn, {
      ...confidentialPost,
      createDpopProof,
      scope: 'api',
      signal,
    });
    expect(result.outcome).toBe('verified');
    expect(createDpopProof).toHaveBeenCalledWith('POST', 'https://op.test/token');
    expect(requests[0]?.init?.headers?.dpop).toBe('dpop-proof');
    expect(requests[0]?.init?.signal).toBe(signal);
    expect(formBody(requests).get('scope')).toBe('api');
    expect(formBody(requests).get('client_id')).toBe('svc');
  });

  it('fails when the proof factory throws', async () => {
    const { fetchFn, requests } = routedFetch({});
    const result = await probeDpop(dpopDiscovery, fetchFn, {
      ...confidential,
      createDpopProof: () => Promise.reject(new Error('no key')),
    });
    expect(result.outcome).toBe('failed');
    expect(result.detail).toBe('DPoP proof signing failed: Request failed: no key.');
    expect(requests).toHaveLength(0);
  });

  it('verifies a DPoP-Nonce header challenge without an error body', async () => {
    const { fetchFn } = routedFetch({
      'https://op.test/token': jsonResponse({}, 400, { 'dpop-nonce': 'n-1' }),
    });
    const result = await probeDpop(dpopDiscovery, fetchFn, dpopContext);
    expect(result.outcome).toBe('verified');
    expect(result.detail).toContain('DPoP nonce challenge');
  });

  it('verifies a use_dpop_nonce error body without the header', async () => {
    const { fetchFn } = routedFetch({
      'https://op.test/token': jsonResponse({ error: 'use_dpop_nonce' }, 400),
    });
    const result = await probeDpop(dpopDiscovery, fetchFn, dpopContext);
    expect(result.outcome).toBe('verified');
    expect(result.detail).toContain('DPoP nonce challenge');
  });

  it('fails a Bearer token issued despite the proof', async () => {
    const { fetchFn } = routedFetch({
      'https://op.test/token': jsonResponse({ access_token: 'abc', token_type: 'Bearer' }),
    });
    const result = await probeDpop(dpopDiscovery, fetchFn, dpopContext);
    expect(result.outcome).toBe('failed');
    expect(result.detail).toBe("HTTP 200 without a JSON 'error' field.");
  });

  it('classifies a non-nonce error on a response without headers', async () => {
    const { fetchFn } = routedFetch({
      'https://op.test/token': {
        ok: false,
        status: 400,
        json: () => Promise.resolve({ error: 'invalid_dpop_proof' }),
        text: () => Promise.resolve(''),
      },
    });
    const result = await probeDpop(dpopDiscovery, fetchFn, dpopContext);
    expect(result.outcome).toBe('verified');
    expect(result.detail).toContain("'invalid_dpop_proof'");
  });

  it('fails a non-JSON error response without a nonce', async () => {
    const { fetchFn } = routedFetch({ 'https://op.test/token': nonJsonResponse(500) });
    const result = await probeDpop(dpopDiscovery, fetchFn, dpopContext);
    expect(result.outcome).toBe('failed');
    expect(result.detail).toBe("HTTP 500 without a JSON 'error' field.");
  });

  it('fails a DPoP token_type delivered on a non-2xx status', async () => {
    const { fetchFn } = routedFetch({
      'https://op.test/token': jsonResponse({ token_type: 'DPoP' }, 500),
    });
    const result = await probeDpop(dpopDiscovery, fetchFn, dpopContext);
    expect(result.outcome).toBe('failed');
  });

  it('fails when the token request throws', async () => {
    const result = await probeDpop(dpopDiscovery, opaqueRejectingFetch, dpopContext);
    expect(result.outcome).toBe('failed');
    expect(result.detail).toBe('Request failed.');
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

  it('records advertised mTLS client-auth methods without bound tokens', () => {
    const result = probeMutualTls({
      token_endpoint_auth_methods_supported: [
        'client_secret_basic',
        'tls_client_auth',
        'self_signed_tls_client_auth',
      ],
    });
    expect(result.outcome).toBe('skipped');
    expect(result.detail).toMatch(/^Advertised \(tls_client_auth, self_signed_tls_client_auth\);/);
    expect(result.detail).not.toContain('certificate-bound');
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

  it('records advertised support without session detail', () => {
    const result = probeBackChannelLogout({ backchannel_logout_supported: true });
    expect(result.outcome).toBe('skipped');
    expect(result.detail).toMatch(/^Advertised; active logout-token receipt/);
  });

  it('treats a non-boolean flag as not advertised', () => {
    const result = probeBackChannelLogout({ backchannel_logout_supported: 'true' });
    expect(result.detail).toBe("'backchannel_logout_supported' is not advertised.");
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

  it('skips every probe without a context when nothing is advertised', async () => {
    const { fetchFn, requests } = routedFetch({});
    const report = await runProbeRunner({}, fetchFn);
    expect(report.results.map((result) => result.id)).toEqual([
      'runner-client-credentials',
      'runner-introspection',
      'runner-revocation',
      'runner-device-code',
      'runner-ciba',
      'runner-private-key-jwt',
      'runner-dpop',
      'runner-mtls',
      'runner-backchannel-logout',
    ]);
    expect(report.totals).toEqual({ verified: 0, failed: 0, skipped: 9 });
    expect(requests).toHaveLength(0);
  });

  it('isolates network failures per probe so the run still completes', async () => {
    const { fetchFn } = routedFetch({
      'https://op.test/device': jsonResponse({
        device_code: 'dev',
        user_code: 'ABCD',
        verification_uri: 'https://op.test/activate',
      }),
    });
    const report = await runProbeRunner(discovery, fetchFn, confidential);
    const outcomes = Object.fromEntries(report.results.map((r) => [r.id, r.outcome]));
    expect(outcomes).toMatchObject({
      'runner-client-credentials': 'failed',
      'runner-introspection': 'failed',
      'runner-revocation': 'failed',
      'runner-device-code': 'failed',
    });
    expect(report.totals.failed).toBe(4);
  });
});
