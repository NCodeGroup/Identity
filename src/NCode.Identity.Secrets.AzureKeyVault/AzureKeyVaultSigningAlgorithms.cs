// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;
using NCode.Identity.Secrets.KeyTypes;
using NCode.Identity.Secrets.RemoteKeys;

namespace NCode.Identity.Secrets.AzureKeyVault;

/// <summary>
/// Provides the rows of the mapping from a JOSE signature algorithm to the Azure Key Vault key type and signature
/// algorithm that create a key and sign for it.
/// </summary>
/// <remarks>
/// RSA keys of 2048, 3072 and 4096 bits sign with <c>RS256</c>, <c>RS384</c> and <c>RS512</c> (PKCS #1 v1.5) and with
/// <c>PS256</c>, <c>PS384</c> and <c>PS512</c> (PSS); ECC keys on <c>P-256</c>, <c>P-384</c> and <c>P-521</c> sign with
/// <c>ES256</c>, <c>ES384</c> and <c>ES512</c>. Key Vault names its signature algorithms as the JOSE algorithms, and
/// returns an ECDSA signature as the IEEE P1363 <c>r‖s</c> concatenation, so no row has a DER signature. Every row has
/// the service sign the digest of the signing input.
/// </remarks>
// See ADR-1471.
internal sealed class AzureKeyVaultSigningAlgorithms : IRemoteSigningAlgorithmSource
{
    private static readonly int[] RsaKeySizesBits = [2048, 3072, 4096];

    private static readonly (
        int HashSizeBits,
        HashAlgorithmName Digest,
        EccCurveSpecification Curve
    )[] Hashes =
    [
        (256, HashAlgorithmName.SHA256, EccCurveSpecification.P256),
        (384, HashAlgorithmName.SHA384, EccCurveSpecification.P384),
        (512, HashAlgorithmName.SHA512, EccCurveSpecification.P521),
    ];

    private static readonly IReadOnlyList<RemoteSigningAlgorithm> Rows = CreateRows();

    /// <inheritdoc />
    public IEnumerable<RemoteSigningAlgorithm> Algorithms => Rows;

    private static List<RemoteSigningAlgorithm> CreateRows()
    {
        var rows = new List<RemoteSigningAlgorithm>();

        foreach (var prefix in new[] { "RS", "PS" })
        {
            foreach (var (hashSizeBits, digest, _) in Hashes)
            {
                foreach (var keySizeBits in RsaKeySizesBits)
                {
                    var algorithm = $"{prefix}{hashSizeBits}";
                    rows.Add(
                        new RemoteSigningAlgorithm
                        {
                            Provider = RemoteKeyProviderNames.AzureKeyVault,
                            Algorithm = algorithm,
                            SecretType = SecretKeyTypes.Rsa,
                            KeySizeBits = keySizeBits,
                            KeySpec = "RSA",
                            SigningAlgorithm = algorithm,
                            DigestAlgorithm = digest,
                        }
                    );
                }
            }
        }

        foreach (var (hashSizeBits, digest, curve) in Hashes)
        {
            var algorithm = $"ES{hashSizeBits}";
            rows.Add(
                new RemoteSigningAlgorithm
                {
                    Provider = RemoteKeyProviderNames.AzureKeyVault,
                    Algorithm = algorithm,
                    SecretType = SecretKeyTypes.Ecc,
                    KeySizeBits = curve.CurveSizeBits,
                    Curve = curve.CurveName,
                    KeySpec = "EC",
                    SigningAlgorithm = algorithm,
                    DigestAlgorithm = digest,
                }
            );
        }

        return rows;
    }
}
