// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.Secrets.AwsKms.Options;
using Xunit;

namespace NCode.Identity.Secrets.AwsKms.Tests.Options;

public class AwsKmsRemoteKeyProviderOptionsValidatorTests
{
    private static AwsKmsRemoteKeyProviderOptionsValidator CreateValidator() => new();

    #region Validate Tests

    [Fact]
    public void Validate_RegionAndDefaultWindow_Succeeds()
    {
        var validator = CreateValidator();

        var result = validator.Validate(
            null,
            new AwsKmsRemoteKeyProviderOptions { Region = "us-west-2" }
        );

        Assert.True(result.Succeeded);
    }

    [Theory]
    [InlineData("us-west-2")]
    [InlineData("eu-central-1")]
    [InlineData("us-gov-west-1")]
    public void Validate_RegionNames_Succeed(string region)
    {
        var validator = CreateValidator();

        var result = validator.Validate(
            null,
            new AwsKmsRemoteKeyProviderOptions { Region = region }
        );

        Assert.True(result.Succeeded);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("global")]
    [InlineData("US-WEST-2")]
    [InlineData("us-west-2\n")]
    [InlineData("us--west-2")]
    [InlineData("-us-west-2")]
    public void Validate_InvalidRegion_Fails(string? region)
    {
        var validator = CreateValidator();

        var result = validator.Validate(
            null,
            new AwsKmsRemoteKeyProviderOptions { Region = region }
        );

        Assert.True(result.Failed);
        Assert.Contains("Region", result.FailureMessage);
    }

    [Theory]
    [InlineData(7)]
    [InlineData(14)]
    [InlineData(30)]
    public void Validate_PendingWindowInRange_Succeeds(int days)
    {
        var validator = CreateValidator();

        var result = validator.Validate(
            null,
            new AwsKmsRemoteKeyProviderOptions { Region = "us-west-2", PendingWindowInDays = days }
        );

        Assert.True(result.Succeeded);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(6)]
    [InlineData(31)]
    public void Validate_PendingWindowOutOfRange_Fails(int days)
    {
        var validator = CreateValidator();

        var result = validator.Validate(
            null,
            new AwsKmsRemoteKeyProviderOptions { Region = "us-west-2", PendingWindowInDays = days }
        );

        Assert.True(result.Failed);
        Assert.Contains("PendingWindowInDays", result.FailureMessage);
    }

    [Fact]
    public void Validate_RegionAndWindowInvalid_ReportsBoth()
    {
        var validator = CreateValidator();

        var result = validator.Validate(
            null,
            new AwsKmsRemoteKeyProviderOptions { Region = null, PendingWindowInDays = 1 }
        );

        Assert.True(result.Failed);
        Assert.Contains("Region", result.FailureMessage);
        Assert.Contains("PendingWindowInDays", result.FailureMessage);
    }

    [Fact]
    public void Validate_NullOptions_Throws()
    {
        var validator = CreateValidator();

        Assert.Throws<ArgumentNullException>(() => validator.Validate(null, null!));
    }

    #endregion
}
