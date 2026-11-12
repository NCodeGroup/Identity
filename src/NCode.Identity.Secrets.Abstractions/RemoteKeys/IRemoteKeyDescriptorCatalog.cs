// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;

namespace NCode.Identity.Secrets.RemoteKeys;

/// <summary>
/// Provides lookup over the descriptors of remote key providers and the rows of the mapping vocabulary that
/// <see cref="IRemoteKeyDescriptorSource"/> instances contribute, without constructing a provider.
/// </summary>
// See ADR-1474.
// See ADR-1476.
// See ADR-1471.
[PublicAPI]
public interface IRemoteKeyDescriptorCatalog
{
    /// <summary>
    /// Gets the described remote key providers, in ordinal name order.
    /// </summary>
    IReadOnlyCollection<RemoteKeyProviderDescriptor> Providers { get; }

    /// <summary>
    /// Gets every row of the mapping vocabulary, of every kind, ordered by provider, algorithm, secret type, key size
    /// and curve, each compared ordinally. The rows of a provider that no source describes are listed too.
    /// </summary>
    IReadOnlyCollection<RemoteKeyAlgorithm> Algorithms { get; }

    /// <summary>
    /// Looks up the row of the mapping vocabulary that the selector names.
    /// </summary>
    /// <param name="selector">The provider, algorithm, secret type, key size and curve to match, compared
    /// ordinally.</param>
    /// <param name="algorithm">When this method returns <c>true</c>, the matching row.</param>
    /// <returns><c>true</c> when a row matches the selector; otherwise <c>false</c>.</returns>
    bool TryGetAlgorithm(
        RemoteKeyAlgorithmSelector selector,
        [NotNullWhen(true)] out RemoteKeyAlgorithm? algorithm
    );
}
