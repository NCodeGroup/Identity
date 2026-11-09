// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Moq;

namespace NCode.Identity.Secrets.RemoteKeys;

public class RemoteKeyDescriptorCatalogExtensionsTests : BaseTests
{
    private Mock<IRemoteKeyDescriptorCatalog> CreateCatalog(
        params RemoteKeyProviderDescriptor[] descriptors
    )
    {
        var catalog = CreateStrictMock<IRemoteKeyDescriptorCatalog>();
        catalog.Setup(x => x.Providers).Returns(descriptors).Verifiable();
        return catalog;
    }

    #region ProvidersThatCreateKeys Property Tests

    [Fact]
    public void ProvidersThatCreateKeys_WhenSomeProvidersCreateNoKeys_ListsOnlyThoseThatDo()
    {
        var catalog = CreateCatalog(
            new RemoteKeyProviderDescriptor { Name = RemoteKeyProviderNames.AwsKms },
            new RemoteKeyProviderDescriptor
            {
                Name = RemoteKeyProviderNames.AzureKeyVault,
                CreatesKeys = false,
            },
            new RemoteKeyProviderDescriptor { Name = RemoteKeyProviderNames.GoogleCloudKms }
        );

        var names = catalog.Object.ProvidersThatCreateKeys.Select(provider => provider.Name);

        Assert.Equal(["aws-kms", "google-cloud-kms"], names);
    }

    #endregion

    #region TryGetProvider Tests

    [Fact]
    public void TryGetProvider_WhenAProviderHasTheName_ReturnsItsDescriptor()
    {
        var aws = new RemoteKeyProviderDescriptor { Name = RemoteKeyProviderNames.AwsKms };
        var azure = new RemoteKeyProviderDescriptor { Name = RemoteKeyProviderNames.AzureKeyVault };
        var catalog = CreateCatalog(aws, azure);

        var found = catalog.Object.TryGetProvider(
            RemoteKeyProviderNames.AzureKeyVault,
            out var descriptor
        );

        Assert.True(found);
        Assert.Same(azure, descriptor);
    }

    [Fact]
    public void TryGetProvider_WhenNoProviderHasTheName_ReturnsFalse()
    {
        var catalog = CreateCatalog(
            new RemoteKeyProviderDescriptor { Name = RemoteKeyProviderNames.AwsKms }
        );

        var found = catalog.Object.TryGetProvider(
            RemoteKeyProviderNames.GoogleCloudKms,
            out var descriptor
        );

        Assert.False(found);
        Assert.Null(descriptor);
    }

    [Fact]
    public void TryGetProvider_WhenOnlyTheCaseDiffers_ReturnsFalse()
    {
        var catalog = CreateCatalog(
            new RemoteKeyProviderDescriptor { Name = RemoteKeyProviderNames.AwsKms }
        );

        var found = catalog.Object.TryGetProvider("AWS-KMS", out var descriptor);

        Assert.False(found);
        Assert.Null(descriptor);
    }

    [Fact]
    public void TryGetProvider_WhenTheCatalogIsNull_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            ((IRemoteKeyDescriptorCatalog)null!).TryGetProvider(
                RemoteKeyProviderNames.AwsKms,
                out _
            )
        );
    }

    #endregion

    #region CreatesKeys Tests

    [Fact]
    public void CreatesKeys_WhenTheProviderCreatesKeys_ReturnsTrue()
    {
        var catalog = CreateCatalog(
            new RemoteKeyProviderDescriptor { Name = RemoteKeyProviderNames.AwsKms }
        );

        Assert.True(catalog.Object.CreatesKeys(RemoteKeyProviderNames.AwsKms));
    }

    [Fact]
    public void CreatesKeys_WhenTheProviderCreatesNoKeys_ReturnsFalse()
    {
        var catalog = CreateCatalog(
            new RemoteKeyProviderDescriptor
            {
                Name = RemoteKeyProviderNames.AwsKms,
                CreatesKeys = false,
            }
        );

        Assert.False(catalog.Object.CreatesKeys(RemoteKeyProviderNames.AwsKms));
    }

    [Fact]
    public void CreatesKeys_WhenNoProviderHasTheName_ReturnsFalse()
    {
        var catalog = CreateCatalog(
            new RemoteKeyProviderDescriptor { Name = RemoteKeyProviderNames.AwsKms }
        );

        Assert.False(catalog.Object.CreatesKeys(RemoteKeyProviderNames.GoogleCloudKms));
    }

    #endregion
}
