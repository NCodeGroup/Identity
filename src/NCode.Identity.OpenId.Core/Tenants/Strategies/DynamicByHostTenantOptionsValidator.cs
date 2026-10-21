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

using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;

namespace NCode.Identity.OpenId.Tenants.Strategies;

/// <summary>
/// Validates <see cref="TenantResolutionOptions.DynamicByHost"/> when the host-based strategy is selected: a literal
/// tenant path, a host pattern that compiles, an issuer template whose every expansion is a host of its own that reaches
/// the tenant, and an address for the root tenant, which is created on demand when a request reaches its host. The
/// options of an unselected strategy are inert, and a layered configuration may legitimately carry them, so they are
/// skipped.
/// </summary>
// See ADR-0137 (fail at startup), ADR-0160 (the same rules apply to tenants written at runtime), and ADR-0165 (every
// served tenant has an issuer, and its strategy supplies the default).
internal class DynamicByHostTenantOptionsValidator : IValidateOptions<TenantResolutionOptions>
{
    private const string Prefix = OpenIdConstants.TenantStrategyCodes.DynamicByHost;

    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, TenantResolutionOptions options)
    {
        if (!string.Equals(options.StrategyCode, Prefix, StringComparison.Ordinal))
            return ValidateOptionsResult.Skip;

        var dynamicByHost = options.DynamicByHost ?? new DynamicByHostTenantOptions();
        var failures = new List<string>();

        var hasLiteralPath = TenantAddressRules.TryParseLiteralPath(
            dynamicByHost.TenantPath,
            $"{Prefix}:{nameof(dynamicByHost.TenantPath)}",
            failures
        );

        Regex? hostPattern = null;
        try
        {
            hostPattern = new Regex(
                dynamicByHost.RegexPattern,
                RegexOptions.CultureInvariant | RegexOptions.Singleline
            );
        }
        catch (ArgumentException exception)
        {
            failures.Add(
                $"{Prefix}:{nameof(dynamicByHost.RegexPattern)} '{dynamicByHost.RegexPattern}' is not a valid "
                    + $"regular expression: {exception.Message}"
            );
        }

        // The address rules below need the tenant path and the host pattern they are judged against.
        if (hasLiteralPath && hostPattern is not null)
        {
            ValidateIssuerTemplate(dynamicByHost, hostPattern, failures);
            ValidateMtlsEndpointAliasTemplate(dynamicByHost, hostPattern, failures);
            ValidateRootIssuer(options.Root, dynamicByHost, hostPattern, failures);
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }

    private static void ValidateIssuerTemplate(
        DynamicByHostTenantOptions options,
        Regex hostPattern,
        List<string> failures
    )
    {
        var issuerTemplate = options.IssuerTemplate;
        if (string.IsNullOrEmpty(issuerTemplate))
            return;

        var key = $"{Prefix}:{nameof(options.IssuerTemplate)}";
        if (!TenantAddressRules.TryValidateIssuerTemplate(issuerTemplate, key, failures))
            return;

        // Tenants served by host must each have a host of their own, so the identifier has to vary the host.
        var hosts = TenantAddressRules
            .ProbeTenantIds.Select(tenantId =>
                new Uri(TenantAddressRules.ExpandIssuerTemplate(issuerTemplate, tenantId)).Host
            )
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();
        if (hosts != TenantAddressRules.ProbeTenantIds.Count)
        {
            failures.Add(
                $"{key} '{issuerTemplate}' must carry '{TenantResolutionOptions.TenantIdPlaceholder}' in its host: "
                    + "tenants served by host must each have a host of their own."
            );
            return;
        }

        var probeIssuer = TenantAddressRules.ExpandIssuerTemplate(
            issuerTemplate,
            TenantAddressRules.ProbeTenantIds[0]
        );
        ValidateServedIssuer(
            new Uri(probeIssuer),
            key,
            issuerTemplate,
            options,
            hostPattern,
            failures
        );
    }

    // See ADR-0195 (a tenant's mTLS endpoint alias is a routed tenant property).
    private static void ValidateMtlsEndpointAliasTemplate(
        DynamicByHostTenantOptions options,
        Regex hostPattern,
        List<string> failures
    )
    {
        var aliasTemplate = options.MtlsEndpointAliasTemplate;
        if (string.IsNullOrEmpty(aliasTemplate))
            return;

        var key = $"{Prefix}:{nameof(options.MtlsEndpointAliasTemplate)}";
        if (!TenantAddressRules.TryValidateMtlsEndpointAliasTemplate(aliasTemplate, key, failures))
            return;

        var aliasHosts = TenantAddressRules
            .ProbeTenantIds.Select(tenantId =>
                new Uri(TenantAddressRules.ExpandIssuerTemplate(aliasTemplate, tenantId)).Host
            )
            .ToList();

        // An alias host routes to its tenant like an issuer host does, so it too must be a host of the tenant's own.
        if (aliasHosts.Distinct(StringComparer.OrdinalIgnoreCase).Count() != aliasHosts.Count)
        {
            failures.Add(
                $"{key} '{aliasTemplate}' must carry '{TenantResolutionOptions.TenantIdPlaceholder}' in its host: "
                    + "tenants served by host must each have a host of their own."
            );
            return;
        }

        var aliasHost = aliasHosts[0];
        var mappedHost = TenantAddressRules.MapToIssuerHost(hostPattern, aliasHost);
        if (!string.Equals(mappedHost, aliasHost, StringComparison.OrdinalIgnoreCase))
        {
            failures.Add(
                $"{key} '{aliasTemplate}' does not reach its tenant: the configured host pattern maps the host "
                    + $"'{aliasHost}' to '{mappedHost}'."
            );
        }

        // The alias serves the endpoints on a host of its own, where the TLS terminator asks for a client certificate.
        var issuerTemplate = options.IssuerTemplate;
        if (
            !string.IsNullOrEmpty(issuerTemplate)
            && TenantAddressRules.TryParseIssuer(
                TenantAddressRules.ExpandIssuerTemplate(
                    issuerTemplate,
                    TenantAddressRules.ProbeTenantIds[0]
                ),
                out var probeIssuer
            )
            && string.Equals(probeIssuer.Host, aliasHost, StringComparison.OrdinalIgnoreCase)
        )
        {
            failures.Add(
                $"{key} '{aliasTemplate}' must give a tenant a host other than its issuer host from "
                    + $"{Prefix}:{nameof(options.IssuerTemplate)}."
            );
        }
    }

    private static void ValidateRootIssuer(
        RootTenantOptions? root,
        DynamicByHostTenantOptions options,
        Regex hostPattern,
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
                        + $"reaches its host: configure {rootIssuerKey} or "
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
                rootIssuerKey,
                rootIssuer,
                options,
                hostPattern,
                failures
            );
    }

    private static void ValidateServedIssuer(
        Uri issuer,
        string key,
        string value,
        DynamicByHostTenantOptions options,
        Regex hostPattern,
        List<string> failures
    )
    {
        var issuerHost = issuer.Host;
        var mappedHost = TenantAddressRules.MapToIssuerHost(hostPattern, issuerHost);
        if (!string.Equals(mappedHost, issuerHost, StringComparison.OrdinalIgnoreCase))
        {
            failures.Add(
                $"{key} '{value}' does not reach its tenant: the configured host pattern maps the host "
                    + $"'{issuerHost}' to '{mappedHost}'."
            );
        }

        var tenantPath = options.TenantPath;
        if (!TenantAddressRules.IssuerEndsWithRoutePath(issuer, tenantPath.Value))
        {
            failures.Add(
                $"{key} '{value}' must end with the tenant path '{tenantPath}', the path the tenant's endpoints and "
                    + "discovery document are served under."
            );
        }
    }
}
