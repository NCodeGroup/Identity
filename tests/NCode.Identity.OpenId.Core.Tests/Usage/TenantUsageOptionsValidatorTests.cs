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

    #endregion
}
