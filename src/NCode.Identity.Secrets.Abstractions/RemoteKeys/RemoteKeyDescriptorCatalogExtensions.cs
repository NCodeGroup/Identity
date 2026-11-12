// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;

namespace NCode.Identity.Secrets.RemoteKeys;

/// <summary>
/// Provides extension members for the <see cref="IRemoteKeyDescriptorCatalog"/> abstraction that find a described
/// provider by name and say which described providers create keys, from its
/// <see cref="IRemoteKeyDescriptorCatalog.Providers"/>.
/// </summary>
// See ADR-1474.
[PublicAPI]
public static class RemoteKeyDescriptorCatalogExtensions
{
    /// <param name="catalog">The <see cref="IRemoteKeyDescriptorCatalog"/> instance.</param>
    extension(IRemoteKeyDescriptorCatalog catalog)
    {
        /// <summary>
        /// Gets the described remote key providers whose descriptor creates and lists keys, in ordinal name order.
        /// </summary>
        public IEnumerable<RemoteKeyProviderDescriptor> ProvidersThatCreateKeys
        {
            get
            {
                ArgumentNullException.ThrowIfNull(catalog);

                return catalog.Providers.Where(provider => provider.CreatesKeys);
            }
        }

        /// <summary>
        /// Looks up the descriptor of the described remote key provider with the given name.
        /// </summary>
        /// <param name="providerName">The provider name, compared ordinally.</param>
        /// <param name="descriptor">When this method returns <c>true</c>, the provider's descriptor.</param>
        /// <returns><c>true</c> when a described provider has the name; otherwise <c>false</c>.</returns>
        public bool TryGetProvider(
            string providerName,
            [NotNullWhen(true)] out RemoteKeyProviderDescriptor? descriptor
        )
        {
            ArgumentNullException.ThrowIfNull(catalog);
            ArgumentNullException.ThrowIfNull(providerName);

            descriptor = catalog.Providers.FirstOrDefault(provider =>
                string.Equals(provider.Name, providerName, StringComparison.Ordinal)
            );
            return descriptor is not null;
        }

        /// <summary>
        /// Determines whether the remote key provider with the given name is described and its descriptor creates and
        /// lists keys.
        /// </summary>
        /// <param name="providerName">The provider name, compared ordinally.</param>
        /// <returns><c>true</c> when the provider is described and creates keys; otherwise <c>false</c>.</returns>
        public bool CreatesKeys(string providerName)
        {
            ArgumentNullException.ThrowIfNull(catalog);

            return catalog.TryGetProvider(providerName, out var descriptor)
                && descriptor.CreatesKeys;
        }
    }
}
