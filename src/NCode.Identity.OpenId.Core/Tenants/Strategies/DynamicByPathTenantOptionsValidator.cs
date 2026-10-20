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
/// issuer a tenant must end with — is determined by the tenant's identifier alone. An issuer template must end with the
/// route bound for a tenant, and the root tenant, which is created on demand when a request reaches its route, needs an
/// address. The options of an unselected strategy are inert, and a layered configuration may legitimately carry them, so
/// they are skipped.
/// </summary>
// See ADR-0137 (fail at startup), ADR-0160 (a path tenant's issuer is checked against its bound route), and ADR-0165
// (every served tenant has an issuer, and its strategy supplies the default).
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

        // The address rules below need a tenant route they can bind for a tenant.
        if (ValidateTenantRoute(dynamicByPath, failures))
        {
            ValidateIssuerTemplate(dynamicByPath, failures);
            ValidateRootIssuer(options.Root, dynamicByPath, failures);
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }

    private static bool ValidateTenantRoute(
        DynamicByPathTenantOptions options,
        List<string> failures
    )
    {
        var parameterName = options.TenantIdRouteParameterName;
        if (string.IsNullOrEmpty(parameterName))
        {
            failures.Add($"{Prefix}:{nameof(options.TenantIdRouteParameterName)} is required.");
            return false;
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
            return false;

        var parameter = tenantRoute.GetParameter(parameterName);
        if (parameter is null || tenantRoute.Parameters.Count != 1)
        {
            failures.Add(
                $"{key} '{options.TenantPath}' must contain the route parameter '{{{parameterName}}}' that identifies "
                    + "the tenant, and no other parameter."
            );
            return false;
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
            return false;
        }

        return true;
    }

    private static void ValidateIssuerTemplate(
        DynamicByPathTenantOptions options,
        List<string> failures
    )
    {
        var issuerTemplate = options.IssuerTemplate;
        if (string.IsNullOrEmpty(issuerTemplate))
            return;

        var key = $"{Prefix}:{nameof(options.IssuerTemplate)}";
        if (!TenantAddressRules.TryValidateIssuerTemplate(issuerTemplate, key, failures))
            return;

        var tenantId = TenantAddressRules.ProbeTenantIds[0];
        var probeIssuer = new Uri(
            TenantAddressRules.ExpandIssuerTemplate(issuerTemplate, tenantId)
        );
        ValidateServedIssuer(probeIssuer, tenantId, key, issuerTemplate, options, failures);
    }

    private static void ValidateRootIssuer(
        RootTenantOptions? root,
        DynamicByPathTenantOptions options,
        List<string> failures
    )
    {
        // An absent or malformed root section is reported by the shared validator.
        if (root is null || !TenantAddressRules.IsValidTenantId(root.TenantId))
            return;

        var rootIssuerKey = $"{nameof(TenantResolutionOptions.Root)}:{nameof(root.Issuer)}";
        var rootIssuer = root.Issuer;
        if (string.IsNullOrEmpty(rootIssuer))
        {
            var issuerTemplate = options.IssuerTemplate;
            if (string.IsNullOrEmpty(issuerTemplate))
            {
                failures.Add(
                    $"The root tenant needs an address under the {Prefix} strategy, which creates it when a request "
                        + $"reaches its route: configure {rootIssuerKey} or "
                        + $"{Prefix}:{nameof(options.IssuerTemplate)}."
                );
                return;
            }

            rootIssuer = TenantAddressRules.ExpandIssuerTemplate(issuerTemplate, root.TenantId);
        }

        // A malformed configured root issuer is reported by the shared validator, and a malformed template by the template
        // rule above.
        if (TenantAddressRules.TryParseIssuer(rootIssuer, out var rootIssuerUri))
            ValidateServedIssuer(
                rootIssuerUri,
                root.TenantId,
                rootIssuerKey,
                rootIssuer,
                options,
                failures
            );
    }

    // The route carries the tenant parameter as its only, plain parameter (checked above), so substituting the identifier
    // for it binds the route exactly as the strategy's template binder does.
    private static void ValidateServedIssuer(
        Uri issuer,
        string tenantId,
        string key,
        string value,
        DynamicByPathTenantOptions options,
        List<string> failures
    )
    {
        var routePath = (options.TenantPath.Value ?? string.Empty).Replace(
            $"{{{options.TenantIdRouteParameterName}}}",
            tenantId,
            StringComparison.Ordinal
        );

        if (TenantAddressRules.IssuerEndsWithRoutePath(issuer, routePath))
            return;

        failures.Add(
            $"{key} '{value}' must end with '{routePath}', the route the tenant '{tenantId}' is served under."
        );
    }
}
