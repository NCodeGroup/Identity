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

using NCode.Identity.OpenId.Settings;
using NCode.Identity.OpenId.Tenants;
using NCode.Identity.Settings;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Tenants;

public class DefaultTenantEnvironmentCatalogTests : BaseTests
{
    private const string KnownProfileName = "production";

    private DefaultTenantEnvironmentCatalog CreateCatalog(params ITenantEnvironmentType[] types)
    {
        var knownProfile = CreateStrictMock<ISecurityProfile>().Object;
        var mockProfileCatalog = CreateStrictMock<ISecurityProfileCatalog>();
        foreach (
            var profileName in types
                .SelectMany(type => type.SecurityProfiles)
                .Distinct(StringComparer.Ordinal)
        )
        {
            var isKnown = profileName == KnownProfileName;
            ISecurityProfile? profile = isKnown ? knownProfile : null;
            mockProfileCatalog
                .Setup(x => x.TryGetProfile(profileName, out profile))
                .Returns(isKnown)
                .Verifiable();
        }

        return new DefaultTenantEnvironmentCatalog(
            types,
            mockProfileCatalog.Object,
            CreateStrictMock<ISettingCollectionFactory>().Object,
            CreateStrictMock<ISettingDescriptorCollectionProvider>().Object
        );
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WhenANameIsRegisteredTwice_Throws()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            CreateCatalog(new FakeType("development", false), new FakeType("development", false))
        );

        Assert.Contains("'development'", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Constructor_WhenATypeSelectsAnUnregisteredProfile_Throws()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            CreateCatalog(new FakeType("development", false, "unknown-profile"))
        );

        Assert.Contains("'development'", exception.Message, StringComparison.Ordinal);
        Assert.Contains("'unknown-profile'", exception.Message, StringComparison.Ordinal);
    }

    #endregion

    #region TryGetType Tests

    [Fact]
    public void TryGetType_WhenRegistered_ReturnsIt()
    {
        var type = new FakeType("staging", false, KnownProfileName);
        var catalog = CreateCatalog(new FakeType("development", false), type);

        var found = catalog.TryGetType("staging", out var resolved);

        Assert.True(found);
        Assert.Same(type, resolved);
    }

    [Fact]
    public void TryGetType_WhenUnknown_ReturnsFalse()
    {
        var catalog = CreateCatalog(new FakeType("development", false));

        var found = catalog.TryGetType("other", out var resolved);

        Assert.False(found);
        Assert.Null(resolved);
    }

    #endregion

    #region GetEffectiveType Tests

    [Fact]
    public void GetEffectiveType_ReadsTheEnvironmentTypeSetting()
    {
        var development = new FakeType("development", false);
        var catalog = CreateCatalog(new FakeType("staging", false), development);
        var settings = CreateLooseMock<IReadOnlySettingCollection>();
        settings
            .Setup(x => x.GetValue(OpenIdSettingKeys.EnvironmentType))
            .Returns("development")
            .Verifiable();

        var result = catalog.GetEffectiveType(settings.Object);

        Assert.Same(development, result);
    }

    [Fact]
    public void GetEffectiveType_WhenUnregistered_Throws()
    {
        var catalog = CreateCatalog(new FakeType("development", false));
        var settings = CreateLooseMock<IReadOnlySettingCollection>();
        settings
            .Setup(x => x.GetValue(OpenIdSettingKeys.EnvironmentType))
            .Returns("unregistered")
            .Verifiable();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            catalog.GetEffectiveType(settings.Object)
        );

        Assert.Contains("'unregistered'", exception.Message, StringComparison.Ordinal);
    }

    #endregion

    #region Scaffolding

    private sealed record FakeType(
        string TypeName,
        bool IsProduction,
        params string[] SecurityProfileNames
    ) : ITenantEnvironmentType
    {
        public IReadOnlyCollection<string> SecurityProfiles => SecurityProfileNames;
    }

    #endregion
}
