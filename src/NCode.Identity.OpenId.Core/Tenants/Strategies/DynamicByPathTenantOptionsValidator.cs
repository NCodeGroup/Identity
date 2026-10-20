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
/// Validates <see cref="TenantResolutionOptions.DynamicByPath"/> when the path-based strategy is selected. The tenant
/// route must carry the tenant's identifier as its only parameter, and as a plain one, so the route — and therefore the
/// issuer a tenant must end with — is determined by the tenant's identifier alone. The options of an unselected
/// strategy are inert, and a layered configuration may legitimately carry them, so they are skipped.
/// </summary>
// See ADR-0137 (fail at startup) and ADR-0160 (a path tenant's issuer is checked against its bound route).
internal class DynamicByPathTenantOptionsValidator : IValidateOptions<TenantResolutionOptions>
{
    private const string Prefix = OpenIdConstants.TenantStrategyCodes.DynamicByPath;

    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, TenantResolutionOptions options)
    {
        if (!string.Equals(options.StrategyCode, Prefix, StringComparison.Ordinal))
            return ValidateOptionsResult.Skip;

        var dynamicByPath = options.DynamicByPath ?? new DynamicByPathTenantOptions();
        var failures = new List<string>();

        ValidateTenantRoute(dynamicByPath, failures);

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }

    private static void ValidateTenantRoute(
        DynamicByPathTenantOptions options,
        List<string> failures
    )
    {
        var parameterName = options.TenantIdRouteParameterName;
        if (string.IsNullOrEmpty(parameterName))
        {
            failures.Add($"{Prefix}:{nameof(options.TenantIdRouteParameterName)} is required.");
            return;
        }

        var key = $"{Prefix}:{nameof(options.TenantPath)}";
        if (
            !TenantAddressRules.TryParseRoute(
                options.TenantPath,
                key,
                failures,
                out var tenantRoute
            )
        )
            return;

        var parameter = tenantRoute.GetParameter(parameterName);
        if (parameter is null || tenantRoute.Parameters.Count != 1)
        {
            failures.Add(
                $"{key} '{options.TenantPath}' must contain the route parameter '{{{parameterName}}}' that identifies "
                    + "the tenant, and no other parameter."
            );
            return;
        }

        if (
            parameter.IsOptional
            || parameter.IsCatchAll
            || parameter.Default is not null
            || parameter.ParameterPolicies.Count > 0
        )
        {
            failures.Add(
                $"{key} '{options.TenantPath}' must declare '{{{parameterName}}}' as a plain parameter, with no "
                    + "constraint, default value, optional marker, or catch-all: the server validates tenant "
                    + "identifiers itself."
            );
        }
    }
}
