/**
 * One signature or key-management algorithm a remote key provider holds keys for, with the key's use, type, size and
 * curve, discovered from the server capabilities.
 */
export type RemoteKeyAlgorithmOption = {
  readonly algorithm: string;
  readonly use: string;
  readonly secretType: string;
  readonly keySizeBits: number;
  readonly curve?: string | null;
};

/**
 * A remote key provider, by name, with the signature or key-management algorithms its key management service holds
 * keys for.
 */
export type RemoteKeyProviderOption = {
  readonly name: string;
  readonly algorithms: readonly RemoteKeyAlgorithmOption[];
};

/** The value a select uses for an algorithm row, which tells apart the rows that share a JOSE algorithm. */
export const remoteKeyAlgorithmValue = (row: RemoteKeyAlgorithmOption): string =>
  [row.algorithm, row.secretType, row.keySizeBits, row.curve ?? ''].join('|');

/** The label of a key use: `signing` for `sig`, `encryption` for `enc` and the use itself for any other value. */
const keyUseLabel = (use: string): string => {
  switch (use) {
    case 'sig':
      return 'signing';
    case 'enc':
      return 'encryption';
    default:
      return use;
  }
};

/**
 * A display label for an algorithm row, such as `ES256 · ECC P-256 · signing` or
 * `RSA-OAEP-256 · RSA 3072 bits · encryption`.
 */
export const remoteKeyAlgorithmLabel = (row: RemoteKeyAlgorithmOption): string => {
  const type = row.secretType.toUpperCase();
  const key = row.curve ? `${type} ${row.curve}` : `${type} ${row.keySizeBits} bits`;
  return `${row.algorithm} · ${key} · ${keyUseLabel(row.use)}`;
};

/** The providers that list at least one algorithm, which are the ones a key can be created or bound in. */
export const offeredRemoteKeyProviders = (
  providers: readonly RemoteKeyProviderOption[],
): readonly RemoteKeyProviderOption[] =>
  providers.filter((provider) => provider.algorithms.length > 0);
