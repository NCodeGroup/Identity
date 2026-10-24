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

using Microsoft.Extensions.Options;

namespace NCode.Identity.OpenId.Tenants.Strategies;

/// <summary>
/// Validates <see cref="TenantResolutionOptions.StaticSingle"/> when the static single-tenant strategy is selected: a
/// literal tenant path, a valid tenant identifier, and an issuer — required, because the tenant is served only beneath it
/// — that is valid and ends with the tenant path. The strategy serves no other tenant, so a configured root tenant issuer
/// would never be honored and is rejected. The options of an unselected strategy are inert, and a layered configuration
/// may legitimately carry them, so they are skipped.
/// </summary>
// See ADR-0137 (fail at startup), ADR-0138 (the tenant route and the issuer), ADR-0160 (the same rules apply to tenants
// written at runtime), and ADR-0165 (every served tenant has an issuer).
internal class StaticSingleTenantOptionsValidator : IValidateOptions<TenantResolutionOptions>
{
    private const string Prefix = OpenIdConstants.TenantStrategyCodes.StaticSingle;

    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, TenantResolutionOptions options)
    {
        if (!string.Equals(options.StrategyCode, Prefix, StringComparison.Ordinal))
            return ValidateOptionsResult.Skip;

        var staticSingle = options.StaticSingle ?? new StaticSingleTenantOptions();
        var failures = new List<string>();

        // An empty identifier selects the default identifier, so only a configured one is checked.
        var tenantId = staticSingle.TenantId;
        if (!string.IsNullOrEmpty(tenantId) && !TenantAddressRules.IsValidTenantId(tenantId))
        {
            failures.Add(
                $"{Prefix}:{nameof(staticSingle.TenantId)} '{tenantId}' must be "
                    + $"{TenantAddressRules.TenantIdRequirement}."
            );
        }

        var tenantPath = staticSingle.TenantPath;
        var tenantPathKey = $"{Prefix}:{nameof(staticSingle.TenantPath)}";
        if (TenantAddressRules.TryParseLiteralPath(tenantPath, tenantPathKey, failures))
            ValidateIssuer(staticSingle, failures);

        var alias = staticSingle.MtlsEndpointAliasAuthority;
        if (
            !string.IsNullOrEmpty(alias)
            && !TenantAddressRules.TryParseMtlsEndpointAliasAuthority(alias, out _)
        )
        {
            failures.Add(
                $"{Prefix}:{nameof(staticSingle.MtlsEndpointAliasAuthority)} '{alias}' must be "
                    + $"{TenantAddressRules.MtlsEndpointAliasAuthorityRequirement}."
            );
        }

        if (!string.IsNullOrEmpty(options.Root?.MtlsEndpointAliasAuthority))
        {
            failures.Add(
                $"{nameof(options.Root)}:{nameof(RootTenantOptions.MtlsEndpointAliasAuthority)} has no effect under the "
                    + $"{Prefix} strategy, which serves only its own tenant; configure "
                    + $"{Prefix}:{nameof(staticSingle.MtlsEndpointAliasAuthority)} instead."
            );
        }

        if (!string.IsNullOrEmpty(options.Root?.Issuer))
        {
            failures.Add(
                $"{nameof(options.Root)}:{nameof(RootTenantOptions.Issuer)} has no effect under the {Prefix} "
                    + $"strategy, which serves only its own tenant; configure {Prefix}:{nameof(staticSingle.Issuer)} "
                    + "instead."
            );
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }

    private static void ValidateIssuer(StaticSingleTenantOptions options, List<string> failures)
    {
        var issuer = options.Issuer;
        var issuerKey = $"{Prefix}:{nameof(options.Issuer)}";
        if (string.IsNullOrEmpty(issuer))
        {
            failures.Add(
                $"{issuerKey} is required: the tenant is served only beneath its issuer, the origin of every URL the "
                    + "server publishes."
            );
            return;
        }

        if (!TenantAddressRules.TryParseIssuer(issuer, out var issuerUri))
        {
            failures.Add($"{issuerKey} '{issuer}' must be {TenantAddressRules.IssuerRequirement}.");
            return;
        }

        var tenantPath = options.TenantPath;
        if (!TenantAddressRules.IssuerEndsWithRoutePath(issuerUri, tenantPath.Value))
        {
            failures.Add(
                $"{issuerKey} '{issuer}' must end with the tenant path '{tenantPath}', the path the tenant's "
                    + "endpoints and discovery document are served under."
            );
        }
    }
}
