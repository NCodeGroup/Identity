// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers;
using System.Security.Cryptography;
using JetBrains.Annotations;

namespace NCode.Identity.Secrets.KeyWrapping;

/// <summary>
/// Provides the ability to wrap and unwrap a key with a key encryption key that the wrapper holds or reaches, for
/// example a key in a key management service or a key file that is mounted into the process. Each implementation is
/// identified by <see cref="Name"/> so that a wrapped key records which wrapper can unwrap it.
/// </summary>
// See ADR-1260.
[PublicAPI]
public interface IKeyWrapper
{
    /// <summary>
    /// Gets the name that identifies this wrapper, such as one of the values in <see cref="KeyWrapperNames"/>.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the problems with the configuration that this wrapper needs to wrap keys under its configured root key,
    /// without reaching the key management service.
    /// </summary>
    /// <returns>
    /// One message per problem, each naming the configuration entry; empty when the configuration is usable.
    /// </returns>
    IReadOnlyList<string> GetConfigurationFailures();

    /// <summary>
    /// Wraps a key with the key encryption key of this wrapper.
    /// </summary>
    /// <param name="key">The key to wrap.</param>
    /// <param name="context">
    /// The associated data that the wrapper binds to the wrapped key when the key encryption key supports it, so that
    /// the wrapped key can only be unwrapped with the same context.
    /// </param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The wrapped key together with the name of this wrapper and the identifier of the key encryption key.</returns>
    ValueTask<WrappedKey> WrapAsync(
        ReadOnlyMemory<byte> key,
        ReadOnlyMemory<byte> context,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Unwraps a key that was wrapped by <see cref="WrapAsync"/> and writes it to a writer that the caller provides.
    /// The wrapper advances <paramref name="destination"/> by the length of the key. The caller cannot know that
    /// length in advance, so it should pass a sensitive writer, such as one from
    /// <c>BufferFactory.CreatePooledBufferWriter(isSensitive: true, …)</c>, and is responsible for zeroing it when the
    /// key is no longer needed.
    /// </summary>
    /// <param name="wrappedKey">The wrapped key to unwrap.</param>
    /// <param name="context">
    /// The associated data that the wrapper binds to the wrapped key when the key encryption key supports it, which
    /// must match the context that was used to wrap the key.
    /// </param>
    /// <param name="destination">
    /// The writer that receives the unwrapped key. When the key cannot be unwrapped, the wrapper advances nothing and
    /// zeroes any span that it obtained from the writer.
    /// </param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <exception cref="CryptographicException">
    /// Thrown when the key cannot be unwrapped, for example because it was altered, because the context does not match,
    /// or because the key encryption key is not available.
    /// </exception>
    /// <exception cref="NotSupportedException">
    /// Thrown when the wrapper can only wrap, for example because it holds only the public half of an asymmetric key.
    /// </exception>
    ValueTask UnwrapAsync(
        WrappedKey wrappedKey,
        ReadOnlyMemory<byte> context,
        IBufferWriter<byte> destination,
        CancellationToken cancellationToken
    );
}
