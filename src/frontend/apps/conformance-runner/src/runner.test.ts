import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
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

  it('feeds the injected signers to the asymmetric probes', async () => {
    const discovery = {
      issuer: 'https://op.test',
      token_endpoint: 'https://op.test/token',
      token_endpoint_auth_methods_supported: ['private_key_jwt'],
      dpop_signing_alg_values_supported: ['ES256'],
    };
    const fetchFn: ProbeFetch = (url) => {
      if (url.endsWith('/.well-known/openid-configuration')) {
        return Promise.resolve(jsonResponse(discovery));
      }
      if (url === 'https://op.test/token') {
        return Promise.resolve(jsonResponse({ access_token: 'abc', token_type: 'DPoP' }));
      }
      return Promise.reject(new Error(`no route for ${url}`));
    };
    const report = await runConformanceRunner(
      { ...config, publicClientId: 'app' },
      {
        fetchFn,
        signClientAssertion: () => Promise.resolve('signed.jwt'),
        createDpopProof: () => Promise.resolve('dpop-proof'),
      },
    );
    const outcomes = Object.fromEntries(report.results.map((r) => [r.id, r.outcome]));
    expect(outcomes['runner-private-key-jwt']).toBe('verified');
    expect(outcomes['runner-dpop']).toBe('verified');
  });

  describe('run-wide timeout', () => {
    beforeEach(() => {
      vi.useFakeTimers();
    });

    afterEach(() => {
      vi.useRealTimers();
    });

    it('aborts an in-flight request once the timeout elapses', async () => {
      const signals: AbortSignal[] = [];
      const fetchFn: ProbeFetch = (_url, init) =>
        new Promise((_resolve, reject) => {
          const signal = init?.signal;
          if (signal) {
            signals.push(signal);
            signal.addEventListener('abort', () => reject(new Error('aborted')));
          }
        });
      const run = runConformanceRunner({ ...config, timeoutMs: 5000 }, { fetchFn });
      const outcome = expect(run).rejects.toThrow('aborted');

      await vi.advanceTimersByTimeAsync(4999);
      expect(signals).toHaveLength(1);
      expect(signals[0]?.aborted).toBe(false);

      await vi.advanceTimersByTimeAsync(1);
      await outcome;
      expect(signals[0]?.aborted).toBe(true);
    });

    it('clears the timer once the run settles', async () => {
      const fetchFn: ProbeFetch = () =>
        Promise.resolve(jsonResponse({ issuer: 'https://op.test' }));
      await runConformanceRunner(config, { fetchFn });
      expect(vi.getTimerCount()).toBe(0);
    });
  });
});
