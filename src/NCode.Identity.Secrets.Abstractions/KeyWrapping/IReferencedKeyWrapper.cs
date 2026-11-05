// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;

namespace NCode.Identity.Secrets.KeyWrapping;

/// <summary>
/// Provides the ability of a key wrapper to wrap a key under a key encryption key that the caller names at run time, in
/// addition to the key encryption key that the wrapper is configured with. Unwrapping needs nothing more than
/// <see cref="IKeyWrapper.UnwrapAsync"/>, because <see cref="WrappedKey.KeyId"/> identifies the exact key.
/// </summary>
// See ADR-1415.
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
    /// Wraps a key with the key encryption key that <paramref name="keyReference"/> names, with the same binding, checks
    /// and error mapping as <see cref="IKeyWrapper.WrapAsync(ReadOnlyMemory{byte}, ReadOnlyMemory{byte}, CancellationToken)"/>.
    /// </summary>
    /// <param name="keyReference">The reference to the key encryption key.</param>
    /// <param name="key">The key to wrap.</param>
    /// <param name="context">The associated data that the wrapper binds to the wrapped key.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>
    /// The wrapped key. Its <see cref="WrappedKey.KeyId"/> is the identifier that <see cref="IKeyWrapper.UnwrapAsync"/>
    /// needs to address the key without any configuration.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="keyReference"/> fails <see cref="TryValidateKeyReference"/>.
    /// </exception>
    ValueTask<WrappedKey> WrapAsync(
        string keyReference,
        ReadOnlyMemory<byte> key,
        ReadOnlyMemory<byte> context,
        CancellationToken cancellationToken
    );
}
