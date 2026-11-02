// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.OpenId.Authentication.Sessions;
using NCode.Time;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Sessions;

public sealed class EndedSessionTerminationOptionsValidatorTests
{
    private EndedSessionTerminationOptionsValidator Validator { get; } = new();

    #region Validate Tests

    [Fact]
    public void Validate_WhenTheDefaultIsKept_Succeeds()
    {
        var options = new EndedSessionTerminationOptions();

        var result = Validator.Validate(null, options);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_WhenTheIntervalIsZero_FailsNamingItsKey()
    {
        var options = new EndedSessionTerminationOptions { Interval = TimeSpan.Zero };

        var result = Validator.Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains(
            "'EndedSessionTermination:Interval'",
            result.FailureMessage,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void Validate_WhenTheIntervalExceedsWhatATimerAccepts_FailsNamingItsKey()
    {
        var options = new EndedSessionTerminationOptions
        {
            Interval = TimerPeriod.MaxValue + TimeSpan.FromMilliseconds(1),
        };

        var result = Validator.Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains(
            "'EndedSessionTermination:Interval'",
            result.FailureMessage,
            StringComparison.Ordinal
        );
    }

    #endregion
}
