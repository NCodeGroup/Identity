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
/// the addressing rules every strategy shares itself — the root tenant's address among them — and the selected strategy
/// validates its own and derives a tenant's default issuer.
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
    public async ValueTask<PersistedTenant> ResolveControlPlaneTenantAsync(
        HttpContext httpContext,
        CancellationToken cancellationToken
    ) => await Selected.ResolveControlPlaneTenantAsync(httpContext, cancellationToken);

    /// <inheritdoc />
    public async ValueTask<string?> GetDefaultIssuerAsync(
        string tenantId,
        CancellationToken cancellationToken
    )
    {
        // A strategy derives an issuer from a well-formed identifier only; a malformed one is reported by validation.
        if (!TenantAddressRules.IsValidTenantId(tenantId))
            return null;

        return await Selected.GetDefaultIssuerAsync(tenantId, cancellationToken);
    }

    /// <inheritdoc />
    public async ValueTask<string?> GetDefaultMtlsEndpointAliasAuthorityAsync(
        string tenantId,
        CancellationToken cancellationToken
    )
    {
        // A strategy derives an alias from a well-formed identifier only; a malformed one is reported by validation.
        if (!TenantAddressRules.IsValidTenantId(tenantId))
            return null;

        return await Selected.GetDefaultMtlsEndpointAliasAuthorityAsync(
            tenantId,
            cancellationToken
        );
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<TenantValidationFailure>> ValidateTenantAsync(
        string tenantId,
        string? issuer,
        string? mtlsEndpointAliasAuthority,
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

        Uri? mtlsEndpointAliasAuthorityUri = null;
        if (
            !string.IsNullOrEmpty(mtlsEndpointAliasAuthority)
            && !TenantAddressRules.TryParseMtlsEndpointAliasAuthority(
                mtlsEndpointAliasAuthority,
                out mtlsEndpointAliasAuthorityUri
            )
        )
        {
            failures.Add(
                new TenantValidationFailure
                {
                    Detail =
                        $"The mTLS endpoint alias authority '{mtlsEndpointAliasAuthority}' must be "
                        + $"{TenantAddressRules.MtlsEndpointAliasAuthorityRequirement}.",
                }
            );
        }

        // The remaining rules, the strategy's included, are only meaningful for a well-formed identifier and issuer.
        if (failures.Count > 0)
            return failures;

        var store = storeManager.GetStore<ITenantStore>();

        // A tenant identifier names its tenant in every route and management call, so one that belonged to a deleted
        // tenant is never reissued (ADR-0189).
        if (await store.IsTenantIdRetiredAsync(tenantId, cancellationToken))
        {
            failures.Add(
                new TenantValidationFailure
                {
                    Detail =
                        $"The tenant identifier '{tenantId}' belonged to a deleted tenant and is never reissued.",
                    IsConflict = true,
                }
            );
        }

        // The issuer is the tenant's identity and is asserted in every token it mints, so two tenants sharing one
        // would mint tokens a relying party cannot tell apart (ADR-0129).
        if (issuerUri is not null)
        {
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
            else if (
                await store.GetRetiredIssuerHolderOrDefaultAsync(
                    issuerUri.OriginalString,
                    cancellationToken
                )
                    is { } formerHolder
                && !string.Equals(formerHolder, tenantId, StringComparison.OrdinalIgnoreCase)
            )
            {
                // Relying parties that trusted a retired issuer would accept another tenant's tokens under it, so only
                // the tenant that gave it up can take it back (ADR-0189).
                failures.Add(
                    new TenantValidationFailure
                    {
                        Detail =
                            $"The issuer '{issuer}' belonged to another tenant and is never reissued.",
                        IsConflict = true,
                    }
                );
            }
            else if (await IsRootTenantAddressAsync(tenantId, issuerUri, cancellationToken))
            {
                // The root tenant is created on demand at its configured address, so a tenant written there first would
                // take the control plane's address and leave the root tenant unable to be created (ADR-0165).
                failures.Add(
                    new TenantValidationFailure
                    {
                        Detail = $"The issuer '{issuer}' is reserved for the root tenant.",
                        IsConflict = true,
                    }
                );
            }
        }

        var strategyFailures = await Selected.ValidateTenantAsync(
            tenantId,
            issuerUri,
            mtlsEndpointAliasAuthorityUri,
            storeManager,
            cancellationToken
        );
        failures.AddRange(strategyFailures);

        return failures;
    }

    private async ValueTask<bool> IsRootTenantAddressAsync(
        string tenantId,
        Uri issuer,
        CancellationToken cancellationToken
    )
    {
        var rootTenantId = Options.Root.TenantId;
        if (string.Equals(tenantId, rootTenantId, StringComparison.OrdinalIgnoreCase))
            return false;

        var rootIssuer = await Selected.GetDefaultIssuerAsync(rootTenantId, cancellationToken);
        return Uri.TryCreate(rootIssuer, UriKind.Absolute, out var rootIssuerUri)
            && Uri.Compare(
                issuer,
                rootIssuerUri,
                UriComponents.AbsoluteUri,
                UriFormat.UriEscaped,
                StringComparison.OrdinalIgnoreCase
            ) == 0;
    }
}
