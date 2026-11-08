// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;

namespace NCode.Identity.Secrets.RemoteKeys;

/// <summary>
/// Provides the default implementation of <see cref="IRemoteKeyProviderCatalog"/>. It holds the registered
/// <see cref="IRemoteKeyProvider"/> instances by name, and the registered providers are exactly the providers that
/// <see cref="IRemoteKeyDescriptorCatalog"/> describes.
/// </summary>
/// <param name="providers">The registered remote key providers.</param>
/// <param name="descriptorCatalog">The catalog of described remote key providers.</param>
/// <exception cref="InvalidOperationException">A provider name is invalid or registered twice, a provider has no
/// descriptor, or a descriptor has no provider.</exception>
// See ADR-1471.
// See ADR-1474.
internal sealed class DefaultRemoteKeyProviderCatalog(
    IEnumerable<IRemoteKeyProvider> providers,
    IRemoteKeyDescriptorCatalog descriptorCatalog
) : IRemoteKeyProviderCatalog
{
    private FrozenDictionary<string, IRemoteKeyProvider> ProvidersByName { get; } =
        IndexProviders(providers, descriptorCatalog);

    /// <inheritdoc />
    public bool TryGetProvider(string name, [NotNullWhen(true)] out IRemoteKeyProvider? provider) =>
        ProvidersByName.TryGetValue(name, out provider);

    private static FrozenDictionary<string, IRemoteKeyProvider> IndexProviders(
        IEnumerable<IRemoteKeyProvider> providers,
        IRemoteKeyDescriptorCatalog descriptorCatalog
    )
    {
        var described = descriptorCatalog
            .Providers.Select(x => x.Name)
            .ToHashSet(StringComparer.Ordinal);

        var byName = new Dictionary<string, IRemoteKeyProvider>(StringComparer.Ordinal);
        foreach (var provider in providers)
        {
            if (!RemoteKeyReference.IsValidProviderName(provider.Name))
            {
                throw new InvalidOperationException(
                    $"The remote key provider name '{provider.Name}' is not valid."
                );
            }

            if (!byName.TryAdd(provider.Name, provider))
            {
                throw new InvalidOperationException(
                    $"The remote key provider '{provider.Name}' is registered more than once."
                );
            }

            if (!described.Contains(provider.Name))
            {
                throw new InvalidOperationException(
                    $"The remote key provider '{provider.Name}' is not described by any IRemoteKeyDescriptorSource."
                );
            }
        }

        foreach (var name in described)
        {
            if (!byName.ContainsKey(name))
            {
                throw new InvalidOperationException(
                    $"The remote key provider '{name}' is described but not registered."
                );
            }
        }

        return byName.ToFrozenDictionary(StringComparer.Ordinal);
    }
}
