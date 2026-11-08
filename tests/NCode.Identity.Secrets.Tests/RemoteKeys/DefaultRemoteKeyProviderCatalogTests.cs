// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;

namespace NCode.Identity.Secrets.RemoteKeys;

// See ADR-1471.
public class DefaultRemoteKeyProviderCatalogTests : BaseTests
{
    private IRemoteKeyProvider CreateProvider(string name)
    {
        var mock = CreateStrictMock<IRemoteKeyProvider>();
        mock.Setup(x => x.Name).Returns(name).Verifiable();
        return mock.Object;
    }

    private IRemoteSigningAlgorithmSource CreateSource(params RemoteSigningAlgorithm[] algorithms)
    {
        var mock = CreateStrictMock<IRemoteSigningAlgorithmSource>();
        mock.Setup(x => x.Algorithms).Returns(algorithms).Verifiable();
        return mock.Object;
    }

    private static RemoteSigningAlgorithm CreateRow(
        string provider = RemoteKeyProviderNames.AwsKms,
        string algorithm = "ES256",
        string secretType = "ecc",
        int keySizeBits = 256,
        string? curve = "P-256"
    ) =>
        new()
        {
            Provider = provider,
            Algorithm = algorithm,
            SecretType = secretType,
            KeySizeBits = keySizeBits,
            Curve = curve,
            KeySpec = "ECC_NIST_P256",
            SigningAlgorithm = "ECDSA_SHA_256",
            DigestAlgorithm = HashAlgorithmName.SHA256,
            IsDerEncodedSignature = true,
        };

    private static RemoteSigningAlgorithmSelector CreateSelector(
        string provider = RemoteKeyProviderNames.AwsKms,
        string algorithm = "ES256",
        string secretType = "ecc",
        int keySizeBits = 256,
        string? curve = "P-256"
    ) =>
        new()
        {
            Provider = provider,
            Algorithm = algorithm,
            SecretType = secretType,
            KeySizeBits = keySizeBits,
            Curve = curve,
        };

    #region Constructor Tests

    [Fact]
    public void Constructor_WhenProviderNameIsRegisteredTwice_Throws()
    {
        var providers = new[]
        {
            CreateProvider(RemoteKeyProviderNames.AwsKms),
            CreateProvider(RemoteKeyProviderNames.AwsKms),
        };

        Assert.Throws<InvalidOperationException>(() =>
            new DefaultRemoteKeyProviderCatalog(providers, [])
        );
    }

    [Theory]
    [InlineData("")]
    [InlineData("AWS-KMS")]
    [InlineData("aws_kms")]
    [InlineData("-aws")]
    [InlineData("aws-")]
    [InlineData("aws--kms")]
    public void Constructor_WhenProviderNameIsInvalid_Throws(string name)
    {
        var providers = new[] { CreateProvider(name) };

        Assert.Throws<InvalidOperationException>(() =>
            new DefaultRemoteKeyProviderCatalog(providers, [])
        );
    }

    [Fact]
    public void Constructor_WhenTwoRowsHaveTheSameSelector_Throws()
    {
        var sources = new[] { CreateSource(CreateRow()), CreateSource(CreateRow()) };

        Assert.Throws<InvalidOperationException>(() =>
            new DefaultRemoteKeyProviderCatalog([], sources)
        );
    }

    #endregion

    #region Providers Tests

    [Fact]
    public void Providers_ListsTheRegisteredProviders()
    {
        var aws = CreateProvider(RemoteKeyProviderNames.AwsKms);
        var azure = CreateProvider(RemoteKeyProviderNames.AzureKeyVault);

        var catalog = new DefaultRemoteKeyProviderCatalog([aws, azure], []);

        Assert.Equal([aws, azure], catalog.Providers);
    }

    #endregion

    #region TryGetProvider Tests

    [Fact]
    public void TryGetProvider_WhenRegistered_ReturnsTheProvider()
    {
        var aws = CreateProvider(RemoteKeyProviderNames.AwsKms);
        var azure = CreateProvider(RemoteKeyProviderNames.AzureKeyVault);
        var catalog = new DefaultRemoteKeyProviderCatalog([aws, azure], []);

        Assert.True(catalog.TryGetProvider(RemoteKeyProviderNames.AzureKeyVault, out var provider));
        Assert.Same(azure, provider);
    }

    [Fact]
    public void TryGetProvider_WhenNotRegistered_ReturnsFalse()
    {
        var catalog = new DefaultRemoteKeyProviderCatalog(
            [CreateProvider(RemoteKeyProviderNames.AwsKms)],
            []
        );

        Assert.False(
            catalog.TryGetProvider(RemoteKeyProviderNames.GoogleCloudKms, out var provider)
        );
        Assert.Null(provider);
    }

    [Fact]
    public void TryGetProvider_ComparesTheNameOrdinally()
    {
        var catalog = new DefaultRemoteKeyProviderCatalog(
            [CreateProvider(RemoteKeyProviderNames.AwsKms)],
            []
        );

        Assert.False(catalog.TryGetProvider("AWS-KMS", out _));
    }

    #endregion

    #region TryGetAlgorithm Tests

    [Fact]
    public void TryGetAlgorithm_WhenTheSelectorMatches_ReturnsTheRow()
    {
        var row = CreateRow();
        var catalog = new DefaultRemoteKeyProviderCatalog([], [CreateSource(row)]);

        Assert.True(catalog.TryGetAlgorithm(CreateSelector(), out var algorithm));
        Assert.Same(row, algorithm);
    }

    [Fact]
    public void TryGetAlgorithm_MergesTheRowsOfEverySource()
    {
        var es256 = CreateRow();
        var es384 = CreateRow(algorithm: "ES384", keySizeBits: 384, curve: "P-384");
        var catalog = new DefaultRemoteKeyProviderCatalog(
            [],
            [CreateSource(es256), CreateSource(es384)]
        );

        Assert.True(catalog.TryGetAlgorithm(CreateSelector(), out var first));
        Assert.Same(es256, first);
        Assert.True(
            catalog.TryGetAlgorithm(
                CreateSelector(algorithm: "ES384", keySizeBits: 384, curve: "P-384"),
                out var second
            )
        );
        Assert.Same(es384, second);
    }

    [Fact]
    public void TryGetAlgorithm_WhenTheProviderIsNotRegistered_KeepsTheRows()
    {
        var row = CreateRow(provider: "other-kms");
        var catalog = new DefaultRemoteKeyProviderCatalog([], [CreateSource(row)]);

        Assert.True(
            catalog.TryGetAlgorithm(CreateSelector(provider: "other-kms"), out var algorithm)
        );
        Assert.Same(row, algorithm);
    }

    [Theory]
    [InlineData("google-cloud-kms", "ES256", "ecc", 256, "P-256")]
    [InlineData("aws-kms", "ES384", "ecc", 256, "P-256")]
    [InlineData("aws-kms", "ES256", "rsa", 256, "P-256")]
    [InlineData("aws-kms", "ES256", "ecc", 384, "P-256")]
    [InlineData("aws-kms", "ES256", "ecc", 256, "P-384")]
    [InlineData("aws-kms", "ES256", "ecc", 256, null)]
    [InlineData("AWS-KMS", "ES256", "ecc", 256, "P-256")]
    public void TryGetAlgorithm_WhenAnySelectorFieldDiffers_ReturnsFalse(
        string provider,
        string algorithm,
        string secretType,
        int keySizeBits,
        string? curve
    )
    {
        var catalog = new DefaultRemoteKeyProviderCatalog([], [CreateSource(CreateRow())]);
        var selector = CreateSelector(provider, algorithm, secretType, keySizeBits, curve);

        Assert.False(catalog.TryGetAlgorithm(selector, out var row));
        Assert.Null(row);
    }

    [Fact]
    public void TryGetAlgorithm_WhenTheRowHasNoCurve_MatchesASelectorWithNoCurve()
    {
        var row = CreateRow(algorithm: "PS256", secretType: "rsa", keySizeBits: 2048, curve: null);
        var catalog = new DefaultRemoteKeyProviderCatalog([], [CreateSource(row)]);
        var selector = CreateSelector(
            algorithm: "PS256",
            secretType: "rsa",
            keySizeBits: 2048,
            curve: null
        );

        Assert.True(catalog.TryGetAlgorithm(selector, out var algorithm));
        Assert.Same(row, algorithm);
    }

    #endregion
}
