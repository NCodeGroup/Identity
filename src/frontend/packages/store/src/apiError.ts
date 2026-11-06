import type { FetchBaseQueryError } from '@reduxjs/toolkit/query';
import type { SerializedError } from '@reduxjs/toolkit';
import type { ManagementProblem } from '@ncode/api';

/** The error shape RTK Query surfaces from a failed query/mutation. */
export type ApiError = FetchBaseQueryError | SerializedError;

function isFetchBaseQueryError(error: ApiError): error is FetchBaseQueryError {
  return 'status' in error;
}

/** Describes a wait of whole seconds in seconds, minutes or hours, rounding up to the next whole unit. */
function describeWait(seconds: number): string {
  if (seconds < 60) {
    return seconds === 1 ? '1 second' : `${seconds} seconds`;
  }
  const minutes = Math.ceil(seconds / 60);
  if (minutes < 60) {
    return minutes === 1 ? '1 minute' : `${minutes} minutes`;
  }
  const hours = Math.ceil(seconds / 3600);
  return hours === 1 ? '1 hour' : `${hours} hours`;
}

function statusMessage(
  error: FetchBaseQueryError,
  problem: ManagementProblem | undefined,
  fallback: string,
): string {
  if (problem) {
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

/**
 * Turns an RTK Query error into a human-readable message. The Management API returns RFC 9457
 * Problem Details (`{ detail, title, status }`), so a server-supplied `detail`/`title` is preferred;
 * otherwise a status-aware fallback is used. When the problem carries a positive whole
 * `retryAfterSeconds`, the message ends with " Try again in N seconds|minutes|hours.".
 * UI-free so it is unit-tested directly (see docs/decisions/1064).
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
    const problem = data && typeof data === 'object' ? (data as ManagementProblem) : undefined;
    const message = statusMessage(error, problem, fallback);
    const wait = problem?.retryAfterSeconds;
    if (typeof wait === 'number' && Number.isInteger(wait) && wait > 0) {
      return `${message} Try again in ${describeWait(wait)}.`;
    }
    return message;
  }

  return error.message ?? fallback;
}
