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
    private const string IssuerTemplate = "https://{tenantId}.auth.example";

    private static DynamicByHostTenantOptionsValidator Validator { get; } = new();

    // A host strategy needs an address for the root tenant, which the issuer template supplies by default.
    private static TenantResolutionOptions Selected(
        DynamicByHostTenantOptions? dynamicByHost = null,
        string? rootIssuer = null
    ) =>
        new()
        {
            StrategyCode = OpenIdConstants.TenantStrategyCodes.DynamicByHost,
            Root = { Issuer = rootIssuer },
            DynamicByHost =
                dynamicByHost ?? new DynamicByHostTenantOptions { IssuerTemplate = IssuerTemplate },
        };

    private static string Failures(TenantResolutionOptions options)
    {
        var result = Validator.Validate(name: null, options);
        Assert.True(result.Failed, "Expected the options to be rejected.");
        return result.FailureMessage;
    }

    #region Validate Tests

    [Fact]
    public void Validate_WhenIssuerTemplateIsConfigured_Succeeds()
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
            Selected(
                new DynamicByHostTenantOptions
                {
                    RegexPattern = "([a-z",
                    IssuerTemplate = IssuerTemplate,
                }
            )
        );

        Assert.Contains("DynamicByHost:RegexPattern", failures, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_WhenTenantPathHasParameter_Fails()
    {
        var failures = Failures(
            Selected(
                new DynamicByHostTenantOptions
                {
                    TenantPath = "/{tenantId}",
                    IssuerTemplate = IssuerTemplate,
                }
            )
        );

        Assert.Contains("DynamicByHost:TenantPath", failures, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_WhenRootTenantHasNoAddress_Fails()
    {
        var failures = Failures(Selected(new DynamicByHostTenantOptions()));

        Assert.Contains("The root tenant needs an address", failures, StringComparison.Ordinal);
        Assert.Contains("Root:Issuer", failures, StringComparison.Ordinal);
        Assert.Contains("DynamicByHost:IssuerTemplate", failures, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_WhenOnlyTheRootIssuerIsConfigured_Succeeds()
    {
        var result = Validator.Validate(
            name: null,
            Selected(new DynamicByHostTenantOptions(), rootIssuer: "https://auth.example")
        );

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_WhenRootIssuerDoesNotEndWithTheTenantPath_Fails()
    {
        var failures = Failures(
            Selected(
                new DynamicByHostTenantOptions { TenantPath = "/identity" },
                rootIssuer: "https://auth.example"
            )
        );

        Assert.Contains("Root:Issuer 'https://auth.example'", failures, StringComparison.Ordinal);
        Assert.Contains("'/identity'", failures, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_WhenRootIssuerHostIsNotMappedToItself_Fails()
    {
        var failures = Failures(
            Selected(
                new DynamicByHostTenantOptions { RegexPattern = "^[^.]+" },
                rootIssuer: "https://auth.example"
            )
        );

        Assert.Contains(
            "maps the host 'auth.example' to 'auth'",
            failures,
            StringComparison.Ordinal
        );
    }

    [Theory]
    [InlineData("https://auth.example/{tenantId}")]
    [InlineData("https://auth.example/?tenant={tenantId}")]
    public void Validate_WhenIssuerTemplateDoesNotVaryTheHost_Fails(string issuerTemplate)
    {
        var failures = Failures(
            Selected(new DynamicByHostTenantOptions { IssuerTemplate = issuerTemplate })
        );

        Assert.Contains("DynamicByHost:IssuerTemplate", failures, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("https://auth.example")]
    [InlineData("https://{tenantId}.{tenantId}.example")]
    [InlineData("https://{tenantId}.auth.example/{region}")]
    [InlineData("http://{tenantId}.auth.example")]
    [InlineData("{tenantId}.auth.example")]
    public void Validate_WhenIssuerTemplateIsMalformed_Fails(string issuerTemplate)
    {
        var failures = Failures(
            Selected(new DynamicByHostTenantOptions { IssuerTemplate = issuerTemplate })
        );

        Assert.Contains(
            $"DynamicByHost:IssuerTemplate '{issuerTemplate}' must be",
            failures,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void Validate_WhenIssuerTemplateEmbedsTheIdentifierInALongerLabel_Succeeds()
    {
        var result = Validator.Validate(
            name: null,
            Selected(
                new DynamicByHostTenantOptions { IssuerTemplate = "https://{tenantId}-dev.example" }
            )
        );

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_WhenMtlsEndpointAliasTemplateIsAPerTenantOrigin_Succeeds()
    {
        var result = Validator.Validate(
            name: null,
            Selected(
                new DynamicByHostTenantOptions
                {
                    IssuerTemplate = IssuerTemplate,
                    MtlsEndpointAliasTemplate = "https://{tenantId}.mtls.example",
                }
            )
        );

        Assert.True(result.Succeeded);
    }

    [Theory]
    [InlineData("https://{tenantId}.mtls.example/oauth2")]
    [InlineData("http://{tenantId}.mtls.example")]
    [InlineData("https://mtls.example")]
    [InlineData("https://{tenantId}.{region}.example")]
    public void Validate_WhenMtlsEndpointAliasTemplateIsNotAnOriginTemplate_Fails(
        string aliasTemplate
    )
    {
        var failures = Failures(
            Selected(
                new DynamicByHostTenantOptions
                {
                    IssuerTemplate = IssuerTemplate,
                    MtlsEndpointAliasTemplate = aliasTemplate,
                }
            )
        );

        Assert.Contains(
            $"DynamicByHost:MtlsEndpointAliasTemplate '{aliasTemplate}' must be",
            failures,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void Validate_WhenHostPatternMapsTheAliasHostElsewhere_Fails()
    {
        var failures = Failures(
            Selected(
                new DynamicByHostTenantOptions
                {
                    RegexPattern = "^[^.]+\\.auth\\.example",
                    IssuerTemplate = "https://{tenantId}.auth.example",
                    MtlsEndpointAliasTemplate = "https://{tenantId}.auth.example.mtls.example",
                }
            )
        );

        Assert.Contains(
            "DynamicByHost:MtlsEndpointAliasTemplate 'https://{tenantId}.auth.example.mtls.example' does not reach "
                + "its tenant",
            failures,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void Validate_WhenMtlsEndpointAliasTemplateGivesTheIssuerHost_Fails()
    {
        var failures = Failures(
            Selected(
                new DynamicByHostTenantOptions
                {
                    IssuerTemplate = "https://{tenantId}.auth.example",
                    MtlsEndpointAliasTemplate = "https://{tenantId}.auth.example:8443",
                }
            )
        );

        Assert.Contains("a host other than its issuer host", failures, StringComparison.Ordinal);
    }

    #endregion
}
