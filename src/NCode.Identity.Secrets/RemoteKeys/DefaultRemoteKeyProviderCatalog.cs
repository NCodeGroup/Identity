// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;

namespace NCode.Identity.Secrets.RemoteKeys;

/// <summary>
/// Provides the default implementation of <see cref="IRemoteKeyProviderCatalog"/>. It holds the registered
/// <see cref="IRemoteKeyProvider"/> instances by name and the rows that every registered
/// <see cref="IRemoteSigningAlgorithmSource"/> contributes, including the rows of a provider that is not registered.
/// </summary>
// See ADR-1471.
internal sealed class DefaultRemoteKeyProviderCatalog : IRemoteKeyProviderCatalog
{
    private FrozenDictionary<string, IRemoteKeyProvider> ProvidersByName { get; }
    private FrozenDictionary<
        RemoteSigningAlgorithmSelector,
        RemoteSigningAlgorithm
    > AlgorithmsBySelector { get; }

    /// <inheritdoc />
    public IReadOnlyCollection<IRemoteKeyProvider> Providers { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultRemoteKeyProviderCatalog"/> class.
    /// </summary>
    /// <param name="providers">The registered remote key providers.</param>
    /// <param name="sources">The registered sources of the mapping vocabulary's rows.</param>
    /// <exception cref="InvalidOperationException">A provider name is invalid or registered twice, or two rows have
    /// the same selector.</exception>
    public DefaultRemoteKeyProviderCatalog(
        IEnumerable<IRemoteKeyProvider> providers,
        IEnumerable<IRemoteSigningAlgorithmSource> sources
    )
    {
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
        }

        var bySelector = new Dictionary<RemoteSigningAlgorithmSelector, RemoteSigningAlgorithm>();
        foreach (var algorithm in sources.SelectMany(source => source.Algorithms))
        {
            var selector = new RemoteSigningAlgorithmSelector
            {
                Provider = algorithm.Provider,
                Algorithm = algorithm.Algorithm,
                SecretType = algorithm.SecretType,
                KeySizeBits = algorithm.KeySizeBits,
                Curve = algorithm.Curve,
            };

            if (!bySelector.TryAdd(selector, algorithm))
            {
                throw new InvalidOperationException(
                    $"The remote signing algorithm for provider '{algorithm.Provider}', algorithm '{algorithm.Algorithm}', "
                        + $"secret type '{algorithm.SecretType}', key size {algorithm.KeySizeBits} and curve '{algorithm.Curve}' "
                        + "is registered more than once."
                );
            }
        }

        ProvidersByName = byName.ToFrozenDictionary(StringComparer.Ordinal);
        AlgorithmsBySelector = bySelector.ToFrozenDictionary();
        Providers = [.. byName.Values];
    }

    /// <inheritdoc />
    public bool TryGetProvider(string name, [NotNullWhen(true)] out IRemoteKeyProvider? provider) =>
        ProvidersByName.TryGetValue(name, out provider);

    /// <inheritdoc />
    public bool TryGetAlgorithm(
        RemoteSigningAlgorithmSelector selector,
        [NotNullWhen(true)] out RemoteSigningAlgorithm? algorithm
    ) => AlgorithmsBySelector.TryGetValue(selector, out algorithm);
}
