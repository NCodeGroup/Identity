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
        int expectedReads = 5
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
            expectedReads: 6
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
        var settings = CreateSettings(new Dictionary<string, string>(), expectedReads: 6);
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

    #endregion
}
