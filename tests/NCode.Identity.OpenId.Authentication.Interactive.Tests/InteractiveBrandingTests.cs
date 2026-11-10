// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using Moq;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.OpenId.Tenants;
using NCode.Identity.Settings;
using Xunit;

namespace NCode.Identity.OpenId.Authentication.Interactive.Tests;

public sealed class InteractiveBrandingTests
{
    private MockRepository MockRepository { get; } = new(MockBehavior.Strict);

    #region Helpers

    private sealed record FakeTenantEnvironmentType(string TypeName, bool IsProduction)
        : ITenantEnvironmentType
    {
        public IReadOnlyCollection<string> SecurityProfiles { get; } = [];
    }

    private IReadOnlySettingCollection CreateSettings(
        IReadOnlyDictionary<string, string> values,
        int expectedReads = 6
    )
    {
        var mockSettings = MockRepository.Create<IReadOnlySettingCollection>();
        mockSettings
            .Setup(settings => settings.GetValue(It.IsAny<SettingKey<string>>()))
            .Returns(
                (SettingKey<string> key) => values.GetValueOrDefault(key.SettingName, string.Empty)
            )
            .Verifiable(Times.Exactly(expectedReads));
        return mockSettings.Object;
    }

    private ITenantEnvironmentCatalog CreateCatalog(
        IReadOnlySettingCollection settings,
        string typeName,
        bool isProduction
    )
    {
        var mockCatalog = MockRepository.Create<ITenantEnvironmentCatalog>();
        mockCatalog
            .Setup(x => x.GetEffectiveType(settings))
            .Returns(new FakeTenantEnvironmentType(typeName, isProduction))
            .Verifiable(Times.Once);
        return mockCatalog.Object;
    }

    #endregion

    #region FromSettings Tests

    [Fact]
    public void FromSettings_WhenEveryTokenIsSet_ReturnsEachToken()
    {
        var settings = CreateSettings(
            new Dictionary<string, string>
            {
                [InteractiveSettingNames.BrandingDisplayName] = "Acme",
                [InteractiveSettingNames.BrandingLogoUri] = "https://cdn.acme.example/logo.svg",
                [InteractiveSettingNames.BrandingPrimaryColor] = "#1565c0",
                [InteractiveSettingNames.BrandingSecondaryColor] = "#9c27b0",
                [InteractiveSettingNames.BrandingColorMode] = "dark",
            }
        );

        var catalog = CreateCatalog(settings, "production", isProduction: true);

        var branding = InteractiveBranding.FromSettings(settings, catalog);

        Assert.Equal(
            new InteractiveBranding(
                "Acme",
                "https://cdn.acme.example/logo.svg",
                null,
                "#1565c0",
                "#9c27b0",
                "dark",
                null
            ),
            branding
        );
        MockRepository.Verify();
    }

    [Fact]
    public void FromSettings_WhenNoTokenIsSet_SerializesAnEmptyObject()
    {
        var settings = CreateSettings(new Dictionary<string, string>());
        var catalog = CreateCatalog(settings, "production", isProduction: true);

        var branding = InteractiveBranding.FromSettings(settings, catalog);

        Assert.Equal("{}", JsonSerializer.Serialize(branding, JsonSerializerOptions.Web));
        MockRepository.Verify();
    }

    [Fact]
    public void FromSettings_WhenSomeTokensAreSet_SerializesOnlyThoseInTheBootstrapBrandingShape()
    {
        var settings = CreateSettings(
            new Dictionary<string, string>
            {
                [InteractiveSettingNames.BrandingLogoUri] = "https://cdn.acme.example/logo.svg",
                [InteractiveSettingNames.BrandingColorMode] = "light",
            }
        );

        var catalog = CreateCatalog(settings, "production", isProduction: true);

        var branding = InteractiveBranding.FromSettings(settings, catalog);

        Assert.Equal(
            "{\"logoUrl\":\"https://cdn.acme.example/logo.svg\",\"mode\":\"light\"}",
            JsonSerializer.Serialize(branding, JsonSerializerOptions.Web)
        );
        MockRepository.Verify();
    }

    [Fact]
    public void FromSettings_WhenTheTenantIsNotProduction_EmbedsItsTypeAndName()
    {
        var settings = CreateSettings(
            new Dictionary<string, string> { [OpenIdSettingNames.EnvironmentName] = "sandbox" },
            expectedReads: 7
        );
        var catalog = CreateCatalog(settings, "development", isProduction: false);

        var branding = InteractiveBranding.FromSettings(settings, catalog);

        Assert.Equal(
            "{\"environment\":{\"type\":\"development\",\"name\":\"sandbox\"}}",
            JsonSerializer.Serialize(branding, JsonSerializerOptions.Web)
        );
        MockRepository.Verify();
    }

    [Fact]
    public void FromSettings_WhenANonProductionTenantHasNoName_OmitsTheName()
    {
        var settings = CreateSettings(new Dictionary<string, string>(), expectedReads: 7);
        var catalog = CreateCatalog(settings, "testing", isProduction: false);

        var branding = InteractiveBranding.FromSettings(settings, catalog);

        Assert.Equal(
            "{\"environment\":{\"type\":\"testing\"}}",
            JsonSerializer.Serialize(branding, JsonSerializerOptions.Web)
        );
        MockRepository.Verify();
    }

    [Fact]
    public void FromSettings_WhenTheTenantIsProduction_OmitsTheEnvironment()
    {
        var settings = CreateSettings(
            new Dictionary<string, string> { [OpenIdSettingNames.EnvironmentName] = "sandbox" }
        );
        var catalog = CreateCatalog(settings, "production", isProduction: true);

        var branding = InteractiveBranding.FromSettings(settings, catalog);

        var json = JsonSerializer.Serialize(branding, JsonSerializerOptions.Web);
        Assert.Equal("{}", json);
        Assert.DoesNotContain("environment", json);
        MockRepository.Verify();
    }

    [Fact]
    public void FromSettings_WhenTheTenantSetsAFavicon_ReadsIt()
    {
        var settings = CreateSettings(
            new Dictionary<string, string>
            {
                [InteractiveSettingNames.BrandingFaviconUri] = "https://cdn.acme.example/tab.ico",
            }
        );
        var catalog = CreateCatalog(settings, "production", isProduction: true);

        var branding = InteractiveBranding.FromSettings(settings, catalog);

        Assert.Equal("https://cdn.acme.example/tab.ico", branding.FaviconUrl);
        Assert.Equal(new Uri("https://cdn.acme.example/tab.ico"), branding.FaviconUri);
        MockRepository.Verify();
    }

    [Fact]
    public void FromSettings_WhenTheTenantSetsNoFavicon_LeavesItNull()
    {
        var settings = CreateSettings(new Dictionary<string, string>());
        var catalog = CreateCatalog(settings, "production", isProduction: true);

        var branding = InteractiveBranding.FromSettings(settings, catalog);

        Assert.Null(branding.FaviconUrl);
        Assert.Null(branding.FaviconUri);
        MockRepository.Verify();
    }

    #endregion

    #region FaviconUri Property Tests

    [Theory]
    [InlineData("http://cdn.acme.example/tab.ico")]
    [InlineData("/tab.ico")]
    public void FaviconUri_WhenTheValueIsNotAbsoluteHttps_IsNull(string value)
    {
        var branding = new InteractiveBranding(null, null, value, null, null, null, null);

        Assert.Null(branding.FaviconUri);
    }

    #endregion

    #region Serialize Tests

    [Fact]
    public void Serialize_WhenTheTenantSetsAFavicon_OmitsIt()
    {
        var branding = new InteractiveBranding(
            null,
            null,
            "https://cdn.acme.example/tab.ico",
            null,
            null,
            null,
            null
        );

        var json = JsonSerializer.Serialize(branding, JsonSerializerOptions.Web);

        Assert.DoesNotContain("favicon", json, StringComparison.OrdinalIgnoreCase);
    }

    #endregion
}
