import { describe, expect, it } from 'vitest';
import type { ProbeFetch, ProbeResponse } from '@ncode/conformance-core';
import { runConformanceRunner } from './runner.js';
import type { RunnerConfig } from './config.js';

function jsonResponse(body: unknown, status = 200): ProbeResponse {
  return {
    ok: status >= 200 && status < 300,
    status,
    headers: { get: () => null },
    json: () => Promise.resolve(body),
    text: () => Promise.resolve(JSON.stringify(body)),
  };
}

const config: RunnerConfig = {
  issuer: 'https://op.test',
  clientId: 'svc',
  clientSecret: 'secret',
  authMethod: 'client_secret_basic',
  json: false,
  timeoutMs: 1000,
};

describe('runConformanceRunner', () => {
  it('loads discovery then runs the probe suite', async () => {
    const discovery = {
      issuer: 'https://op.test',
      token_endpoint: 'https://op.test/token',
    };
    const fetchFn: ProbeFetch = (url) => {
      if (url.endsWith('/.well-known/openid-configuration')) {
        return Promise.resolve(jsonResponse(discovery));
      }
      if (url === 'https://op.test/token') {
        return Promise.resolve(jsonResponse({ access_token: 'abc' }));
      }
      return Promise.reject(new Error(`no route for ${url}`));
    };
    const report = await runConformanceRunner(config, { fetchFn });
    expect(report.results).toHaveLength(9);
    expect(report.totals.verified).toBeGreaterThanOrEqual(1);
  });

  it('propagates a discovery failure', async () => {
    const fetchFn: ProbeFetch = () => Promise.resolve(jsonResponse({ error: 'nope' }, 404));
    await expect(runConformanceRunner(config, { fetchFn })).rejects.toThrow();
  });
});
