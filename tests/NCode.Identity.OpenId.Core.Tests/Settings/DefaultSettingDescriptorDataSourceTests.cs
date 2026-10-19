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
using NCode.Identity.Jose.Algorithms;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.Settings;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Settings;

public class DefaultSettingDescriptorDataSourceTests : BaseTests
{
    private DefaultSettingDescriptorDataSource CreateDataSource(params string[] profileNames)
    {
        var profiles = profileNames.Select(ISecurityProfile (name) => new FakeProfile(name));

        return new DefaultSettingDescriptorDataSource(
            CreateStrictMock<INullChangeToken>().Object,
            CreateStrictMock<IAlgorithmCollectionProvider>().Object,
            profiles
        );
    }

    private static SettingDescriptor GetDescriptor(
        DefaultSettingDescriptorDataSource dataSource,
        string settingName
    ) => dataSource.Collection.Single(descriptor => descriptor.Name == settingName);

    #region Collection Property Tests

    [Theory]
    [InlineData("", true)]
    [InlineData("fapi2", true)]
    [InlineData("fapi-2", false)]
    [InlineData("FAPI2", false)]
    public void Collection_SecurityProfile_AcceptsOnlyEmptyOrARegisteredProfile(
        string value,
        bool expected
    )
    {
        var dataSource = CreateDataSource("fapi2");
        var descriptor = GetDescriptor(dataSource, OpenIdSettingNames.SecurityProfile);

        var valid = descriptor.TryValidate(value, out _);

        Assert.Equal(expected, valid);
    }

    [Fact]
    public void Collection_SecurityProfile_WhenNoProfileIsRegistered_AcceptsOnlyEmpty()
    {
        var dataSource = CreateDataSource();
        var descriptor = GetDescriptor(dataSource, OpenIdSettingNames.SecurityProfile);

        var emptyIsValid = descriptor.TryValidate(string.Empty, out _);
        var fapi2IsValid = descriptor.TryValidate("fapi2", out var failure);

        Assert.True(emptyIsValid);
        Assert.False(fapi2IsValid);
        Assert.Equal("must be empty, because no security profile is registered", failure);
    }

    [Fact]
    public void Collection_EveryDefaultIsInItsOwnDomain()
    {
        var dataSource = CreateDataSource("fapi2");

        foreach (var descriptor in dataSource.Collection.Where(descriptor => descriptor.HasDefault))
        {
            var setting = descriptor.CreateDefault();
            var valid = descriptor.TryValidate(setting.GetValue(), out var failure);

            Assert.True(valid, $"The default of '{descriptor.Name}' {failure}.");
        }
    }

    [Fact]
    public void Collection_DoesNotDescribeTheClientAuthenticationMethod()
    {
        // The registered client-authentication handlers define its domain, so the authentication slice describes it.
        var dataSource = CreateDataSource();

        Assert.DoesNotContain(
            dataSource.Collection,
            descriptor => descriptor.Name == OpenIdSettingNames.TokenEndpointAuthMethod
        );
    }

    #endregion

    #region Scaffolding

    private sealed class FakeProfile(string name) : ISecurityProfile
    {
        public string ProfileName => name;
        public IReadOnlyCollection<ISecurityProfileRequirement> Requirements => [];

        public void ApplyCeilingSettings(ISettingCollection ceiling) { }
    }

    #endregion
}
