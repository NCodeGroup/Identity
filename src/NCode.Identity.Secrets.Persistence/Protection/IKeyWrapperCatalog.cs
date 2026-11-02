// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.Secrets.KeyWrapping;

namespace NCode.Identity.Secrets.Persistence.Protection;

/// <summary>
/// Provides the ability to find a key wrapper by name, and the key wrapper that the options select as the root key
/// wrapper. A wrapped key records the name of the key wrapper that wrapped it, so the catalog resolves the key wrapper
/// that can unwrap it as well as the one that the options select for wrapping new keys.
/// </summary>
// See ADR-1235.
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
}
