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

    private SettingDescriptor<int> GetDescriptor(string name) =>
        Assert.IsType<SettingDescriptor<int>>(
            Assert.Single(DataSource.Collection, d => d.Name == name)
        );

    private SettingDescriptor<int> GetMaxTenants() => GetDescriptor(OpenIdSettingNames.MaxTenants);

    private SettingDescriptor<int> GetMaxTeams() => GetDescriptor(OpenIdSettingNames.MaxTeams);

    private SettingDescriptor<int> GetMaxTeamDepth() =>
        GetDescriptor(OpenIdSettingNames.MaxTeamDepth);

    [Fact]
    public void GetChangeToken_WhenCalled_ReturnsTheNullChangeToken()
    {
        Assert.Same(MockNullChangeToken.Object, DataSource.GetChangeToken());
    }

    [Fact]
    public void Collection_WhenEnumerated_AppliesToTeamsOnlyIsALimitAndIsNotDiscoverable()
    {
        var descriptor = GetMaxTenants();

        Assert.Equal(OpenIdSettingNames.MaxTenants, descriptor.Name);
        Assert.Equal([OpenIdSettingScopes.Team], descriptor.AppliesTo.AsEnumerable());
        Assert.True(descriptor.IsLimit);
        Assert.False(descriptor.IsDiscoverable);
    }

    [Fact]
    public void Collection_WhenEnumerated_YieldsThreeDescriptors()
    {
        Assert.Equal(3, DataSource.Collection.Count());
    }

    [Fact]
    public void Collection_WhenEnumerated_MaxTeamsAppliesToTeamsOnlyIsALimitAndIsNotDiscoverable()
    {
        var descriptor = GetMaxTeams();

        Assert.Equal([OpenIdSettingScopes.Team], descriptor.AppliesTo.AsEnumerable());
        Assert.True(descriptor.IsLimit);
        Assert.False(descriptor.IsDiscoverable);
    }

    [Fact]
    public void Collection_WhenEnumerated_MaxTeamDepthAppliesToThePlatformOnlyIsNotALimitAndIsNotDiscoverable()
    {
        var descriptor = GetMaxTeamDepth();

        Assert.Equal([OpenIdSettingScopes.Platform], descriptor.AppliesTo.AsEnumerable());
        Assert.False(descriptor.IsLimit);
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

    [Theory]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void MaxTeams_WhenNegative_IsRejected(int value)
    {
        Assert.False(GetMaxTeams().TryValidate(value, out _));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(25)]
    public void MaxTeams_WhenNotNegative_IsAccepted(int value)
    {
        Assert.True(GetMaxTeams().TryValidate(value, out var failure), failure);
    }

    [Fact]
    public void MaxTeams_MergesWithMin()
    {
        Assert.Equal(3, GetMaxTeams().OnMerge(10, 3));
        Assert.Equal(3, GetMaxTeams().OnMerge(3, 10));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(9)]
    public void MaxTeamDepth_WhenOutsideOneToEight_IsRejected(int value)
    {
        Assert.False(GetMaxTeamDepth().TryValidate(value, out _));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(8)]
    public void MaxTeamDepth_WhenWithinOneToEight_IsAccepted(int value)
    {
        Assert.True(GetMaxTeamDepth().TryValidate(value, out var failure), failure);
    }
}
