// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Primitives;
using NCode.Collections.Providers;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.Secrets.RemoteKeys;
using NCode.Identity.Settings;

namespace NCode.Identity.OpenId.Tenants.Keys;

/// <summary>
/// Provides an additive <see cref="SettingDescriptor"/> data source for the tenant <c>tenant_key_provider</c>
/// setting. The domain of the setting is the empty string, which keeps the tenant's keys in the server's store, or
/// the name of a provider described by the <see cref="IRemoteKeyDescriptorCatalog"/> that creates keys, so a value
/// that names no such provider is rejected rather than stored. It is a tenant setting that is not advertised in discovery and
/// whose value belongs to the environment, so promotion never copies it.
/// </summary>
internal class TenantKeyProviderSettingDescriptorDataSource(
    INullChangeToken nullChangeToken,
    IRemoteKeyDescriptorCatalog catalog
) : ICollectionDataSource<SettingDescriptor>
{
    private INullChangeToken NullChangeToken { get; } = nullChangeToken;

    private IRemoteKeyDescriptorCatalog Catalog { get; } = catalog;

    /// <inheritdoc />
    public IChangeToken GetChangeToken() => NullChangeToken;

    /// <inheritdoc />
    public IEnumerable<SettingDescriptor> Collection
    {
        get
        {
            var providerNames = Catalog
                .Providers.Where(provider => provider.CreatesKeys)
                .Select(provider => provider.Name)
                .Order(StringComparer.Ordinal)
                .ToList();

            // tenant_key_provider: the name of a registered remote key provider that creates keys and holds the
            // tenant's keys.
            // Empty by default, which keeps them in the server's store; a platform or team document carries the
            // value to its tenants. Override (Replace): a child fully replaces the parent value.
            yield return new SettingDescriptor<string>
            {
                Name = OpenIdSettingNames.TenantKeyProvider,
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
