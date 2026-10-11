import { describe, expect, it } from 'vitest';
import { apiErrorMessage } from './apiError.js';

describe('apiErrorMessage', () => {
  it('returns the fallback when there is no error', () => {
    expect(apiErrorMessage(undefined, 'nope')).toBe('nope');
  });

  it('prefers the Problem Details detail', () => {
    expect(
      apiErrorMessage({ status: 400, data: { detail: 'Domain already in use.', title: 'Bad' } }),
    ).toBe('Domain already in use.');
  });

  it('falls back to the Problem Details title', () => {
    expect(apiErrorMessage({ status: 400, data: { title: 'Validation failed' } })).toBe(
      'Validation failed',
    );
  });

  it('explains a 409 conflict', () => {
    expect(apiErrorMessage({ status: 409, data: undefined })).toMatch(/dependents/);
  });

  it('explains a 412 precondition failure', () => {
    expect(apiErrorMessage({ status: 412, data: undefined })).toMatch(/changed since/);
  });

  it('reports a generic numeric status', () => {
    expect(apiErrorMessage({ status: 500, data: undefined })).toMatch(/HTTP 500/);
  });

  it('reads the message from a serialized error', () => {
    expect(apiErrorMessage({ message: 'boom' })).toBe('boom');
  });
});
