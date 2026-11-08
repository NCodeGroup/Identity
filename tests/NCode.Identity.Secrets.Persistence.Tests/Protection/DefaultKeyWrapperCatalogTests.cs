// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Moq;
using NCode.Identity.Secrets.KeyWrapping;
using NCode.Identity.Secrets.Persistence.Options;

namespace NCode.Identity.Secrets.Persistence.Protection;

public class DefaultKeyWrapperCatalogTests : BaseTests
{
    private Mock<IKeyWrapper> CreateProvider(string providerName)
    {
        var provider = CreateStrictMock<IKeyWrapper>();
        provider.Setup(x => x.Name).Returns(providerName).Verifiable();
        return provider;
    }

    private Mock<IReferencedKeyWrapper> CreateReferencedProvider(string providerName)
    {
        var provider = CreateStrictMock<IReferencedKeyWrapper>();
        provider.Setup(x => x.Name).Returns(providerName).Verifiable();
        return provider;
    }

    private static DefaultKeyWrapperCatalog CreateCatalog(
        IEnumerable<IKeyWrapper> providers,
        string? rootKeyProvider = null,
        string[]? ownerProviders = null
    ) =>
        new(
            providers,
            Microsoft.Extensions.Options.Options.Create(
                new SecretProtectionOptions
                {
                    RootKeyProvider = rootKeyProvider,
                    OwnerRootKeys = new OwnerRootKeyOptions { Providers = ownerProviders ?? [] },
                }
            )
        );

    #region Constructor Tests

    [Fact]
    public void Constructor_DuplicateNames_Throws()
    {
        var first = CreateProvider("duplicate");
        var second = CreateProvider("duplicate");
        var other = CreateProvider("other");

        var exception = Assert.Throws<InvalidOperationException>(() =>
            CreateCatalog([first.Object, second.Object, other.Object])
        );

        Assert.Contains("'duplicate'", exception.Message);
        Assert.DoesNotContain("'other'", exception.Message);
    }

    [Fact]
    public void Constructor_OwnerProvidersThatAreReferencedWrappers_Composes()
    {
        var local = CreateProvider(KeyWrapperNames.Local);
        var awsKms = CreateReferencedProvider(KeyWrapperNames.AwsKms);
        var azureKeyVault = CreateReferencedProvider(KeyWrapperNames.AzureKeyVault);

        var catalog = CreateCatalog(
            [local.Object, awsKms.Object, azureKeyVault.Object],
            ownerProviders: [KeyWrapperNames.AwsKms, KeyWrapperNames.AzureKeyVault]
        );

        Assert.Same(awsKms.Object, catalog.GetReferenced(KeyWrapperNames.AwsKms));
    }

    [Fact]
    public void Constructor_OwnerProviderThatDoesNotWrapUnderANamedKey_ThrowsListingReferencedNamesInOrder()
    {
        var local = CreateProvider(KeyWrapperNames.Local);
        var googleCloudKms = CreateReferencedProvider(KeyWrapperNames.GoogleCloudKms);
        var awsKms = CreateReferencedProvider(KeyWrapperNames.AwsKms);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            CreateCatalog(
                [local.Object, googleCloudKms.Object, awsKms.Object],
                ownerProviders: [KeyWrapperNames.Local]
            )
        );

        Assert.StartsWith(
            $"SecretProtection:OwnerRootKeys:Providers names '{KeyWrapperNames.Local}'",
            exception.Message
        );
        Assert.Contains(
            "is registered but does not wrap under a key the caller names",
            exception.Message
        );
        Assert.EndsWith(
            $"are: '{KeyWrapperNames.AwsKms}', '{KeyWrapperNames.GoogleCloudKms}'.",
            exception.Message
        );
    }

    [Fact]
    public void Constructor_UnregisteredOwnerProvider_Throws()
    {
        var awsKms = CreateReferencedProvider(KeyWrapperNames.AwsKms);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            CreateCatalog([awsKms.Object], ownerProviders: ["missing"])
        );

        Assert.Contains("'missing', which is not registered", exception.Message);
    }

    [Fact]
    public void Constructor_OwnerProvidersWithNoReferencedWrapper_ThrowsListingNone()
    {
        var local = CreateProvider(KeyWrapperNames.Local);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            CreateCatalog([local.Object], ownerProviders: [KeyWrapperNames.AwsKms])
        );

        Assert.EndsWith("are: none.", exception.Message);
    }

    [Fact]
    public void Constructor_SeveralOffendingOwnerProviders_ThrowsOnceNamingEach()
    {
        var local = CreateProvider(KeyWrapperNames.Local);
        var awsKms = CreateReferencedProvider(KeyWrapperNames.AwsKms);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            CreateCatalog(
                [local.Object, awsKms.Object],
                ownerProviders: [KeyWrapperNames.Local, "missing"]
            )
        );

        Assert.Contains($"names '{KeyWrapperNames.Local}', which is registered", exception.Message);
        Assert.Contains("names 'missing', which is not registered", exception.Message);
    }

    [Fact]
    public void Constructor_BlankAndRepeatedOwnerProviders_AreLeftToTheValidator()
    {
        var awsKms = CreateReferencedProvider(KeyWrapperNames.AwsKms);

        var catalog = CreateCatalog(
            [awsKms.Object],
            ownerProviders: [KeyWrapperNames.AwsKms, KeyWrapperNames.AwsKms, " ", ""]
        );

        Assert.Same(awsKms.Object, catalog.GetReferenced(KeyWrapperNames.AwsKms));
    }

    #endregion

    #region Get Tests

    [Fact]
    public void Get_Registered_ReturnsProvider()
    {
        var ephemeral = CreateProvider(KeyWrapperNames.Ephemeral);
        var local = CreateProvider(KeyWrapperNames.Local);
        var catalog = CreateCatalog([ephemeral.Object, local.Object]);

        var result = catalog.Get(KeyWrapperNames.Local);

        Assert.Same(local.Object, result);
    }

    [Fact]
    public void Get_Unknown_ThrowsListingNames()
    {
        var ephemeral = CreateProvider(KeyWrapperNames.Ephemeral);
        var local = CreateProvider(KeyWrapperNames.Local);
        var catalog = CreateCatalog([ephemeral.Object, local.Object]);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            catalog.Get(KeyWrapperNames.AwsKms)
        );

        Assert.Contains($"'{KeyWrapperNames.AwsKms}'", exception.Message);
        Assert.Contains($"'{KeyWrapperNames.Ephemeral}'", exception.Message);
        Assert.Contains($"'{KeyWrapperNames.Local}'", exception.Message);
    }

    #endregion

    #region GetConfigured Tests

    [Fact]
    public void GetConfigured_Unset_ThrowsNamingTheSetting()
    {
        var ephemeral = CreateProvider(KeyWrapperNames.Ephemeral);
        var local = CreateProvider(KeyWrapperNames.Local);
        var catalog = CreateCatalog([ephemeral.Object, local.Object]);

        var exception = Assert.Throws<InvalidOperationException>(() => catalog.GetConfigured());

        Assert.Contains(
            "SecretProtection:RootKeyProvider is required, but it is not set.",
            exception.Message
        );
        Assert.Contains($"'{KeyWrapperNames.Local}'", exception.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void GetConfigured_Blank_ThrowsNamingTheSetting(string rootKeyProvider)
    {
        var ephemeral = CreateProvider(KeyWrapperNames.Ephemeral);
        var local = CreateProvider(KeyWrapperNames.Local);
        var catalog = CreateCatalog(
            [ephemeral.Object, local.Object],
            rootKeyProvider: rootKeyProvider
        );

        var exception = Assert.Throws<InvalidOperationException>(() => catalog.GetConfigured());

        Assert.Contains(
            $"SecretProtection:RootKeyProvider is required, but it is blank ('{rootKeyProvider}').",
            exception.Message
        );
        Assert.Contains($"'{KeyWrapperNames.Local}'", exception.Message);
    }

    [Fact]
    public void GetConfigured_Set_ReturnsNamedProvider()
    {
        var ephemeral = CreateProvider(KeyWrapperNames.Ephemeral);
        var local = CreateProvider(KeyWrapperNames.Local);
        var catalog = CreateCatalog(
            [ephemeral.Object, local.Object],
            rootKeyProvider: KeyWrapperNames.Local
        );

        var result = catalog.GetConfigured();

        Assert.Same(local.Object, result);
    }

    #endregion

    #region GetReferenced Tests

    [Fact]
    public void GetReferenced_ReferencedWrapper_ReturnsWrapper()
    {
        var local = CreateProvider(KeyWrapperNames.Local);
        var awsKms = CreateReferencedProvider(KeyWrapperNames.AwsKms);
        var catalog = CreateCatalog([local.Object, awsKms.Object]);

        var result = catalog.GetReferenced(KeyWrapperNames.AwsKms);

        Assert.Same(awsKms.Object, result);
    }

    [Fact]
    public void GetReferenced_PlainWrapper_ThrowsListingReferencedNamesInOrder()
    {
        var local = CreateProvider(KeyWrapperNames.Local);
        var googleCloudKms = CreateReferencedProvider(KeyWrapperNames.GoogleCloudKms);
        var awsKms = CreateReferencedProvider(KeyWrapperNames.AwsKms);
        var catalog = CreateCatalog([local.Object, googleCloudKms.Object, awsKms.Object]);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            catalog.GetReferenced(KeyWrapperNames.Local)
        );

        Assert.Contains($"'{KeyWrapperNames.Local}'", exception.Message);
        var awsIndex = exception.Message.LastIndexOf(
            $"'{KeyWrapperNames.AwsKms}'",
            StringComparison.Ordinal
        );
        var googleIndex = exception.Message.LastIndexOf(
            $"'{KeyWrapperNames.GoogleCloudKms}'",
            StringComparison.Ordinal
        );
        Assert.InRange(awsIndex, 1, googleIndex - 1);
    }

    [Fact]
    public void GetReferenced_Unknown_Throws()
    {
        var awsKms = CreateReferencedProvider(KeyWrapperNames.AwsKms);
        var catalog = CreateCatalog([awsKms.Object]);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            catalog.GetReferenced(KeyWrapperNames.AzureKeyVault)
        );

        Assert.Contains($"'{KeyWrapperNames.AzureKeyVault}'", exception.Message);
    }

    [Fact]
    public void TryGetReferenced_ReferencedWrapper_ReturnsTrue()
    {
        var awsKms = CreateReferencedProvider(KeyWrapperNames.AwsKms);
        var catalog = CreateCatalog([awsKms.Object]);

        var found = catalog.TryGetReferenced(KeyWrapperNames.AwsKms, out var wrapper);

        Assert.True(found);
        Assert.Same(awsKms.Object, wrapper);
    }

    [Fact]
    public void TryGetReferenced_PlainOrUnknownWrapper_ReturnsFalse()
    {
        var local = CreateProvider(KeyWrapperNames.Local);
        var catalog = CreateCatalog([local.Object]);

        Assert.False(catalog.TryGetReferenced(KeyWrapperNames.Local, out var plain));
        Assert.Null(plain);
        Assert.False(catalog.TryGetReferenced("unknown", out var unknown));
        Assert.Null(unknown);
    }

    #endregion
}
