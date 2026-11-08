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
/// <param name="sources">The registered sources of provider descriptors and mapping vocabulary rows.</param>
/// <exception cref="InvalidOperationException">A provider name is invalid or described twice, or two rows have the
/// same selector.</exception>
// See ADR-1474.
internal sealed class DefaultRemoteKeyDescriptorCatalog(
    IEnumerable<IRemoteKeyDescriptorSource> sources
) : IRemoteKeyDescriptorCatalog
{
    private FrozenDictionary<
        RemoteSigningAlgorithmSelector,
        RemoteSigningAlgorithm
    > AlgorithmsBySelector { get; } = IndexAlgorithms(sources);

    /// <inheritdoc />
    public IReadOnlyCollection<RemoteKeyProviderDescriptor> Providers { get; } =
        CollectProviders(sources);

    /// <inheritdoc />
    public bool TryGetAlgorithm(
        RemoteSigningAlgorithmSelector selector,
        [NotNullWhen(true)] out RemoteSigningAlgorithm? algorithm
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

    private static FrozenDictionary<
        RemoteSigningAlgorithmSelector,
        RemoteSigningAlgorithm
    > IndexAlgorithms(IEnumerable<IRemoteKeyDescriptorSource> sources)
    {
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

        return bySelector.ToFrozenDictionary();
    }
}
