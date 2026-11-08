// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Primitives;
using NCode.Collections.Providers;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.Secrets.RemoteKeys;
using NCode.Identity.Settings;

namespace NCode.Identity.OpenId.Tenants.Keys;

/// <summary>
/// Provides an additive <see cref="SettingDescriptor"/> data source for the tenant <c>tenant_signing_key_provider</c>
/// setting. The domain of the setting is the empty string, which keeps the tenant's signing keys in the server's
/// store, or the name of a provider registered with the <see cref="IRemoteKeyProviderCatalog"/>, so a value that names
/// no registered provider is rejected rather than stored. It is a tenant setting that is not advertised in discovery
/// and whose value belongs to the environment, so promotion never copies it.
/// </summary>
internal class TenantSigningKeySettingDescriptorDataSource(
    INullChangeToken nullChangeToken,
    IRemoteKeyProviderCatalog catalog
) : ICollectionDataSource<SettingDescriptor>
{
    private INullChangeToken NullChangeToken { get; } = nullChangeToken;

    private IRemoteKeyProviderCatalog Catalog { get; } = catalog;

    /// <inheritdoc />
    public IChangeToken GetChangeToken() => NullChangeToken;

    /// <inheritdoc />
    public IEnumerable<SettingDescriptor> Collection
    {
        get
        {
            var providerNames = Catalog
                .Providers.Select(provider => provider.Name)
                .Order(StringComparer.Ordinal)
                .ToList();

            // tenant_signing_key_provider: the name of the registered remote key provider that holds the tenant's
            // signing keys. Empty by default, which keeps them in the server's store; a platform or team document
            // carries the value to its tenants. Override (Replace): a child fully replaces the parent value.
            yield return new SettingDescriptor<string>
            {
                Name = OpenIdSettingNames.TenantSigningKeyProvider,
                AppliesTo = [OpenIdSettingScopes.Tenant],
                Rule = SettingRules.EmptyOr(SettingRules.OneOf(providerNames)),
                Default = string.Empty,

                IsDiscoverable = false,
                IsEnvironmentBound = true,
                OnMerge = SettingMerge.Replace,
            };
        }
    }
}
