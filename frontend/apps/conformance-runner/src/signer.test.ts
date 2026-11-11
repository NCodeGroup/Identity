import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import type * as NodeCrypto from 'node:crypto';
import {
  generateKeyPairSync,
  createPrivateKey,
  createPublicKey,
  verify,
  constants,
  type KeyObject,
} from 'node:crypto';
import {
  createClientAssertionSigner,
  createDpopProofFactory,
  isSupportedAlg,
  loadSigningKey,
  type JwaSignatureAlg,
  type LoadSigningKeyOptions,
} from './signer.js';

// Pass-through spies: every test signs with real Node crypto; the platform-guard tests below inject a single
// out-of-contract key/JWK to prove the signer fails loudly instead of emitting a malformed key or JWS.
vi.mock('node:crypto', async (importOriginal) => {
  const actual = await importOriginal<typeof NodeCrypto>();
  return {
    ...actual,
    createPrivateKey: vi.fn(actual.createPrivateKey),
    createPublicKey: vi.fn(actual.createPublicKey),
  };
});

function pem(key: KeyObject): string {
  return key.export({ format: 'pem', type: 'pkcs8' }).toString();
}

function decodeSegment(segment: string): Record<string, unknown> {
  return JSON.parse(Buffer.from(segment, 'base64url').toString('utf8')) as Record<string, unknown>;
}

/** Verifies a compact JWS against the signing public key, applying the alg's digest and padding. */
function verifyJws(jws: string, publicKey: KeyObject, alg: JwaSignatureAlg): boolean {
  const [header, payload, signature] = jws.split('.');
  const data = Buffer.from(`${header}.${payload}`, 'ascii');
  const sig = Buffer.from(signature ?? '', 'base64url');
  if (alg.startsWith('ES')) {
    const digest = alg === 'ES256' ? 'sha256' : alg === 'ES384' ? 'sha384' : 'sha512';
    return verify(digest, data, { key: publicKey, dsaEncoding: 'ieee-p1363' }, sig);
  }
  if (alg.startsWith('PS')) {
    const digest = alg === 'PS256' ? 'sha256' : alg === 'PS384' ? 'sha384' : 'sha512';
    return verify(
      digest,
      data,
      {
        key: publicKey,
        padding: constants.RSA_PKCS1_PSS_PADDING,
        saltLength: constants.RSA_PSS_SALTLEN_DIGEST,
      },
      sig,
    );
  }
  if (alg.startsWith('RS')) {
    const digest = alg === 'RS256' ? 'sha256' : alg === 'RS384' ? 'sha384' : 'sha512';
    return verify(digest, data, publicKey, sig);
  }
  return verify(null, data, publicKey, sig);
}

const ecPair = generateKeyPairSync('ec', { namedCurve: 'prime256v1' });
const rsaPair = generateKeyPairSync('rsa', { modulusLength: 2048 });
const edPair = generateKeyPairSync('ed25519');

const ecPairsByAlg: Record<
  'ES256' | 'ES384' | 'ES512',
  { privateKey: KeyObject; publicKey: KeyObject }
> = {
  ES256: ecPair,
  ES384: generateKeyPairSync('ec', { namedCurve: 'secp384r1' }),
  ES512: generateKeyPairSync('ec', { namedCurve: 'secp521r1' }),
};

describe('loadSigningKey — algorithm resolution', () => {
  it.each(['ES256', 'ES384', 'ES512'] as const)('infers %s from the matching EC curve', (alg) => {
    expect(loadSigningKey(pem(ecPairsByAlg[alg].privateKey)).alg).toBe(alg);
  });

  it('accepts an override that matches the EC curve', () => {
    expect(loadSigningKey(pem(ecPair.privateKey), { alg: 'ES256' }).alg).toBe('ES256');
  });

  it('rejects an EC key on an unsupported curve', () => {
    const k1 = generateKeyPairSync('ec', { namedCurve: 'secp256k1' });
    expect(() => loadSigningKey(pem(k1.privateKey))).toThrow("Unsupported EC curve 'secp256k1'.");
  });

  it('explains a contradicting EC override', () => {
    expect(() => loadSigningKey(pem(ecPair.privateKey), { alg: 'ES384' })).toThrow(
      "Algorithm 'ES384' does not match the EC curve 'prime256v1' (ES256).",
    );
  });

  it('explains a non-RSA override for an RSA key', () => {
    expect(() => loadSigningKey(pem(rsaPair.privateKey), { alg: 'EdDSA' })).toThrow(
      "Algorithm 'EdDSA' is not an RSA algorithm.",
    );
  });

  it('infers EdDSA from an Ed448 key', () => {
    expect(loadSigningKey(pem(generateKeyPairSync('ed448').privateKey)).alg).toBe('EdDSA');
  });

  it('accepts an explicit EdDSA override for an Edwards-curve key', () => {
    expect(loadSigningKey(pem(edPair.privateKey), { alg: 'EdDSA' }).alg).toBe('EdDSA');
  });

  it('rejects a non-EdDSA override for an Edwards-curve key', () => {
    expect(() => loadSigningKey(pem(edPair.privateKey), { alg: 'ES256' })).toThrow(
      "Algorithm 'ES256' does not match an Edwards-curve key (EdDSA or Ed25519).",
    );
  });

  it('accepts the fully-specified Ed25519 override for an Ed25519 key', () => {
    expect(loadSigningKey(pem(edPair.privateKey), { alg: 'Ed25519' }).alg).toBe('Ed25519');
  });

  it('accepts the fully-specified Ed448 override for an Ed448 key', () => {
    const ed448 = generateKeyPairSync('ed448').privateKey;
    expect(loadSigningKey(pem(ed448), { alg: 'Ed448' }).alg).toBe('Ed448');
  });

  it('rejects a fully-specified override for the other Edwards curve', () => {
    expect(() => loadSigningKey(pem(edPair.privateKey), { alg: 'Ed448' })).toThrow(
      "Algorithm 'Ed448' does not match an Edwards-curve key (EdDSA or Ed25519).",
    );
  });

  it('signs an Ed25519 client assertion that verifies under the alg it names', async () => {
    const key = loadSigningKey(pem(edPair.privateKey), { alg: 'Ed25519' });
    const jws = await createClientAssertionSigner(key)({ iss: 'client', sub: 'client' });
    expect(decodeSegment(jws.split('.')[0] ?? '')).toMatchObject({ alg: 'Ed25519' });
    expect(verifyJws(jws, edPair.publicKey, 'Ed25519')).toBe(true);
  });

  it.each<[string, LoadSigningKeyOptions]>([
    ['inferred', {}],
    ['a PS256 override', { alg: 'PS256' }],
  ])('rejects an RSA-PSS-restricted key with an actionable error (%s)', (_label, options) => {
    const rsaPss = generateKeyPairSync('rsa-pss', {
      modulusLength: 2048,
      hashAlgorithm: 'sha256',
      mgf1HashAlgorithm: 'sha256',
    });
    expect(rsaPss.privateKey.asymmetricKeyType).toBe('rsa-pss');
    expect(() => loadSigningKey(pem(rsaPss.privateKey), options)).toThrow(
      "RSA-PSS-restricted keys (id-RSASSA-PSS) cannot be published as a JWK; supply an ordinary RSA (rsaEncryption) key — 'PS256'/'PS384'/'PS512' already sign it with PSS padding.",
    );
  });

  it('rejects a key type that cannot sign', () => {
    const x25519 = generateKeyPairSync('x25519');
    expect(() => loadSigningKey(pem(x25519.privateKey))).toThrow("Unsupported key type 'x25519'.");
  });

  it('rejects material that is not a private key', () => {
    expect(() => loadSigningKey('not a key')).toThrow();
  });

  it('rejects malformed JWK JSON', () => {
    expect(() => loadSigningKey('{ "kty": ')).toThrow(SyntaxError);
  });
});

describe('loadSigningKey — key material', () => {
  it('loads a JWK private key without a kid', () => {
    const jwk = ecPair.privateKey.export({ format: 'jwk' });
    const key = loadSigningKey(`  ${JSON.stringify(jwk)}\n`);
    expect(key.alg).toBe('ES256');
    expect(key).not.toHaveProperty('kid');
  });

  it('ignores a non-string JWK kid', () => {
    const jwk = ecPair.privateKey.export({ format: 'jwk' });
    expect(loadSigningKey(JSON.stringify({ ...jwk, kid: 7 }))).not.toHaveProperty('kid');
  });

  it('projects an RSA key to its public modulus and exponent only', () => {
    const key = loadSigningKey(pem(rsaPair.privateKey));
    const expected = rsaPair.publicKey.export({ format: 'jwk' });
    expect(key.publicJwk).toEqual({ kty: 'RSA', n: expected.n, e: expected.e });
  });

  it('projects an Edwards-curve key to its public OKP members only', () => {
    const key = loadSigningKey(pem(edPair.privateKey));
    const expected = edPair.publicKey.export({ format: 'jwk' });
    expect(key.publicJwk).toEqual({ kty: 'OKP', crv: 'Ed25519', x: expected.x });
  });
});

describe('loadSigningKey', () => {
  it('infers ES256 from a P-256 EC key', () => {
    expect(loadSigningKey(pem(ecPair.privateKey)).alg).toBe('ES256');
  });

  it('defaults an RSA key to PS256', () => {
    expect(loadSigningKey(pem(rsaPair.privateKey)).alg).toBe('PS256');
  });

  it('honors an RS256 override for an RSA key', () => {
    expect(loadSigningKey(pem(rsaPair.privateKey), { alg: 'RS256' }).alg).toBe('RS256');
  });

  it('infers EdDSA from an Ed25519 key', () => {
    expect(loadSigningKey(pem(edPair.privateKey)).alg).toBe('EdDSA');
  });

  it('rejects an override that contradicts the EC curve', () => {
    expect(() => loadSigningKey(pem(ecPair.privateKey), { alg: 'ES384' })).toThrow();
  });

  it('rejects a non-RSA override for an RSA key', () => {
    expect(() => loadSigningKey(pem(rsaPair.privateKey), { alg: 'ES256' })).toThrow();
  });

  it('loads a JWK private key and carries its kid', () => {
    const jwk = ecPair.privateKey.export({ format: 'jwk' });
    const key = loadSigningKey(JSON.stringify({ ...jwk, kid: 'key-1' }));
    expect(key.alg).toBe('ES256');
    expect(key.kid).toBe('key-1');
  });

  it('exposes only public members in the JWK', () => {
    const key = loadSigningKey(pem(ecPair.privateKey));
    expect(key.publicJwk).toEqual({
      kty: 'EC',
      crv: expect.any(String),
      x: expect.any(String),
      y: expect.any(String),
    });
    expect('d' in key.publicJwk).toBe(false);
  });
});

describe('createClientAssertionSigner', () => {
  it('signs a verifiable client assertion with the key kid', async () => {
    const jwk = ecPair.privateKey.export({ format: 'jwk' });
    const key = loadSigningKey(JSON.stringify({ ...jwk, kid: 'key-1' }));
    const sign = createClientAssertionSigner(key);
    const jws = await sign({ iss: 'client', aud: 'https://op.test/token' });
    const header = decodeSegment(jws.split('.')[0] ?? '');
    expect(header).toMatchObject({ alg: 'ES256', typ: 'JWT', kid: 'key-1' });
    expect(decodeSegment(jws.split('.')[1] ?? '')).toMatchObject({ iss: 'client' });
    expect(verifyJws(jws, ecPair.publicKey, 'ES256')).toBe(true);
  });

  it('signs a verifiable RS256 assertion', async () => {
    const key = loadSigningKey(pem(rsaPair.privateKey), { alg: 'RS256' });
    const jws = await createClientAssertionSigner(key)({ iss: 'client' });
    expect(verifyJws(jws, rsaPair.publicKey, 'RS256')).toBe(true);
  });

  it.each(['PS256', 'PS384', 'PS512', 'RS256', 'RS384', 'RS512'] as const)(
    'signs a verifiable %s assertion with an RSA key',
    async (alg) => {
      const key = loadSigningKey(pem(rsaPair.privateKey), { alg });
      const jws = await createClientAssertionSigner(key)({ iss: 'client' });
      expect(decodeSegment(jws.split('.')[0] ?? '')).toEqual({ alg, typ: 'JWT' });
      expect(verifyJws(jws, rsaPair.publicKey, alg)).toBe(true);
    },
  );

  it.each(['ES256', 'ES384', 'ES512'] as const)(
    'signs a verifiable %s assertion with the matching EC key',
    async (alg) => {
      const pair = ecPairsByAlg[alg];
      const jws = await createClientAssertionSigner(loadSigningKey(pem(pair.privateKey)))({
        iss: 'client',
      });
      expect(decodeSegment(jws.split('.')[0] ?? '')).toMatchObject({ alg });
      expect(verifyJws(jws, pair.publicKey, alg)).toBe(true);
    },
  );

  it('signs a verifiable EdDSA assertion', async () => {
    const jws = await createClientAssertionSigner(loadSigningKey(pem(edPair.privateKey)))({
      iss: 'client',
    });
    expect(decodeSegment(jws.split('.')[0] ?? '')).toMatchObject({ alg: 'EdDSA' });
    expect(verifyJws(jws, edPair.publicKey, 'EdDSA')).toBe(true);
  });

  it('does not verify against a different key', async () => {
    const other = generateKeyPairSync('ec', { namedCurve: 'prime256v1' });
    const jws = await createClientAssertionSigner(loadSigningKey(pem(ecPair.privateKey)))({});
    expect(verifyJws(jws, other.publicKey, 'ES256')).toBe(false);
  });
});

describe('createDpopProofFactory', () => {
  it('embeds the public JWK and binds the method and URI', async () => {
    const key = loadSigningKey(pem(ecPair.privateKey));
    const proof = await createDpopProofFactory(key)('POST', 'https://op.test/token');
    const header = decodeSegment(proof.split('.')[0] ?? '');
    const payload = decodeSegment(proof.split('.')[1] ?? '');
    expect(header).toMatchObject({ typ: 'dpop+jwt', alg: 'ES256' });
    expect(header.jwk).toEqual(key.publicJwk);
    expect(payload).toMatchObject({ htm: 'POST', htu: 'https://op.test/token' });
    expect(typeof payload.jti).toBe('string');
    expect(typeof payload.iat).toBe('number');
    expect(verifyJws(proof, createPublicKey(ecPair.privateKey), 'ES256')).toBe(true);
  });

  it('mints a fresh jti per proof', async () => {
    const factory = createDpopProofFactory(loadSigningKey(pem(ecPair.privateKey)));
    const first = decodeSegment(
      (await factory('POST', 'https://op.test/token')).split('.')[1] ?? '',
    );
    const second = decodeSegment(
      (await factory('POST', 'https://op.test/token')).split('.')[1] ?? '',
    );
    expect(first.jti).not.toBe(second.jti);
  });

  describe('with a fixed clock', () => {
    beforeEach(() => {
      vi.useFakeTimers();
      vi.setSystemTime(new Date('2026-01-01T00:00:00.900Z'));
    });

    afterEach(() => {
      vi.useRealTimers();
    });

    it('stamps iat with the current time in whole seconds', async () => {
      const factory = createDpopProofFactory(loadSigningKey(pem(edPair.privateKey)));
      const proof = await factory('GET', 'https://op.test/userinfo');
      const payload = decodeSegment(proof.split('.')[1] ?? '');
      expect(payload.iat).toBe(Date.UTC(2026, 0, 1) / 1000);
      expect(verifyJws(proof, edPair.publicKey, 'EdDSA')).toBe(true);
    });
  });
});

describe('loadSigningKey — platform guards', () => {
  /** A stand-in for a KeyObject the platform reports outside the documented contract. */
  function fakePrivateKey(
    shape: Partial<Pick<KeyObject, 'asymmetricKeyType' | 'asymmetricKeyDetails'>>,
  ): KeyObject {
    return shape as KeyObject;
  }

  /** A stand-in public key whose JWK export yields the given members. */
  function fakePublicKey(jwk: Record<string, string>): KeyObject {
    return { export: () => jwk } as unknown as KeyObject;
  }

  it('rejects an EC key whose curve the platform does not report', () => {
    vi.mocked(createPrivateKey).mockReturnValueOnce(
      fakePrivateKey({ asymmetricKeyType: 'ec', asymmetricKeyDetails: {} }),
    );
    expect(() => loadSigningKey('pem')).toThrow("Unsupported EC curve ''.");
  });

  it('rejects a key whose type the platform does not report', () => {
    vi.mocked(createPrivateKey).mockReturnValueOnce(fakePrivateKey({}));
    expect(() => loadSigningKey('pem')).toThrow("Unsupported key type 'unknown'.");
  });

  it('rejects a public JWK of an unsupported key type', () => {
    vi.mocked(createPublicKey).mockReturnValueOnce(fakePublicKey({ kty: 'oct' }));
    expect(() => loadSigningKey(pem(ecPair.privateKey))).toThrow("Unsupported key type 'oct'.");
  });

  it('rejects a public JWK without a key type', () => {
    vi.mocked(createPublicKey).mockReturnValueOnce(fakePublicKey({}));
    expect(() => loadSigningKey(pem(ecPair.privateKey))).toThrow("Unsupported key type 'unknown'.");
  });

  it.each<[string, Record<string, string>]>([
    ['y', { kty: 'EC', crv: 'P-256', x: 'x' }],
    ['e', { kty: 'RSA', n: 'n' }],
    ['x', { kty: 'OKP', crv: 'Ed25519' }],
  ])("rejects a public JWK missing the '%s' member", (member, jwk) => {
    vi.mocked(createPublicKey).mockReturnValueOnce(fakePublicKey(jwk));
    expect(() => loadSigningKey(pem(ecPair.privateKey))).toThrow(
      `Public JWK is missing the '${member}' member.`,
    );
  });
});

describe('isSupportedAlg', () => {
  it('accepts a supported algorithm', () => {
    expect(isSupportedAlg('ES256')).toBe(true);
  });

  it('rejects an unsupported algorithm', () => {
    expect(isSupportedAlg('HS256')).toBe(false);
  });
});
