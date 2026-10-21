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
    public void Validate_WhenRootIsNull_Fails()
    {
        var failures = Failures(new TenantResolutionOptions { Root = null! });

        Assert.Contains("Root is required", failures, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_WhenRootTenantIdIsEmpty_Fails()
    {
        var failures = Failures(new TenantResolutionOptions { Root = { TenantId = string.Empty } });

        Assert.Contains("Root:TenantId is required", failures, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Root")]
    [InlineData("root_tenant")]
    [InlineData("-root")]
    public void Validate_WhenRootTenantIdIsNotATenantIdentifier_Fails(string rootTenantId)
    {
        var failures = Failures(new TenantResolutionOptions { Root = { TenantId = rootTenantId } });

        Assert.Contains($"Root:TenantId '{rootTenantId}'", failures, StringComparison.Ordinal);
        Assert.Contains("lowercase letters", failures, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_WhenRootDisplayNameIsEmpty_Fails()
    {
        var failures = Failures(
            new TenantResolutionOptions { Root = { DisplayName = string.Empty } }
        );

        Assert.Contains("Root:DisplayName is required", failures, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("auth.example")]
    [InlineData("http://auth.example")]
    [InlineData("https://auth.example?x=1")]
    public void Validate_WhenRootIssuerIsNotAnIssuer_Fails(string rootIssuer)
    {
        var failures = Failures(new TenantResolutionOptions { Root = { Issuer = rootIssuer } });

        Assert.Contains($"Root:Issuer '{rootIssuer}'", failures, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_WhenRootIssuerIsAnIssuer_Succeeds()
    {
        var result = Validator.Validate(
            name: null,
            new TenantResolutionOptions { Root = { Issuer = "https://auth.example" } }
        );

        Assert.True(result.Succeeded);
    }

    [Theory]
    [InlineData("mtls.example")]
    [InlineData("http://mtls.example")]
    [InlineData("https://mtls.example/path")]
    public void Validate_WhenRootMtlsEndpointAliasAuthorityIsNotAnOrigin_Fails(string rootAlias)
    {
        var failures = Failures(
            new TenantResolutionOptions { Root = { MtlsEndpointAliasAuthority = rootAlias } }
        );

        Assert.Contains(
            $"Root:MtlsEndpointAliasAuthority '{rootAlias}'",
            failures,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void Validate_WhenRootMtlsEndpointAliasAuthorityIsAnOrigin_Succeeds()
    {
        var result = Validator.Validate(
            name: null,
            new TenantResolutionOptions
            {
                Root = { MtlsEndpointAliasAuthority = "https://mtls.example" },
            }
        );

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_WhenStrategyOptionsAreInvalid_LeavesThemToTheStrategy()
    {
        // Each strategy validates its own options beside it, so this validator never needs to know which exist.
        var options = new TenantResolutionOptions
        {
            StrategyCode = OpenIdConstants.TenantStrategyCodes.DynamicByPath,
            DynamicByPath = new DynamicByPathTenantOptions { TenantPath = "/fixed" },
        };

        var result = Validator.Validate(name: null, options);

        Assert.True(result.Succeeded);
    }

    #endregion
}
