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
    public void Validate_WithZeroRequestTimeout_Fails()
    {
        var result = Validator.Validate(
            null,
            new OpenIdClientOptions { RequestTimeout = TimeSpan.Zero }
        );

        Assert.True(result.Failed);
        Assert.Contains(nameof(OpenIdClientOptions.RequestTimeout), result.FailureMessage);
    }

    [Fact]
    public void Validate_WithRequestTimeoutOverTheMaximum_Fails()
    {
        var result = Validator.Validate(
            null,
            new OpenIdClientOptions { RequestTimeout = TimeSpan.FromMinutes(6) }
        );

        Assert.True(result.Failed);
        Assert.Contains(nameof(OpenIdClientOptions.RequestTimeout), result.FailureMessage);
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

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Validate_WhenIdTokenSigningAlgorithmsBlank_Fails(string? algorithm)
    {
        var result = Validator.Validate(
            null,
            new OpenIdClientOptions { IdTokenSigningAlgorithms = ["ES256", algorithm!] }
        );

        Assert.True(result.Failed);
        Assert.Contains(
            nameof(OpenIdClientOptions.IdTokenSigningAlgorithms),
            result.FailureMessage
        );
    }

    [Theory]
    [InlineData("none")]
    [InlineData("NONE")]
    public void Validate_WhenIdTokenSigningAlgorithmsNone_Fails(string algorithm)
    {
        var result = Validator.Validate(
            null,
            new OpenIdClientOptions { IdTokenSigningAlgorithms = [algorithm] }
        );

        Assert.True(result.Failed);
        Assert.Contains(
            nameof(OpenIdClientOptions.IdTokenSigningAlgorithms),
            result.FailureMessage
        );
    }

    [Fact]
    public void Validate_WhenIdTokenSigningAlgorithmsDuplicate_Fails()
    {
        var result = Validator.Validate(
            null,
            new OpenIdClientOptions { IdTokenSigningAlgorithms = ["ES256", "ES256"] }
        );

        Assert.True(result.Failed);
        Assert.Contains(
            nameof(OpenIdClientOptions.IdTokenSigningAlgorithms),
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
