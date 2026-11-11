import type { BffRuntimeConfig } from '@ncode/config';
import type { BffUser } from './sessionSlice.js';

const MALFORMED_USER = 'The session probe answered with a malformed user.';

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value);
}

function requireString(value: unknown): string {
  if (typeof value !== 'string' || value === '') {
    throw new Error(MALFORMED_USER);
  }
  return value;
}

function requireNumber(value: unknown): number {
  if (typeof value !== 'number') {
    throw new Error(MALFORMED_USER);
  }
  return value;
}

/**
 * Validates the JSON the BFF's user endpoint answers with. `name` is kept only when it is a non-empty
 * string; every other field is required.
 */
export function parseBffUser(raw: unknown): BffUser {
  if (!isRecord(raw) || !isRecord(raw.session) || !isRecord(raw.claims)) {
    throw new Error(MALFORMED_USER);
  }

  const sub = requireString(raw.sub);
  const logoutUrl = requireString(raw.logoutUrl);
  const logoutSid = requireString(raw.logoutSid);
  const expiresAt = requireNumber(raw.session.expiresAt);
  const idleExpiresAt = requireNumber(raw.session.idleExpiresAt);
  const name = typeof raw.name === 'string' && raw.name !== '' ? raw.name : undefined;

  return {
    sub,
    ...(name === undefined ? {} : { name }),
    claims: raw.claims,
    session: { expiresAt, idleExpiresAt },
    logoutUrl,
    logoutSid,
  };
}

/**
 * Probes the BFF's user endpoint with the session cookie. Answers the signed-in user, or `null` when
 * the BFF answers `401` (no session). Any other failure throws. The probe never slides the session's
 * idle window (ADR-1434).
 */
export async function fetchBffUser(
  runtime: BffRuntimeConfig,
  fetchFn: typeof fetch = fetch,
): Promise<BffUser | null> {
  const { basePath, antiforgeryHeader } = runtime.bff;
  const response = await fetchFn(`${basePath}/user`, {
    credentials: 'same-origin',
    cache: 'no-store',
    headers: { accept: 'application/json', [antiforgeryHeader.name]: antiforgeryHeader.value },
  });

  if (response.status === 401) {
    return null;
  }
  if (!response.ok) {
    throw new Error(`The session probe failed (HTTP ${response.status}).`);
  }
  return parseBffUser(await response.json());
}
