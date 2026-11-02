// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers;
using JetBrains.Annotations;

namespace NCode.Identity.Jose;

/// <summary>
/// Provides an abstraction to encode a JOSE token. An instance is created from a set of options (via
/// <see cref="IJoseSerializer.CreateEncoder(JoseSigningOptions)"/> or
/// <see cref="IJoseSerializer.CreateEncoder(JoseEncryptionOptions)"/>) and carries all the credentials and options
/// needed to encode, so the <c>Encode</c> methods only require the payload and an optional destination.
/// </summary>
[PublicAPI]
public abstract class JoseEncoder
{
    /// <summary>
    /// Encodes a JOSE token given the specified payload.
    /// </summary>
    /// <param name="tokenWriter">The destination for the encoded JOSE token.</param>
    /// <param name="payload">The payload to encode.</param>
    public abstract void Encode(IBufferWriter<char> tokenWriter, ReadOnlySpan<byte> payload);

    /// <summary>
    /// Encodes a JOSE token given the specified payload.
    /// </summary>
    /// <param name="payload">The payload to encode.</param>
    /// <typeparam name="T">The type of the payload to encode.</typeparam>
    /// <returns>The encoded JOSE token.</returns>
    public abstract string Encode<T>(T payload);

    /// <summary>
    /// Encodes a JOSE token given the specified payload.
    /// </summary>
    /// <param name="tokenWriter">The destination for the encoded JOSE token.</param>
    /// <param name="payload">The payload to encode.</param>
    /// <typeparam name="T">The type of the payload to encode.</typeparam>
    public abstract void Encode<T>(IBufferWriter<char> tokenWriter, T payload);

    /// <summary>
    /// Encodes a JOSE token given the specified payload.
    /// </summary>
    /// <param name="payload">The payload to encode.</param>
    /// <returns>The encoded JOSE token.</returns>
    public abstract string Encode(string payload);

    /// <summary>
    /// Encodes a JOSE token given the specified payload.
    /// </summary>
    /// <param name="tokenWriter">The destination for the encoded JOSE token.</param>
    /// <param name="payload">The payload to encode.</param>
    public abstract void Encode(IBufferWriter<char> tokenWriter, string payload);

    /// <summary>
    /// Encodes a JOSE token given the specified payload.
    /// </summary>
    /// <param name="payload">The payload to encode.</param>
    /// <returns>The encoded JOSE token.</returns>
    public abstract string Encode(ReadOnlySpan<char> payload);

    /// <summary>
    /// Encodes a JOSE token given the specified payload.
    /// </summary>
    /// <param name="tokenWriter">The destination for the encoded JOSE token.</param>
    /// <param name="payload">The payload to encode.</param>
    public abstract void Encode(IBufferWriter<char> tokenWriter, ReadOnlySpan<char> payload);

    /// <summary>
    /// Encodes a JOSE token given the specified payload.
    /// </summary>
    /// <param name="payload">The payload to encode.</param>
    /// <returns>The encoded JOSE token.</returns>
    public abstract string Encode(ReadOnlySpan<byte> payload);
}
