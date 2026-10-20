#region Copyright Preamble

//
//    Copyright @ 2023 NCode Group
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

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.Options;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Tenants;

/// <summary>
/// Provides a default implementation of the <see cref="ITenantResolver"/> abstraction that selects the configured
/// <see cref="ITenantStrategy"/> by <see cref="TenantResolutionOptions.StrategyCode"/> and delegates to it. It validates
/// the addressing rules every strategy shares itself, and the selected strategy validates its own.
/// </summary>
internal class DefaultTenantResolver(
    IOptions<TenantResolutionOptions> optionsAccessor,
    IEnumerable<ITenantStrategy> strategies
) : ITenantResolver
{
    private TenantResolutionOptions Options { get; } = optionsAccessor.Value;

    private ImmutableArray<ITenantStrategy> Strategies { get; } = [.. strategies];

    private ITenantStrategy? SelectedOrNull { get; set; }

    private ITenantStrategy Selected => SelectedOrNull ??= SelectStrategy();

    private ITenantStrategy SelectStrategy()
    {
        var strategyCode = Options.StrategyCode;
        return Strategies.FirstOrDefault(strategy =>
                string.Equals(strategyCode, strategy.StrategyCode, StringComparison.Ordinal)
            )
            ?? throw new InvalidOperationException(
                $"Unable to find a tenant strategy with code '{strategyCode}'."
            );
    }

    /// <inheritdoc />
    public RoutePattern GetTenantRoute() => Selected.GetTenantRoute();

    /// <inheritdoc />
    public bool TryGetTenantId(HttpContext httpContext, [NotNullWhen(true)] out string? tenantId) =>
        Selected.TryGetTenantId(httpContext, out tenantId);

    /// <inheritdoc />
    public async ValueTask<PersistedTenant> ResolveTenantAsync(
        HttpContext httpContext,
        CancellationToken cancellationToken
    ) => await Selected.ResolveTenantAsync(httpContext, cancellationToken);

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<TenantValidationFailure>> ValidateTenantAsync(
        string tenantId,
        string? issuer,
        IStoreManager storeManager,
        CancellationToken cancellationToken
    )
    {
        var failures = new List<TenantValidationFailure>();

        if (!TenantAddressRules.IsValidTenantId(tenantId))
        {
            failures.Add(
                new TenantValidationFailure
                {
                    Detail =
                        $"The tenant identifier '{tenantId}' must be {TenantAddressRules.TenantIdRequirement}.",
                }
            );
        }

        Uri? issuerUri = null;
        if (
            !string.IsNullOrEmpty(issuer)
            && !TenantAddressRules.TryParseIssuer(issuer, out issuerUri)
        )
        {
            failures.Add(
                new TenantValidationFailure
                {
                    Detail =
                        $"The issuer '{issuer}' must be {TenantAddressRules.IssuerRequirement}.",
                }
            );
        }

        // The remaining rules, the strategy's included, are only meaningful for a well-formed identifier and issuer.
        if (failures.Count > 0)
            return failures;

        // The issuer is the tenant's identity and is asserted in every token it mints, so two tenants sharing one
        // would mint tokens a relying party cannot tell apart (ADR-0129).
        if (issuerUri is not null)
        {
            var store = storeManager.GetStore<ITenantStore>();
            var holder = await store.GetOrDefaultByIssuerAsync(
                issuerUri.OriginalString,
                cancellationToken
            );
            if (
                holder is not null
                && !string.Equals(holder.TenantId, tenantId, StringComparison.OrdinalIgnoreCase)
            )
            {
                failures.Add(
                    new TenantValidationFailure
                    {
                        Detail = $"Another tenant already has the issuer '{issuer}'.",
                        IsConflict = true,
                    }
                );
            }
        }

        var strategyFailures = await Selected.ValidateTenantAsync(
            tenantId,
            issuerUri,
            storeManager,
            cancellationToken
        );
        failures.AddRange(strategyFailures);

        return failures;
    }
}
