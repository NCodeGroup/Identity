// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;

namespace NCode.Identity.Secrets.KeyTypes;

/// <summary>
/// Provides lookup over the registered <see cref="ISecretKeyTypeDescriptor"/> instances — the single source of
/// truth for the server's secret key-type taxonomy and capabilities.
/// </summary>
// See ADR-1067.
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
