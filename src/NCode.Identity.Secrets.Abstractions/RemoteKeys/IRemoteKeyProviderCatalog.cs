// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;

namespace NCode.Identity.Secrets.RemoteKeys;

/// <summary>
/// Provides lookup over the registered <see cref="IRemoteKeyProvider"/> instances. Code that only needs to know
/// which providers and rows exist reads <see cref="IRemoteKeyDescriptorCatalog"/> instead.
/// </summary>
// See ADR-1471.
// See ADR-1474.
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
}
