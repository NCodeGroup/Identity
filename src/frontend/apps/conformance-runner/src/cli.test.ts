import { describe, expect, it, vi } from 'vitest';
import { generateKeyPairSync, verify, type KeyObject } from 'node:crypto';
import type { ProbeFetch, ProbeResponse } from '@ncode/conformance-core';
import { runCli, type CliIo } from './cli.js';
import { USAGE } from './config.js';

const ISSUER = 'https://op.test';
const DISCOVERY_URL = `${ISSUER}/.well-known/openid-configuration`;
const TOKEN_URL = `${ISSUER}/token`;

const ecPair = generateKeyPairSync('ec', { namedCurve: 'prime256v1' });
const rsaPair = generateKeyPairSync('rsa', { modulusLength: 2048 });

function pem(key: KeyObject): string {
  return key.export({ format: 'pem', type: 'pkcs8' }).toString();
}

function jsonResponse(body: unknown, status = 200): ProbeResponse {
  return {
    ok: status >= 200 && status < 300,
    status,
    headers: { get: () => null },
    json: () => Promise.resolve(body),
    text: () => Promise.resolve(JSON.stringify(body)),
  };
}

function decodeSegment(segment: string | undefined): Record<string, unknown> {
  return JSON.parse(Buffer.from(segment ?? '', 'base64url').toString('utf8')) as Record<
    string,
    unknown
  >;
}

type Request = { readonly url: string; readonly init?: Parameters<ProbeFetch>[1] };

/** A fake process: in-memory files, a routed fetch, and captured stdout/stderr. */
function fakeIo(
  options: {
    readonly files?: Readonly<Record<string, string>>;
    readonly routes?: Readonly<Record<string, ProbeResponse>>;
    readonly fetch?: ProbeFetch;
  } = {},
) {
  const stdout: string[] = [];
  const stderr: string[] = [];
  const requests: Request[] = [];
  const readFile = vi.fn<CliIo['readFile']>((path) => {
    const content = options.files?.[path];
    return content === undefined
      ? Promise.reject(new Error(`ENOENT: no such file '${path}'`))
      : Promise.resolve(content);
  });
  const routedFetch: ProbeFetch = (url, init) => {
    requests.push({ url, ...(init ? { init } : {}) });
    const response = options.routes?.[url];
    return response ? Promise.resolve(response) : Promise.reject(new Error(`no route for ${url}`));
  };
  const io: CliIo = {
    readFile,
    fetch: options.fetch ?? routedFetch,
    log: (message) => stdout.push(message),
    error: (message) => stderr.push(message),
  };
  return { io, stdout, stderr, requests, readFile };
}

/** Discovery advertising only the issuer: every probe skips, so the run passes without further requests. */
const bareDiscovery = { [DISCOVERY_URL]: jsonResponse({ issuer: ISSUER }) };

describe('runCli — help and argument errors', () => {
  it.each(['--help', '-h'])('prints the usage to stdout and exits 0 for %s', async (flag) => {
    const { io, stdout, stderr, requests } = fakeIo();
    await expect(runCli([flag], {}, io)).resolves.toBe(0);
    expect(stdout).toEqual([USAGE]);
    expect(stderr).toEqual([]);
    expect(requests).toEqual([]);
  });

  it('prints the errors and usage to stderr and exits 2 for invalid arguments', async () => {
    const { io, stdout, stderr, requests } = fakeIo();
    const code = await runCli(['--auth-method', 'mtls'], {}, io);
    expect(code).toBe(2);
    expect(stderr).toEqual([
      "Missing required '--issuer' (or env CONFORMANCE_ISSUER).\nInvalid '--auth-method' (expected 'basic' or 'post').",
      `\n${USAGE}`,
    ]);
    expect(stdout).toEqual([]);
    expect(requests).toEqual([]);
  });
});

describe('runCli — report output and exit code', () => {
  it('prints the human-readable report and exits 0 when no probe fails', async () => {
    const { io, stdout, stderr, requests } = fakeIo({ routes: bareDiscovery });
    const code = await runCli(['--issuer', ISSUER], {}, io);
    expect(code).toBe(0);
    expect(stderr).toEqual([]);
    expect(stdout).toHaveLength(1);
    expect(stdout[0]).toMatch(/^Conformance probe runner — https:\/\/op\.test\n/);
    expect(stdout[0]).toContain('0 verified · 0 failed · 9 skipped');
    expect(requests.map((request) => request.url)).toEqual([DISCOVERY_URL]);
  });

  it('prints the JSON report for --json', async () => {
    const { io, stdout } = fakeIo({ routes: bareDiscovery });
    const code = await runCli(['--issuer', ISSUER, '--json'], {}, io);
    expect(code).toBe(0);
    const report = JSON.parse(stdout[0] ?? '') as Record<string, unknown>;
    expect(report).toMatchObject({
      issuer: ISSUER,
      totals: { verified: 0, failed: 0, skipped: 9 },
    });
  });

  it('exits 1 when a probe fails, reading the client credentials from the environment', async () => {
    const { io, stdout, stderr, requests } = fakeIo({
      routes: {
        [DISCOVERY_URL]: jsonResponse({ issuer: ISSUER, token_endpoint: TOKEN_URL }),
        [TOKEN_URL]: jsonResponse({ error: 'invalid_client' }, 401),
      },
    });
    const env = { CONFORMANCE_CLIENT_ID: 'svc', CONFORMANCE_CLIENT_SECRET: 's3cr3t' };
    const code = await runCli(['--issuer', ISSUER, '--json'], env, io);
    expect(code).toBe(1);
    expect(stderr).toEqual([]);
    const report = JSON.parse(stdout[0] ?? '') as { totals: { failed: number } };
    expect(report.totals.failed).toBe(1);
    expect(requests.find((r) => r.url === TOKEN_URL)?.init?.headers?.authorization).toBe(
      `Basic ${Buffer.from('svc:s3cr3t').toString('base64')}`,
    );
  });
});

describe('runCli — run failures', () => {
  it('reports an Error cause and exits 2 when discovery cannot be loaded', async () => {
    const { io, stdout, stderr } = fakeIo({
      routes: { [DISCOVERY_URL]: jsonResponse({}, 404) },
    });
    const code = await runCli(['--issuer', ISSUER], {}, io);
    expect(code).toBe(2);
    expect(stderr).toEqual(['Runner failed: Failed to fetch discovery metadata (HTTP 404).']);
    expect(stdout).toEqual([]);
  });

  it('reports a non-Error cause and exits 2', async () => {
    const { io, stderr } = fakeIo({ fetch: () => Promise.reject('socket hang up') });
    const code = await runCli(['--issuer', ISSUER], {}, io);
    expect(code).toBe(2);
    expect(stderr).toEqual(['Runner failed: socket hang up']);
  });

  it('exits 2 without any request when a key file cannot be read', async () => {
    const { io, stderr, requests } = fakeIo({ routes: bareDiscovery });
    const code = await runCli(['--issuer', ISSUER, '--private-key', 'missing.pem'], {}, io);
    expect(code).toBe(2);
    expect(stderr).toEqual(["Runner failed: ENOENT: no such file 'missing.pem'"]);
    expect(requests).toEqual([]);
  });

  it('exits 2 when a key cannot be loaded for the requested algorithm', async () => {
    const { io, stderr, requests } = fakeIo({
      files: { 'dpop.pem': pem(ecPair.privateKey) },
      routes: bareDiscovery,
    });
    const argv = ['--issuer', ISSUER, '--dpop-key', 'dpop.pem', '--dpop-key-alg', 'ES384'];
    const code = await runCli(argv, {}, io);
    expect(code).toBe(2);
    expect(stderr).toEqual([
      "Runner failed: Algorithm 'ES384' does not match the EC curve 'prime256v1' (ES256).",
    ]);
    expect(requests).toEqual([]);
  });
});

describe('runCli — asymmetric signing keys', () => {
  /** Discovery advertising both asymmetric legs, with a token endpoint that issues a DPoP-bound token. */
  const asymmetricRoutes = {
    [DISCOVERY_URL]: jsonResponse({
      issuer: ISSUER,
      token_endpoint: TOKEN_URL,
      token_endpoint_auth_methods_supported: ['private_key_jwt'],
      dpop_signing_alg_values_supported: ['ES256', 'PS256', 'RS256'],
    }),
    [TOKEN_URL]: jsonResponse({ access_token: 'abc', token_type: 'DPoP' }),
  };
  const env = { CONFORMANCE_CLIENT_ID: 'svc', CONFORMANCE_CLIENT_SECRET: 's3cr3t' };

  function clientAssertion(requests: readonly Request[]): string | undefined {
    const assertions = requests
      .map((request) => new URLSearchParams(request.init?.body).get('client_assertion'))
      .filter((assertion): assertion is string => assertion !== null);
    return assertions[0];
  }

  function dpopProof(requests: readonly Request[]): string | undefined {
    return requests.map((request) => request.init?.headers?.dpop).find((proof) => proof);
  }

  it('skips both asymmetric legs when no key path is configured', async () => {
    const { io, stdout, readFile } = fakeIo({ routes: asymmetricRoutes });
    const code = await runCli(['--issuer', ISSUER, '--public-client-id', 'app', '--json'], env, io);
    expect(code).toBe(0);
    expect(readFile).not.toHaveBeenCalled();
    const report = JSON.parse(stdout[0] ?? '') as { results: { id: string; outcome: string }[] };
    const outcomes = Object.fromEntries(report.results.map((r) => [r.id, r.outcome]));
    expect(outcomes).toMatchObject({
      'runner-private-key-jwt': 'skipped',
      'runner-dpop': 'skipped',
    });
  });

  it('signs the client assertion with the key read from --private-key (inferred alg)', async () => {
    const { io, requests, readFile } = fakeIo({
      files: { 'client.pem': pem(ecPair.privateKey) },
      routes: asymmetricRoutes,
    });
    const argv = ['--issuer', ISSUER, '--public-client-id', 'app', '--private-key', 'client.pem'];
    const code = await runCli(argv, {}, io);
    expect(code).toBe(0);
    expect(readFile).toHaveBeenCalledExactlyOnceWith('client.pem');
    const assertion = clientAssertion(requests);
    const [header, payload, signature] = (assertion ?? '').split('.');
    expect(decodeSegment(header)).toEqual({ alg: 'ES256', typ: 'JWT' });
    expect(decodeSegment(payload)).toMatchObject({ iss: 'app', sub: 'app', aud: TOKEN_URL });
    const signed = verify(
      'sha256',
      Buffer.from(`${header}.${payload}`, 'ascii'),
      { key: ecPair.publicKey, dsaEncoding: 'ieee-p1363' },
      Buffer.from(signature ?? '', 'base64url'),
    );
    expect(signed).toBe(true);
    expect(dpopProof(requests)).toBeUndefined();
  });

  it('honors --private-key-alg for the client assertion', async () => {
    const { io, requests } = fakeIo({
      files: { 'client.pem': pem(rsaPair.privateKey) },
      routes: asymmetricRoutes,
    });
    const argv = [
      '--issuer',
      ISSUER,
      '--public-client-id',
      'app',
      '--private-key',
      'client.pem',
      '--private-key-alg',
      'RS256',
    ];
    await runCli(argv, {}, io);
    expect(decodeSegment(clientAssertion(requests)?.split('.')[0])).toMatchObject({
      alg: 'RS256',
    });
  });

  it('builds the DPoP proof from the key read from --dpop-key (inferred alg)', async () => {
    const { io, requests, readFile } = fakeIo({
      files: { 'dpop.pem': pem(rsaPair.privateKey) },
      routes: asymmetricRoutes,
    });
    const code = await runCli(['--issuer', ISSUER, '--dpop-key', 'dpop.pem', '--json'], env, io);
    expect(code).toBe(0);
    expect(readFile).toHaveBeenCalledExactlyOnceWith('dpop.pem');
    const header = decodeSegment(dpopProof(requests)?.split('.')[0]);
    expect(header).toMatchObject({ typ: 'dpop+jwt', alg: 'PS256', jwk: { kty: 'RSA' } });
    expect(clientAssertion(requests)).toBeUndefined();
  });

  it('honors --dpop-key-alg for the DPoP proof', async () => {
    const { io, requests } = fakeIo({
      files: { 'dpop.pem': pem(rsaPair.privateKey) },
      routes: asymmetricRoutes,
    });
    const argv = ['--issuer', ISSUER, '--dpop-key', 'dpop.pem', '--dpop-key-alg', 'RS256'];
    await runCli(argv, env, io);
    expect(decodeSegment(dpopProof(requests)?.split('.')[0])).toMatchObject({ alg: 'RS256' });
  });

  it('loads both keys and verifies both asymmetric legs', async () => {
    const { io, stdout, readFile } = fakeIo({
      files: { 'client.pem': pem(ecPair.privateKey), 'dpop.pem': pem(ecPair.privateKey) },
      routes: asymmetricRoutes,
    });
    const argv = [
      '--issuer',
      ISSUER,
      '--public-client-id',
      'app',
      '--private-key',
      'client.pem',
      '--dpop-key',
      'dpop.pem',
      '--json',
    ];
    const code = await runCli(argv, env, io);
    expect(code).toBe(0);
    expect(readFile.mock.calls).toEqual([['client.pem'], ['dpop.pem']]);
    const report = JSON.parse(stdout[0] ?? '') as { results: { id: string; outcome: string }[] };
    const outcomes = Object.fromEntries(report.results.map((r) => [r.id, r.outcome]));
    expect(outcomes).toMatchObject({
      'runner-private-key-jwt': 'verified',
      'runner-dpop': 'verified',
    });
  });
});
