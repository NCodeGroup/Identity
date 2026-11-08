// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;

namespace NCode.Identity.Secrets.RemoteKeys;

/// <summary>
/// Provides the default implementation of <see cref="IRemoteKeyDescriptorCatalog"/>. It holds the provider
/// descriptors and the rows that every registered <see cref="IRemoteKeyDescriptorSource"/> contributes, including the
/// rows of a provider that no source describes.
/// </summary>
// See ADR-1474.
// See ADR-1476.
// See ADR-1477.
internal sealed class DefaultRemoteKeyDescriptorCatalog : IRemoteKeyDescriptorCatalog
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultRemoteKeyDescriptorCatalog"/> class.
    /// </summary>
    /// <param name="sources">The registered sources of provider descriptors and mapping vocabulary rows.</param>
    /// <exception cref="InvalidOperationException">A provider name is invalid or described twice, or two rows have the
    /// same selector.</exception>
    public DefaultRemoteKeyDescriptorCatalog(IEnumerable<IRemoteKeyDescriptorSource> sources)
    {
        var materialized = sources.ToArray();
        Algorithms = CollectAlgorithms(materialized);
        Providers = CollectProviders(materialized);
        AlgorithmsBySelector = Algorithms.ToFrozenDictionary(row => row.Selector);
    }

    private FrozenDictionary<
        RemoteKeyAlgorithmSelector,
        RemoteKeyAlgorithm
    > AlgorithmsBySelector { get; }

    /// <inheritdoc />
    public IReadOnlyCollection<RemoteKeyProviderDescriptor> Providers { get; }

    /// <inheritdoc />
    public IReadOnlyCollection<RemoteKeyAlgorithm> Algorithms { get; }

    /// <inheritdoc />
    public bool TryGetAlgorithm(
        RemoteKeyAlgorithmSelector selector,
        [NotNullWhen(true)] out RemoteKeyAlgorithm? algorithm
    ) => AlgorithmsBySelector.TryGetValue(selector, out algorithm);

    private static RemoteKeyProviderDescriptor[] CollectProviders(
        IEnumerable<IRemoteKeyDescriptorSource> sources
    )
    {
        var byName = new Dictionary<string, RemoteKeyProviderDescriptor>(StringComparer.Ordinal);
        foreach (var descriptor in sources.SelectMany(source => source.Providers))
        {
            if (!RemoteKeyReference.IsValidProviderName(descriptor.Name))
            {
                throw new InvalidOperationException(
                    $"The remote key provider name '{descriptor.Name}' is not valid."
                );
            }

            if (!byName.TryAdd(descriptor.Name, descriptor))
            {
                throw new InvalidOperationException(
                    $"The remote key provider '{descriptor.Name}' is described more than once."
                );
            }
        }

        var descriptors = byName.Values.ToArray();
        Array.Sort(descriptors, (x, y) => string.CompareOrdinal(x.Name, y.Name));
        return descriptors;
    }

    private static RemoteKeyAlgorithm[] CollectAlgorithms(
        IEnumerable<IRemoteKeyDescriptorSource> sources
    )
    {
        var bySelector = new Dictionary<RemoteKeyAlgorithmSelector, RemoteKeyAlgorithm>();
        foreach (var algorithm in sources.SelectMany(source => source.Algorithms))
        {
            if (!bySelector.TryAdd(algorithm.Selector, algorithm))
            {
                throw new InvalidOperationException(
                    $"The remote key algorithm for provider '{algorithm.Provider}', algorithm '{algorithm.Algorithm}', "
                        + $"secret type '{algorithm.SecretType}', key size {algorithm.KeySizeBits} and curve '{algorithm.Curve}' "
                        + "is registered more than once."
                );
            }
        }

        return bySelector
            .Values.OrderBy(row => row.Provider, StringComparer.Ordinal)
            .ThenBy(row => row.Algorithm, StringComparer.Ordinal)
            .ThenBy(row => row.SecretType, StringComparer.Ordinal)
            .ThenBy(row => row.KeySizeBits)
            .ThenBy(row => row.Curve, StringComparer.Ordinal)
            .ToArray();
    }
}
