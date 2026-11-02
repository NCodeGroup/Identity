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

    #endregion
}
