/**
 * Node-`crypto`-backed signer (UI-free, unit-tested) for the probe runner's asymmetric legs. Loads a PEM- or
 * JWK-encoded private key, infers (or accepts) its JWA signature algorithm, and produces the two compact JWS artifacts
 * the confidential probes need: a `private_key_jwt` client assertion (OpenID Connect Core §9) and a DPoP proof
 * (RFC 9449 §4, embedding the public JWK in the header). Every function is pure given the key material so the runner
 * stays deterministic and the signing is unit-tested with freshly generated keys — no network, no ambient key store.
 */

import {
  constants,
  createPrivateKey,
  createPublicKey,
  randomUUID,
  sign,
  type JsonWebKey,
  type KeyObject,
} from 'node:crypto';
import type { ClientAssertionSigner, DpopProofFactory } from './probeRunner.js';

/** The JWA signature algorithms the signer supports, mirroring the modern asymmetric set OPs advertise. */
export const SUPPORTED_SIGNATURE_ALGS = [
  'ES256',
  'ES384',
  'ES512',
  'PS256',
  'PS384',
  'PS512',
  'RS256',
  'RS384',
  'RS512',
  'EdDSA',
] as const;

/** A JWA signature algorithm value (`alg` header) the signer can produce. */
export type JwaSignatureAlg = (typeof SUPPORTED_SIGNATURE_ALGS)[number];

/** A loaded private key together with its resolved signature algorithm and public JWK (for the DPoP header). */
export type SigningKey = {
  readonly alg: JwaSignatureAlg;
  readonly privateKey: KeyObject;
  readonly publicJwk: JsonWebKey;
  readonly kid?: string;
};

/** Options accepted when loading a signing key. */
export type LoadSigningKeyOptions = {
  /** Overrides the inferred algorithm. Required to pick an RSA scheme (`RS*`) other than the `PS256` default. */
  readonly alg?: JwaSignatureAlg;
};

const EC_ALG_BY_CURVE: Readonly<Record<string, JwaSignatureAlg>> = {
  prime256v1: 'ES256',
  secp384r1: 'ES384',
  secp521r1: 'ES512',
};

const RSA_ALGS: ReadonlySet<JwaSignatureAlg> = new Set([
  'PS256',
  'PS384',
  'PS512',
  'RS256',
  'RS384',
  'RS512',
]);

/** Narrows an arbitrary string to a supported {@link JwaSignatureAlg}. */
export function isSupportedAlg(value: string): value is JwaSignatureAlg {
  return (SUPPORTED_SIGNATURE_ALGS as readonly string[]).includes(value);
}

/**
 * Loads a PEM- or JWK-encoded private key, resolving its signature algorithm. The algorithm is inferred from the key
 * (the curve for EC keys, `EdDSA` for Ed25519/Ed448, `PS256` for RSA) and can be overridden via {@link
 * LoadSigningKeyOptions.alg}; an override that contradicts the key material (wrong family or EC curve) is rejected.
 */
export function loadSigningKey(material: string, options: LoadSigningKeyOptions = {}): SigningKey {
  const { privateKey, kid } = parsePrivateKey(material);
  const alg = resolveAlg(privateKey, options.alg);
  const publicJwk = toPublicJwk(privateKey);
  return { alg, privateKey, publicJwk, ...(kid !== undefined ? { kid } : {}) };
}

/**
 * Builds a {@link ClientAssertionSigner} that signs the `private_key_jwt` client-assertion claims into a compact JWS
 * (header `alg`/`typ: JWT`, and `kid` when the loaded JWK carried one). OpenID Connect Core §9.
 */
export function createClientAssertionSigner(key: SigningKey): ClientAssertionSigner {
  return (claims) => {
    const header = { alg: key.alg, typ: 'JWT', ...(key.kid !== undefined ? { kid: key.kid } : {}) };
    return Promise.resolve(compactJws(key, header, claims));
  };
}

/**
 * Builds a {@link DpopProofFactory} that signs a DPoP proof bound to the given HTTP method + URI, embedding the public
 * JWK in the `dpop+jwt` header and a fresh `jti`/`iat` in the payload. RFC 9449 §4.
 */
export function createDpopProofFactory(key: SigningKey): DpopProofFactory {
  return (htm, htu) => {
    const header = { typ: 'dpop+jwt', alg: key.alg, jwk: key.publicJwk };
    const payload = { jti: randomUUID(), htm, htu, iat: Math.floor(Date.now() / 1000) };
    return Promise.resolve(compactJws(key, header, payload));
  };
}

function parsePrivateKey(material: string): { privateKey: KeyObject; kid?: string } {
  const trimmed = material.trim();
  if (trimmed.startsWith('{')) {
    const jwk = JSON.parse(trimmed) as JsonWebKey & { kid?: unknown };
    const privateKey = createPrivateKey({ key: jwk, format: 'jwk' });
    return { privateKey, ...(typeof jwk.kid === 'string' ? { kid: jwk.kid } : {}) };
  }
  return { privateKey: createPrivateKey(material) };
}

function resolveAlg(privateKey: KeyObject, override?: JwaSignatureAlg): JwaSignatureAlg {
  const type = privateKey.asymmetricKeyType;
  if (type === 'ec') {
    const curve = privateKey.asymmetricKeyDetails?.namedCurve ?? '';
    const inferred = EC_ALG_BY_CURVE[curve];
    if (inferred === undefined) {
      throw new Error(`Unsupported EC curve '${curve}'.`);
    }
    if (override !== undefined && override !== inferred) {
      throw new Error(
        `Algorithm '${override}' does not match the EC curve '${curve}' (${inferred}).`,
      );
    }
    return inferred;
  }
  if (type === 'rsa' || type === 'rsa-pss') {
    if (override !== undefined) {
      if (!RSA_ALGS.has(override)) {
        throw new Error(`Algorithm '${override}' is not an RSA algorithm.`);
      }
      return override;
    }
    return 'PS256';
  }
  if (type === 'ed25519' || type === 'ed448') {
    if (override !== undefined && override !== 'EdDSA') {
      throw new Error(`Algorithm '${override}' does not match an Edwards-curve key (EdDSA).`);
    }
    return 'EdDSA';
  }
  throw new Error(`Unsupported key type '${type ?? 'unknown'}'.`);
}

function toPublicJwk(privateKey: KeyObject): JsonWebKey {
  const jwk = createPublicKey(privateKey).export({ format: 'jwk' }) as JsonWebKey;
  if (jwk.kty === 'EC') {
    return {
      kty: 'EC',
      crv: required(jwk.crv, 'crv'),
      x: required(jwk.x, 'x'),
      y: required(jwk.y, 'y'),
    };
  }
  if (jwk.kty === 'RSA') {
    return { kty: 'RSA', n: required(jwk.n, 'n'), e: required(jwk.e, 'e') };
  }
  if (jwk.kty === 'OKP') {
    return { kty: 'OKP', crv: required(jwk.crv, 'crv'), x: required(jwk.x, 'x') };
  }
  throw new Error(`Unsupported key type '${jwk.kty ?? 'unknown'}'.`);
}

function required(value: string | undefined, member: string): string {
  if (value === undefined) {
    throw new Error(`Public JWK is missing the '${member}' member.`);
  }
  return value;
}

function compactJws(
  key: SigningKey,
  header: Readonly<Record<string, unknown>>,
  payload: Readonly<Record<string, unknown>>,
): string {
  const signingInput = `${base64url(JSON.stringify(header))}.${base64url(JSON.stringify(payload))}`;
  const signature = signData(key, Buffer.from(signingInput, 'ascii'));
  return `${signingInput}.${signature.toString('base64url')}`;
}

function signData(key: SigningKey, data: Buffer): Buffer {
  switch (key.alg) {
    case 'ES256':
      return sign('sha256', data, { key: key.privateKey, dsaEncoding: 'ieee-p1363' });
    case 'ES384':
      return sign('sha384', data, { key: key.privateKey, dsaEncoding: 'ieee-p1363' });
    case 'ES512':
      return sign('sha512', data, { key: key.privateKey, dsaEncoding: 'ieee-p1363' });
    case 'PS256':
      return sign('sha256', data, pssOptions(key.privateKey));
    case 'PS384':
      return sign('sha384', data, pssOptions(key.privateKey));
    case 'PS512':
      return sign('sha512', data, pssOptions(key.privateKey));
    case 'RS256':
      return sign('sha256', data, key.privateKey);
    case 'RS384':
      return sign('sha384', data, key.privateKey);
    case 'RS512':
      return sign('sha512', data, key.privateKey);
    case 'EdDSA':
      return sign(null, data, key.privateKey);
  }
}

function pssOptions(privateKey: KeyObject) {
  return {
    key: privateKey,
    padding: constants.RSA_PKCS1_PSS_PADDING,
    saltLength: constants.RSA_PSS_SALTLEN_DIGEST,
  };
}

function base64url(value: string): string {
  return Buffer.from(value, 'utf8').toString('base64url');
}
