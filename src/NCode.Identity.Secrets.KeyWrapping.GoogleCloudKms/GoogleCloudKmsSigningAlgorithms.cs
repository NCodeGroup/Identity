// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;
using NCode.Identity.Secrets.KeyTypes;
using NCode.Identity.Secrets.RemoteKeys;

namespace NCode.Identity.Secrets.KeyWrapping.GoogleCloudKms;

/// <summary>
/// Provides the rows of the mapping from a JOSE signature algorithm to the Google Cloud Key Management Service
/// crypto key version algorithm that creates a key and signs for it.
/// </summary>
/// <remarks>
/// <para>
/// A Cloud KMS algorithm fixes the key size, the padding and the digest, so the key specification and the signing
/// algorithm of a row are the same name. RSA keys of 2048, 3072 and 4096 bits sign with <c>RS256</c> (PKCS #1 v1.5) and
/// with <c>PS256</c> (PSS) over SHA-256, and keys of 4096 bits also sign with <c>RS512</c> and <c>PS512</c> over
/// SHA-512. ECC keys on <c>P-256</c> and <c>P-384</c> sign with <c>ES256</c> and <c>ES384</c>, and the service returns
/// an ECDSA signature as a DER <c>SEQUENCE</c>.
/// </para>
/// <para>
/// Every row has the service sign the digest of the signing input.
/// </para>
/// </remarks>
// See ADR-1471.
internal sealed class GoogleCloudKmsSigningAlgorithms : IRemoteSigningAlgorithmSource
{
    private static readonly int[] RsaKeySizesBits = [2048, 3072, 4096];

    private static readonly IReadOnlyList<RemoteSigningAlgorithm> Rows = CreateRows();

    /// <inheritdoc />
    public IEnumerable<RemoteSigningAlgorithm> Algorithms => Rows;

    private static List<RemoteSigningAlgorithm> CreateRows()
    {
        var rows = new List<RemoteSigningAlgorithm>();

        foreach (var keySizeBits in RsaKeySizesBits)
        {
            rows.Add(
                CreateRsaRow(
                    "RS256",
                    keySizeBits,
                    $"RSA_SIGN_PKCS1_{keySizeBits}_SHA256",
                    HashAlgorithmName.SHA256
                )
            );
        }

        rows.Add(
            CreateRsaRow("RS512", 4096, "RSA_SIGN_PKCS1_4096_SHA512", HashAlgorithmName.SHA512)
        );

        rows.Add(CreateRsaRow("PS512", 4096, "RSA_SIGN_PSS_4096_SHA512", HashAlgorithmName.SHA512));

        foreach (var keySizeBits in RsaKeySizesBits)
        {
            rows.Add(
                CreateRsaRow(
                    "PS256",
                    keySizeBits,
                    $"RSA_SIGN_PSS_{keySizeBits}_SHA256",
                    HashAlgorithmName.SHA256
                )
            );
        }

        rows.Add(
            CreateEccRow(
                "ES256",
                EccCurveSpecification.P256,
                "EC_SIGN_P256_SHA256",
                HashAlgorithmName.SHA256
            )
        );

        rows.Add(
            CreateEccRow(
                "ES384",
                EccCurveSpecification.P384,
                "EC_SIGN_P384_SHA384",
                HashAlgorithmName.SHA384
            )
        );

        return rows;
    }

    private static RemoteSigningAlgorithm CreateRsaRow(
        string algorithm,
        int keySizeBits,
        string keySpec,
        HashAlgorithmName digest
    ) =>
        new()
        {
            Provider = RemoteKeyProviderNames.GoogleCloudKms,
            Algorithm = algorithm,
            SecretType = SecretKeyTypes.Rsa,
            KeySizeBits = keySizeBits,
            KeySpec = keySpec,
            SigningAlgorithm = keySpec,
            DigestAlgorithm = digest,
        };

    private static RemoteSigningAlgorithm CreateEccRow(
        string algorithm,
        EccCurveSpecification curve,
        string keySpec,
        HashAlgorithmName digest
    ) =>
        new()
        {
            Provider = RemoteKeyProviderNames.GoogleCloudKms,
            Algorithm = algorithm,
            SecretType = SecretKeyTypes.Ecc,
            KeySizeBits = curve.CurveSizeBits,
            Curve = curve.CurveName,
            KeySpec = keySpec,
            SigningAlgorithm = keySpec,
            DigestAlgorithm = digest,
            IsDerEncodedSignature = true,
        };
}
