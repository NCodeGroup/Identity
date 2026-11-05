// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Moq;
using NCode.Collections.Providers;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.OpenId.Teams;
using NCode.Identity.Settings;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Teams;

public class TeamSettingDescriptorDataSourceTests : BaseTests
{
    private Mock<INullChangeToken> MockNullChangeToken { get; }
    private TeamSettingDescriptorDataSource DataSource { get; }

    public TeamSettingDescriptorDataSourceTests()
    {
        MockNullChangeToken = CreateStrictMock<INullChangeToken>();
        DataSource = new TeamSettingDescriptorDataSource(MockNullChangeToken.Object);
    }

    private SettingDescriptor<int> GetMaxTenants() =>
        Assert.IsType<SettingDescriptor<int>>(
            Assert.Single(DataSource.Collection, d => d.Name == OpenIdSettingNames.MaxTenants)
        );

    [Fact]
    public void GetChangeToken_WhenCalled_ReturnsTheNullChangeToken()
    {
        Assert.Same(MockNullChangeToken.Object, DataSource.GetChangeToken());
    }

    [Fact]
    public void Collection_WhenEnumerated_AppliesToTeamsOnlyIsALimitAndIsNotDiscoverable()
    {
        var descriptor = Assert.Single(DataSource.Collection);

        Assert.Equal(OpenIdSettingNames.MaxTenants, descriptor.Name);
        Assert.Equal([OpenIdSettingScopes.Team], descriptor.AppliesTo.AsEnumerable());
        Assert.True(descriptor.IsLimit);
        Assert.False(descriptor.IsDiscoverable);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void MaxTenants_WhenNegative_IsRejected(int value)
    {
        Assert.False(GetMaxTenants().TryValidate(value, out _));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(25)]
    public void MaxTenants_WhenNotNegative_IsAccepted(int value)
    {
        Assert.True(GetMaxTenants().TryValidate(value, out var failure), failure);
    }

    [Fact]
    public void MaxTenants_MergesWithMin()
    {
        var merged = GetMaxTenants().OnMerge(10, 3);

        Assert.Equal(3, merged);
        Assert.Equal(3, GetMaxTenants().OnMerge(3, 10));
    }
}
