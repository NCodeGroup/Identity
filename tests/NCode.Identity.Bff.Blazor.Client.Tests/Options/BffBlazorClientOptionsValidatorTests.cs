// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.Bff.Blazor.Client.Options;
using Xunit;

namespace NCode.Identity.Bff.Blazor.Client.Tests.Options;

public class BffBlazorClientOptionsValidatorTests : BaseTests
{
    private static BffBlazorClientOptionsValidator CreateValidator() => new();

    #region Validate Tests

    [Fact]
    public void Validate_Defaults_Succeeds()
    {
        var result = CreateValidator().Validate(null, new BffBlazorClientOptions());

        Assert.True(result.Succeeded);
    }

    [Theory]
    [InlineData("")]
    [InlineData("bff/user")]
    [InlineData("//bff/user")]
    public void Validate_WhenUserPathIsNotOneRootedPath_Fails(string path)
    {
        var options = new BffBlazorClientOptions { UserPath = path };

        var result = CreateValidator().Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains(
            $"BffBlazorClientOptions.UserPath '{path}' must start with one '/' and not '//'.",
            result.Failures ?? []
        );
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void Validate_WhenAntiforgeryHeaderNameIsBlank_Fails(string value)
    {
        var options = new BffBlazorClientOptions { AntiforgeryHeaderName = value };

        var result = CreateValidator().Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains(
            "BffBlazorClientOptions.AntiforgeryHeaderName must not be blank.",
            result.Failures ?? []
        );
    }

    [Theory]
    [InlineData("Content-Type")]
    [InlineData("authorization")]
    [InlineData("DPoP")]
    public void Validate_WhenAntiforgeryHeaderNameIsForbidden_Fails(string value)
    {
        var options = new BffBlazorClientOptions { AntiforgeryHeaderName = value };

        var result = CreateValidator().Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains(
            $"BffBlazorClientOptions.AntiforgeryHeaderName '{value}' must not be a header that skips a CORS preflight or carries credentials.",
            result.Failures ?? []
        );
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void Validate_WhenAntiforgeryHeaderValueIsBlank_Fails(string value)
    {
        var options = new BffBlazorClientOptions { AntiforgeryHeaderValue = value };

        var result = CreateValidator().Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains(
            "BffBlazorClientOptions.AntiforgeryHeaderValue must not be blank.",
            result.Failures ?? []
        );
    }

    [Fact]
    public void Validate_WhenProbeIntervalIsNotATimerPeriod_Fails()
    {
        var options = new BffBlazorClientOptions { ProbeInterval = TimeSpan.Zero };

        var result = CreateValidator().Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains(
            $"BffBlazorClientOptions.ProbeInterval '{options.ProbeInterval}' must be a valid timer period.",
            result.Failures ?? []
        );
    }

    #endregion
}
