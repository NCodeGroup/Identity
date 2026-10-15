import { describe, expect, it } from 'vitest';
import { parseArgs, toProbeContext, type RunnerConfig } from './config.js';

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
});
