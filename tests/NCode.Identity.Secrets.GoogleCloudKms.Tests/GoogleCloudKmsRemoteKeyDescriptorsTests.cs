// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;
using NCode.Identity.Secrets.GoogleCloudKms.Options;
using NCode.Identity.Secrets.RemoteKeys;
using Xunit;

namespace NCode.Identity.Secrets.GoogleCloudKms.Tests;

public class GoogleCloudKmsRemoteKeyDescriptorsTests
{
    private static HashAlgorithmName Sha256 { get; } = HashAlgorithmName.SHA256;
    private static HashAlgorithmName Sha384 { get; } = HashAlgorithmName.SHA384;
    private static HashAlgorithmName Sha512 { get; } = HashAlgorithmName.SHA512;

    private static (
        string Algorithm,
        string SecretType,
        int KeySizeBits,
        string? Curve,
        string KeySpec,
        HashAlgorithmName Digest,
        bool IsDer
    )[] Expected { get; } =
    [
        ("RS256", "rsa", 2048, null, "RSA_SIGN_PKCS1_2048_SHA256", Sha256, false),
        ("RS256", "rsa", 3072, null, "RSA_SIGN_PKCS1_3072_SHA256", Sha256, false),
        ("RS256", "rsa", 4096, null, "RSA_SIGN_PKCS1_4096_SHA256", Sha256, false),
        ("RS512", "rsa", 4096, null, "RSA_SIGN_PKCS1_4096_SHA512", Sha512, false),
        ("PS256", "rsa", 2048, null, "RSA_SIGN_PSS_2048_SHA256", Sha256, false),
        ("PS256", "rsa", 3072, null, "RSA_SIGN_PSS_3072_SHA256", Sha256, false),
        ("PS256", "rsa", 4096, null, "RSA_SIGN_PSS_4096_SHA256", Sha256, false),
        ("PS512", "rsa", 4096, null, "RSA_SIGN_PSS_4096_SHA512", Sha512, false),
        ("ES256", "ecc", 256, "P-256", "EC_SIGN_P256_SHA256", Sha256, true),
        ("ES384", "ecc", 384, "P-384", "EC_SIGN_P384_SHA384", Sha384, true),
    ];

    private static (
        string Algorithm,
        int KeySizeBits,
        string KeySpec
    )[] ExpectedDecryption { get; } =
    [
        ("RSA-OAEP", 2048, "RSA_DECRYPT_OAEP_2048_SHA1"),
        ("RSA-OAEP", 3072, "RSA_DECRYPT_OAEP_3072_SHA1"),
        ("RSA-OAEP", 4096, "RSA_DECRYPT_OAEP_4096_SHA1"),
        ("RSA-OAEP-256", 2048, "RSA_DECRYPT_OAEP_2048_SHA256"),
        ("RSA-OAEP-256", 3072, "RSA_DECRYPT_OAEP_3072_SHA256"),
        ("RSA-OAEP-256", 4096, "RSA_DECRYPT_OAEP_4096_SHA256"),
        ("RSA-OAEP-512", 4096, "RSA_DECRYPT_OAEP_4096_SHA512"),
    ];

    private static GoogleCloudKmsRemoteKeyDescriptors CreateSource(
        string? keyRing = "projects/p/locations/l/keyRings/r"
    ) =>
        new(
            Microsoft.Extensions.Options.Options.Create(
                new GoogleCloudKmsRemoteKeyProviderOptions { KeyRing = keyRing }
            )
        );

    #region Providers Tests

    [Fact]
    public void Providers_DescribesTheGoogleCloudKmsProvider()
    {
        var source = CreateSource();

        var descriptor = Assert.Single(source.Providers);

        Assert.Equal(RemoteKeyProviderNames.GoogleCloudKms, descriptor.Name);
    }

    [Fact]
    public void Providers_WithKeyRing_CreatesKeys()
    {
        var source = CreateSource("projects/p/locations/l/keyRings/r");

        var descriptor = Assert.Single(source.Providers);

        Assert.True(descriptor.CreatesKeys);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Providers_WithoutKeyRing_CreatesNoKeys(string? keyRing)
    {
        var source = CreateSource(keyRing);

        var descriptor = Assert.Single(source.Providers);

        Assert.False(descriptor.CreatesKeys);
    }

    #endregion

    #region Algorithms Tests

    [Fact]
    public void Algorithms_Rows_MatchTheMapping()
    {
        var source = CreateSource();

        var rows = source.Algorithms.OfType<RemoteSigningAlgorithm>().ToList();

        Assert.Equal(Expected.Length, rows.Count);
        foreach (var expected in Expected)
        {
            var row = Assert.Single(
                rows,
                x => x.Algorithm == expected.Algorithm && x.KeySizeBits == expected.KeySizeBits
            );
            Assert.Equal(RemoteKeyProviderNames.GoogleCloudKms, row.Provider);
            Assert.Equal("sig", row.Use);
            Assert.Equal(expected.SecretType, row.SecretType);
            Assert.Equal(expected.Curve, row.Curve);
            Assert.Equal(expected.KeySpec, row.KeySpec);
            Assert.Equal(expected.KeySpec, row.SigningAlgorithm);
            Assert.Equal(expected.Digest, row.DigestAlgorithm);
            Assert.Equal(expected.IsDer, row.IsDerEncodedSignature);
        }
    }

    [Fact]
    public void Algorithms_DecryptionRows_MatchTheMapping()
    {
        var source = CreateSource();

        var rows = source.Algorithms.OfType<RemoteDecryptionAlgorithm>().ToList();

        Assert.Equal(ExpectedDecryption.Length, rows.Count);
        foreach (var expected in ExpectedDecryption)
        {
            var row = Assert.Single(
                rows,
                x => x.Algorithm == expected.Algorithm && x.KeySizeBits == expected.KeySizeBits
            );
            Assert.Equal(RemoteKeyProviderNames.GoogleCloudKms, row.Provider);
            Assert.Equal("enc", row.Use);
            Assert.Equal(SecretKeyTypes.Rsa, row.SecretType);
            Assert.Null(row.Curve);
            Assert.Equal(expected.KeySpec, row.KeySpec);
            Assert.Equal(expected.KeySpec, row.EncryptionAlgorithm);
        }
    }

    [Fact]
    public void Algorithms_KeyAgreementRows_AreAbsent()
    {
        var source = CreateSource();

        Assert.DoesNotContain(source.Algorithms, x => x is RemoteKeyAgreementAlgorithm);
    }

    [Fact]
    public void Algorithms_Rows_HaveUniqueSelectors()
    {
        var source = CreateSource();

        var selectors = source
            .Algorithms.Select(x => (x.Provider, x.Algorithm, x.SecretType, x.KeySizeBits, x.Curve))
            .ToList();

        Assert.Equal(selectors.Count, selectors.Distinct().Count());
    }

    [Fact]
    public void Algorithms_EveryRow_SignsADigest()
    {
        var source = CreateSource();

        Assert.All(
            source.Algorithms.OfType<RemoteSigningAlgorithm>(),
            row => Assert.NotNull(row.DigestAlgorithm)
        );
    }

    [Fact]
    public void Algorithms_EveryRow_KeySpecMapsToAnEnumMember()
    {
        var source = CreateSource();

        Assert.All(
            source.Algorithms,
            row =>
            {
                Assert.True(
                    GoogleCloudKmsRemoteKeyProvider.TryGetAlgorithm(row.KeySpec, out var algorithm)
                );
                Assert.NotEqual(
                    Google
                        .Cloud
                        .Kms
                        .V1
                        .CryptoKeyVersion
                        .Types
                        .CryptoKeyVersionAlgorithm
                        .Unspecified,
                    algorithm
                );
            }
        );
    }

    [Fact]
    public void Algorithms_ProvisionedDefaults_ArePresent()
    {
        var source = CreateSource();

        var rows = source.Algorithms.ToList();

        Assert.Contains(rows, x => x.Algorithm == "RS256" && x.KeySizeBits == 3072);
        Assert.Contains(rows, x => x.Algorithm == "PS256" && x.KeySizeBits == 3072);
        Assert.Contains(rows, x => x.Algorithm == "ES256" && x.Curve == "P-256");
    }

    #endregion
}
