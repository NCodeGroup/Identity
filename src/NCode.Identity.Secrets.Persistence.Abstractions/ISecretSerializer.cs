// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;
using JetBrains.Annotations;
using NCode.Identity.Secrets.Keys;
using NCode.Identity.Secrets.Persistence.DataContracts;
using NCode.Identity.Secrets.Persistence.Protection;
using NCode.Identity.Secrets.Verifiers;

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
    // See ADR-1235.
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
    // See ADR-1235.
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
    // See ADR-1235.
    SecretKey DeserializePublicSecret(PersistedSecret persistedSecret);

    /// <summary>
    /// Deserializes a <see cref="PersistedSecret"/> instance whose encoding is <see cref="SecretEncodings.Remote"/> as
    /// the key that its registered provider holds, from its stored <see cref="PersistedSecret.PublicKey"/>, without a
    /// protector.
    /// </summary>
    /// <param name="persistedSecret">The <see cref="PersistedSecret"/> instance to deserialize into an <see cref="SecretKey"/> instance.</param>
    /// <returns>The deserialized <see cref="SecretKey"/> instance.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when <paramref name="persistedSecret"/> is not a remote secret, or its provider, public key, algorithm
    /// or mapping row is missing.
    /// </exception>
    /// <exception cref="FormatException">
    /// Thrown when the remote key reference of <paramref name="persistedSecret"/> is malformed.
    /// </exception>
    // See ADR-1471.
    SecretKey DeserializeRemoteSecret(PersistedSecret persistedSecret);

    /// <summary>
    /// Deserializes a collection of <see cref="PersistedSecret"/> instances that hold one-way verifiers of shared
    /// secrets (<see cref="SecretEncodings.Verifier"/>) by unprotecting each verifier with a
    /// <see cref="ISecretProtector"/> and reading it into a <see cref="SecretVerifier"/>. A verifier is not key material,
    /// so <see cref="DeserializeSecrets(ISecretProtector, IReadOnlyCollection{PersistedSecret})"/> does not read it.
    /// </summary>
    /// <param name="protector">The <see cref="ISecretProtector"/> of the owner that the secrets belong to.</param>
    /// <param name="persistedSecrets">The <see cref="PersistedSecret"/> instances to deserialize, each with the
    /// <see cref="SecretEncodings.Verifier"/> encoding.</param>
    /// <returns>The collection of <see cref="SecretVerifier"/> instances.</returns>
    /// <exception cref="CryptographicException">
    /// Thrown when a verifier cannot be unprotected.
    /// </exception>
    /// <exception cref="FormatException">
    /// Thrown when a verifier names a scheme that is not known or has parameters that are not valid.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when a secret does not hold a verifier.
    /// </exception>
    // See ADR-1391.
    IReadOnlyCollection<SecretVerifier> DeserializeVerifiers(
        ISecretProtector protector,
        IReadOnlyCollection<PersistedSecret> persistedSecrets
    );
}
