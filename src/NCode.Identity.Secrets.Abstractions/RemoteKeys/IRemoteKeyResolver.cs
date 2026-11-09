// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;
using JetBrains.Annotations;
using NCode.Identity.Secrets.Keys;

namespace NCode.Identity.Secrets.RemoteKeys;

/// <summary>
/// Resolves the provider and the row for a key that a key management service holds, and reads the key's public half
/// into an <see cref="IRemoteSecretKey"/>.
/// </summary>
// See ADR-1479.
[PublicAPI]
public interface IRemoteKeyResolver
{
    /// <summary>
    /// Finds, without calling a key management service, the registered provider and the row of the mapping
    /// vocabulary that apply to a key.
    /// </summary>
    /// <param name="selector">Identifies the provider, algorithm, secret type, key size and curve of the key.</param>
    /// <returns>The provider and the row.</returns>
    /// <exception cref="RemoteKeyRefusedException">
    /// The secret type cannot be held in a key management service, the provider is not registered, or the provider
    /// has no row for the selector.
    /// </exception>
    RemoteKeyResolution Resolve(RemoteKeyAlgorithmSelector selector);

    /// <summary>
    /// Reads the public half of a key with <see cref="IRemoteKeyProvider.GetPublicKeyAsync"/>, then creates the key as
    /// <see cref="CreateKey"/> does.
    /// </summary>
    /// <param name="binding">Binds the key to its provider, its reference and its row.</param>
    /// <param name="metadata">The metadata of the key.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The <see cref="IRemoteSecretKey"/> that holds the public half.</returns>
    /// <exception cref="RemoteKeyRefusedException">
    /// The public half is unreadable or is not a key of the row's type, size and curve.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// The key's identifier is not one the provider reads, or the row names a key specification the provider does not
    /// know.
    /// </exception>
    /// <exception cref="CryptographicException">
    /// The provider refuses the key: its usage, specification or signing algorithms do not match the row, or it holds
    /// no public key.
    /// </exception>
    /// <exception cref="IOException">The service's response fails an integrity check.</exception>
    ValueTask<SecretKey> ReadKeyAsync(
        RemoteKeyBinding binding,
        KeyMetadata metadata,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Creates the <see cref="IRemoteSecretKey"/> from the public half. The key's <c>use</c> and <c>alg</c> are the
    /// row's, and its id and lifecycle are the metadata's.
    /// </summary>
    /// <param name="binding">Binds the key to its provider, its reference and its row.</param>
    /// <param name="metadata">The metadata of the key.</param>
    /// <param name="subjectPublicKeyInfo">The DER-encoded <c>SubjectPublicKeyInfo</c> of the public half.</param>
    /// <returns>The <see cref="IRemoteSecretKey"/> that holds the public half.</returns>
    /// <exception cref="RemoteKeyRefusedException">
    /// The public half is unreadable or is not a key of the row's type, size and curve.
    /// </exception>
    SecretKey CreateKey(
        RemoteKeyBinding binding,
        KeyMetadata metadata,
        ReadOnlySpan<byte> subjectPublicKeyInfo
    );
}
