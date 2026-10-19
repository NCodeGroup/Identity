import { describe, expect, it } from 'vitest';
import { decodeJwtClaims, getSubject } from './jwt.js';
import { base64UrlEncode } from './pkce.js';
import { AuthError } from './flow.js';

function fakeJwt(claims: Record<string, unknown>): string {
  const header = base64UrlEncode(new TextEncoder().encode(JSON.stringify({ alg: 'none' })));
  const payload = base64UrlEncode(new TextEncoder().encode(JSON.stringify(claims)));
  return `${header}.${payload}.sig`;
}

describe('decodeJwtClaims', () => {
  it('decodes the payload claims', () => {
    const token = fakeJwt({ sub: 'user-123', scope: 'openid read:clients' });
    expect(decodeJwtClaims(token)).toEqual({ sub: 'user-123', scope: 'openid read:clients' });
  });

  it('decodes non-ASCII claim values (UTF-8)', () => {
    const token = fakeJwt({ name: 'Zoë' });
    expect(decodeJwtClaims(token).name).toBe('Zoë');
  });

  it('throws on a malformed token', () => {
    expect(() => decodeJwtClaims('not-a-jwt')).toThrow(AuthError);
  });

  it('throws when the payload segment is empty', () => {
    expect(() => decodeJwtClaims('header..sig')).toThrow(
      /Malformed JWT: expected at least a header and payload/,
    );
  });

  it('throws when the payload is not valid JSON', () => {
    const payload = base64UrlEncode(new TextEncoder().encode('not-json'));
    expect(() => decodeJwtClaims(`header.${payload}.sig`)).toThrow(
      /Malformed JWT: payload is not valid JSON/,
    );
  });
});

describe('getSubject', () => {
  it('returns the sub claim', () => {
    expect(getSubject(fakeJwt({ sub: 'user-123' }))).toBe('user-123');
  });

  it('returns undefined when sub is absent', () => {
    expect(getSubject(fakeJwt({ name: 'x' }))).toBeUndefined();
  });

  it('returns undefined when sub is not a string', () => {
    expect(getSubject(fakeJwt({ sub: 42 }))).toBeUndefined();
  });
});
