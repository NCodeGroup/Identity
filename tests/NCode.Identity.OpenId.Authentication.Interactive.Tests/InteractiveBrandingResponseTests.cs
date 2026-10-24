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
using NCode.Identity.Settings;
using Xunit;

namespace NCode.Identity.OpenId.Authentication.Interactive.Tests;

public sealed class InteractiveBrandingResponseTests
{
    private MockRepository MockRepository { get; } = new(MockBehavior.Strict);

    #region Helpers

    private IReadOnlySettingCollection CreateSettings(IReadOnlyDictionary<string, string> values)
    {
        var mockSettings = MockRepository.Create<IReadOnlySettingCollection>();
        mockSettings
            .Setup(settings => settings.GetValue(It.IsAny<SettingKey<string>>()))
            .Returns(
                (SettingKey<string> key) => values.GetValueOrDefault(key.SettingName, string.Empty)
            )
            .Verifiable(Times.Exactly(5));
        return mockSettings.Object;
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

        var response = InteractiveBrandingResponse.FromSettings(settings);

        Assert.Equal(
            new InteractiveBrandingResponse(
                "Acme",
                "https://cdn.acme.example/logo.svg",
                "#1565c0",
                "#9c27b0",
                "dark"
            ),
            response
        );
        MockRepository.Verify();
    }

    [Fact]
    public void FromSettings_WhenNoTokenIsSet_SerializesAnEmptyObject()
    {
        var settings = CreateSettings(new Dictionary<string, string>());

        var response = InteractiveBrandingResponse.FromSettings(settings);

        Assert.Equal("{}", JsonSerializer.Serialize(response, JsonSerializerOptions.Web));
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

        var response = InteractiveBrandingResponse.FromSettings(settings);

        Assert.Equal(
            "{\"logoUrl\":\"https://cdn.acme.example/logo.svg\",\"mode\":\"light\"}",
            JsonSerializer.Serialize(response, JsonSerializerOptions.Web)
        );
        MockRepository.Verify();
    }

    #endregion
}
