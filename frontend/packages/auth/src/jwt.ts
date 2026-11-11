import { AuthError } from './flow.js';

/** Decodes the UTF-8 payload of a base64url segment (no signature verification). */
function base64UrlDecode(segment: string): string {
  const padded = segment
    .replace(/-/g, '+')
    .replace(/_/g, '/')
    .padEnd(Math.ceil(segment.length / 4) * 4, '=');
  const binary = atob(padded);
  // Reinterpret the binary string as UTF-8.
  const bytes = Uint8Array.from(binary, (c) => c.charCodeAt(0));
  return new TextDecoder().decode(bytes);
}

/**
 * Decodes (does **not** verify) the claims of a JWT, so the Playground can show what the OpenID
 * Provider issued; signature verification is the token consumer's responsibility.
 */
export function decodeJwtClaims(token: string): Record<string, unknown> {
  const parts = token.split('.');
  if (parts.length < 2 || !parts[1]) {
    throw new AuthError('Malformed JWT: expected at least a header and payload.');
  }
  try {
    return JSON.parse(base64UrlDecode(parts[1])) as Record<string, unknown>;
  } catch {
    throw new AuthError('Malformed JWT: payload is not valid JSON.');
  }
}
