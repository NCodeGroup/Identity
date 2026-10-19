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
using Xunit;

namespace NCode.Identity.OpenId.Core.Tenants;

public sealed class TenantResolutionOptionsValidatorTests
{
    private static TenantResolutionOptionsValidator Validator { get; } = new();

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
    public void Validate_WhenStrategyCodeIsEmpty_Fails()
    {
        var failures = Failures(new TenantResolutionOptions { StrategyCode = string.Empty });

        Assert.Contains(
            nameof(TenantResolutionOptions.StrategyCode),
            failures,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void Validate_WhenRootTenantIdIsEmpty_Fails()
    {
        var failures = Failures(new TenantResolutionOptions { RootTenantId = string.Empty });

        Assert.Contains(
            nameof(TenantResolutionOptions.RootTenantId),
            failures,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void Validate_WhenStrategyIsCustom_SkipsBuiltInStrategyOptions()
    {
        var options = new TenantResolutionOptions
        {
            StrategyCode = "Custom",
            DynamicByPath = new DynamicByPathTenantOptions { TenantPath = "/fixed" },
        };

        var result = Validator.Validate(name: null, options);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_WhenUnselectedStrategyOptionsAreInvalid_Succeeds()
    {
        // A layered configuration may carry another strategy's options; only the selected strategy's are in force.
        var options = new TenantResolutionOptions
        {
            StrategyCode = OpenIdConstants.TenantStrategyCodes.StaticSingle,
            DynamicByPath = new DynamicByPathTenantOptions { TenantPath = "/fixed" },
        };

        var result = Validator.Validate(name: null, options);

        Assert.True(result.Succeeded);
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/identity")]
    [InlineData("/identity/v1")]
    public void Validate_WhenStaticSingleTenantPathIsLiteral_Succeeds(string tenantPath)
    {
        var options = new TenantResolutionOptions
        {
            StaticSingle = new StaticSingleTenantOptions { TenantPath = tenantPath },
        };

        var result = Validator.Validate(name: null, options);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_WhenStaticSingleTenantPathHasParameter_Fails()
    {
        var options = new TenantResolutionOptions
        {
            StaticSingle = new StaticSingleTenantOptions { TenantPath = "/{tenantId}" },
        };

        var failures = Failures(options);

        Assert.Contains("StaticSingle:TenantPath", failures, StringComparison.Ordinal);
        Assert.Contains("literal path", failures, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_WhenStaticSingleTenantPathIsMalformed_Fails()
    {
        var options = new TenantResolutionOptions
        {
            StaticSingle = new StaticSingleTenantOptions { TenantPath = "/{tenantId" },
        };

        var failures = Failures(options);

        Assert.Contains("not a valid route pattern", failures, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("https://op.example", null)]
    [InlineData("https://op.example", "/")]
    [InlineData("https://op.example/identity", "/identity")]
    [InlineData("https://op.example/identity/", "/identity")]
    [InlineData("https://op.example/auth/identity", "/identity")]
    [InlineData("https://op.example/Identity", "/identity")]
    [InlineData("http://localhost:5000", null)]
    public void Validate_WhenStaticSingleIssuerEndsWithTenantPath_Succeeds(
        string issuer,
        string? tenantPath
    )
    {
        var options = new TenantResolutionOptions
        {
            StaticSingle = new StaticSingleTenantOptions
            {
                Issuer = issuer,
                TenantPath = tenantPath,
            },
        };

        var result = Validator.Validate(name: null, options);

        Assert.True(result.Succeeded);
    }

    [Theory]
    [InlineData("https://op.example", "/identity")]
    [InlineData("https://op.example/myidentity", "/identity")]
    [InlineData("https://op.example/identity/v2", "/identity")]
    public void Validate_WhenStaticSingleIssuerDoesNotEndWithTenantPath_Fails(
        string issuer,
        string tenantPath
    )
    {
        var options = new TenantResolutionOptions
        {
            StaticSingle = new StaticSingleTenantOptions
            {
                Issuer = issuer,
                TenantPath = tenantPath,
            },
        };

        var failures = Failures(options);

        Assert.Contains("StaticSingle:Issuer", failures, StringComparison.Ordinal);
        Assert.Contains("must end with the tenant path", failures, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("op.example")]
    [InlineData("/identity")]
    [InlineData("ftp://op.example")]
    [InlineData("https://op.example?tenant=a")]
    [InlineData("https://op.example#a")]
    public void Validate_WhenStaticSingleIssuerIsNotAnHttpUrl_Fails(string issuer)
    {
        var options = new TenantResolutionOptions
        {
            StaticSingle = new StaticSingleTenantOptions { Issuer = issuer },
        };

        var failures = Failures(options);

        Assert.Contains("absolute http or https URL", failures, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_WhenDynamicByHostRegexIsInvalid_Fails()
    {
        var options = new TenantResolutionOptions
        {
            StrategyCode = OpenIdConstants.TenantStrategyCodes.DynamicByHost,
            DynamicByHost = new DynamicByHostTenantOptions { RegexPattern = "([a-z" },
        };

        var failures = Failures(options);

        Assert.Contains("DynamicByHost:RegexPattern", failures, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_WhenDynamicByHostTenantPathHasParameter_Fails()
    {
        var options = new TenantResolutionOptions
        {
            StrategyCode = OpenIdConstants.TenantStrategyCodes.DynamicByHost,
            DynamicByHost = new DynamicByHostTenantOptions { TenantPath = "/{tenantId}" },
        };

        var failures = Failures(options);

        Assert.Contains("DynamicByHost:TenantPath", failures, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_WhenDynamicByPathDefaults_Succeeds()
    {
        var options = new TenantResolutionOptions
        {
            StrategyCode = OpenIdConstants.TenantStrategyCodes.DynamicByPath,
        };

        var result = Validator.Validate(name: null, options);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_WhenDynamicByPathRouteLacksTheTenantParameter_Fails()
    {
        var options = new TenantResolutionOptions
        {
            StrategyCode = OpenIdConstants.TenantStrategyCodes.DynamicByPath,
            DynamicByPath = new DynamicByPathTenantOptions
            {
                TenantPath = "/tenants/{id}",
                TenantIdRouteParameterName = "tenantId",
            },
        };

        var failures = Failures(options);

        Assert.Contains("'{tenantId}'", failures, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_WhenDynamicByPathParameterNameIsEmpty_Fails()
    {
        var options = new TenantResolutionOptions
        {
            StrategyCode = OpenIdConstants.TenantStrategyCodes.DynamicByPath,
            DynamicByPath = new DynamicByPathTenantOptions
            {
                TenantIdRouteParameterName = string.Empty,
            },
        };

        var failures = Failures(options);

        Assert.Contains(
            "DynamicByPath:TenantIdRouteParameterName",
            failures,
            StringComparison.Ordinal
        );
    }

    #endregion
}
