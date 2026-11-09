// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;
using NCode.Identity.Secrets.RemoteKeys;
using Xunit;

namespace NCode.Identity.Secrets.AwsKms.Tests;

public class AwsKmsRemoteKeyDescriptorsTests
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

    #region Providers Tests

    [Fact]
    public void Providers_DescribesTheAwsKmsProvider()
    {
        var source = new AwsKmsRemoteKeyDescriptors();

        var descriptor = Assert.Single(source.Providers);

        Assert.Equal(RemoteKeyProviderNames.AwsKms, descriptor.Name);
    }

    #endregion

    #region Algorithms Tests

    [Fact]
    public void Algorithms_Rows_MatchTheMapping()
    {
        var source = new AwsKmsRemoteKeyDescriptors();

        var rows = source.Algorithms.OfType<RemoteSigningAlgorithm>().ToList();

        Assert.Equal(Expected.Length, rows.Count);
        foreach (var expected in Expected)
        {
            var row = Assert.Single(
                rows,
                x => x.Algorithm == expected.Algorithm && x.KeySizeBits == expected.KeySizeBits
            );
            Assert.Equal(RemoteKeyProviderNames.AwsKms, row.Provider);
            Assert.Equal("sig", row.Use);
            Assert.Equal(expected.SecretType, row.SecretType);
            Assert.Equal(expected.Curve, row.Curve);
            Assert.Equal(expected.KeySpec, row.KeySpec);
            Assert.Equal(expected.SigningAlgorithm, row.SigningAlgorithm);
            Assert.Equal(expected.Digest, row.DigestAlgorithm);
            Assert.Equal(expected.IsDer, row.IsDerEncodedSignature);
        }
    }

    [Fact]
    public void Algorithms_DecryptionRows_MatchTheMapping()
    {
        var source = new AwsKmsRemoteKeyDescriptors();

        var rows = source.Algorithms.OfType<RemoteDecryptionAlgorithm>().ToList();

        Assert.Equal(6, rows.Count);
        foreach (var keySizeBits in new[] { 2048, 3072, 4096 })
        {
            var oaep = Assert.Single(
                rows,
                x => x.Algorithm == "RSA-OAEP" && x.KeySizeBits == keySizeBits
            );
            Assert.Equal("RSAES_OAEP_SHA_1", oaep.EncryptionAlgorithm);

            var oaep256 = Assert.Single(
                rows,
                x => x.Algorithm == "RSA-OAEP-256" && x.KeySizeBits == keySizeBits
            );
            Assert.Equal("RSAES_OAEP_SHA_256", oaep256.EncryptionAlgorithm);
        }

        Assert.All(
            rows,
            row =>
            {
                Assert.Equal(RemoteKeyProviderNames.AwsKms, row.Provider);
                Assert.Equal("enc", row.Use);
                Assert.Equal(SecretKeyTypes.Rsa, row.SecretType);
                Assert.Null(row.Curve);
                Assert.Equal($"RSA_{row.KeySizeBits}", row.KeySpec);
            }
        );
    }

    [Fact]
    public void Algorithms_KeyAgreementRows_MatchTheMapping()
    {
        var source = new AwsKmsRemoteKeyDescriptors();

        var rows = source.Algorithms.OfType<RemoteKeyAgreementAlgorithm>().ToList();

        Assert.Equal(12, rows.Count);
        foreach (
            var algorithm in new[]
            {
                "ECDH-ES",
                "ECDH-ES+A128KW",
                "ECDH-ES+A192KW",
                "ECDH-ES+A256KW",
            }
        )
        {
            Assert.Single(rows, x => x.Algorithm == algorithm && x.Curve == "P-256");
            Assert.Single(rows, x => x.Algorithm == algorithm && x.Curve == "P-384");
            Assert.Single(rows, x => x.Algorithm == algorithm && x.Curve == "P-521");
        }

        var p521 = Assert.Single(rows, x => x.Algorithm == "ECDH-ES" && x.Curve == "P-521");
        Assert.Equal("ECC_NIST_P521", p521.KeySpec);
        Assert.Equal(521, p521.KeySizeBits);

        Assert.All(
            rows,
            row =>
            {
                Assert.Equal(RemoteKeyProviderNames.AwsKms, row.Provider);
                Assert.Equal("enc", row.Use);
                Assert.Equal(SecretKeyTypes.Ecc, row.SecretType);
                Assert.Equal("ECDH", row.KeyAgreementAlgorithm);
            }
        );
    }

    [Fact]
    public void Algorithms_Rows_CountsEveryKind()
    {
        var source = new AwsKmsRemoteKeyDescriptors();

        var rows = source.Algorithms.ToList();

        Assert.Equal(Expected.Length + 6 + 12, rows.Count);
    }

    [Fact]
    public void Algorithms_Rows_HaveUniqueSelectors()
    {
        var source = new AwsKmsRemoteKeyDescriptors();

        var selectors = source
            .Algorithms.Select(x => (x.Provider, x.Algorithm, x.SecretType, x.KeySizeBits, x.Curve))
            .ToList();

        Assert.Equal(selectors.Count, selectors.Distinct().Count());
    }

    [Fact]
    public void Algorithms_EveryRow_SignsADigest()
    {
        var source = new AwsKmsRemoteKeyDescriptors();

        Assert.All(
            source.Algorithms.OfType<RemoteSigningAlgorithm>(),
            row => Assert.NotNull(row.DigestAlgorithm)
        );
    }

    [Fact]
    public void Algorithms_ProvisionedDefaults_ArePresent()
    {
        var source = new AwsKmsRemoteKeyDescriptors();

        var rows = source.Algorithms.ToList();

        Assert.Contains(rows, x => x.Algorithm == "RS256" && x.KeySizeBits == 3072);
        Assert.Contains(rows, x => x.Algorithm == "PS256" && x.KeySizeBits == 3072);
        Assert.Contains(rows, x => x.Algorithm == "ES256" && x.Curve == "P-256");
    }

    #endregion
}
