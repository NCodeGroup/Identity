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

public sealed class StaticSingleTenantOptionsValidatorTests
{
    private static StaticSingleTenantOptionsValidator Validator { get; } = new();

    private static TenantResolutionOptions Selected(StaticSingleTenantOptions staticSingle) =>
        new()
        {
            StrategyCode = OpenIdConstants.TenantStrategyCodes.StaticSingle,
            StaticSingle = staticSingle,
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
        var result = Validator.Validate(name: null, new TenantResolutionOptions());

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_WhenAnotherStrategyIsSelected_Skips()
    {
        // A layered configuration may carry another strategy's options; only the selected strategy's are in force.
        var options = new TenantResolutionOptions
        {
            StrategyCode = OpenIdConstants.TenantStrategyCodes.DynamicByPath,
            StaticSingle = new StaticSingleTenantOptions { TenantPath = "/{tenantId}" },
        };

        var result = Validator.Validate(name: null, options);

        Assert.True(result.Skipped);
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/identity")]
    [InlineData("/identity/v1")]
    public void Validate_WhenTenantPathIsLiteral_Succeeds(string tenantPath)
    {
        var result = Validator.Validate(
            name: null,
            Selected(new StaticSingleTenantOptions { TenantPath = tenantPath })
        );

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_WhenTenantPathHasParameter_Fails()
    {
        var failures = Failures(
            Selected(new StaticSingleTenantOptions { TenantPath = "/{tenantId}" })
        );

        Assert.Contains("StaticSingle:TenantPath", failures, StringComparison.Ordinal);
        Assert.Contains("literal path", failures, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_WhenTenantPathIsMalformed_Fails()
    {
        var failures = Failures(
            Selected(new StaticSingleTenantOptions { TenantPath = "/{tenantId" })
        );

        Assert.Contains("not a valid route pattern", failures, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_WhenTenantIdIsEmpty_SucceedsBecauseTheDefaultApplies()
    {
        var result = Validator.Validate(
            name: null,
            Selected(new StaticSingleTenantOptions { TenantId = string.Empty })
        );

        Assert.True(result.Succeeded);
    }

    [Theory]
    [InlineData("Default")]
    [InlineData("my_tenant")]
    [InlineData("tenant.one")]
    public void Validate_WhenTenantIdIsNotATenantIdentifier_Fails(string tenantId)
    {
        var failures = Failures(Selected(new StaticSingleTenantOptions { TenantId = tenantId }));

        Assert.Contains($"StaticSingle:TenantId '{tenantId}'", failures, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("https://op.example", null)]
    [InlineData("https://op.example", "/")]
    [InlineData("https://op.example/identity", "/identity")]
    [InlineData("https://op.example/identity/", "/identity")]
    [InlineData("https://op.example/auth/identity", "/identity")]
    [InlineData("https://op.example/Identity", "/identity")]
    [InlineData("http://localhost:5000", null)]
    [InlineData("http://127.0.0.1:5000/identity", "/identity")]
    public void Validate_WhenIssuerEndsWithTenantPath_Succeeds(string issuer, string? tenantPath)
    {
        var result = Validator.Validate(
            name: null,
            Selected(new StaticSingleTenantOptions { Issuer = issuer, TenantPath = tenantPath })
        );

        Assert.True(result.Succeeded);
    }

    [Theory]
    [InlineData("https://op.example", "/identity")]
    [InlineData("https://op.example/myidentity", "/identity")]
    [InlineData("https://op.example/identity/v2", "/identity")]
    public void Validate_WhenIssuerDoesNotEndWithTenantPath_Fails(string issuer, string tenantPath)
    {
        var failures = Failures(
            Selected(new StaticSingleTenantOptions { Issuer = issuer, TenantPath = tenantPath })
        );

        Assert.Contains("StaticSingle:Issuer", failures, StringComparison.Ordinal);
        Assert.Contains("must end with the tenant path", failures, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("op.example")]
    [InlineData("/identity")]
    [InlineData("ftp://op.example")]
    [InlineData("http://op.example")]
    [InlineData("https://user@op.example")]
    [InlineData("https://op.example?tenant=a")]
    [InlineData("https://op.example/?")]
    [InlineData("https://op.example#a")]
    public void Validate_WhenIssuerIsNotAValidIssuer_Fails(string issuer)
    {
        var failures = Failures(Selected(new StaticSingleTenantOptions { Issuer = issuer }));

        Assert.Contains("StaticSingle:Issuer", failures, StringComparison.Ordinal);
        Assert.Contains("absolute https URL", failures, StringComparison.Ordinal);
    }

    #endregion
}
