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
    private Regex? IssuerHostRegex { get; set; }

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
        var options = Options;

        var regex = IssuerHostRegex ??= new Regex(
            options.RegexPattern,
            RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.Singleline
        );

        var host = httpContext.Request.Host.Host;
        var match = regex.Match(host);
        var issuerHost = match.Success ? match.Value : host;

        return await GetTenantByIssuerHostAsync(issuerHost, cancellationToken);
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
