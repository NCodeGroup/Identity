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

using Moq;
using NCode.Collections.Providers;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.Settings;
using Xunit;

namespace NCode.Identity.OpenId.Authentication.Interactive.Tests;

public sealed class InteractiveSettingDescriptorDataSourceTests
{
    private MockRepository MockRepository { get; } = new(MockBehavior.Strict);
    private Mock<INullChangeToken> MockNullChangeToken { get; }
    private InteractiveSettingDescriptorDataSource DataSource { get; }

    public InteractiveSettingDescriptorDataSourceTests()
    {
        MockNullChangeToken = MockRepository.Create<INullChangeToken>();
        DataSource = new InteractiveSettingDescriptorDataSource(MockNullChangeToken.Object);
    }

    #region Helpers

    private SettingDescriptor<string> GetDescriptor(string name) =>
        Assert.IsType<SettingDescriptor<string>>(
            Assert.Single(DataSource.Collection, descriptor => descriptor.Name == name)
        );

    #endregion

    #region GetChangeToken Tests

    [Fact]
    public void GetChangeToken_WhenCalled_ReturnsTheNullChangeToken()
    {
        var result = DataSource.GetChangeToken();

        Assert.Same(MockNullChangeToken.Object, result);
    }

    #endregion

    #region Collection Tests

    [Fact]
    public void Collection_WhenEnumerated_DescribesTheBrandingAsUnsetTenantSettings()
    {
        var descriptors = DataSource.Collection.ToList();

        Assert.Equal(
            [
                InteractiveSettingNames.BrandingDisplayName,
                InteractiveSettingNames.BrandingLogoUri,
                InteractiveSettingNames.BrandingPrimaryColor,
                InteractiveSettingNames.BrandingSecondaryColor,
                InteractiveSettingNames.BrandingColorMode,
            ],
            descriptors.Select(descriptor => descriptor.Name)
        );
        Assert.All(
            descriptors,
            descriptor =>
            {
                var typed = Assert.IsType<SettingDescriptor<string>>(descriptor);
                Assert.Equal([OpenIdSettingScopes.Tenant], typed.AppliesTo.AsEnumerable());
                Assert.Equal(string.Empty, typed.Default);
                Assert.False(typed.IsDiscoverable);
                Assert.True(typed.Rule.TryValidate(string.Empty, out _));
            }
        );
    }

    [Theory]
    [InlineData(InteractiveSettingNames.BrandingDisplayName, "Acme Corporation")]
    [InlineData(InteractiveSettingNames.BrandingLogoUri, "https://cdn.acme.example/logo.svg")]
    [InlineData(InteractiveSettingNames.BrandingPrimaryColor, "#1565c0")]
    [InlineData(InteractiveSettingNames.BrandingPrimaryColor, "#FFF")]
    [InlineData(InteractiveSettingNames.BrandingSecondaryColor, "#9c27b0")]
    [InlineData(InteractiveSettingNames.BrandingColorMode, "light")]
    [InlineData(InteractiveSettingNames.BrandingColorMode, "dark")]
    public void Collection_WhenAValueHasTheThemedShape_AcceptsIt(string name, string value)
    {
        var descriptor = GetDescriptor(name);

        Assert.True(descriptor.Rule.TryValidate(value, out var failure), failure);
    }

    [Theory]
    [InlineData(InteractiveSettingNames.BrandingDisplayName, "Acme\nCorporation")]
    [InlineData(InteractiveSettingNames.BrandingLogoUri, "http://cdn.acme.example/logo.svg")]
    [InlineData(InteractiveSettingNames.BrandingLogoUri, "/logo.svg")]
    [InlineData(InteractiveSettingNames.BrandingLogoUri, "javascript:alert(1)")]
    [InlineData(InteractiveSettingNames.BrandingPrimaryColor, "blue")]
    [InlineData(InteractiveSettingNames.BrandingPrimaryColor, "#12345")]
    [InlineData(InteractiveSettingNames.BrandingPrimaryColor, "#1565c0\n")]
    [InlineData(InteractiveSettingNames.BrandingSecondaryColor, "rgb(0, 0, 0)")]
    [InlineData(InteractiveSettingNames.BrandingColorMode, "Dark")]
    [InlineData(InteractiveSettingNames.BrandingColorMode, "system")]
    public void Collection_WhenAValueDoesNotHaveTheThemedShape_RejectsIt(string name, string value)
    {
        var descriptor = GetDescriptor(name);

        Assert.False(descriptor.Rule.TryValidate(value, out _));
    }

    [Fact]
    public void Collection_WhenTheDisplayNameIsTooLong_RejectsIt()
    {
        var descriptor = GetDescriptor(InteractiveSettingNames.BrandingDisplayName);

        Assert.False(descriptor.Rule.TryValidate(new string('a', 101), out _));
        Assert.True(descriptor.Rule.TryValidate(new string('a', 100), out _));
    }

    #endregion
}
