// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.Secrets.RemoteKeys;

// See ADR-1471.
// See ADR-1474.
public class DefaultRemoteKeyProviderCatalogTests : BaseTests
{
    private IRemoteKeyProvider CreateProvider(string name)
    {
        var mock = CreateStrictMock<IRemoteKeyProvider>();
        mock.Setup(x => x.Name).Returns(name).Verifiable();
        return mock.Object;
    }

    private IRemoteKeyDescriptorCatalog CreateDescriptorCatalog(params string[] names)
    {
        var mock = CreateStrictMock<IRemoteKeyDescriptorSource>();
        mock.Setup(x => x.Providers)
            .Returns(
                names.Select(name => new RemoteKeyProviderDescriptor { Name = name }).ToArray()
            )
            .Verifiable();
        mock.Setup(x => x.Algorithms).Returns([]).Verifiable();
        return new DefaultRemoteKeyDescriptorCatalog([mock.Object]);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WhenProviderNameIsRegisteredTwice_Throws()
    {
        var providers = new[]
        {
            CreateProvider(RemoteKeyProviderNames.AwsKms),
            CreateProvider(RemoteKeyProviderNames.AwsKms),
        };
        var descriptorCatalog = CreateDescriptorCatalog(RemoteKeyProviderNames.AwsKms);

        Assert.Throws<InvalidOperationException>(() =>
            new DefaultRemoteKeyProviderCatalog(providers, descriptorCatalog)
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
        var descriptorCatalog = CreateDescriptorCatalog(RemoteKeyProviderNames.AwsKms);

        Assert.Throws<InvalidOperationException>(() =>
            new DefaultRemoteKeyProviderCatalog(providers, descriptorCatalog)
        );
    }

    [Fact]
    public void Constructor_WhenProviderHasNoDescriptor_Throws()
    {
        var providers = new[] { CreateProvider(RemoteKeyProviderNames.AwsKms) };
        var descriptorCatalog = CreateDescriptorCatalog();

        Assert.Throws<InvalidOperationException>(() =>
            new DefaultRemoteKeyProviderCatalog(providers, descriptorCatalog)
        );
    }

    [Fact]
    public void Constructor_WhenDescriptorHasNoProvider_Throws()
    {
        var descriptorCatalog = CreateDescriptorCatalog(RemoteKeyProviderNames.AwsKms);

        Assert.Throws<InvalidOperationException>(() =>
            new DefaultRemoteKeyProviderCatalog([], descriptorCatalog)
        );
    }

    #endregion

    #region TryGetProvider Tests

    [Fact]
    public void TryGetProvider_WhenRegistered_ReturnsTheProvider()
    {
        var aws = CreateProvider(RemoteKeyProviderNames.AwsKms);
        var azure = CreateProvider(RemoteKeyProviderNames.AzureKeyVault);
        var descriptorCatalog = CreateDescriptorCatalog(
            RemoteKeyProviderNames.AwsKms,
            RemoteKeyProviderNames.AzureKeyVault
        );
        var catalog = new DefaultRemoteKeyProviderCatalog([aws, azure], descriptorCatalog);

        Assert.True(catalog.TryGetProvider(RemoteKeyProviderNames.AzureKeyVault, out var provider));
        Assert.Same(azure, provider);
    }

    [Fact]
    public void TryGetProvider_WhenNotRegistered_ReturnsFalse()
    {
        var descriptorCatalog = CreateDescriptorCatalog(RemoteKeyProviderNames.AwsKms);
        var catalog = new DefaultRemoteKeyProviderCatalog(
            [CreateProvider(RemoteKeyProviderNames.AwsKms)],
            descriptorCatalog
        );

        Assert.False(
            catalog.TryGetProvider(RemoteKeyProviderNames.GoogleCloudKms, out var provider)
        );
        Assert.Null(provider);
    }

    [Fact]
    public void TryGetProvider_ComparesTheNameOrdinally()
    {
        var descriptorCatalog = CreateDescriptorCatalog(RemoteKeyProviderNames.AwsKms);
        var catalog = new DefaultRemoteKeyProviderCatalog(
            [CreateProvider(RemoteKeyProviderNames.AwsKms)],
            descriptorCatalog
        );

        Assert.False(catalog.TryGetProvider("AWS-KMS", out _));
    }

    #endregion
}
