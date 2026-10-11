import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { createTokenRenewer, DEFAULT_RENEWAL_SKEW_SECONDS, renewalDelayMs } from './renew.js';
import type { FetchLike } from './flow.js';

describe('renewalDelayMs', () => {
  it('schedules skewSeconds ahead of absolute expiry', () => {
    const now = 1_000_000;
    const expiresAt = now + 3600 * 1000;
    expect(renewalDelayMs(expiresAt, now, 60)).toBe(3600 * 1000 - 60 * 1000);
  });

  it('defaults the skew to DEFAULT_RENEWAL_SKEW_SECONDS', () => {
    const now = 0;
    const expiresAt = 120 * 1000;
    expect(renewalDelayMs(expiresAt, now)).toBe((120 - DEFAULT_RENEWAL_SKEW_SECONDS) * 1000);
  });

  it('floors at zero when renewal is already due or past', () => {
    expect(renewalDelayMs(1000, 10_000, 60)).toBe(0);
  });
});

function okJson(body: unknown): ReturnType<FetchLike> {
  return Promise.resolve({
    ok: true,
    status: 200,
    json: () => Promise.resolve(body),
    text: () => Promise.resolve(JSON.stringify(body)),
  });
}

describe('createTokenRenewer', () => {
  beforeEach(() => {
    vi.useFakeTimers();
    vi.setSystemTime(0);
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it('renews skewSeconds before expiry and reports the new token set', async () => {
    const fetchFn = vi.fn<FetchLike>(() =>
      okJson({ access_token: 'at2', token_type: 'Bearer', expires_in: 3600, refresh_token: 'rt2' }),
    );
    const onRenewed = vi.fn();
    const onError = vi.fn();
    const renewer = createTokenRenewer({
      tokenEndpoint: 'https://op.test/token',
      clientId: 'mgmt-spa',
      fetchFn,
      getRefreshToken: () => 'rt1',
      onRenewed,
      onError,
      skewSeconds: 60,
    });

    renewer.schedule(3600 * 1000);

    expect(fetchFn).not.toHaveBeenCalled();
    await vi.advanceTimersByTimeAsync((3600 - 60) * 1000);

    expect(fetchFn).toHaveBeenCalledTimes(1);
    expect(onRenewed).toHaveBeenCalledWith(
      expect.objectContaining({ accessToken: 'at2', refreshToken: 'rt2' }),
    );
    expect(onError).not.toHaveBeenCalled();
  });

  it('re-arms itself from the renewed token expiry', async () => {
    const fetchFn = vi.fn<FetchLike>(() =>
      okJson({ access_token: 'at', token_type: 'Bearer', expires_in: 3600, refresh_token: 'rt' }),
    );
    const renewer = createTokenRenewer({
      tokenEndpoint: 'https://op.test/token',
      clientId: 'mgmt-spa',
      fetchFn,
      getRefreshToken: () => 'rt',
      onRenewed: vi.fn(),
      onError: vi.fn(),
      skewSeconds: 60,
    });

    renewer.schedule(3600 * 1000);
    await vi.advanceTimersByTimeAsync((3600 - 60) * 1000);
    expect(fetchFn).toHaveBeenCalledTimes(1);

    await vi.advanceTimersByTimeAsync((3600 - 60) * 1000);
    expect(fetchFn).toHaveBeenCalledTimes(2);
  });

  it('reports an error and stops when no refresh token is available', async () => {
    const fetchFn = vi.fn<FetchLike>(() => okJson({}));
    const onError = vi.fn();
    const renewer = createTokenRenewer({
      tokenEndpoint: 'https://op.test/token',
      clientId: 'mgmt-spa',
      fetchFn,
      getRefreshToken: () => null,
      onRenewed: vi.fn(),
      onError,
      skewSeconds: 60,
    });

    renewer.schedule(3600 * 1000);
    await vi.advanceTimersByTimeAsync((3600 - 60) * 1000);

    expect(fetchFn).not.toHaveBeenCalled();
    expect(onError).toHaveBeenCalledTimes(1);
  });

  it('surfaces a failed refresh through onError', async () => {
    const fetchFn = vi.fn<FetchLike>(() =>
      Promise.resolve({
        ok: false,
        status: 400,
        json: () => Promise.resolve({ error: 'invalid_grant' }),
        text: () => Promise.resolve('{"error":"invalid_grant"}'),
      }),
    );
    const onError = vi.fn();
    const renewer = createTokenRenewer({
      tokenEndpoint: 'https://op.test/token',
      clientId: 'mgmt-spa',
      fetchFn,
      getRefreshToken: () => 'rt',
      onRenewed: vi.fn(),
      onError,
      skewSeconds: 60,
    });

    renewer.schedule(3600 * 1000);
    await vi.advanceTimersByTimeAsync((3600 - 60) * 1000);

    expect(onError).toHaveBeenCalledTimes(1);
  });

  it('cancels a pending renewal when scheduled with null', async () => {
    const fetchFn = vi.fn<FetchLike>(() => okJson({}));
    const renewer = createTokenRenewer({
      tokenEndpoint: 'https://op.test/token',
      clientId: 'mgmt-spa',
      fetchFn,
      getRefreshToken: () => 'rt',
      onRenewed: vi.fn(),
      onError: vi.fn(),
    });

    renewer.schedule(3600 * 1000);
    renewer.schedule(null);
    await vi.advanceTimersByTimeAsync(3600 * 1000);

    expect(fetchFn).not.toHaveBeenCalled();
  });

  it('stop() prevents a scheduled renewal from firing', async () => {
    const fetchFn = vi.fn<FetchLike>(() => okJson({}));
    const renewer = createTokenRenewer({
      tokenEndpoint: 'https://op.test/token',
      clientId: 'mgmt-spa',
      fetchFn,
      getRefreshToken: () => 'rt',
      onRenewed: vi.fn(),
      onError: vi.fn(),
    });

    renewer.schedule(3600 * 1000);
    renewer.stop();
    await vi.advanceTimersByTimeAsync(3600 * 1000);

    expect(fetchFn).not.toHaveBeenCalled();
  });
});
