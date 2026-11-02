// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.OpenId.Client.Options;
using Xunit;

namespace NCode.Identity.OpenId.Client.Tests.Options;

public class OpenIdClientOptionsValidatorTests : BaseTests
{
    private OpenIdClientOptionsValidator Validator { get; } = new();

    #region Validate Tests

    [Fact]
    public void Validate_WithDefaults_Succeeds()
    {
        var result = Validator.Validate(null, new OpenIdClientOptions());

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_WithZeroTotalTimeout_Fails()
    {
        var result = Validator.Validate(
            null,
            new OpenIdClientOptions { TotalTimeout = TimeSpan.Zero }
        );

        Assert.True(result.Failed);
        Assert.Contains(nameof(OpenIdClientOptions.TotalTimeout), result.FailureMessage);
    }

    [Fact]
    public void Validate_WithTotalTimeoutOverTheMaximum_Fails()
    {
        var result = Validator.Validate(
            null,
            new OpenIdClientOptions { TotalTimeout = TimeSpan.FromMinutes(6) }
        );

        Assert.True(result.Failed);
        Assert.Contains(nameof(OpenIdClientOptions.TotalTimeout), result.FailureMessage);
    }

    [Fact]
    public void Validate_WithNonPositiveMetadataRefreshInterval_Fails()
    {
        var result = Validator.Validate(
            null,
            new OpenIdClientOptions { MetadataRefreshInterval = TimeSpan.Zero }
        );

        Assert.True(result.Failed);
        Assert.Contains(nameof(OpenIdClientOptions.MetadataRefreshInterval), result.FailureMessage);
    }

    [Fact]
    public void Validate_WithNonPositiveMinimumRefreshInterval_Fails()
    {
        var result = Validator.Validate(
            null,
            new OpenIdClientOptions { MinimumRefreshInterval = TimeSpan.Zero }
        );

        Assert.True(result.Failed);
        Assert.Contains(nameof(OpenIdClientOptions.MinimumRefreshInterval), result.FailureMessage);
    }

    [Fact]
    public void Validate_WithNonPositiveStaleLifetime_Fails()
    {
        var result = Validator.Validate(
            null,
            new OpenIdClientOptions { StaleLifetime = TimeSpan.Zero }
        );

        Assert.True(result.Failed);
        Assert.Contains(nameof(OpenIdClientOptions.StaleLifetime), result.FailureMessage);
    }

    [Fact]
    public void Validate_WithStaleLifetimeBelowRefreshInterval_Fails()
    {
        var result = Validator.Validate(
            null,
            new OpenIdClientOptions
            {
                MetadataRefreshInterval = TimeSpan.FromHours(2),
                StaleLifetime = TimeSpan.FromHours(1),
            }
        );

        Assert.True(result.Failed);
        Assert.Contains(nameof(OpenIdClientOptions.StaleLifetime), result.FailureMessage);
    }

    [Fact]
    public void Validate_WithTinyMaxResponseSize_Fails()
    {
        var result = Validator.Validate(
            null,
            new OpenIdClientOptions { MaxResponseSizeBytes = 100 }
        );

        Assert.True(result.Failed);
        Assert.Contains(nameof(OpenIdClientOptions.MaxResponseSizeBytes), result.FailureMessage);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Validate_WithBlankSigningAlgorithm_Fails(string? algorithm)
    {
        var result = Validator.Validate(
            null,
            new OpenIdClientOptions { ClientAssertionSigningAlgorithms = ["ES256", algorithm!] }
        );

        Assert.True(result.Failed);
        Assert.Contains(
            nameof(OpenIdClientOptions.ClientAssertionSigningAlgorithms),
            result.FailureMessage
        );
    }

    [Theory]
    [InlineData("none")]
    [InlineData("NONE")]
    public void Validate_WithNoneSigningAlgorithm_Fails(string algorithm)
    {
        var result = Validator.Validate(
            null,
            new OpenIdClientOptions { ClientAssertionSigningAlgorithms = [algorithm] }
        );

        Assert.True(result.Failed);
        Assert.Contains(
            nameof(OpenIdClientOptions.ClientAssertionSigningAlgorithms),
            result.FailureMessage
        );
    }

    [Fact]
    public void Validate_WithDuplicateSigningAlgorithm_Fails()
    {
        var result = Validator.Validate(
            null,
            new OpenIdClientOptions { ClientAssertionSigningAlgorithms = ["ES256", "ES256"] }
        );

        Assert.True(result.Failed);
        Assert.Contains(
            nameof(OpenIdClientOptions.ClientAssertionSigningAlgorithms),
            result.FailureMessage
        );
    }

    [Fact]
    public void Validate_WithValidSigningAlgorithms_Succeeds()
    {
        var result = Validator.Validate(
            null,
            new OpenIdClientOptions { ClientAssertionSigningAlgorithms = ["ES256", "RS256"] }
        );

        Assert.True(result.Succeeded);
    }

    #endregion
}
