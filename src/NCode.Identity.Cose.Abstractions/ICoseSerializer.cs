// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Cose;

/// <summary>
/// Reads and writes CBOR Object Signing and Encryption (COSE) messages (RFC 9052). Reading is strict and bounded, and
/// writing is deterministic.
/// </summary>
// See ADR-1409.
[PublicAPI]
public interface ICoseSerializer
{
    /// <summary>
    /// Reads the type and the headers of a message without verifying it. The result carries no content.
    /// </summary>
    /// <param name="message">The encoded message.</param>
    /// <param name="untaggedMessageType">The type of the message when it carries no tag, or <c>null</c> to refuse an
    /// untagged message.</param>
    /// <returns>The type and headers of the message.</returns>
    /// <exception cref="CoseException">The message is malformed or exceeds a limit.</exception>
    CoseMessageInfo Inspect(ReadOnlyMemory<byte> message, CoseMessageType? untaggedMessageType);

    /// <summary>
    /// Decodes a CBOR map with integer and text labels, such as a CBOR Web Token claims set, under the size, depth and
    /// strictness rules of the library.
    /// </summary>
    /// <param name="encodedMap">The encoded map.</param>
    /// <returns>The entries of the map.</returns>
    /// <exception cref="CoseException">The map is malformed or exceeds a limit.</exception>
    IReadOnlyDictionary<CoseLabel, CoseValue> DecodeMap(ReadOnlyMemory<byte> encodedMap);

    /// <summary>
    /// Encodes a CBOR map with its entries in deterministic order (RFC 8949 §4.2.1).
    /// </summary>
    /// <param name="map">The entries.</param>
    /// <returns>The encoded map.</returns>
    /// <exception cref="ArgumentException">A value is not a definite-length item within the nesting limit.</exception>
    byte[] EncodeMap(IReadOnlyDictionary<CoseLabel, CoseValue> map);
}
