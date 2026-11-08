// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;
using NCode.Identity.Secrets.RemoteKeys;
using Xunit;

namespace NCode.Identity.Secrets.AzureKeyVault.Tests;

public class AzureKeyVaultSigningAlgorithmsTests
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
        HashAlgorithmName Digest
    )[] Expected =
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

    #region Algorithms Tests

    [Fact]
    public void Algorithms_Rows_MatchTheMapping()
    {
        var source = new AzureKeyVaultSigningAlgorithms();

        var rows = source.Algorithms.ToList();

        Assert.Equal(Expected.Length, rows.Count);
        foreach (var expected in Expected)
        {
            var row = Assert.Single(
                rows,
                x => x.Algorithm == expected.Algorithm && x.KeySizeBits == expected.KeySizeBits
            );
            Assert.Equal(RemoteKeyProviderNames.AzureKeyVault, row.Provider);
            Assert.Equal(expected.SecretType, row.SecretType);
            Assert.Equal(expected.Curve, row.Curve);
            Assert.Equal(expected.KeySpec, row.KeySpec);
            Assert.Equal(expected.Algorithm, row.SigningAlgorithm);
            Assert.Equal(expected.Digest, row.DigestAlgorithm);
            Assert.False(row.IsDerEncodedSignature);
        }
    }

    [Fact]
    public void Algorithms_Rows_HaveUniqueSelectors()
    {
        var source = new AzureKeyVaultSigningAlgorithms();

        var selectors = source
            .Algorithms.Select(x => (x.Provider, x.Algorithm, x.SecretType, x.KeySizeBits, x.Curve))
            .ToList();

        Assert.Equal(selectors.Count, selectors.Distinct().Count());
    }

    [Fact]
    public void Algorithms_EveryRow_SignsADigest()
    {
        var source = new AzureKeyVaultSigningAlgorithms();

        Assert.All(source.Algorithms, row => Assert.NotNull(row.DigestAlgorithm));
    }

    [Fact]
    public void Algorithms_ProvisionedDefaults_ArePresent()
    {
        var source = new AzureKeyVaultSigningAlgorithms();

        var rows = source.Algorithms.ToList();

        Assert.Contains(rows, x => x.Algorithm == "RS256" && x.KeySizeBits == 3072);
        Assert.Contains(rows, x => x.Algorithm == "PS256" && x.KeySizeBits == 3072);
        Assert.Contains(rows, x => x.Algorithm == "ES256" && x.Curve == "P-256");
    }

    #endregion
}
