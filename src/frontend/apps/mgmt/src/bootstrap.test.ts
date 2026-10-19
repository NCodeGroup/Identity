import { beforeEach, describe, expect, it, vi } from 'vitest';
import { loadRuntimeConfig, type RuntimeConfig } from '@ncode/config';
import { bootstrapRuntimeConfig, currentOrigin } from './bootstrap.js';

vi.mock('@ncode/config', () => ({ loadRuntimeConfig: vi.fn() }));

const runtime: RuntimeConfig = {
  apiBaseUrl: 'https://api.test',
  issuer: 'https://op.test',
  authorizationEndpoint: 'https://op.test/authorize',
  tokenEndpoint: 'https://op.test/token',
  scopes: ['openid'],
  features: {},
};

describe('bootstrap', () => {
  beforeEach(() => {
    vi.mocked(loadRuntimeConfig).mockReset();
    vi.mocked(loadRuntimeConfig).mockResolvedValue(runtime);
  });

  it('resolves the origin that serves the bundle', () => {
    expect(currentOrigin()).toBe(window.location.origin);
  });

  it('loads the mgmt surface config from the window origin without a signal when none is given', async () => {
    await expect(bootstrapRuntimeConfig()).resolves.toBe(runtime);

    expect(loadRuntimeConfig).toHaveBeenCalledTimes(1);
    const options = vi.mocked(loadRuntimeConfig).mock.calls[0]![0];
    expect(options).toEqual({ origin: window.location.origin, surface: 'mgmt' });
    expect(options).not.toHaveProperty('signal');
  });

  it('forwards the abort signal when one is given', async () => {
    const controller = new AbortController();

    await bootstrapRuntimeConfig(controller.signal);

    expect(loadRuntimeConfig).toHaveBeenCalledWith({
      origin: window.location.origin,
      surface: 'mgmt',
      signal: controller.signal,
    });
  });

  it('propagates a discovery failure to the caller', async () => {
    vi.mocked(loadRuntimeConfig).mockRejectedValue(new Error('bootstrap.json returned 404'));

    await expect(bootstrapRuntimeConfig()).rejects.toThrow('bootstrap.json returned 404');
  });
});
