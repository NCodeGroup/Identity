import type { FetchBaseQueryError } from '@reduxjs/toolkit/query';
import type { SerializedError } from '@reduxjs/toolkit';

/** The error shape RTK Query surfaces from a failed query/mutation. */
export type ApiError = FetchBaseQueryError | SerializedError;

function isFetchBaseQueryError(error: ApiError): error is FetchBaseQueryError {
  return 'status' in error;
}

/**
 * Turns an RTK Query error into a human-readable message. The Management API returns RFC 7807
 * Problem Details (`{ detail, title, status }`), so a server-supplied `detail`/`title` is preferred;
 * otherwise a status-aware fallback is used. UI-free so it is unit-tested directly (see docs/adr/0064).
 */
export function apiErrorMessage(
  error: ApiError | undefined,
  fallback = 'Something went wrong.',
): string {
  if (!error) {
    return fallback;
  }

  if (isFetchBaseQueryError(error)) {
    const data = error.data;
    if (data && typeof data === 'object') {
      const problem = data as { detail?: unknown; title?: unknown };
      if (typeof problem.detail === 'string' && problem.detail.length > 0) {
        return problem.detail;
      }
      if (typeof problem.title === 'string' && problem.title.length > 0) {
        return problem.title;
      }
    }

    if (error.status === 409) {
      return 'The item was modified by someone else, or still has dependents. Reload and try again.';
    }
    if (error.status === 412) {
      return 'The item changed since you loaded it. Reload and try again.';
    }
    if (typeof error.status === 'number') {
      return `The server rejected the request (HTTP ${error.status}).`;
    }
    return fallback;
  }

  return error.message ?? fallback;
}
