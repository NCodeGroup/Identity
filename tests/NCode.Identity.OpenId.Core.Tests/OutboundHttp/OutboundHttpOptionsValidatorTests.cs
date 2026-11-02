// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.OpenId.Authentication.OutboundHttp;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.OutboundHttp;

public sealed class OutboundHttpOptionsValidatorTests
{
    private OutboundHttpOptionsValidator Validator { get; } = new();

    #region Validate Tests

    [Fact]
    public void Validate_WhenTheDefaultsAreKept_Succeeds()
    {
        var options = new OutboundHttpOptions();

        var result = Validator.Validate(null, options);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_WhenTheAttemptTimeoutIsBelowTenMilliseconds_FailsNamingItsKey()
    {
        var options = new OutboundHttpOptions { AttemptTimeout = TimeSpan.FromMilliseconds(9) };

        var result = Validator.Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains(
            "'OutboundHttp:AttemptTimeout'",
            result.FailureMessage,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void Validate_WhenTheAttemptTimeoutExceedsTheTotalTimeout_FailsNamingBothKeys()
    {
        var options = new OutboundHttpOptions
        {
            AttemptTimeout = TimeSpan.FromSeconds(20),
            TotalTimeout = TimeSpan.FromSeconds(10),
        };

        var result = Validator.Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains(
            "'OutboundHttp:AttemptTimeout'",
            result.FailureMessage,
            StringComparison.Ordinal
        );
        Assert.Contains(
            "'OutboundHttp:TotalTimeout'",
            result.FailureMessage,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void Validate_WhenMaxRetryAttemptsIsNegative_FailsNamingItsKey()
    {
        var options = new OutboundHttpOptions { MaxRetryAttempts = -1 };

        var result = Validator.Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains(
            "'OutboundHttp:MaxRetryAttempts'",
            result.FailureMessage,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void Validate_WhenTheRetryDelayIsNegative_FailsNamingItsKey()
    {
        var options = new OutboundHttpOptions { RetryDelay = TimeSpan.FromSeconds(-1) };

        var result = Validator.Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains(
            "'OutboundHttp:RetryDelay'",
            result.FailureMessage,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void Validate_WhenTheFailureCacheDurationIsZero_FailsNamingItsKey()
    {
        var options = new OutboundHttpOptions { FailureCacheDuration = TimeSpan.Zero };

        var result = Validator.Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains(
            "'OutboundHttp:FailureCacheDuration'",
            result.FailureMessage,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void Validate_WhenTheFailureCacheDurationIsOverOneDay_FailsNamingItsKey()
    {
        var options = new OutboundHttpOptions
        {
            FailureCacheDuration = TimeSpan.FromDays(1) + TimeSpan.FromSeconds(1),
        };

        var result = Validator.Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains(
            "'OutboundHttp:FailureCacheDuration'",
            result.FailureMessage,
            StringComparison.Ordinal
        );
    }

    #endregion
}
