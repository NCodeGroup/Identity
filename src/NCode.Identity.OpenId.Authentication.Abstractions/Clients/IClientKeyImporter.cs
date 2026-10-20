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
using System.Text.Json;
using JetBrains.Annotations;
using NCode.Identity.Secrets.Persistence;
using NCode.Identity.Secrets.Persistence.DataContracts;

namespace NCode.Identity.OpenId.Authentication.Clients;

/// <summary>
/// Imports a client's public keys under one policy at every point they enter the server: the JSON Web Key Set a client
/// registers by value at dynamic client registration, the JSON Web Key Set a client publishes at its <c>jwks_uri</c>,
/// and a single key registered for a client through the management API. A client key is a public key of a registered
/// key type, carries no private or symmetric material, declares a <c>use</c>, <c>key_ops</c>, and <c>alg</c> that agree
/// with each other and with the key, and has a <c>kid</c> that is unique among the client's keys.
/// </summary>
// See ADR-0161.
[PublicAPI]
public interface IClientKeyImporter
{
    /// <summary>
    /// Imports the JSON Web Key Set a client registers by value. Every key must satisfy the client-key policy and carry
    /// a <c>kid</c> unique within the set; a key of a type or for an algorithm the server does not support is an error,
    /// because the client would be registering a key that can never be used.
    /// </summary>
    /// <param name="keySet">The JSON Web Key Set document, an object with a <c>keys</c> array.</param>
    /// <param name="createdWhen">The instant the imported keys are created.</param>
    /// <param name="secrets">When this method returns <c>true</c>, the imported keys, identified by their <c>kid</c>.</param>
    /// <param name="error">When this method returns <c>false</c>, a human-readable reason the set is refused.</param>
    /// <returns><c>true</c> when every key was imported; otherwise <c>false</c>.</returns>
    bool TryImportRegisteredKeySet(
        JsonElement keySet,
        DateTimeOffset createdWhen,
        [NotNullWhen(true)] out IReadOnlyList<PersistedSecret>? secrets,
        [NotNullWhen(false)] out string? error
    );

    /// <summary>
    /// Imports the JSON Web Key Set a client publishes at its <c>jwks_uri</c>. As RFC 7517 §5 directs, a key the
    /// server cannot use — one of an unrecognized type or algorithm, without a <c>kid</c>, malformed, or with metadata
    /// that contradicts the key — is ignored, because the set may also serve other parties. A set that exposes private
    /// or symmetric key material, or that gives two usable keys the same <c>kid</c>, is refused as a whole, so a
    /// compromised or ambiguous set is never used.
    /// </summary>
    /// <param name="keySet">The JSON Web Key Set document, an object with a <c>keys</c> array.</param>
    /// <param name="createdWhen">The instant the imported keys are created.</param>
    /// <param name="secrets">When this method returns <c>true</c>, the imported keys, identified by their <c>kid</c>.</param>
    /// <param name="error">When this method returns <c>false</c>, a human-readable reason the set is refused.</param>
    /// <returns><c>true</c> when the set was accepted; otherwise <c>false</c>.</returns>
    bool TryImportPublishedKeySet(
        JsonElement keySet,
        DateTimeOffset createdWhen,
        [NotNullWhen(true)] out IReadOnlyList<PersistedSecret>? secrets,
        [NotNullWhen(false)] out string? error
    );

    /// <summary>
    /// Imports a single public key registered for a client in any supported format. A key supplied as a JSON Web Key
    /// keeps the <c>kid</c>, <c>use</c>, and <c>alg</c> it declares, which the request may repeat but not contradict; a
    /// key supplied in another format is identified by <see cref="SecretKeyImportRequest.KeyId"/>, or by its secret
    /// identifier when that is unset. Uniqueness of the <c>kid</c> among the client's other keys is the caller's to
    /// check, since only the caller knows them.
    /// </summary>
    /// <param name="request">The key material and the metadata to import it with.</param>
    /// <param name="secret">When this method returns <c>true</c>, the imported key.</param>
    /// <param name="error">When this method returns <c>false</c>, a human-readable reason the key is refused.</param>
    /// <returns><c>true</c> when the key was imported; otherwise <c>false</c>.</returns>
    bool TryImportKey(
        SecretKeyImportRequest request,
        [NotNullWhen(true)] out PersistedSecret? secret,
        [NotNullWhen(false)] out string? error
    );
}
