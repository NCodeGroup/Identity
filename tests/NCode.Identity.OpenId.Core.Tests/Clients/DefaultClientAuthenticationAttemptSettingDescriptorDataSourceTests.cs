// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Collections.Providers;
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.Settings;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Clients;

public sealed class DefaultClientAuthenticationAttemptSettingDescriptorDataSourceTests : BaseTests
{
    private DefaultClientAuthenticationAttemptSettingDescriptorDataSource CreateDataSource() =>
        new(CreateStrictMock<INullChangeToken>().Object);

    private static SettingDescriptor<T> GetDescriptor<T>(
        DefaultClientAuthenticationAttemptSettingDescriptorDataSource dataSource,
        string name
    )
        where T : notnull =>
        Assert.IsType<SettingDescriptor<T>>(
            Assert.Single(dataSource.Collection, descriptor => descriptor.Name == name)
        );

    #region Collection Property Tests

    [Fact]
    public void Collection_Limit_DefaultsToTenWithinOneToOneHundredAsACeiling()
    {
        var descriptor = GetDescriptor<int>(
            CreateDataSource(),
            OpenIdSettingNames.ClientAuthenticationFailedAttemptsLimit
        );

        Assert.Equal(10, descriptor.Default);
        Assert.False(descriptor.TryValidate(0, out _));
        Assert.True(descriptor.TryValidate(100, out _));
        Assert.False(descriptor.TryValidate(101, out _));
        Assert.Equal(5, descriptor.OnMerge(10, 5));
        Assert.Equal(10, descriptor.OnMerge(10, 50));
        Assert.Equal(OpenIdSettingScopes.Tenant, Assert.Single(descriptor.AppliesTo));
        Assert.False(descriptor.IsDiscoverable);
    }

    [Fact]
    public void Collection_AddressLimit_DefaultsToOneHundredWithinOneToTenThousandAsACeiling()
    {
        var descriptor = GetDescriptor<int>(
            CreateDataSource(),
            OpenIdSettingNames.ClientAuthenticationFailedAttemptsAddressLimit
        );

        Assert.Equal(100, descriptor.Default);
        Assert.False(descriptor.TryValidate(0, out _));
        Assert.True(descriptor.TryValidate(10_000, out _));
        Assert.False(descriptor.TryValidate(10_001, out _));
        Assert.Equal(20, descriptor.OnMerge(100, 20));
        Assert.Equal(100, descriptor.OnMerge(100, 500));
    }

    [Fact]
    public void Collection_Window_DefaultsToFifteenMinutesWithinOneMinuteToOneDayAsAFloor()
    {
        var descriptor = GetDescriptor<TimeSpan>(
            CreateDataSource(),
            OpenIdSettingNames.ClientAuthenticationFailedAttemptsWindow
        );

        Assert.Equal(TimeSpan.FromMinutes(15), descriptor.Default);
        Assert.False(descriptor.TryValidate(TimeSpan.FromSeconds(59), out _));
        Assert.True(descriptor.TryValidate(TimeSpan.FromDays(1), out _));
        Assert.False(descriptor.TryValidate(TimeSpan.FromDays(1.5), out _));
        Assert.Equal(
            TimeSpan.FromMinutes(15),
            descriptor.OnMerge(TimeSpan.FromMinutes(15), TimeSpan.FromMinutes(5))
        );
    }

    #endregion
}
