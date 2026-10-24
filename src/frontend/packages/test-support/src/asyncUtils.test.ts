import { describe, expect, it, vi } from 'vitest';
import { ASYNC_UTIL_TIMEOUT_MS, configureAsyncUtilTimeout } from './asyncUtils.js';

describe('configureAsyncUtilTimeout', () => {
  it('gives every Testing Library wait the one timeout', () => {
    const configure = vi.fn();

    configureAsyncUtilTimeout(configure);

    expect(configure).toHaveBeenCalledExactlyOnceWith({ asyncUtilTimeout: ASYNC_UTIL_TIMEOUT_MS });
  });

  it('waits ten seconds', () => {
    expect(ASYNC_UTIL_TIMEOUT_MS).toBe(10_000);
  });
});
