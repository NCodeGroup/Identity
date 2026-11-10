// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Hosting;
using NCode.Identity.ManagementConsole.Options;
using Xunit;

namespace NCode.Identity.ManagementConsole.Tests.Options;

public class ManagementConsoleOptionsValidatorTests : BaseTests
{
    private static ManagementConsoleOptions CreateValidOptions() =>
        new()
        {
            ManagementApiUrl = "https://id.example.com/api",
            BundleRootPath = "bundle",
            Features = { ["audit"] = true },
            Branding =
            {
                DisplayName = "Console",
                LogoUrl = "https://cdn.example.com/logo.svg",
                PrimaryColor = "#112233",
                SecondaryColor = "#445566",
                Mode = "dark",
            },
        };

    private ManagementConsoleOptionsValidator CreateValidator(string environmentName = "Production")
    {
        var mockEnvironment = CreateStrictMock<IHostEnvironment>();
        mockEnvironment.Setup(x => x.EnvironmentName).Returns(environmentName).Verifiable();
        return new ManagementConsoleOptionsValidator(mockEnvironment.Object);
    }

    #region Validate Tests

    [Fact]
    public void Validate_WhenValid_Succeeds()
    {
        var result = CreateValidator().Validate(null, CreateValidOptions());

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_WhenOptionalSettingsAreUnset_Succeeds()
    {
        var options = new ManagementConsoleOptions
        {
            ManagementApiUrl = "https://id.example.com/api",
            BundleRootPath = "bundle",
        };

        var result = CreateValidator().Validate(null, options);

        Assert.True(result.Succeeded);
    }

    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData("   ", "   ")]
    [InlineData("id.example.com/api", "id.example.com/api")]
    [InlineData("http://id.example.com/api", "http://id.example.com/api")]
    [InlineData("https://user:secret@id.example.com/api", "https://id.example.com/api")]
    [InlineData("https://id.example.com/api?x=1", "https://id.example.com/api")]
    [InlineData("https://id.example.com/api#section", "https://id.example.com/api")]
    [InlineData("https://id.example.com/api#", "https://id.example.com/api")]
    public void Validate_WhenManagementApiUrlIsInvalid_Fails(string? url, string expectedValue)
    {
        var options = CreateValidOptions();
        options.ManagementApiUrl = url;

        var result = CreateValidator().Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains(
            $"ManagementConsole:ManagementApiUrl must be an absolute https URL without user info, query or fragment; it is '{expectedValue}'.",
            result.Failures ?? []
        );
    }

    [Fact]
    public void Validate_WhenManagementApiUrlHasUserInfo_FailsWithoutShowingIt()
    {
        var options = CreateValidOptions();
        options.ManagementApiUrl = "https://user:secret@id.example.com:8443/api";

        var result = CreateValidator().Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains(
            "ManagementConsole:ManagementApiUrl must be an absolute https URL without user info, query or fragment; it is 'https://id.example.com:8443/api'.",
            result.Failures ?? []
        );
        Assert.DoesNotContain(
            "secret",
            string.Concat(result.Failures ?? []),
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void Validate_WhenHttpManagementApiUrlInDevelopment_Succeeds()
    {
        var options = CreateValidOptions();
        options.ManagementApiUrl = "http://localhost:5000/api";

        var result = CreateValidator("Development").Validate(null, options);

        Assert.True(result.Succeeded);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_WhenBundleRootPathIsBlank_Fails(string? path)
    {
        var options = CreateValidOptions();
        options.BundleRootPath = path;

        var result = CreateValidator().Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains(
            $"ManagementConsole:BundleRootPath must name the directory of the built apps/mgmt bundle; it is '{path}'.",
            result.Failures ?? []
        );
    }

    [Theory]
    [InlineData("")]
    [InlineData("logo.svg")]
    [InlineData("http://cdn.example.com/logo.svg")]
    public void Validate_WhenLogoUrlIsInvalid_Fails(string url)
    {
        var options = CreateValidOptions();
        options.Branding.LogoUrl = url;

        var result = CreateValidator().Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains(
            $"ManagementConsole:Branding:LogoUrl must be an absolute https URL; it is '{url}'.",
            result.Failures ?? []
        );
    }

    [Theory]
    [InlineData("http://cdn.example/tab.ico")]
    [InlineData("/tab.ico")]
    [InlineData("javascript:alert(1)")]
    public void Validate_WhenTheFaviconUrlIsNotAbsoluteHttps_Fails(string url)
    {
        var options = CreateValidOptions();
        options.Branding.FaviconUrl = url;

        var result = CreateValidator().Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains(
            "ManagementConsole:Branding:FaviconUrl must be an absolute https URL",
            result.Failures?.Single() ?? string.Empty
        );
    }

    [Fact]
    public void Validate_WhenTheFaviconUrlIsAbsoluteHttps_Succeeds()
    {
        var options = CreateValidOptions();
        options.Branding.FaviconUrl = "https://cdn.example/tab.ico";

        var result = CreateValidator().Validate(null, options);

        Assert.True(result.Succeeded);
    }

    [Theory]
    [InlineData("")]
    [InlineData("auto")]
    [InlineData("Dark")]
    public void Validate_WhenModeIsInvalid_Fails(string mode)
    {
        var options = CreateValidOptions();
        options.Branding.Mode = mode;

        var result = CreateValidator().Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains(
            $"ManagementConsole:Branding:Mode must be 'light' or 'dark'; it is '{mode}'.",
            result.Failures ?? []
        );
    }

    [Theory]
    [InlineData("light")]
    [InlineData("dark")]
    public void Validate_WhenModeIsLightOrDark_Succeeds(string mode)
    {
        var options = CreateValidOptions();
        options.Branding.Mode = mode;

        var result = CreateValidator().Validate(null, options);

        Assert.True(result.Succeeded);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_WhenAFeatureHasNoName_Fails(string feature)
    {
        var options = CreateValidOptions();
        options.Features[feature] = true;

        var result = CreateValidator().Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains(
            $"ManagementConsole:Features has a feature without a name; it is '{feature}'.",
            result.Failures ?? []
        );
    }

    #endregion
}
