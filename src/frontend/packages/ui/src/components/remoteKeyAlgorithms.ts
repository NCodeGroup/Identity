/**
 * One signing algorithm a remote key provider holds keys for, with the key's type, size and curve, discovered from
 * the server capabilities.
 */
export type RemoteKeyAlgorithmOption = {
  readonly algorithm: string;
  readonly secretType: string;
  readonly keySizeBits: number;
  readonly curve?: string | null;
};

/** A remote key provider, by name, with the signing algorithms its key management service holds keys for. */
export type RemoteKeyProviderOption = {
  readonly name: string;
  readonly algorithms: readonly RemoteKeyAlgorithmOption[];
};

/** The value a select uses for an algorithm row, which tells apart the rows that share a JOSE algorithm. */
export const remoteKeyAlgorithmValue = (row: RemoteKeyAlgorithmOption): string =>
  [row.algorithm, row.secretType, row.keySizeBits, row.curve ?? ''].join('|');

/** A display label for an algorithm row, such as `ES256 · ECC P-256` or `RS256 · RSA 2048 bits`. */
export const remoteKeyAlgorithmLabel = (row: RemoteKeyAlgorithmOption): string => {
  const type = row.secretType.toUpperCase();
  return row.curve
    ? `${row.algorithm} · ${type} ${row.curve}`
    : `${row.algorithm} · ${type} ${row.keySizeBits} bits`;
};

/** The providers that list at least one algorithm, which are the ones a key can be created or bound in. */
export const offeredRemoteKeyProviders = (
  providers: readonly RemoteKeyProviderOption[],
): readonly RemoteKeyProviderOption[] =>
  providers.filter((provider) => provider.algorithms.length > 0);
