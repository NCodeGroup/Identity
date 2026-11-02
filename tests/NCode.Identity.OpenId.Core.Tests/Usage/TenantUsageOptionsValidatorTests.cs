// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.OpenId.Authentication.Usage;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Usage;

public sealed class TenantUsageOptionsValidatorTests
{
    private TenantUsageOptionsValidator Validator { get; } = new();

    #region Validate Tests

    [Fact]
    public void Validate_WhenTheDefaultsAreKept_Succeeds()
    {
        var options = new TenantUsageOptions();

        var result = Validator.Validate(null, options);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_WhenTheFlushIntervalIsZero_FailsNamingItsKey()
    {
        var options = new TenantUsageOptions { FlushInterval = TimeSpan.Zero };

        var result = Validator.Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains(
            "'TenantUsage:FlushInterval'",
            result.FailureMessage,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void Validate_WhenTheRetentionIsNegative_FailsNamingItsKey()
    {
        var options = new TenantUsageOptions { Retention = TimeSpan.FromDays(-1) };

        var result = Validator.Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains("'TenantUsage:Retention'", result.FailureMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_WhenThePruneIntervalIsZero_FailsNamingItsKey()
    {
        var options = new TenantUsageOptions { PruneInterval = TimeSpan.Zero };

        var result = Validator.Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains(
            "'TenantUsage:PruneInterval'",
            result.FailureMessage,
            StringComparison.Ordinal
        );
    }

    #endregion
}
