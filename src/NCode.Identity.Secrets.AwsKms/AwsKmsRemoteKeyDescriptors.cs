// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;
using NCode.Identity.Secrets.KeyTypes;
using NCode.Identity.Secrets.RemoteKeys;

namespace NCode.Identity.Secrets.AwsKms;

/// <summary>
/// Describes the AWS KMS remote key provider and its rows of the mapping from a JOSE signature algorithm to the
/// AWS Key Management Service key specification and signing algorithm that create a key and sign for it.
/// </summary>
/// <remarks>
/// RSA keys of 2048, 3072 and 4096 bits sign with <c>RS256</c>, <c>RS384</c> and <c>RS512</c> (PKCS #1 v1.5) and with
/// <c>PS256</c>, <c>PS384</c> and <c>PS512</c> (PSS); ECC keys on <c>P-256</c>, <c>P-384</c> and <c>P-521</c> sign with
/// <c>ES256</c>, <c>ES384</c> and <c>ES512</c>, and the service returns an ECDSA signature as a DER <c>SEQUENCE</c>.
/// Every row has the service sign the digest of the signing input.
/// </remarks>
// See ADR-1471.
internal sealed class AwsKmsRemoteKeyDescriptors : IRemoteKeyDescriptorSource
{
    private static IReadOnlyList<RemoteKeyProviderDescriptor> ProviderDescriptors { get; } =
    [new() { Name = RemoteKeyProviderNames.AwsKms }];

    private static int[] RsaKeySizesBits { get; } = [2048, 3072, 4096];

    private static (
        int HashSizeBits,
        HashAlgorithmName Digest,
        EccCurveSpecification Curve
    )[] Hashes { get; } =
    [
        (256, HashAlgorithmName.SHA256, EccCurveSpecification.P256),
        (384, HashAlgorithmName.SHA384, EccCurveSpecification.P384),
        (512, HashAlgorithmName.SHA512, EccCurveSpecification.P521),
    ];

    private static IReadOnlyList<RemoteSigningAlgorithm> Rows { get; } = CreateRows();

    /// <inheritdoc />
    public IEnumerable<RemoteKeyProviderDescriptor> Providers => ProviderDescriptors;

    /// <inheritdoc />
    public IEnumerable<RemoteSigningAlgorithm> Algorithms => Rows;

    private static List<RemoteSigningAlgorithm> CreateRows()
    {
        var rows = new List<RemoteSigningAlgorithm>();

        foreach (var (hashSizeBits, digest, _) in Hashes)
        {
            foreach (var keySizeBits in RsaKeySizesBits)
            {
                rows.Add(
                    CreateRsaRow(
                        $"RS{hashSizeBits}",
                        keySizeBits,
                        $"RSASSA_PKCS1_V1_5_SHA_{hashSizeBits}",
                        digest
                    )
                );
            }
        }

        foreach (var (hashSizeBits, digest, _) in Hashes)
        {
            foreach (var keySizeBits in RsaKeySizesBits)
            {
                rows.Add(
                    CreateRsaRow(
                        $"PS{hashSizeBits}",
                        keySizeBits,
                        $"RSASSA_PSS_SHA_{hashSizeBits}",
                        digest
                    )
                );
            }
        }

        foreach (var (hashSizeBits, digest, curve) in Hashes)
        {
            rows.Add(
                new RemoteSigningAlgorithm
                {
                    Provider = RemoteKeyProviderNames.AwsKms,
                    Algorithm = $"ES{hashSizeBits}",
                    SecretType = SecretKeyTypes.Ecc,
                    KeySizeBits = curve.CurveSizeBits,
                    Curve = curve.CurveName,
                    KeySpec = $"ECC_NIST_P{curve.CurveSizeBits}",
                    SigningAlgorithm = $"ECDSA_SHA_{hashSizeBits}",
                    DigestAlgorithm = digest,
                    IsDerEncodedSignature = true,
                }
            );
        }

        return rows;
    }

    private static RemoteSigningAlgorithm CreateRsaRow(
        string algorithm,
        int keySizeBits,
        string signingAlgorithm,
        HashAlgorithmName digest
    ) =>
        new()
        {
            Provider = RemoteKeyProviderNames.AwsKms,
            Algorithm = algorithm,
            SecretType = SecretKeyTypes.Rsa,
            KeySizeBits = keySizeBits,
            KeySpec = $"RSA_{keySizeBits}",
            SigningAlgorithm = signingAlgorithm,
            DigestAlgorithm = digest,
        };
}
