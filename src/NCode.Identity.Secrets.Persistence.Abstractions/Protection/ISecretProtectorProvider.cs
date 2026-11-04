// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;
using JetBrains.Annotations;

namespace NCode.Identity.Secrets.Persistence.Protection;

/// <summary>
/// Provides the <see cref="ISecretProtector"/> of a <see cref="SecretOwner"/>. Obtaining a protector is asynchronous
/// because it may load the master keys of the owner from a store and unwrap them through a root key provider; the
/// protector itself is synchronous.
/// </summary>
/// <remarks>
/// A protector is short-lived, so callers obtain one for a single request or batch and must not cache it.
/// </remarks>
// See ADR-1235.
[PublicAPI]
public interface ISecretProtectorProvider
{
    /// <summary>
    /// Gets the <see cref="ISecretProtector"/> of the specified owner. This method loads the master keys of the owner,
    /// creating the first master key when the owner has none, and guarantees that every value in
    /// <paramref name="protectedValues"/> can be unprotected by the returned protector.
    /// </summary>
    /// <param name="owner">The <see cref="SecretOwner"/> whose master keys protect the values.</param>
    /// <param name="protectedValues">The protected values that the returned protector must be able to unprotect. Values
    /// that were not protected by a master key are ignored, and fail when they are unprotected.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the
    /// <see cref="ISecretProtector"/> of the owner.</returns>
    /// <exception cref="CryptographicException">
    /// Thrown when a master key that one of the values requires does not exist or cannot be unwrapped.
    /// </exception>
    ValueTask<ISecretProtector> GetProtectorAsync(
        SecretOwner owner,
        IReadOnlyCollection<string> protectedValues,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Reads the identifier of the master key that protects a value, without loading any master key. The identifier is
    /// not secret.
    /// </summary>
    /// <param name="protectedValue">The protected value.</param>
    /// <param name="masterKeyId">The identifier of the master key that protects the value, when the method returns
    /// <see langword="true"/>.</param>
    /// <returns><see langword="true"/> when the value is a protected value that names a master key; otherwise
    /// <see langword="false"/>.</returns>
    bool TryReadMasterKeyId(ReadOnlySpan<char> protectedValue, out Guid masterKeyId);

    /// <summary>
    /// Removes the master keys of the specified owner from memory so that the next call to
    /// <see cref="GetProtectorAsync"/> loads them again. Protectors that were already obtained remain usable.
    /// </summary>
    /// <param name="owner">The <see cref="SecretOwner"/> whose master keys are removed from memory.</param>
    void Evict(SecretOwner owner);

    /// <summary>
    /// Removes the master keys of every owner from memory so that the next protector of each owner loads them again.
    /// Protectors that were already obtained remain usable.
    /// </summary>
    void EvictAll();
}
