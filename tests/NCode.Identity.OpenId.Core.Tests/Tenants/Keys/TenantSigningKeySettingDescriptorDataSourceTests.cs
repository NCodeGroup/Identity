// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Moq;
using NCode.Collections.Providers;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.OpenId.Tenants.Keys;
using NCode.Identity.Secrets.RemoteKeys;
using NCode.Identity.Settings;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Tenants.Keys;

public class TenantSigningKeySettingDescriptorDataSourceTests : BaseTests
{
    private Mock<INullChangeToken> MockNullChangeToken { get; }
    private Mock<IRemoteKeyDescriptorCatalog> MockCatalog { get; }

    public TenantSigningKeySettingDescriptorDataSourceTests()
    {
        MockNullChangeToken = CreateStrictMock<INullChangeToken>();
        MockCatalog = CreateStrictMock<IRemoteKeyDescriptorCatalog>();
    }

    #region Helpers

    private TenantSigningKeySettingDescriptorDataSource CreateDataSource(params string[] names)
    {
        var providers = names
            .Select(name => new RemoteKeyProviderDescriptor { Name = name })
            .ToList();

        MockCatalog.Setup(x => x.Providers).Returns(providers).Verifiable();

        return new TenantSigningKeySettingDescriptorDataSource(
            MockNullChangeToken.Object,
            MockCatalog.Object
        );
    }

    private static SettingDescriptor<string> GetDescriptor(
        TenantSigningKeySettingDescriptorDataSource dataSource
    ) =>
        Assert.IsType<SettingDescriptor<string>>(
            Assert.Single(
                dataSource.Collection,
                descriptor => descriptor.Name == OpenIdSettingNames.TenantSigningKeyProvider
            )
        );

    #endregion

    #region GetChangeToken Tests

    [Fact]
    public void GetChangeToken_WhenCalled_ReturnsTheNullChangeToken()
    {
        var dataSource = new TenantSigningKeySettingDescriptorDataSource(
            MockNullChangeToken.Object,
            MockCatalog.Object
        );

        var result = dataSource.GetChangeToken();

        Assert.Same(MockNullChangeToken.Object, result);
    }

    #endregion

    #region Collection Tests

    [Fact]
    public void Collection_WhenEnumerated_DescribesOnlyTheTenantSigningKeyProviderSetting()
    {
        var dataSource = CreateDataSource("aws-kms");

        var descriptor = Assert.Single(dataSource.Collection);

        Assert.Equal(OpenIdSettingNames.TenantSigningKeyProvider, descriptor.Name);
    }

    [Fact]
    public void Collection_WhenEnumerated_AppliesToTenantsOnlyAndDefaultsToEmpty()
    {
        var dataSource = CreateDataSource("aws-kms");

        var descriptor = GetDescriptor(dataSource);

        Assert.Equal([OpenIdSettingScopes.Tenant], descriptor.AppliesTo.AsEnumerable());
        Assert.Equal(string.Empty, descriptor.Default);
    }

    [Fact]
    public void Collection_WhenEnumerated_IsEnvironmentBoundAndNotDiscoverable()
    {
        var dataSource = CreateDataSource("aws-kms");

        var descriptor = GetDescriptor(dataSource);

        Assert.True(descriptor.IsEnvironmentBound);
        Assert.False(descriptor.IsDiscoverable);
    }

    [Fact]
    public void Collection_WhenEnumerated_ReplacesTheParentValueOnMerge()
    {
        var dataSource = CreateDataSource("aws-kms");

        var descriptor = GetDescriptor(dataSource);

        Assert.Equal(SettingMerge.Replace, descriptor.OnMerge);
    }

    [Fact]
    public void Collection_WhenTheValueIsEmpty_AcceptsIt()
    {
        var dataSource = CreateDataSource("aws-kms", "azure-key-vault");
        var descriptor = GetDescriptor(dataSource);

        Assert.True(descriptor.TryValidate(string.Empty, out var failure), failure);
    }

    [Theory]
    [InlineData("aws-kms")]
    [InlineData("azure-key-vault")]
    [InlineData("gcp-kms")]
    public void Collection_WhenTheProviderIsRegistered_AcceptsIt(string name)
    {
        var dataSource = CreateDataSource("gcp-kms", "aws-kms", "azure-key-vault");
        var descriptor = GetDescriptor(dataSource);

        Assert.True(descriptor.TryValidate(name, out var failure), failure);
    }

    [Theory]
    [InlineData("hsm")]
    [InlineData("AWS-KMS")]
    [InlineData("aws-kms ")]
    public void Collection_WhenTheProviderIsNotRegistered_RejectsIt(string name)
    {
        var dataSource = CreateDataSource("aws-kms", "azure-key-vault");
        var descriptor = GetDescriptor(dataSource);

        Assert.False(descriptor.TryValidate(name, out _));
    }

    [Fact]
    public void Collection_WhenNoProviderIsRegistered_AcceptsOnlyEmpty()
    {
        var dataSource = CreateDataSource();
        var descriptor = GetDescriptor(dataSource);

        Assert.True(descriptor.TryValidate(string.Empty, out var failure), failure);
        Assert.False(descriptor.TryValidate("aws-kms", out _));
    }

    #endregion
}
