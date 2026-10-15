import { describe, expect, it } from 'vitest';
import {
  generateKeyPairSync,
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
} from './signer.js';

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
});

describe('isSupportedAlg', () => {
  it('accepts a supported algorithm', () => {
    expect(isSupportedAlg('ES256')).toBe(true);
  });

  it('rejects an unsupported algorithm', () => {
    expect(isSupportedAlg('HS256')).toBe(false);
  });
});
