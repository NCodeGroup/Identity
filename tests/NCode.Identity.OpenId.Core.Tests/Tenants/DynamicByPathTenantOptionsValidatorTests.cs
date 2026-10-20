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

public sealed class DynamicByPathTenantOptionsValidatorTests
{
    private static DynamicByPathTenantOptionsValidator Validator { get; } = new();

    private static TenantResolutionOptions Selected(
        DynamicByPathTenantOptions? dynamicByPath = null
    ) =>
        new()
        {
            StrategyCode = OpenIdConstants.TenantStrategyCodes.DynamicByPath,
            DynamicByPath = dynamicByPath,
        };

    private static string Failures(TenantResolutionOptions options)
    {
        var result = Validator.Validate(name: null, options);
        Assert.True(result.Failed, "Expected the options to be rejected.");
        return result.FailureMessage;
    }

    #region Validate Tests

    [Theory]
    [InlineData("/{tenantId}")]
    [InlineData("/tenants/{tenantId}")]
    public void Validate_WhenRouteCarriesOnlyThePlainTenantParameter_Succeeds(string tenantPath)
    {
        var result = Validator.Validate(
            name: null,
            Selected(new DynamicByPathTenantOptions { TenantPath = tenantPath })
        );

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_WhenDefaults_Succeeds()
    {
        var result = Validator.Validate(name: null, Selected());

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_WhenAnotherStrategyIsSelected_Skips()
    {
        // A layered configuration may carry another strategy's options; only the selected strategy's are in force.
        var options = new TenantResolutionOptions
        {
            DynamicByPath = new DynamicByPathTenantOptions { TenantPath = "/fixed" },
        };

        var result = Validator.Validate(name: null, options);

        Assert.True(result.Skipped);
    }

    [Fact]
    public void Validate_WhenRouteLacksTheTenantParameter_Fails()
    {
        var failures = Failures(
            Selected(
                new DynamicByPathTenantOptions
                {
                    TenantPath = "/tenants/{id}",
                    TenantIdRouteParameterName = "tenantId",
                }
            )
        );

        Assert.Contains("'{tenantId}'", failures, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_WhenRouteHasAnotherParameter_Fails()
    {
        var failures = Failures(
            Selected(new DynamicByPathTenantOptions { TenantPath = "/{region}/{tenantId}" })
        );

        Assert.Contains("no other parameter", failures, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("/{tenantId:alpha}")]
    [InlineData("/{tenantId?}")]
    [InlineData("/{tenantId=default}")]
    [InlineData("/{*tenantId}")]
    public void Validate_WhenTenantParameterIsNotPlain_Fails(string tenantPath)
    {
        var failures = Failures(
            Selected(new DynamicByPathTenantOptions { TenantPath = tenantPath })
        );

        Assert.Contains("plain parameter", failures, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_WhenRouteIsMalformed_Fails()
    {
        var failures = Failures(
            Selected(new DynamicByPathTenantOptions { TenantPath = "/{tenantId" })
        );

        Assert.Contains("not a valid route pattern", failures, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_WhenParameterNameIsEmpty_Fails()
    {
        var failures = Failures(
            Selected(new DynamicByPathTenantOptions { TenantIdRouteParameterName = string.Empty })
        );

        Assert.Contains(
            "DynamicByPath:TenantIdRouteParameterName",
            failures,
            StringComparison.Ordinal
        );
    }

    #endregion
}
