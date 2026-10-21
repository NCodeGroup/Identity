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
) : TenantStrategy(storeManagerFactory, optionsAccessor)
{
    private Regex? IssuerHostRegexOrNull { get; set; }

    private Regex IssuerHostRegex =>
        IssuerHostRegexOrNull ??= new Regex(
            Options.RegexPattern,
            RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.Singleline
        );

    private DynamicByHostTenantOptions Options =>
        ResolutionOptions.DynamicByHost ?? new DynamicByHostTenantOptions();

    /// <inheritdoc />
    public override string StrategyCode => OpenIdConstants.TenantStrategyCodes.DynamicByHost;

    /// <inheritdoc />
    protected override PathString TenantPath => Options.TenantPath;

    /// <inheritdoc />
    protected override string? IssuerTemplate => Options.IssuerTemplate;

    /// <inheritdoc />
    protected override string? MtlsEndpointAliasTemplate => Options.MtlsEndpointAliasTemplate;

    /// <inheritdoc />
    /// <remarks>
    /// A request to the host of the root tenant's issuer finds the root tenant even on a fresh deployment: it is created
    /// on demand.
    /// </remarks>
    public override async ValueTask<PersistedTenant> ResolveTenantAsync(
        HttpContext httpContext,
        CancellationToken cancellationToken
    )
    {
        var issuerHost = TenantAddressRules.MapToIssuerHost(
            IssuerHostRegex,
            httpContext.Request.Host.Host
        );

        var persistedTenant = await GetTenantByIssuerHostAsync(issuerHost, cancellationToken);

        if (persistedTenant is null && await IsRootIssuerHostAsync(issuerHost, cancellationToken))
        {
            persistedTenant = await EnsureRootTenantAsync(httpContext, cancellationToken);
        }

        if (persistedTenant is null)
            throw TypedResults
                .NotFound()
                .AsException($"A tenant with issuer host '{issuerHost}' could not be found.");

        return ThrowIfDisabled(persistedTenant);
    }

    /// <inheritdoc />
    /// <remarks>
    /// A tenant is reached only through the host of its issuer — and of its mutual-TLS endpoint-alias authority, when it
    /// has one — so it needs an issuer, the configured host pattern must map each of those hosts to itself, and no other
    /// tenant may hold either of them as its issuer host or its alias host: a request to a shared host could not tell the
    /// tenants apart.
    /// </remarks>
    // See ADR-0129 (host uniqueness is a rule of the host strategy, not the schema), ADR-0160, and ADR-0195.
    public override async ValueTask<IReadOnlyList<TenantValidationFailure>> ValidateTenantAsync(
        string tenantId,
        Uri? issuer,
        Uri? mtlsEndpointAliasAuthority,
        IStoreManager storeManager,
        CancellationToken cancellationToken
    )
    {
        var failures = new List<TenantValidationFailure>(
            await base.ValidateTenantAsync(
                tenantId,
                issuer,
                mtlsEndpointAliasAuthority,
                storeManager,
                cancellationToken
            )
        );

        if (issuer is not null)
        {
            await ValidateHostAsync(
                tenantId,
                issuer.Host,
                "issuer host",
                storeManager,
                failures,
                cancellationToken
            );
        }

        if (mtlsEndpointAliasAuthority is not null)
        {
            var aliasHost = mtlsEndpointAliasAuthority.Host;
            if (
                issuer is not null
                && string.Equals(aliasHost, issuer.Host, StringComparison.OrdinalIgnoreCase)
            )
            {
                failures.Add(
                    new TenantValidationFailure
                    {
                        Detail =
                            $"The mTLS endpoint alias host '{aliasHost}' must differ from the issuer host: the alias "
                            + "exists to serve the tenant's endpoints on a host of its own.",
                    }
                );
            }
            else
            {
                await ValidateHostAsync(
                    tenantId,
                    aliasHost,
                    "mTLS endpoint alias host",
                    storeManager,
                    failures,
                    cancellationToken
                );
            }
        }

        return failures;
    }

    private async ValueTask ValidateHostAsync(
        string tenantId,
        string host,
        string hostKind,
        IStoreManager storeManager,
        List<TenantValidationFailure> failures,
        CancellationToken cancellationToken
    )
    {
        var mappedHost = TenantAddressRules.MapToIssuerHost(IssuerHostRegex, host);
        if (!string.Equals(mappedHost, host, StringComparison.OrdinalIgnoreCase))
        {
            failures.Add(
                new TenantValidationFailure
                {
                    Detail =
                        $"The {hostKind} '{host}' does not reach the tenant: the configured host pattern maps it to "
                        + $"'{mappedHost}'.",
                }
            );
        }

        // The lookup matches issuer hosts and alias hosts alike, so a host is judged against both kinds at once. It
        // lists every holder rather than routing, so a host that is already ambiguous is still reported as taken.
        var store = storeManager.GetStore<ITenantStore>();
        var holders = await store.ListTenantIdsByHostAsync(host, cancellationToken);
        if (
            holders.Any(holder =>
                !string.Equals(holder, tenantId, StringComparison.OrdinalIgnoreCase)
            )
        )
        {
            failures.Add(
                new TenantValidationFailure
                {
                    Detail =
                        $"Another tenant already serves the host '{host}', and tenants served by host must each have "
                        + "hosts of their own.",
                    IsConflict = true,
                }
            );
        }
    }

    private async ValueTask<bool> IsRootIssuerHostAsync(
        string issuerHost,
        CancellationToken cancellationToken
    )
    {
        var rootIssuer = await GetDefaultIssuerAsync(RootOptions.TenantId, cancellationToken);
        return Uri.TryCreate(rootIssuer, UriKind.Absolute, out var rootIssuerUri)
            && string.Equals(rootIssuerUri.Host, issuerHost, StringComparison.OrdinalIgnoreCase);
    }

    // The host is matched against the host component of each tenant's issuer, which is derived from that issuer rather
    // than stored alongside it — so the value routed on cannot drift from the identity the tenant publishes.
    private async ValueTask<PersistedTenant?> GetTenantByIssuerHostAsync(
        string issuerHost,
        CancellationToken cancellationToken
    )
    {
        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<ITenantStore>();

        return await store.GetOrDefaultByHostAsync(issuerHost, cancellationToken);
    }
}
