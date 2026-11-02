// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.OpenId.Authentication.Persistence;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Persistence;

public sealed class GrantRetentionOptionsValidatorTests
{
    private GrantRetentionOptionsValidator Validator { get; } = new();

    #region Validate Tests

    [Fact]
    public void Validate_WhenTheDefaultsAreKept_Succeeds()
    {
        var options = new GrantRetentionOptions();

        var result = Validator.Validate(null, options);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_WhenTheRetentionIsZero_Succeeds()
    {
        var options = new GrantRetentionOptions { Retention = TimeSpan.Zero };

        var result = Validator.Validate(null, options);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_WhenTheRetentionIsNegative_FailsNamingItsKey()
    {
        var options = new GrantRetentionOptions { Retention = TimeSpan.FromDays(-1) };

        var result = Validator.Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains(
            "'GrantRetention:Retention'",
            result.FailureMessage,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void Validate_WhenThePruneIntervalIsZero_FailsNamingItsKey()
    {
        var options = new GrantRetentionOptions { PruneInterval = TimeSpan.Zero };

        var result = Validator.Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains(
            "'GrantRetention:PruneInterval'",
            result.FailureMessage,
            StringComparison.Ordinal
        );
    }

    #endregion
}
