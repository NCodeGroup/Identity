// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;
using NCode.Identity.Secrets.RemoteKeys;
using Xunit;

namespace NCode.Identity.Secrets.KeyWrapping.AwsKms.Tests;

public class AwsKmsSigningAlgorithmsTests
{
    private static readonly HashAlgorithmName Sha256 = HashAlgorithmName.SHA256;
    private static readonly HashAlgorithmName Sha384 = HashAlgorithmName.SHA384;
    private static readonly HashAlgorithmName Sha512 = HashAlgorithmName.SHA512;

    private static readonly (
        string Algorithm,
        string SecretType,
        int KeySizeBits,
        string? Curve,
        string KeySpec,
        string SigningAlgorithm,
        HashAlgorithmName Digest,
        bool IsDer
    )[] Expected =
    [
        ("RS256", "rsa", 2048, null, "RSA_2048", "RSASSA_PKCS1_V1_5_SHA_256", Sha256, false),
        ("RS256", "rsa", 3072, null, "RSA_3072", "RSASSA_PKCS1_V1_5_SHA_256", Sha256, false),
        ("RS256", "rsa", 4096, null, "RSA_4096", "RSASSA_PKCS1_V1_5_SHA_256", Sha256, false),
        ("RS384", "rsa", 2048, null, "RSA_2048", "RSASSA_PKCS1_V1_5_SHA_384", Sha384, false),
        ("RS384", "rsa", 3072, null, "RSA_3072", "RSASSA_PKCS1_V1_5_SHA_384", Sha384, false),
        ("RS384", "rsa", 4096, null, "RSA_4096", "RSASSA_PKCS1_V1_5_SHA_384", Sha384, false),
        ("RS512", "rsa", 2048, null, "RSA_2048", "RSASSA_PKCS1_V1_5_SHA_512", Sha512, false),
        ("RS512", "rsa", 3072, null, "RSA_3072", "RSASSA_PKCS1_V1_5_SHA_512", Sha512, false),
        ("RS512", "rsa", 4096, null, "RSA_4096", "RSASSA_PKCS1_V1_5_SHA_512", Sha512, false),
        ("PS256", "rsa", 2048, null, "RSA_2048", "RSASSA_PSS_SHA_256", Sha256, false),
        ("PS256", "rsa", 3072, null, "RSA_3072", "RSASSA_PSS_SHA_256", Sha256, false),
        ("PS256", "rsa", 4096, null, "RSA_4096", "RSASSA_PSS_SHA_256", Sha256, false),
        ("PS384", "rsa", 2048, null, "RSA_2048", "RSASSA_PSS_SHA_384", Sha384, false),
        ("PS384", "rsa", 3072, null, "RSA_3072", "RSASSA_PSS_SHA_384", Sha384, false),
        ("PS384", "rsa", 4096, null, "RSA_4096", "RSASSA_PSS_SHA_384", Sha384, false),
        ("PS512", "rsa", 2048, null, "RSA_2048", "RSASSA_PSS_SHA_512", Sha512, false),
        ("PS512", "rsa", 3072, null, "RSA_3072", "RSASSA_PSS_SHA_512", Sha512, false),
        ("PS512", "rsa", 4096, null, "RSA_4096", "RSASSA_PSS_SHA_512", Sha512, false),
        ("ES256", "ecc", 256, "P-256", "ECC_NIST_P256", "ECDSA_SHA_256", Sha256, true),
        ("ES384", "ecc", 384, "P-384", "ECC_NIST_P384", "ECDSA_SHA_384", Sha384, true),
        ("ES512", "ecc", 521, "P-521", "ECC_NIST_P521", "ECDSA_SHA_512", Sha512, true),
    ];

    #region Algorithms Tests

    [Fact]
    public void Algorithms_Rows_MatchTheMapping()
    {
        var source = new AwsKmsSigningAlgorithms();

        var rows = source.Algorithms.ToList();

        Assert.Equal(Expected.Length, rows.Count);
        foreach (var expected in Expected)
        {
            var row = Assert.Single(
                rows,
                x => x.Algorithm == expected.Algorithm && x.KeySizeBits == expected.KeySizeBits
            );
            Assert.Equal(RemoteKeyProviderNames.AwsKms, row.Provider);
            Assert.Equal(expected.SecretType, row.SecretType);
            Assert.Equal(expected.Curve, row.Curve);
            Assert.Equal(expected.KeySpec, row.KeySpec);
            Assert.Equal(expected.SigningAlgorithm, row.SigningAlgorithm);
            Assert.Equal(expected.Digest, row.DigestAlgorithm);
            Assert.Equal(expected.IsDer, row.IsDerEncodedSignature);
        }
    }

    [Fact]
    public void Algorithms_Rows_HaveUniqueSelectors()
    {
        var source = new AwsKmsSigningAlgorithms();

        var selectors = source
            .Algorithms.Select(x => (x.Provider, x.Algorithm, x.SecretType, x.KeySizeBits, x.Curve))
            .ToList();

        Assert.Equal(selectors.Count, selectors.Distinct().Count());
    }

    [Fact]
    public void Algorithms_EveryRow_SignsADigest()
    {
        var source = new AwsKmsSigningAlgorithms();

        Assert.All(source.Algorithms, row => Assert.NotNull(row.DigestAlgorithm));
    }

    [Fact]
    public void Algorithms_ProvisionedDefaults_ArePresent()
    {
        var source = new AwsKmsSigningAlgorithms();

        var rows = source.Algorithms.ToList();

        Assert.Contains(rows, x => x.Algorithm == "RS256" && x.KeySizeBits == 3072);
        Assert.Contains(rows, x => x.Algorithm == "PS256" && x.KeySizeBits == 3072);
        Assert.Contains(rows, x => x.Algorithm == "ES256" && x.Curve == "P-256");
    }

    #endregion
}
