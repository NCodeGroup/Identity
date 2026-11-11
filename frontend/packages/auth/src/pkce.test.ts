import { describe, expect, it } from 'vitest';
import {
  base64UrlEncode,
  deriveCodeChallenge,
  generateCodeVerifier,
  randomUrlSafe,
  type RandomBytes,
} from './pkce.js';

describe('base64UrlEncode', () => {
  it('encodes without padding and with url-safe alphabet', () => {
    // 0xFB 0xFF -> base64 "+/8=" -> url-safe no-pad "-_8"
    expect(base64UrlEncode(new Uint8Array([0xfb, 0xff]))).toBe('-_8');
  });
});

describe('randomUrlSafe', () => {
  it('maps bytes deterministically into the unreserved set', () => {
    const rng: RandomBytes = (n) => new Uint8Array(n).fill(0);
    // byte 0 -> index 0 -> 'A'
    expect(randomUrlSafe(4, rng)).toBe('AAAA');
  });

  it('produces the requested length', () => {
    const rng: RandomBytes = (n) => new Uint8Array([...Array(n).keys()]);
    expect(randomUrlSafe(10, rng)).toHaveLength(10);
  });
});

describe('generateCodeVerifier', () => {
  it('generates a 64-char verifier', () => {
    expect(generateCodeVerifier()).toHaveLength(64);
  });
});

describe('deriveCodeChallenge', () => {
  it('matches the RFC 7636 Appendix B S256 test vector', async () => {
    const verifier = 'dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk';
    const challenge = await deriveCodeChallenge(verifier);
    expect(challenge).toBe('E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM');
  });
});
