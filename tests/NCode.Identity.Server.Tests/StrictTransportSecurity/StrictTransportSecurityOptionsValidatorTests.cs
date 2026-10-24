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

using NCode.Identity.Server.StrictTransportSecurity;
using Xunit;

namespace NCode.Identity.Server.Tests.StrictTransportSecurity;

public sealed class StrictTransportSecurityOptionsValidatorTests
{
    private static StrictTransportSecurityOptionsValidator Validator { get; } = new();

    #region Validate Tests

    [Fact]
    public void Validate_WhenDefaults_Succeeds()
    {
        var result = Validator.Validate(name: null, new StrictTransportSecurityOptions());

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_WhenMaxAgeIsZero_Succeeds()
    {
        var result = Validator.Validate(
            name: null,
            new StrictTransportSecurityOptions { MaxAge = TimeSpan.Zero }
        );

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_WhenMaxAgeIsNegative_FailsNamingIt()
    {
        var result = Validator.Validate(
            name: null,
            new StrictTransportSecurityOptions { MaxAge = TimeSpan.FromSeconds(-1) }
        );

        Assert.True(result.Failed);
        Assert.Contains(
            "StrictTransportSecurity:MaxAge",
            result.FailureMessage,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void Validate_WhenPreloadWithoutIncludeSubDomains_Fails()
    {
        var result = Validator.Validate(
            name: null,
            new StrictTransportSecurityOptions
            {
                Preload = true,
                IncludeSubDomains = false,
                MaxAge = TimeSpan.FromDays(365),
            }
        );

        Assert.True(result.Failed);
        Assert.Contains(
            "StrictTransportSecurity:Preload requires StrictTransportSecurity:IncludeSubDomains",
            result.FailureMessage,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void Validate_WhenPreloadWithAShortMaxAge_Fails()
    {
        var result = Validator.Validate(
            name: null,
            new StrictTransportSecurityOptions { Preload = true, MaxAge = TimeSpan.FromDays(30) }
        );

        Assert.True(result.Failed);
        Assert.Contains(
            "StrictTransportSecurity:Preload requires a StrictTransportSecurity:MaxAge",
            result.FailureMessage,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void Validate_WhenPreloadWithAYearAndSubdomains_Succeeds()
    {
        var result = Validator.Validate(
            name: null,
            new StrictTransportSecurityOptions
            {
                Preload = true,
                IncludeSubDomains = true,
                MaxAge = TimeSpan.FromDays(365),
            }
        );

        Assert.True(result.Succeeded);
    }

    [Theory]
    [InlineData("internal.example.com")]
    [InlineData("10.0.0.4")]
    [InlineData("[2001:db8::1]")]
    public void Validate_WhenAnExcludedHostIsAHostName_Succeeds(string host)
    {
        var result = Validator.Validate(
            name: null,
            new StrictTransportSecurityOptions { ExcludedHosts = [host] }
        );

        Assert.True(result.Succeeded);
    }

    [Theory]
    [InlineData("")]
    [InlineData("https://internal.example.com")]
    [InlineData("internal.example.com:443")]
    [InlineData("::1")]
    [InlineData("*.example.com")]
    public void Validate_WhenAnExcludedHostIsNotAHostName_FailsNamingTheEntry(string host)
    {
        var result = Validator.Validate(
            name: null,
            new StrictTransportSecurityOptions { ExcludedHosts = [host] }
        );

        Assert.True(result.Failed);
        var failure = Assert.Single(result.Failures);
        Assert.StartsWith(
            "StrictTransportSecurity:ExcludedHosts:0 '",
            failure,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void Validate_WhenSeveralEntriesAreInvalid_ReportsEachOne()
    {
        var result = Validator.Validate(
            name: null,
            new StrictTransportSecurityOptions
            {
                MaxAge = TimeSpan.FromSeconds(-1),
                ExcludedHosts = ["bad host"],
            }
        );

        Assert.Equal(2, result.Failures?.Count());
    }

    #endregion
}
