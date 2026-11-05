// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using JetBrains.Annotations;

namespace NCode.Identity.Secrets.KeyWrapping;

/// <summary>
/// Provides the ability of a key wrapper to wrap a key under a key encryption key that the caller names at run time, in
/// addition to the key encryption key that the wrapper is configured with, and to reach that key through an identity
/// that the holder of the key grants. Unwrapping through the identity of the deployment needs nothing more than
/// <see cref="IKeyWrapper.UnwrapAsync"/>, because <see cref="WrappedKey.KeyId"/> identifies the exact key. Unwrapping a
/// key that is reached through an identity that the holder grants takes that identity, through
/// <see cref="UnwrapAsync(WrappedKey, KeyAccess, ReadOnlyMemory{byte}, IBufferWriter{byte}, CancellationToken)"/>.
/// </summary>
// See ADR-1415 and ADR-1429.
[PublicAPI]
public interface IReferencedKeyWrapper : IKeyWrapper
{
    /// <summary>
    /// Checks the syntax of a key reference, without reaching the key management service.
    /// </summary>
    /// <param name="keyReference">The reference to the key encryption key, such as a key identifier or an ARN.</param>
    /// <param name="failure">
    /// When this method returns <see langword="false"/>, a phrase that follows the name of the field, such as
    /// "must be the ARN of a KMS key"; otherwise <see langword="null"/>.
    /// </param>
    /// <returns><see langword="true"/> when the reference is well formed; otherwise, <see langword="false"/>.</returns>
    bool TryValidateKeyReference(string keyReference, [NotNullWhen(false)] out string? failure);

    /// <summary>
    /// Checks the syntax of an access identity, without reaching the key management service.
    /// </summary>
    /// <param name="accessIdentity">
    /// The identity that the wrapper takes on to reach the key, such as an IAM role ARN, a service account email address
    /// or a Microsoft Entra tenant ID.
    /// </param>
    /// <param name="failure">
    /// When this method returns <see langword="false"/>, a phrase that follows the name of the field, such as
    /// "must be the ARN of an IAM role"; otherwise <see langword="null"/>.
    /// </param>
    /// <returns><see langword="true"/> when the identity is well formed; otherwise, <see langword="false"/>.</returns>
    bool TryValidateAccessIdentity(string accessIdentity, [NotNullWhen(false)] out string? failure);

    /// <summary>
    /// Wraps a key with the key encryption key that <paramref name="referencedKey"/> names, reaching it through the
    /// identity that its access names, or through the identity of the deployment when the access is
    /// <see langword="null"/>, with the same binding, checks and error mapping as
    /// <see cref="IKeyWrapper.WrapAsync(ReadOnlyMemory{byte}, ReadOnlyMemory{byte}, CancellationToken)"/>.
    /// </summary>
    /// <param name="referencedKey">The reference to the key encryption key and the optional identity that reaches it.</param>
    /// <param name="key">The key to wrap.</param>
    /// <param name="context">The associated data that the wrapper binds to the wrapped key.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>
    /// The wrapped key. Its <see cref="WrappedKey.KeyId"/> is the identifier that
    /// <see cref="IKeyWrapper.UnwrapAsync"/> and
    /// <see cref="UnwrapAsync(WrappedKey, KeyAccess, ReadOnlyMemory{byte}, IBufferWriter{byte}, CancellationToken)"/>
    /// need to address the key without any configuration.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// Thrown when the key reference of <paramref name="referencedKey"/> fails <see cref="TryValidateKeyReference"/>, or
    /// its access identity is set and fails <see cref="TryValidateAccessIdentity"/>.
    /// </exception>
    ValueTask<WrappedKey> WrapAsync(
        ReferencedKey referencedKey,
        ReadOnlyMemory<byte> key,
        ReadOnlyMemory<byte> context,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Unwraps a key that was wrapped under a key encryption key that the access reaches, with the same binding, checks
    /// and error mapping as <see cref="IKeyWrapper.UnwrapAsync"/>.
    /// </summary>
    /// <param name="wrappedKey">The wrapped key to unwrap.</param>
    /// <param name="access">The identity that reaches the key encryption key.</param>
    /// <param name="context">The associated data that must match the context that was used to wrap the key.</param>
    /// <param name="destination">The writer that receives the unwrapped key.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <exception cref="CryptographicException">
    /// Thrown when the key cannot be unwrapped, for example because it was altered or because the context does not match.
    /// </exception>
    ValueTask UnwrapAsync(
        WrappedKey wrappedKey,
        KeyAccess access,
        ReadOnlyMemory<byte> context,
        IBufferWriter<byte> destination,
        CancellationToken cancellationToken
    );
}
