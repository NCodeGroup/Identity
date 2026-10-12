import { DISCOVERY_PATH } from '@ncode/config';
import type { RawDiscovery } from './audit.js';

/** Minimal fetch surface so discovery retrieval is testable without a DOM/network. */
export type FetchLike = (
  url: string,
  init?: { signal?: AbortSignal },
) => Promise<{ readonly ok: boolean; readonly status: number; json(): Promise<unknown> }>;

/**
 * Fetches the OpenID Provider's full, raw discovery metadata so the audit can inspect every advertised field — a
 * superset of the subset `@ncode/config` normalizes for app wiring. Throws when the document cannot be retrieved or
 * is not a JSON object.
 */
export async function fetchRawDiscovery(
  issuer: string,
  fetchFn: FetchLike,
  signal?: AbortSignal,
): Promise<RawDiscovery> {
  const base = issuer.endsWith('/') ? issuer.slice(0, -1) : issuer;
  const response = await fetchFn(`${base}${DISCOVERY_PATH}`, signal ? { signal } : undefined);
  if (!response.ok) {
    throw new Error(`Failed to fetch discovery metadata (HTTP ${response.status}).`);
  }
  const raw = await response.json();
  if (raw === null || typeof raw !== 'object' || Array.isArray(raw)) {
    throw new Error('Discovery metadata must be a JSON object.');
  }
  return raw as RawDiscovery;
}
