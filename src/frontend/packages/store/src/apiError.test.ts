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

  it('ignores an empty detail and a non-string title in the Problem Details', () => {
    expect(apiErrorMessage({ status: 400, data: { detail: '', title: 42 } })).toBe(
      'The server rejected the request (HTTP 400).',
    );
  });

  it('ignores an empty title in the Problem Details', () => {
    expect(apiErrorMessage({ status: 422, data: { title: '' } })).toMatch(/HTTP 422/);
  });

  it('ignores a non-object error body', () => {
    expect(apiErrorMessage({ status: 502, data: 'Bad Gateway' })).toMatch(/HTTP 502/);
  });

  it('returns the fallback for a transport failure with no HTTP status', () => {
    expect(apiErrorMessage({ status: 'FETCH_ERROR', error: 'TypeError: Failed to fetch' })).toBe(
      'Something went wrong.',
    );
  });

  it('returns the supplied fallback for a custom client-side error', () => {
    expect(
      apiErrorMessage(
        { status: 'CUSTOM_ERROR', error: 'Runtime configuration has not been loaded.' },
        'Could not load tenants.',
      ),
    ).toBe('Could not load tenants.');
  });

  it('reads the message from a serialized error', () => {
    expect(apiErrorMessage({ message: 'boom' })).toBe('boom');
  });

  it('returns the fallback for a serialized error without a message', () => {
    expect(apiErrorMessage({ name: 'AbortError' }, 'Cancelled.')).toBe('Cancelled.');
  });

  it.each([
    [1, 'Busy. Try again in 1 second.'],
    [59, 'Busy. Try again in 59 seconds.'],
    [60, 'Busy. Try again in 1 minute.'],
    [91, 'Busy. Try again in 2 minutes.'],
    [3599, 'Busy. Try again in 1 hour.'],
    [7200, 'Busy. Try again in 2 hours.'],
  ])('appends the wait of %i seconds to the detail', (seconds, expected) => {
    expect(
      apiErrorMessage({ status: 409, data: { detail: 'Busy.', retryAfterSeconds: seconds } }),
    ).toBe(expected);
  });

  it('appends the wait to the status fallback', () => {
    expect(apiErrorMessage({ status: 503, data: { retryAfterSeconds: 120 } })).toBe(
      'The server rejected the request (HTTP 503). Try again in 2 minutes.',
    );
  });

  it.each([0, -5, 1.5, '60'])('does not append the wait %s', (wait) => {
    expect(
      apiErrorMessage({ status: 409, data: { detail: 'Busy.', retryAfterSeconds: wait } }),
    ).toBe('Busy.');
  });
});
