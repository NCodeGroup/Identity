// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;

namespace NCode.Identity.Secrets.RemoteKeys;

/// <summary>
/// Provides lookup over the registered <see cref="IRemoteKeyProvider"/> instances and the rows of the mapping
/// vocabulary that <see cref="IRemoteSigningAlgorithmSource"/> instances contribute.
/// </summary>
// See ADR-1471.
[PublicAPI]
public interface IRemoteKeyProviderCatalog
{
    /// <summary>
    /// Gets the registered remote key providers.
    /// </summary>
    IReadOnlyCollection<IRemoteKeyProvider> Providers { get; }

    /// <summary>
    /// Looks up the provider with the specified name.
    /// </summary>
    /// <param name="name">The provider name, compared ordinally. See <see cref="RemoteKeyProviderNames"/>.</param>
    /// <param name="provider">When this method returns <c>true</c>, the matching provider.</param>
    /// <returns><c>true</c> when a provider is registered with the name; otherwise <c>false</c>.</returns>
    bool TryGetProvider(string name, [NotNullWhen(true)] out IRemoteKeyProvider? provider);

    /// <summary>
    /// Looks up the row of the mapping vocabulary that the selector names.
    /// </summary>
    /// <param name="selector">The provider, algorithm, secret type, key size and curve to match, compared
    /// ordinally.</param>
    /// <param name="algorithm">When this method returns <c>true</c>, the matching row.</param>
    /// <returns><c>true</c> when a row matches the selector; otherwise <c>false</c>.</returns>
    bool TryGetAlgorithm(
        RemoteSigningAlgorithmSelector selector,
        [NotNullWhen(true)] out RemoteSigningAlgorithm? algorithm
    );
}
