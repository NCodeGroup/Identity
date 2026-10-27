#region Copyright Preamble

//
//    Copyright @ 2023 NCode Group
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

using System.Security.Cryptography;
using JetBrains.Annotations;
using NCode.Identity.Secrets.Keys;
using NCode.Identity.Secrets.Persistence.DataContracts;
using NCode.Identity.Secrets.Persistence.Protection;

namespace NCode.Identity.Secrets.Persistence;

/// <summary>
/// Provides the ability to serialize and deserialize secrets from persisted storage.
/// </summary>
[PublicAPI]
public interface ISecretSerializer
{
    /// <summary>
    /// Deserializes a collection of <see cref="PersistedSecret"/> instances by unprotecting their private and symmetric
    /// key material with a <see cref="ISecretProtector"/> and converting them into a collection of
    /// <see cref="SecretKey"/> instances.
    /// The resulting collection is sorted descending by the <see cref="KeyMetadata.ExpiresWhen"/> property.
    /// </summary>
    /// <param name="protector">The <see cref="ISecretProtector"/> of the owner that the secrets belong to.</param>
    /// <param name="persistedSecrets">The <see cref="PersistedSecret"/> instances to deserialize into <see cref="SecretKey"/> instances.</param>
    /// <returns>The collection of <see cref="SecretKey"/> instances.</returns>
    /// <exception cref="CryptographicException">
    /// Thrown when the key material of a secret cannot be unprotected.
    /// </exception>
    // See ADR-0235.
    IReadOnlyCollection<SecretKey> DeserializeSecrets(
        ISecretProtector protector,
        IReadOnlyCollection<PersistedSecret> persistedSecrets
    );

    /// <summary>
    /// Deserializes a <see cref="PersistedSecret"/> instance by unprotecting its private or symmetric key material with a
    /// <see cref="ISecretProtector"/> and converting it into a disposable <see cref="SecretKey"/> instance.
    /// </summary>
    /// <param name="protector">The <see cref="ISecretProtector"/> of the owner that the secret belongs to.</param>
    /// <param name="persistedSecret">The <see cref="PersistedSecret"/> instance to deserialize into an <see cref="SecretKey"/> instance.</param>
    /// <returns>The deserialized <see cref="SecretKey"/> instance.</returns>
    /// <exception cref="CryptographicException">
    /// Thrown when the key material of the secret cannot be unprotected.
    /// </exception>
    // See ADR-0235.
    SecretKey DeserializeSecret(ISecretProtector protector, PersistedSecret persistedSecret);

    /// <summary>
    /// Deserializes the public half of a secret without a protector: its own material when its encoding is public, or its
    /// stored <see cref="PersistedSecret.PublicKey"/> when it holds a private key.
    /// </summary>
    /// <param name="persistedSecret">The <see cref="PersistedSecret"/> instance to deserialize into an <see cref="SecretKey"/> instance.</param>
    /// <returns>The deserialized <see cref="SecretKey"/> instance.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when <paramref name="persistedSecret"/> holds no public key material.
    /// </exception>
    // See ADR-0235.
    SecretKey DeserializePublicSecret(PersistedSecret persistedSecret);
}
