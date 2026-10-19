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

using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.Options;

namespace NCode.Identity.OpenId.Tenants;

/// <summary>
/// Validates <see cref="TenantResolutionOptions"/>, so a tenant-resolution configuration that cannot work stops the host
/// when it starts rather than failing, or silently publishing the wrong URLs, on the first request that resolves a
/// tenant. Only the selected strategy's options are validated: the others are inert, and a layered configuration may
/// legitimately carry them.
/// </summary>
// See ADR-0137 (fail at startup) and ADR-0138 (the tenant route and the issuer).
internal class TenantResolutionOptionsValidator : IValidateOptions<TenantResolutionOptions>
{
    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, TenantResolutionOptions options)
    {
        var failures = new List<string>();

        if (string.IsNullOrEmpty(options.StrategyCode))
            failures.Add($"{nameof(TenantResolutionOptions.StrategyCode)} is required.");

        if (string.IsNullOrEmpty(options.RootTenantId))
            failures.Add($"{nameof(TenantResolutionOptions.RootTenantId)} is required.");

        switch (options.StrategyCode)
        {
            case OpenIdConstants.TenantStrategyCodes.StaticSingle:
                var staticSingle = options.StaticSingle ?? new StaticSingleTenantOptions();
                ValidateStaticSingle(staticSingle, failures);
                break;

            case OpenIdConstants.TenantStrategyCodes.DynamicByHost:
                var dynamicByHost = options.DynamicByHost ?? new DynamicByHostTenantOptions();
                ValidateDynamicByHost(dynamicByHost, failures);
                break;

            case OpenIdConstants.TenantStrategyCodes.DynamicByPath:
                var dynamicByPath = options.DynamicByPath ?? new DynamicByPathTenantOptions();
                ValidateDynamicByPath(dynamicByPath, failures);
                break;
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }

    private static void ValidateStaticSingle(
        StaticSingleTenantOptions options,
        List<string> failures
    )
    {
        const string prefix = OpenIdConstants.TenantStrategyCodes.StaticSingle;

        var tenantPath = options.TenantPath;
        if (!TryParseLiteralPath(tenantPath, $"{prefix}:{nameof(options.TenantPath)}", failures))
            return;

        var issuer = options.Issuer;
        if (string.IsNullOrEmpty(issuer))
            return;

        if (!TryParseIssuer(issuer, out var issuerUri))
        {
            failures.Add(
                $"{prefix}:{nameof(options.Issuer)} '{issuer}' must be an absolute http or https URL with no query "
                    + "or fragment."
            );
            return;
        }

        // The tenant's endpoints, its discovery document included, are served beneath the tenant path, and OpenID
        // Connect Discovery requires the issuer to be the URL that document is served under. Any path base the host
        // is mounted under precedes the tenant path, so the issuer has to end with it rather than equal it.
        var issuerPath = PathString.FromUriComponent(issuerUri).Value?.TrimEnd('/') ?? string.Empty;
        var routePath = tenantPath.Value?.TrimEnd('/') ?? string.Empty;
        if (!issuerPath.EndsWith(routePath, StringComparison.OrdinalIgnoreCase))
        {
            failures.Add(
                $"{prefix}:{nameof(options.Issuer)} '{issuer}' must end with the tenant path '{tenantPath}', the "
                    + "path the tenant's endpoints and discovery document are served under."
            );
        }
    }

    private static void ValidateDynamicByHost(
        DynamicByHostTenantOptions options,
        List<string> failures
    )
    {
        const string prefix = OpenIdConstants.TenantStrategyCodes.DynamicByHost;

        TryParseLiteralPath(options.TenantPath, $"{prefix}:{nameof(options.TenantPath)}", failures);

        try
        {
            _ = new Regex(
                options.RegexPattern,
                RegexOptions.CultureInvariant | RegexOptions.Singleline
            );
        }
        catch (ArgumentException exception)
        {
            failures.Add(
                $"{prefix}:{nameof(options.RegexPattern)} '{options.RegexPattern}' is not a valid regular "
                    + $"expression: {exception.Message}"
            );
        }
    }

    private static void ValidateDynamicByPath(
        DynamicByPathTenantOptions options,
        List<string> failures
    )
    {
        const string prefix = OpenIdConstants.TenantStrategyCodes.DynamicByPath;

        var parameterName = options.TenantIdRouteParameterName;
        if (string.IsNullOrEmpty(parameterName))
        {
            failures.Add($"{prefix}:{nameof(options.TenantIdRouteParameterName)} is required.");
            return;
        }

        var key = $"{prefix}:{nameof(options.TenantPath)}";
        if (!TryParseRoute(options.TenantPath, key, failures, out var tenantRoute))
            return;

        if (tenantRoute.GetParameter(parameterName) is null)
        {
            failures.Add(
                $"{key} '{options.TenantPath}' must contain the route parameter '{{{parameterName}}}' that identifies "
                    + "the tenant."
            );
        }
    }

    private static bool TryParseLiteralPath(PathString path, string key, List<string> failures)
    {
        if (!TryParseRoute(path, key, failures, out var route))
            return false;

        if (route.Parameters.Count == 0)
            return true;

        failures.Add(
            $"{key} '{path}' must be a literal path: this strategy does not take the tenant from the route."
        );
        return false;
    }

    private static bool TryParseRoute(
        PathString path,
        string key,
        List<string> failures,
        [NotNullWhen(true)] out RoutePattern? route
    )
    {
        try
        {
            route = RoutePatternFactory.Parse(path.Value ?? string.Empty);
            return true;
        }
        catch (RoutePatternException exception)
        {
            failures.Add($"{key} '{path}' is not a valid route pattern: {exception.Message}");
            route = null;
            return false;
        }
    }

    private static bool TryParseIssuer(string issuer, [NotNullWhen(true)] out Uri? issuerUri) =>
        Uri.TryCreate(issuer, UriKind.Absolute, out issuerUri)
        && (issuerUri.Scheme == Uri.UriSchemeHttps || issuerUri.Scheme == Uri.UriSchemeHttp)
        && string.IsNullOrEmpty(issuerUri.Query)
        && string.IsNullOrEmpty(issuerUri.Fragment);
}
