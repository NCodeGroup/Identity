import { describe, expect, it } from 'vitest';
import {
  offeredRemoteKeyProviders,
  remoteKeyAlgorithmLabel,
  remoteKeyAlgorithmValue,
  type RemoteKeyProviderOption,
} from './remoteKeyAlgorithms.js';

describe('remoteKeyAlgorithmValue', () => {
  it('joins the algorithm, type, size and curve of a curve row', () => {
    expect(
      remoteKeyAlgorithmValue({
        algorithm: 'ES256',
        use: 'sig',
        secretType: 'ecc',
        keySizeBits: 256,
        curve: 'P-256',
      }),
    ).toBe('ES256|ecc|256|P-256');
  });

  it('leaves the curve empty for a size-only row with a null curve', () => {
    expect(
      remoteKeyAlgorithmValue({
        algorithm: 'RS256',
        use: 'sig',
        secretType: 'rsa',
        keySizeBits: 2048,
        curve: null,
      }),
    ).toBe('RS256|rsa|2048|');
  });

  it('leaves the curve empty for a size-only row without a curve', () => {
    expect(
      remoteKeyAlgorithmValue({
        algorithm: 'RS256',
        use: 'sig',
        secretType: 'rsa',
        keySizeBits: 2048,
      }),
    ).toBe('RS256|rsa|2048|');
  });
});

describe('remoteKeyAlgorithmLabel', () => {
  it('names the curve of a curve row', () => {
    expect(
      remoteKeyAlgorithmLabel({
        algorithm: 'ES256',
        use: 'sig',
        secretType: 'ecc',
        keySizeBits: 256,
        curve: 'P-256',
      }),
    ).toBe('ES256 · ECC P-256 · signing');
  });

  it('names the size of a size-only row', () => {
    expect(
      remoteKeyAlgorithmLabel({
        algorithm: 'RS256',
        use: 'sig',
        secretType: 'rsa',
        keySizeBits: 2048,
        curve: null,
      }),
    ).toBe('RS256 · RSA 2048 bits · signing');
  });

  it('names an encryption row by its use', () => {
    expect(
      remoteKeyAlgorithmLabel({
        algorithm: 'RSA-OAEP-256',
        use: 'enc',
        secretType: 'rsa',
        keySizeBits: 3072,
      }),
    ).toBe('RSA-OAEP-256 · RSA 3072 bits · encryption');
  });

  it('names an unknown use by the use itself', () => {
    expect(
      remoteKeyAlgorithmLabel({
        algorithm: 'RS256',
        use: 'custom',
        secretType: 'rsa',
        keySizeBits: 2048,
      }),
    ).toBe('RS256 · RSA 2048 bits · custom');
  });
});

describe('offeredRemoteKeyProviders', () => {
  it('keeps the providers that list an algorithm, in order', () => {
    const awsKms: RemoteKeyProviderOption = {
      name: 'aws-kms',
      algorithms: [
        { algorithm: 'ES256', use: 'sig', secretType: 'ecc', keySizeBits: 256, curve: 'P-256' },
      ],
    };
    const empty: RemoteKeyProviderOption = { name: 'empty', algorithms: [] };
    const gcpKms: RemoteKeyProviderOption = {
      name: 'gcp-kms',
      algorithms: [{ algorithm: 'PS256', use: 'sig', secretType: 'rsa', keySizeBits: 3072 }],
    };

    expect(offeredRemoteKeyProviders([awsKms, empty, gcpKms])).toEqual([awsKms, gcpKms]);
  });

  it('gives an empty list for no providers', () => {
    expect(offeredRemoteKeyProviders([])).toEqual([]);
  });
});
