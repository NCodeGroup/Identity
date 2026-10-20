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

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;

namespace NCode.Identity.Secrets.KeyTypes;

/// <summary>
/// Provides lookup over the registered <see cref="ISecretKeyTypeDescriptor"/> instances — the single source of
/// truth for the server's secret key-type taxonomy and capabilities.
/// </summary>
// See ADR-0067.
[PublicAPI]
public interface ISecretKeyTypeCatalog
{
    /// <summary>
    /// Gets the registered secret key-type descriptors.
    /// </summary>
    IReadOnlyCollection<ISecretKeyTypeDescriptor> Descriptors { get; }

    /// <summary>
    /// Looks up the descriptor for the specified key type.
    /// </summary>
    /// <param name="keyType">The key-type identifier. See <see cref="SecretKeyTypes"/>.</param>
    /// <param name="descriptor">When this method returns <c>true</c>, the matching descriptor.</param>
    /// <returns><c>true</c> when a descriptor is registered for the key type; otherwise <c>false</c>.</returns>
    bool TryGet(string keyType, [NotNullWhen(true)] out ISecretKeyTypeDescriptor? descriptor);

    /// <summary>
    /// Looks up the descriptor whose keys a JSON Web Key of the specified <c>kty</c> represents.
    /// </summary>
    /// <param name="jwkKeyType">The JSON Web Key <c>kty</c> value, compared ordinally.</param>
    /// <param name="descriptor">When this method returns <c>true</c>, the matching descriptor.</param>
    /// <returns><c>true</c> when a descriptor is registered for the <c>kty</c>; otherwise <c>false</c>.</returns>
    bool TryGetByJwkKeyType(
        string jwkKeyType,
        [NotNullWhen(true)] out ISecretKeyTypeDescriptor? descriptor
    );
}
