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

using NCode.Identity.OpenId.Tenants;
using NCode.Identity.OpenId.Tenants.Strategies;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tenants;

public sealed class DynamicByHostTenantOptionsValidatorTests
{
    private static DynamicByHostTenantOptionsValidator Validator { get; } = new();

    private static TenantResolutionOptions Selected(
        DynamicByHostTenantOptions? dynamicByHost = null
    ) =>
        new()
        {
            StrategyCode = OpenIdConstants.TenantStrategyCodes.DynamicByHost,
            DynamicByHost = dynamicByHost,
        };

    private static string Failures(TenantResolutionOptions options)
    {
        var result = Validator.Validate(name: null, options);
        Assert.True(result.Failed, "Expected the options to be rejected.");
        return result.FailureMessage;
    }

    #region Validate Tests

    [Fact]
    public void Validate_WhenDefaults_Succeeds()
    {
        var result = Validator.Validate(name: null, Selected());

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_WhenAnotherStrategyIsSelected_Skips()
    {
        var options = new TenantResolutionOptions
        {
            DynamicByHost = new DynamicByHostTenantOptions { RegexPattern = "([a-z" },
        };

        var result = Validator.Validate(name: null, options);

        Assert.True(result.Skipped);
    }

    [Fact]
    public void Validate_WhenRegexIsInvalid_Fails()
    {
        var failures = Failures(
            Selected(new DynamicByHostTenantOptions { RegexPattern = "([a-z" })
        );

        Assert.Contains("DynamicByHost:RegexPattern", failures, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_WhenTenantPathHasParameter_Fails()
    {
        var failures = Failures(
            Selected(new DynamicByHostTenantOptions { TenantPath = "/{tenantId}" })
        );

        Assert.Contains("DynamicByHost:TenantPath", failures, StringComparison.Ordinal);
    }

    #endregion
}
