// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Frozen;
using Microsoft.Extensions.Options;
using NCode.Identity.Secrets.KeyWrapping;
using NCode.Identity.Secrets.Persistence.Options;

namespace NCode.Identity.Secrets.Persistence.Protection;

/// <summary>
/// Provides the default implementation of <see cref="IKeyWrapperCatalog"/> over the registered
/// <see cref="IKeyWrapper"/> instances. Providers are keyed ordinally by
/// <see cref="IKeyWrapper.Name"/>.
/// </summary>
/// <remarks>
/// The constructor throws an <see cref="InvalidOperationException"/> when more than one registered provider has the
/// same name, so an inconsistent registration fails when the catalog is composed rather than when a key is wrapped.
/// </remarks>
/// <param name="providers">The registered root key providers.</param>
/// <param name="options">The options that select the provider for wrapping new keys.</param>
// See ADR-1150 and ADR-1235.
internal class DefaultKeyWrapperCatalog(
    IEnumerable<IKeyWrapper> providers,
    IOptions<SecretProtectionOptions> options
) : IKeyWrapperCatalog
{
    private FrozenDictionary<string, IKeyWrapper> ProvidersByName { get; } = IndexByName(providers);

    private IOptions<SecretProtectionOptions> ProtectionOptions { get; } = options;

    /// <inheritdoc />
    public IKeyWrapper Get(string providerName)
    {
        if (ProvidersByName.TryGetValue(providerName, out var provider))
            return provider;

        throw new InvalidOperationException(
            $"No root key provider is registered with the name '{providerName}'. "
                + $"The registered providers are: {FormatNames(ProvidersByName.Keys)}."
        );
    }

    /// <inheritdoc />
    public IKeyWrapper GetConfigured()
    {
        var name = ProtectionOptions.Value.RootKeyProvider;
        return Get(string.IsNullOrWhiteSpace(name) ? KeyWrapperNames.Ephemeral : name);
    }

    private static FrozenDictionary<string, IKeyWrapper> IndexByName(
        IEnumerable<IKeyWrapper> providers
    )
    {
        var providersByName = new Dictionary<string, IKeyWrapper>(StringComparer.Ordinal);
        var duplicateNames = new HashSet<string>(StringComparer.Ordinal);

        foreach (var provider in providers)
        {
            var providerName = provider.Name;

            if (!providersByName.TryAdd(providerName, provider))
                duplicateNames.Add(providerName);
        }

        if (duplicateNames.Count > 0)
            throw new InvalidOperationException(
                $"More than one root key provider is registered with the same name: {FormatNames(duplicateNames)}."
            );

        return providersByName.ToFrozenDictionary(StringComparer.Ordinal);
    }

    private static string FormatNames(IEnumerable<string> names) =>
        string.Join(", ", names.Order(StringComparer.Ordinal).Select(name => $"'{name}'"));
}
