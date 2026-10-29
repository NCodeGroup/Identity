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
using NCode.Identity.OpenId.Tenants;
using NCode.Identity.Settings;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Tenants;

public class TenantEnvironmentSettingDescriptorDataSourceTests : BaseTests
{
    private Mock<INullChangeToken> MockNullChangeToken { get; }
    private TenantEnvironmentSettingDescriptorDataSource DataSource { get; }

    public TenantEnvironmentSettingDescriptorDataSourceTests()
    {
        MockNullChangeToken = CreateStrictMock<INullChangeToken>();
        DataSource = new TenantEnvironmentSettingDescriptorDataSource(
            MockNullChangeToken.Object,
            [new FakeType("staging"), new FakeType("production"), new FakeType("development")]
        );
    }

    #region Helpers

    private SettingDescriptor<T> GetDescriptor<T>(string name)
        where T : notnull =>
        Assert.IsType<SettingDescriptor<T>>(
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
    public void Collection_WhenEnumerated_DescribesTheThreeTenantSettingsInOrder()
    {
        var descriptors = DataSource.Collection.ToList();

        Assert.Equal(
            [
                OpenIdSettingNames.EnvironmentType,
                OpenIdSettingNames.EnvironmentName,
                OpenIdSettingNames.DeletionProtection,
            ],
            descriptors.Select(descriptor => descriptor.Name)
        );
    }

    [Fact]
    public void Collection_WhenEnumerated_AppliesToTenantsOnlyAndIsNotDiscoverable()
    {
        var descriptors = DataSource.Collection.ToList();

        Assert.All(
            descriptors,
            descriptor =>
            {
                Assert.Equal([OpenIdSettingScopes.Tenant], descriptor.AppliesTo.AsEnumerable());
                Assert.False(descriptor.IsDiscoverable);
            }
        );
    }

    [Fact]
    public void Collection_WhenEnumerated_DefaultsEachSettingToItsUnprotectedValue()
    {
        var type = GetDescriptor<string>(OpenIdSettingNames.EnvironmentType);
        var name = GetDescriptor<string>(OpenIdSettingNames.EnvironmentName);
        var protection = GetDescriptor<bool>(OpenIdSettingNames.DeletionProtection);

        Assert.Equal(OpenIdConstants.TenantEnvironmentTypes.Production, type.Default);
        Assert.Equal(string.Empty, name.Default);
        Assert.False(protection.Default);
    }

    [Theory]
    [InlineData("staging")]
    [InlineData("production")]
    [InlineData("development")]
    public void Collection_WhenTheTypeIsRegistered_AcceptsIt(string typeName)
    {
        var descriptor = GetDescriptor<string>(OpenIdSettingNames.EnvironmentType);

        Assert.True(descriptor.TryValidate(typeName, out var failure), failure);
    }

    [Theory]
    [InlineData("prod")]
    [InlineData("Production")]
    [InlineData("")]
    public void Collection_WhenTheTypeIsNotRegistered_RejectsIt(string typeName)
    {
        var descriptor = GetDescriptor<string>(OpenIdSettingNames.EnvironmentType);

        Assert.False(descriptor.TryValidate(typeName, out _));
    }

    [Fact]
    public void Collection_WhenTheNameIsEmpty_AcceptsIt()
    {
        var descriptor = GetDescriptor<string>(OpenIdSettingNames.EnvironmentName);

        Assert.True(descriptor.TryValidate(string.Empty, out var failure), failure);
    }

    [Fact]
    public void Collection_WhenTheNameIsAtMostSixtyFourCharacters_AcceptsIt()
    {
        var descriptor = GetDescriptor<string>(OpenIdSettingNames.EnvironmentName);

        Assert.True(descriptor.TryValidate(new string('a', 64), out var failure), failure);
    }

    [Fact]
    public void Collection_WhenTheNameIsTooLong_RejectsIt()
    {
        var descriptor = GetDescriptor<string>(OpenIdSettingNames.EnvironmentName);

        Assert.False(descriptor.TryValidate(new string('a', 65), out _));
    }

    [Fact]
    public void Collection_WhenTheNameHasAControlCharacter_RejectsIt()
    {
        var descriptor = GetDescriptor<string>(OpenIdSettingNames.EnvironmentName);

        Assert.False(descriptor.TryValidate("sand\nbox", out _));
    }

    #endregion

    #region Scaffolding

    private sealed record FakeType(string TypeName) : ITenantEnvironmentType
    {
        public bool IsProduction => false;

        public IReadOnlyCollection<string> SecurityProfiles => [];
    }

    #endregion
}
