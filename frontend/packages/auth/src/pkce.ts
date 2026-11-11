/**
 * PKCE primitives (RFC 7636). Randomness and SHA-256 are injectable so the helpers are deterministic
 * under test; by default they use the Web Crypto global available in browsers and Node ≥ 20.
 */

/** Characters allowed in a PKCE code verifier (RFC 7636 §4.1 unreserved set). */
const UNRESERVED = 'ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-._~';

/** Fills `length` bytes of cryptographically-strong randomness. Injectable for tests. */
export type RandomBytes = (length: number) => Uint8Array;

const defaultRandomBytes: RandomBytes = (length) => {
  const bytes = new Uint8Array(length);
  crypto.getRandomValues(bytes);
  return bytes;
};

/** Base64url-encodes bytes without padding (RFC 7636 §A). */
export function base64UrlEncode(bytes: Uint8Array): string {
  let binary = '';
  for (const byte of bytes) {
    binary += String.fromCharCode(byte);
  }
  return btoa(binary).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
}

/** Generates a random URL-safe string of the given length from the unreserved set. */
export function randomUrlSafe(
  length: number,
  randomBytes: RandomBytes = defaultRandomBytes,
): string {
  const bytes = randomBytes(length);
  let out = '';
  for (let i = 0; i < length; i += 1) {
    out += UNRESERVED[bytes[i]! % UNRESERVED.length];
  }
  return out;
}

/** Generates a PKCE code verifier (43–128 chars; defaults to 64). */
export function generateCodeVerifier(randomBytes?: RandomBytes): string {
  return randomUrlSafe(64, randomBytes);
}

/** Derives the S256 code challenge for a verifier: base64url(SHA-256(verifier)). */
export async function deriveCodeChallenge(
  verifier: string,
  subtle: SubtleCrypto = crypto.subtle,
): Promise<string> {
  const data = new TextEncoder().encode(verifier);
  const digest = await subtle.digest('SHA-256', data);
  return base64UrlEncode(new Uint8Array(digest));
}
