// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;

namespace NCode.Identity.Secrets.RemoteKeys;

// See ADR-1474.
public class DefaultRemoteKeyDescriptorCatalogTests : BaseTests
{
    private IRemoteKeyDescriptorSource CreateSource(
        RemoteKeyProviderDescriptor[] providers,
        params RemoteSigningAlgorithm[] rows
    ) => CreateSource(Times.Once(), providers, rows);

    // A catalog that fails on a repeated selector may fail before it reads the providers, so a test of that failure allows the read at most once.
    private IRemoteKeyDescriptorSource CreateSource(
        Times providersRead,
        RemoteKeyProviderDescriptor[] providers,
        RemoteSigningAlgorithm[] rows
    )
    {
        var mock = CreateStrictMock<IRemoteKeyDescriptorSource>();
        mock.Setup(x => x.Providers).Returns(providers).Verifiable(providersRead);
        mock.Setup(x => x.Algorithms).Returns(rows).Verifiable();
        return mock.Object;
    }

    private static RemoteKeyProviderDescriptor CreateDescriptor(string name) =>
        new() { Name = name };

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

    [Theory]
    [InlineData("")]
    [InlineData("AWS-KMS")]
    [InlineData("aws_kms")]
    [InlineData("-aws")]
    [InlineData("aws-")]
    [InlineData("aws--kms")]
    public void Constructor_WhenProviderNameIsInvalid_Throws(string name)
    {
        var sources = new[] { CreateSource([CreateDescriptor(name)]) };

        Assert.Throws<InvalidOperationException>(() =>
            new DefaultRemoteKeyDescriptorCatalog(sources)
        );
    }

    [Fact]
    public void Constructor_WhenProviderIsDescribedTwice_Throws()
    {
        var sources = new[]
        {
            CreateSource([CreateDescriptor(RemoteKeyProviderNames.AwsKms)]),
            CreateSource([CreateDescriptor(RemoteKeyProviderNames.AwsKms)]),
        };

        Assert.Throws<InvalidOperationException>(() =>
            new DefaultRemoteKeyDescriptorCatalog(sources)
        );
    }

    [Fact]
    public void Constructor_WhenTwoRowsHaveTheSameSelector_Throws()
    {
        var sources = new[]
        {
            CreateSource(Times.AtMostOnce(), [], [CreateRow()]),
            CreateSource(Times.AtMostOnce(), [], [CreateRow()]),
        };

        Assert.Throws<InvalidOperationException>(() =>
            new DefaultRemoteKeyDescriptorCatalog(sources)
        );
    }

    #endregion

    #region Providers Property Tests

    [Fact]
    public void Providers_ListsTheDescribedProvidersInOrdinalNameOrder()
    {
        var azure = CreateDescriptor(RemoteKeyProviderNames.AzureKeyVault);
        var aws = CreateDescriptor(RemoteKeyProviderNames.AwsKms);
        var google = CreateDescriptor(RemoteKeyProviderNames.GoogleCloudKms);

        var catalog = new DefaultRemoteKeyDescriptorCatalog([
            CreateSource([azure, aws]),
            CreateSource([google]),
        ]);

        Assert.Equal([aws, azure, google], catalog.Providers);
    }

    #endregion

    #region Algorithms Property Tests

    [Fact]
    public void Algorithms_WhenSourcesContributeRows_ListsThemOrderedByProviderAlgorithmTypeSizeAndCurve()
    {
        var awsEs384 = CreateRow(algorithm: "ES384", keySizeBits: 384, curve: "P-384");
        var awsEs256 = CreateRow();
        var awsRs256Small = CreateRow(
            algorithm: "RS256",
            secretType: "rsa",
            keySizeBits: 2048,
            curve: null
        );
        var awsRs256Large = CreateRow(
            algorithm: "RS256",
            secretType: "rsa",
            keySizeBits: 3072,
            curve: null
        );
        var azure = CreateRow(provider: RemoteKeyProviderNames.AzureKeyVault);
        var undescribed = CreateRow(provider: "other-kms");

        var catalog = new DefaultRemoteKeyDescriptorCatalog([
            CreateSource(
                [CreateDescriptor(RemoteKeyProviderNames.AwsKms)],
                undescribed,
                awsRs256Large,
                awsEs384
            ),
            CreateSource(
                [CreateDescriptor(RemoteKeyProviderNames.AzureKeyVault)],
                azure,
                awsRs256Small,
                awsEs256
            ),
        ]);

        Assert.Equal(
            [awsEs256, awsEs384, awsRs256Small, awsRs256Large, azure, undescribed],
            catalog.Algorithms
        );
    }

    [Fact]
    public void Algorithms_WhenNoSourceContributesRows_IsEmpty()
    {
        var catalog = new DefaultRemoteKeyDescriptorCatalog([
            CreateSource([CreateDescriptor(RemoteKeyProviderNames.AwsKms)]),
        ]);

        Assert.Empty(catalog.Algorithms);
    }

    #endregion

    #region TryGetAlgorithm Tests

    [Fact]
    public void TryGetAlgorithm_WhenTheSelectorMatches_ReturnsTheRow()
    {
        var row = CreateRow();
        var catalog = new DefaultRemoteKeyDescriptorCatalog([
            CreateSource([CreateDescriptor(RemoteKeyProviderNames.AwsKms)], row),
        ]);

        Assert.True(catalog.TryGetAlgorithm(CreateSelector(), out var algorithm));
        Assert.Same(row, algorithm);
    }

    [Fact]
    public void TryGetAlgorithm_MergesTheRowsOfEverySource()
    {
        var es256 = CreateRow();
        var es384 = CreateRow(algorithm: "ES384", keySizeBits: 384, curve: "P-384");
        var catalog = new DefaultRemoteKeyDescriptorCatalog([
            CreateSource([], es256),
            CreateSource([], es384),
        ]);

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
    public void TryGetAlgorithm_WhenTheProviderIsNotDescribed_KeepsTheRows()
    {
        var row = CreateRow(provider: "other-kms");
        var catalog = new DefaultRemoteKeyDescriptorCatalog([CreateSource([], row)]);

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
        var catalog = new DefaultRemoteKeyDescriptorCatalog([
            CreateSource([CreateDescriptor(RemoteKeyProviderNames.AwsKms)], CreateRow()),
        ]);
        var selector = CreateSelector(provider, algorithm, secretType, keySizeBits, curve);

        Assert.False(catalog.TryGetAlgorithm(selector, out var row));
        Assert.Null(row);
    }

    [Fact]
    public void TryGetAlgorithm_WhenTheRowHasNoCurve_MatchesASelectorWithNoCurve()
    {
        var row = CreateRow(algorithm: "PS256", secretType: "rsa", keySizeBits: 2048, curve: null);
        var catalog = new DefaultRemoteKeyDescriptorCatalog([CreateSource([], row)]);
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
