import { describe, expect, it } from 'vitest';
import { parseArgs, toProbeContext, USAGE, type RunnerConfig } from './config.js';

describe('parseArgs', () => {
  it('parses an issuer and defaults', () => {
    const result = parseArgs(['--issuer', 'https://op.test']);
    expect(result).toMatchObject({
      kind: 'config',
      config: { issuer: 'https://op.test', authMethod: 'client_secret_basic', json: false },
    });
  });

  it('reads the issuer and credentials from the environment', () => {
    const result = parseArgs([], {
      CONFORMANCE_ISSUER: 'https://op.test',
      CONFORMANCE_CLIENT_ID: 'svc',
      CONFORMANCE_CLIENT_SECRET: 'secret',
    });
    expect(result).toMatchObject({
      kind: 'config',
      config: { clientId: 'svc', clientSecret: 'secret' },
    });
  });

  it('maps --auth-method post to client_secret_post', () => {
    const result = parseArgs(['--issuer', 'https://op.test', '--auth-method', 'post']);
    expect(result.kind === 'config' && result.config.authMethod).toBe('client_secret_post');
  });

  it('returns help for --help', () => {
    expect(parseArgs(['--help']).kind).toBe('help');
  });

  it('errors when the issuer is missing', () => {
    const result = parseArgs(['--scope', 'openid']);
    expect(result.kind).toBe('error');
  });

  it('errors on an invalid auth method', () => {
    const result = parseArgs(['--issuer', 'https://op.test', '--auth-method', 'mtls']);
    expect(result.kind).toBe('error');
  });

  it('errors when only one of client id / secret is given', () => {
    const result = parseArgs(['--issuer', 'https://op.test', '--client-id', 'svc']);
    expect(result.kind).toBe('error');
  });

  it('errors on a non-positive timeout', () => {
    const result = parseArgs(['--issuer', 'https://op.test', '--timeout', '0']);
    expect(result.kind).toBe('error');
  });

  it('errors when an option is missing its value', () => {
    const result = parseArgs(['--issuer']);
    expect(result.kind).toBe('error');
  });

  it('ignores a standalone -- end-of-options delimiter', () => {
    const result = parseArgs(['--', '--issuer', 'https://op.test']);
    expect(result).toMatchObject({ kind: 'config', config: { issuer: 'https://op.test' } });
  });

  it('parses the key paths and algorithm overrides', () => {
    const result = parseArgs([
      '--issuer',
      'https://op.test',
      '--private-key',
      'client.pem',
      '--private-key-alg',
      'RS256',
      '--dpop-key',
      'dpop.jwk',
    ]);
    expect(result).toMatchObject({
      kind: 'config',
      config: {
        privateKeyPath: 'client.pem',
        privateKeyAlg: 'RS256',
        dpopKeyPath: 'dpop.jwk',
      },
    });
  });

  it('errors on an unsupported signing algorithm', () => {
    const result = parseArgs(['--issuer', 'https://op.test', '--private-key-alg', 'HS256']);
    expect(result.kind).toBe('error');
  });

  it('sets json for --json', () => {
    const result = parseArgs(['--json', '--issuer', 'https://op.test']);
    expect(result.kind === 'config' && result.config.json).toBe(true);
  });

  it('returns help for -h even after other arguments', () => {
    expect(parseArgs(['--issuer', 'https://op.test', '-h']).kind).toBe('help');
  });

  it('defaults the timeout to ten seconds', () => {
    const result = parseArgs(['--issuer', 'https://op.test']);
    expect(result.kind === 'config' && result.config.timeoutMs).toBe(10_000);
  });

  it('parses a positive timeout', () => {
    const result = parseArgs(['--issuer', 'https://op.test', '--timeout', '2500']);
    expect(result.kind === 'config' && result.config.timeoutMs).toBe(2500);
  });

  it.each(['abc', '-5', 'Infinity'])("errors on the timeout '%s'", (timeout) => {
    const result = parseArgs(['--issuer', 'https://op.test', '--timeout', timeout]);
    expect(result).toEqual({
      kind: 'error',
      errors: ["Invalid '--timeout' (expected a positive number of milliseconds)."],
    });
  });

  it('errors on a positional argument', () => {
    const result = parseArgs(['--issuer', 'https://op.test', 'extra']);
    expect(result).toEqual({ kind: 'error', errors: ["Unexpected argument 'extra'."] });
  });

  it('errors when an option is followed by another option instead of a value', () => {
    const result = parseArgs(['--scope', '--issuer', 'https://op.test']);
    expect(result).toEqual({ kind: 'error', errors: ["Option '--scope' expects a value."] });
  });

  it('errors when only the client secret is given', () => {
    const result = parseArgs(['--issuer', 'https://op.test'], { CONFORMANCE_CLIENT_SECRET: 's' });
    expect(result).toEqual({
      kind: 'error',
      errors: ['A confidential client needs both --client-id and --client-secret.'],
    });
  });

  it('errors on an unsupported DPoP signing algorithm', () => {
    const result = parseArgs(['--issuer', 'https://op.test', '--dpop-key-alg', 'none']);
    expect(result).toEqual({
      kind: 'error',
      errors: ["Invalid '--dpop-key-alg' (unsupported signature algorithm 'none')."],
    });
  });

  it('accumulates every validation error in order', () => {
    const result = parseArgs(['--auth-method', 'mtls', '--timeout', '0', 'stray']);
    expect(result).toEqual({
      kind: 'error',
      errors: [
        "Unexpected argument 'stray'.",
        "Missing required '--issuer' (or env CONFORMANCE_ISSUER).",
        "Invalid '--auth-method' (expected 'basic' or 'post').",
        "Invalid '--timeout' (expected a positive number of milliseconds).",
      ],
    });
  });

  it('prefers command-line values over the environment', () => {
    const result = parseArgs(['--issuer', 'https://cli.test', '--client-id', 'cli'], {
      CONFORMANCE_ISSUER: 'https://env.test',
      CONFORMANCE_CLIENT_ID: 'env',
      CONFORMANCE_CLIENT_SECRET: 'secret',
    });
    expect(result).toMatchObject({
      kind: 'config',
      config: { issuer: 'https://cli.test', clientId: 'cli', clientSecret: 'secret' },
    });
  });

  it('parses every optional setting into the config', () => {
    const result = parseArgs([
      '--issuer',
      'https://op.test',
      '--auth-method',
      'basic',
      '--public-client-id',
      'app',
      '--scope',
      'openid api',
      '--login-hint',
      'alice',
      '--dpop-key',
      'dpop.pem',
      '--dpop-key-alg',
      'ES256',
    ]);
    expect(result).toEqual({
      kind: 'config',
      config: {
        issuer: 'https://op.test',
        authMethod: 'client_secret_basic',
        json: false,
        timeoutMs: 10_000,
        publicClientId: 'app',
        scope: 'openid api',
        loginHint: 'alice',
        dpopKeyPath: 'dpop.pem',
        dpopKeyAlg: 'ES256',
      },
    });
  });

  it('omits unset optional settings from the config', () => {
    const result = parseArgs(['--issuer', 'https://op.test']);
    expect(result).toEqual({
      kind: 'config',
      config: {
        issuer: 'https://op.test',
        authMethod: 'client_secret_basic',
        json: false,
        timeoutMs: 10_000,
      },
    });
  });
});

describe('USAGE', () => {
  it('documents every option and the default timeout', () => {
    for (const option of ['--issuer', '--auth-method', '--dpop-key-alg', '--timeout', '--json']) {
      expect(USAGE).toContain(option);
    }
    expect(USAGE).toContain('default 10000');
  });

  it('documents all three exit codes', () => {
    expect(USAGE).toMatch(/exits 0 when no probe fails, 1 when any probe fails, and 2 on invalid/);
  });
});

describe('toProbeContext', () => {
  it('projects a confidential client', () => {
    const config: RunnerConfig = {
      issuer: 'https://op.test',
      clientId: 'svc',
      clientSecret: 'secret',
      authMethod: 'client_secret_basic',
      json: false,
      timeoutMs: 1000,
    };
    expect(toProbeContext(config).client).toEqual({
      clientId: 'svc',
      clientSecret: 'secret',
      authMethod: 'client_secret_basic',
    });
  });

  it('omits the client when no credentials are configured', () => {
    const config: RunnerConfig = {
      issuer: 'https://op.test',
      authMethod: 'client_secret_basic',
      json: false,
      timeoutMs: 1000,
      publicClientId: 'app',
    };
    const context = toProbeContext(config);
    expect(context.client).toBeUndefined();
    expect(context.publicClientId).toBe('app');
  });

  it('projects the scope and login hint', () => {
    const config: RunnerConfig = {
      issuer: 'https://op.test',
      authMethod: 'client_secret_post',
      json: false,
      timeoutMs: 1000,
      scope: 'openid',
      loginHint: 'alice',
    };
    expect(toProbeContext(config)).toEqual({ scope: 'openid', loginHint: 'alice' });
  });

  it('omits the client when only the client id is configured', () => {
    const config: RunnerConfig = {
      issuer: 'https://op.test',
      clientId: 'svc',
      authMethod: 'client_secret_basic',
      json: false,
      timeoutMs: 1000,
    };
    expect(toProbeContext(config)).toEqual({});
  });
});
