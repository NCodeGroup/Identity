// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;
using NCode.Identity.Secrets.KeyTypes;
using NCode.Identity.Secrets.RemoteKeys;

namespace NCode.Identity.Secrets.AwsKms;

/// <summary>
/// Describes the AWS KMS remote key provider and its rows of the mapping from a JOSE algorithm to the AWS Key
/// Management Service key specification and the signing, encryption or key agreement algorithm that create a key and
/// operate with it.
/// </summary>
/// <remarks>
/// RSA keys of 2048, 3072 and 4096 bits sign with <c>RS256</c>, <c>RS384</c> and <c>RS512</c> (PKCS #1 v1.5) and with
/// <c>PS256</c>, <c>PS384</c> and <c>PS512</c> (PSS); ECC keys on <c>P-256</c>, <c>P-384</c> and <c>P-521</c> sign with
/// <c>ES256</c>, <c>ES384</c> and <c>ES512</c>, and the service returns an ECDSA signature as a DER <c>SEQUENCE</c>.
/// Every signing row has the service sign the digest of the signing input. RSA keys also decrypt a content key with
/// OAEP over SHA-1 (<c>RSA-OAEP</c>) or SHA-256 (<c>RSA-OAEP-256</c>), and ECC keys also agree a shared secret with
/// <c>DeriveSharedSecret</c> for <c>ECDH-ES</c> and <c>ECDH-ES+A128KW</c>, <c>ECDH-ES+A192KW</c> and
/// <c>ECDH-ES+A256KW</c>.
/// </remarks>
// See ADR-1471.
// See ADR-1477.
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

    private static (string Algorithm, string EncryptionAlgorithm)[] DecryptionAlgorithms { get; } =
    [("RSA-OAEP", "RSAES_OAEP_SHA_1"), ("RSA-OAEP-256", "RSAES_OAEP_SHA_256")];

    private static string[] KeyAgreementAlgorithmNames { get; } =
    ["ECDH-ES", "ECDH-ES+A128KW", "ECDH-ES+A192KW", "ECDH-ES+A256KW"];

    private static IReadOnlyList<RemoteKeyAlgorithm> Rows { get; } = CreateRows();

    /// <inheritdoc />
    public IEnumerable<RemoteKeyProviderDescriptor> Providers => ProviderDescriptors;

    /// <inheritdoc />
    public IEnumerable<RemoteKeyAlgorithm> Algorithms => Rows;

    private static List<RemoteKeyAlgorithm> CreateRows()
    {
        var rows = new List<RemoteKeyAlgorithm>();

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

        foreach (var (algorithm, encryptionAlgorithm) in DecryptionAlgorithms)
        {
            foreach (var keySizeBits in RsaKeySizesBits)
            {
                rows.Add(
                    new RemoteDecryptionAlgorithm
                    {
                        Provider = RemoteKeyProviderNames.AwsKms,
                        Algorithm = algorithm,
                        SecretType = SecretKeyTypes.Rsa,
                        KeySizeBits = keySizeBits,
                        KeySpec = $"RSA_{keySizeBits}",
                        EncryptionAlgorithm = encryptionAlgorithm,
                    }
                );
            }
        }

        foreach (var algorithm in KeyAgreementAlgorithmNames)
        {
            foreach (var (_, _, curve) in Hashes)
            {
                rows.Add(
                    new RemoteKeyAgreementAlgorithm
                    {
                        Provider = RemoteKeyProviderNames.AwsKms,
                        Algorithm = algorithm,
                        SecretType = SecretKeyTypes.Ecc,
                        KeySizeBits = curve.CurveSizeBits,
                        Curve = curve.CurveName,
                        KeySpec = $"ECC_NIST_P{curve.CurveSizeBits}",
                        KeyAgreementAlgorithm = "ECDH",
                    }
                );
            }
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
