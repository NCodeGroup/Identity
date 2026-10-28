#region Copyright Preamble

// Copyright @ 2026 NCode Group
//
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
//
//        http://www.apache.org/licenses/LICENSE-2.0
//
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.

#endregion

using NCode.Identity.Secrets.KeyWrapping;

namespace NCode.Identity.Secrets.Persistence.Protection;

/// <summary>
/// Provides the ability to find a root key provider by name. A wrapped key records the name of the provider that
/// wrapped it, so the catalog resolves the provider that can unwrap it as well as the provider that the options select
/// for wrapping new keys.
/// </summary>
// See ADR-0235.
internal interface IRootKeyProviderCatalog
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
    /// Gets the root key provider that the options select for wrapping new keys. When the options do not name a
    /// provider, the ephemeral provider is selected.
    /// </summary>
    /// <returns>The configured provider.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the selected provider is not registered.
    /// </exception>
    IKeyWrapper GetConfigured();
}
