// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;
using NCode.Identity.Secrets.AzureKeyVault.Options;
using NCode.Identity.Secrets.RemoteKeys;
using Xunit;

namespace NCode.Identity.Secrets.AzureKeyVault.Tests;

public class AzureKeyVaultRemoteKeyDescriptorsTests
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
        HashAlgorithmName Digest
    )[] Expected { get; } =
    [
        ("RS256", "rsa", 2048, null, "RSA", Sha256),
        ("RS256", "rsa", 3072, null, "RSA", Sha256),
        ("RS256", "rsa", 4096, null, "RSA", Sha256),
        ("RS384", "rsa", 2048, null, "RSA", Sha384),
        ("RS384", "rsa", 3072, null, "RSA", Sha384),
        ("RS384", "rsa", 4096, null, "RSA", Sha384),
        ("RS512", "rsa", 2048, null, "RSA", Sha512),
        ("RS512", "rsa", 3072, null, "RSA", Sha512),
        ("RS512", "rsa", 4096, null, "RSA", Sha512),
        ("PS256", "rsa", 2048, null, "RSA", Sha256),
        ("PS256", "rsa", 3072, null, "RSA", Sha256),
        ("PS256", "rsa", 4096, null, "RSA", Sha256),
        ("PS384", "rsa", 2048, null, "RSA", Sha384),
        ("PS384", "rsa", 3072, null, "RSA", Sha384),
        ("PS384", "rsa", 4096, null, "RSA", Sha384),
        ("PS512", "rsa", 2048, null, "RSA", Sha512),
        ("PS512", "rsa", 3072, null, "RSA", Sha512),
        ("PS512", "rsa", 4096, null, "RSA", Sha512),
        ("ES256", "ecc", 256, "P-256", "EC", Sha256),
        ("ES384", "ecc", 384, "P-384", "EC", Sha384),
        ("ES512", "ecc", 521, "P-521", "EC", Sha512),
    ];

    private static AzureKeyVaultRemoteKeyDescriptors CreateSource(
        string? vaultUri = "https://contoso.vault.azure.net"
    ) =>
        new(
            Microsoft.Extensions.Options.Options.Create(
                new AzureKeyVaultRemoteKeyProviderOptions { VaultUri = vaultUri }
            )
        );

    #region Providers Tests

    [Fact]
    public void Providers_DescribesTheAzureKeyVaultProvider()
    {
        var source = CreateSource();

        var descriptor = Assert.Single(source.Providers);

        Assert.Equal(RemoteKeyProviderNames.AzureKeyVault, descriptor.Name);
    }

    [Fact]
    public void Providers_WithVaultUri_CreatesKeys()
    {
        var source = CreateSource("https://contoso.vault.azure.net");

        var descriptor = Assert.Single(source.Providers);

        Assert.True(descriptor.CreatesKeys);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Providers_WithoutVaultUri_CreatesNoKeys(string? vaultUri)
    {
        var source = CreateSource(vaultUri);

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
            Assert.Equal(RemoteKeyProviderNames.AzureKeyVault, row.Provider);
            Assert.Equal("sig", row.Use);
            Assert.Equal(expected.SecretType, row.SecretType);
            Assert.Equal(expected.Curve, row.Curve);
            Assert.Equal(expected.KeySpec, row.KeySpec);
            Assert.Equal(expected.Algorithm, row.SigningAlgorithm);
            Assert.Equal(expected.Digest, row.DigestAlgorithm);
            Assert.False(row.IsDerEncodedSignature);
        }
    }

    [Fact]
    public void Algorithms_DecryptionRows_MatchTheMapping()
    {
        var source = CreateSource();

        var rows = source.Algorithms.OfType<RemoteDecryptionAlgorithm>().ToList();

        Assert.Equal(6, rows.Count);
        foreach (var algorithm in new[] { "RSA-OAEP", "RSA-OAEP-256" })
        {
            foreach (var keySizeBits in new[] { 2048, 3072, 4096 })
            {
                var row = Assert.Single(
                    rows,
                    x => x.Algorithm == algorithm && x.KeySizeBits == keySizeBits
                );
                Assert.Equal(algorithm, row.EncryptionAlgorithm);
            }
        }

        Assert.All(
            rows,
            row =>
            {
                Assert.Equal(RemoteKeyProviderNames.AzureKeyVault, row.Provider);
                Assert.Equal("enc", row.Use);
                Assert.Equal(SecretKeyTypes.Rsa, row.SecretType);
                Assert.Null(row.Curve);
                Assert.Equal("RSA", row.KeySpec);
            }
        );
    }

    [Fact]
    public void Algorithms_KeyAgreementRows_AreAbsent()
    {
        var source = CreateSource();

        Assert.DoesNotContain(source.Algorithms, x => x is RemoteKeyAgreementAlgorithm);
    }

    [Fact]
    public void Algorithms_Rows_CountsEveryKind()
    {
        var source = CreateSource();

        var rows = source.Algorithms.ToList();

        Assert.Equal(Expected.Length + 6, rows.Count);
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
