// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers;
using JetBrains.Annotations;

namespace NCode.Identity.Jose;

/// <summary>
/// Provides an abstraction to encode a JOSE token. An instance is created from a set of options (via
/// <see cref="IJoseSerializer.CreateEncoder(JoseSigningOptions)"/> or
/// <see cref="IJoseSerializer.CreateEncoder(JoseEncryptionOptions)"/>) and carries all the credentials and options
/// needed to encode, so the <c>EncodeAsync</c> methods only require the payload and an optional destination. Every
/// encode is asynchronous because a signing key may be held by a remote key management service.
/// </summary>
// See ADR-1472.
[PublicAPI]
public abstract class JoseEncoder
{
    /// <summary>
    /// Encodes a JOSE token given the specified payload.
    /// </summary>
    /// <param name="tokenWriter">The destination for the encoded JOSE token.</param>
    /// <param name="payload">The payload to encode.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>A task that completes when the token is written to <paramref name="tokenWriter"/>.</returns>
    public abstract ValueTask EncodeAsync(
        IBufferWriter<char> tokenWriter,
        ReadOnlyMemory<byte> payload,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Encodes a JOSE token given the specified payload.
    /// </summary>
    /// <param name="payload">The payload to encode.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <typeparam name="T">The type of the payload to encode.</typeparam>
    /// <returns>The encoded JOSE token.</returns>
    public abstract ValueTask<string> EncodeAsync<T>(
        T payload,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Encodes a JOSE token given the specified payload.
    /// </summary>
    /// <param name="tokenWriter">The destination for the encoded JOSE token.</param>
    /// <param name="payload">The payload to encode.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <typeparam name="T">The type of the payload to encode.</typeparam>
    /// <returns>A task that completes when the token is written to <paramref name="tokenWriter"/>.</returns>
    public abstract ValueTask EncodeAsync<T>(
        IBufferWriter<char> tokenWriter,
        T payload,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Encodes a JOSE token given the specified payload.
    /// </summary>
    /// <param name="payload">The payload to encode.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The encoded JOSE token.</returns>
    public abstract ValueTask<string> EncodeAsync(
        string payload,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Encodes a JOSE token given the specified payload.
    /// </summary>
    /// <param name="tokenWriter">The destination for the encoded JOSE token.</param>
    /// <param name="payload">The payload to encode.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>A task that completes when the token is written to <paramref name="tokenWriter"/>.</returns>
    public abstract ValueTask EncodeAsync(
        IBufferWriter<char> tokenWriter,
        string payload,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Encodes a JOSE token given the specified payload.
    /// </summary>
    /// <param name="payload">The payload to encode.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The encoded JOSE token.</returns>
    public abstract ValueTask<string> EncodeAsync(
        ReadOnlyMemory<char> payload,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Encodes a JOSE token given the specified payload.
    /// </summary>
    /// <param name="tokenWriter">The destination for the encoded JOSE token.</param>
    /// <param name="payload">The payload to encode.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>A task that completes when the token is written to <paramref name="tokenWriter"/>.</returns>
    public abstract ValueTask EncodeAsync(
        IBufferWriter<char> tokenWriter,
        ReadOnlyMemory<char> payload,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Encodes a JOSE token given the specified payload.
    /// </summary>
    /// <param name="payload">The payload to encode.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The encoded JOSE token.</returns>
    public abstract ValueTask<string> EncodeAsync(
        ReadOnlyMemory<byte> payload,
        CancellationToken cancellationToken
    );
}
