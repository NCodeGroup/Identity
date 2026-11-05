// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using NCode.Identity.Secrets.KeyWrapping;

namespace NCode.Identity.Secrets.Persistence.Protection;

/// <summary>
/// Provides the ability to find a key wrapper by name, and the key wrapper that the options select as the root key
/// wrapper. A wrapped key records the name of the key wrapper that wrapped it, so the catalog resolves the key wrapper
/// that can unwrap it as well as the one that the options select for wrapping new keys.
/// </summary>
// See ADR-1235 and ADR-1415.
internal interface IKeyWrapperCatalog
{
    /// <summary>
    /// Gets the root key provider that has the specified name.
    /// </summary>
    /// <param name="providerName">The name of the provider, compared ordinally.</param>
    /// <returns>The registered provider with the specified name.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when no provider is registered with the specified name.
    /// </exception>
    IKeyWrapper Get(string providerName);

    /// <summary>
    /// Gets the root key provider that the options select for wrapping new keys. The options must name a provider:
    /// none is selected by default.
    /// </summary>
    /// <returns>The configured provider.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the options name no provider, or the named provider is not registered.
    /// </exception>
    IKeyWrapper GetConfigured();

    /// <summary>
    /// Gets the key wrapper that has the specified name and wraps under a key that the caller names.
    /// </summary>
    /// <param name="name">The name of the key wrapper, compared ordinally.</param>
    /// <param name="wrapper">The registered key wrapper, when it is found.</param>
    /// <returns>
    /// <see langword="true"/> when a key wrapper with the name is registered and implements
    /// <see cref="IReferencedKeyWrapper"/>; otherwise, <see langword="false"/>.
    /// </returns>
    bool TryGetReferenced(string name, [NotNullWhen(true)] out IReferencedKeyWrapper? wrapper);

    /// <summary>
    /// Gets the key wrapper that has the specified name and wraps under a key that the caller names.
    /// </summary>
    /// <param name="name">The name of the key wrapper, compared ordinally.</param>
    /// <returns>The registered key wrapper.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when no key wrapper with the name is registered, or it does not implement
    /// <see cref="IReferencedKeyWrapper"/>.
    /// </exception>
    IReferencedKeyWrapper GetReferenced(string name);
}
