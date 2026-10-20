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

using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.Results;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Tenants.Strategies;

/// <summary>
/// Provides a tenant-selection strategy that resolves the tenant dynamically from the request host.
/// </summary>
internal class DynamicByHostTenantStrategy(
    IStoreManagerFactory storeManagerFactory,
    IOptions<TenantResolutionOptions> optionsAccessor
) : TenantStrategy(storeManagerFactory)
{
    private Regex? IssuerHostRegexOrNull { get; set; }

    private Regex IssuerHostRegex =>
        IssuerHostRegexOrNull ??= new Regex(
            Options.RegexPattern,
            RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.Singleline
        );

    private DynamicByHostTenantOptions Options =>
        optionsAccessor.Value.DynamicByHost ?? new DynamicByHostTenantOptions();

    /// <inheritdoc />
    public override string StrategyCode => OpenIdConstants.TenantStrategyCodes.DynamicByHost;

    /// <inheritdoc />
    protected override PathString TenantPath => Options.TenantPath;

    /// <inheritdoc />
    public override async ValueTask<PersistedTenant> ResolveTenantAsync(
        HttpContext httpContext,
        CancellationToken cancellationToken
    )
    {
        var issuerHost = MapToIssuerHost(httpContext.Request.Host.Host);
        return await GetTenantByIssuerHostAsync(issuerHost, cancellationToken);
    }

    /// <inheritdoc />
    /// <remarks>
    /// A tenant is reached only through the host of its issuer, so it needs an issuer, the configured host pattern must
    /// map that host to itself, and no other tenant may hold it — a request to a shared host could not tell the tenants
    /// apart.
    /// </remarks>
    // See ADR-0129 (host uniqueness is a rule of the host strategy, not the schema) and ADR-0160.
    public override async ValueTask<IReadOnlyList<TenantValidationFailure>> ValidateTenantAsync(
        string tenantId,
        Uri? issuer,
        IStoreManager storeManager,
        CancellationToken cancellationToken
    )
    {
        if (issuer is null)
        {
            return
            [
                new TenantValidationFailure
                {
                    Detail =
                        "The tenant requires an issuer: requests reach a tenant only through the host of its issuer.",
                },
            ];
        }

        var failures = new List<TenantValidationFailure>(
            await base.ValidateTenantAsync(tenantId, issuer, storeManager, cancellationToken)
        );

        var issuerHost = issuer.Host;
        var mappedHost = MapToIssuerHost(issuerHost);
        if (!string.Equals(mappedHost, issuerHost, StringComparison.OrdinalIgnoreCase))
        {
            failures.Add(
                new TenantValidationFailure
                {
                    Detail =
                        $"The issuer host '{issuerHost}' does not reach the tenant: the configured host pattern maps "
                        + $"it to '{mappedHost}'.",
                }
            );
        }

        var store = storeManager.GetStore<ITenantStore>();
        var holder = await store.GetOrDefaultByIssuerHostAsync(issuerHost, cancellationToken);
        if (
            holder is not null
            && !string.Equals(holder.TenantId, tenantId, StringComparison.OrdinalIgnoreCase)
        )
        {
            failures.Add(
                new TenantValidationFailure
                {
                    Detail =
                        $"Another tenant already has an issuer on the host '{issuerHost}', and tenants served by "
                        + "host must each have a host of their own.",
                    IsConflict = true,
                }
            );
        }

        return failures;
    }

    private string MapToIssuerHost(string host)
    {
        var match = IssuerHostRegex.Match(host);
        return match.Success ? match.Value : host;
    }

    // The host is matched against the host component of each tenant's issuer, which is derived from that issuer rather
    // than stored alongside it — so the value routed on cannot drift from the identity the tenant publishes.
    private async ValueTask<PersistedTenant> GetTenantByIssuerHostAsync(
        string issuerHost,
        CancellationToken cancellationToken
    )
    {
        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<ITenantStore>();

        var persistedTenant = await store.GetOrDefaultByIssuerHostAsync(
            issuerHost,
            cancellationToken
        );

        if (persistedTenant is null)
            throw TypedResults
                .NotFound()
                .AsException($"A tenant with issuer host '{issuerHost}' could not be found.");

        if (persistedTenant.IsDisabled)
            throw TypedResults
                .NotFound()
                .AsException($"The tenant with issuer host '{issuerHost}' is disabled.");

        return persistedTenant;
    }
}
