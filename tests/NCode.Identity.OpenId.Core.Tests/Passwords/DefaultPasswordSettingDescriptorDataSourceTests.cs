#region Copyright Preamble

// Copyright @ 2026 NCode Group
//
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
//
//        http://www.apache.org/licenses/LICENSE-2.0
//
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.

#endregion

using NCode.Collections.Providers;
using NCode.Identity.OpenId.Authentication.Passwords;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.Settings;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Passwords;

public sealed class DefaultPasswordSettingDescriptorDataSourceTests : BaseTests
{
    private DefaultPasswordSettingDescriptorDataSource CreateDataSource() =>
        new(CreateStrictMock<INullChangeToken>().Object);

    private static SettingDescriptor<T> GetDescriptor<T>(
        DefaultPasswordSettingDescriptorDataSource dataSource,
        string name
    )
        where T : notnull =>
        Assert.IsType<SettingDescriptor<T>>(
            Assert.Single(dataSource.Collection, descriptor => descriptor.Name == name)
        );

    #region Collection Property Tests

    [Fact]
    public void Collection_PasswordMinLength_DefaultsToFifteenWithinEightToSixtyFour()
    {
        var dataSource = CreateDataSource();

        var descriptor = GetDescriptor<int>(dataSource, OpenIdSettingNames.PasswordMinLength);

        Assert.Equal(15, descriptor.Default);
        Assert.False(descriptor.TryValidate(7, out _));
        Assert.True(descriptor.TryValidate(8, out _));
        Assert.True(descriptor.TryValidate(64, out _));
        Assert.False(descriptor.TryValidate(65, out _));
    }

    [Fact]
    public void Collection_PasswordMinLength_IsAFloorANarrowerScopeCanOnlyRaise()
    {
        var dataSource = CreateDataSource();
        var descriptor = GetDescriptor<int>(dataSource, OpenIdSettingNames.PasswordMinLength);

        var lowered = descriptor.OnMerge(15, 10);
        var raised = descriptor.OnMerge(15, 20);

        Assert.Equal(15, lowered);
        Assert.Equal(20, raised);
    }

    [Fact]
    public void Collection_PasswordMaxLength_DefaultsTo128WithinSixtyFourTo4096()
    {
        var dataSource = CreateDataSource();

        var descriptor = GetDescriptor<int>(dataSource, OpenIdSettingNames.PasswordMaxLength);
        var merged = descriptor.OnMerge(128, 256);

        Assert.Equal(128, descriptor.Default);
        Assert.False(descriptor.TryValidate(63, out _));
        Assert.True(descriptor.TryValidate(64, out _));
        Assert.True(descriptor.TryValidate(4096, out _));
        Assert.False(descriptor.TryValidate(4097, out _));
        Assert.Equal(256, merged);
    }

    [Fact]
    public void Collection_PasswordFailedAttemptsLimit_DefaultsToTenAndIsACeiling()
    {
        var dataSource = CreateDataSource();

        var descriptor = GetDescriptor<int>(
            dataSource,
            OpenIdSettingNames.PasswordFailedAttemptsLimit
        );
        var raised = descriptor.OnMerge(10, 20);
        var lowered = descriptor.OnMerge(10, 5);

        Assert.Equal(10, descriptor.Default);
        Assert.False(descriptor.TryValidate(0, out _));
        Assert.True(descriptor.TryValidate(1, out _));
        Assert.True(descriptor.TryValidate(100, out _));
        Assert.False(descriptor.TryValidate(101, out _));
        Assert.Equal(10, raised);
        Assert.Equal(5, lowered);
    }

    [Fact]
    public void Collection_PasswordFailedAttemptsWindow_DefaultsToFifteenMinutesAndIsAFloor()
    {
        var dataSource = CreateDataSource();

        var descriptor = GetDescriptor<TimeSpan>(
            dataSource,
            OpenIdSettingNames.PasswordFailedAttemptsWindow
        );
        var shortened = descriptor.OnMerge(TimeSpan.FromMinutes(15), TimeSpan.FromMinutes(5));
        var lengthened = descriptor.OnMerge(TimeSpan.FromMinutes(15), TimeSpan.FromHours(1));

        Assert.Equal(TimeSpan.FromMinutes(15), descriptor.Default);
        Assert.False(descriptor.TryValidate(TimeSpan.FromSeconds(59), out _));
        Assert.True(descriptor.TryValidate(TimeSpan.FromMinutes(1), out _));
        Assert.True(descriptor.TryValidate(TimeSpan.FromDays(1), out _));
        Assert.False(descriptor.TryValidate(TimeSpan.FromDays(1) + TimeSpan.FromSeconds(1), out _));
        Assert.Equal(TimeSpan.FromMinutes(15), shortened);
        Assert.Equal(TimeSpan.FromHours(1), lengthened);
    }

    [Fact]
    public void Collection_EveryPasswordSetting_AppliesToTenantsAndIsNotDiscoverable()
    {
        var dataSource = CreateDataSource();

        var descriptors = dataSource.Collection.ToList();

        Assert.Equal(4, descriptors.Count);
        foreach (var descriptor in descriptors)
        {
            Assert.Equal<string>([OpenIdSettingScopes.Tenant], descriptor.AppliesTo);
            Assert.False(descriptor.IsDiscoverable);
        }
    }

    #endregion
}
