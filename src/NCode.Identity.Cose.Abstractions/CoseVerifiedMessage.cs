// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using NCode.Identity.Cose.Algorithms;
using NCode.Identity.Secrets.Keys;

namespace NCode.Identity.Cose;

/// <summary>
/// The result of verifying or decrypting a COSE message. Its content is the only way to read the content of a message,
/// so the content is readable only once the signature, the authentication code or the authentication tag has verified.
/// </summary>
// See ADR-1409.
[PublicAPI]
public sealed class CoseVerifiedMessage
{
    /// <summary>
    /// Gets the type of the message.
    /// </summary>
    public required CoseMessageType MessageType { get; init; }

    /// <summary>
    /// Gets the verified content, or the authenticated plaintext.
    /// </summary>
    public required ReadOnlyMemory<byte> Content { get; init; }

    /// <summary>
    /// Gets the protected header bucket of the message body.
    /// </summary>
    public required IReadOnlyDictionary<CoseLabel, CoseValue> ProtectedHeaders { get; init; }

    /// <summary>
    /// Gets the unprotected header bucket of the message body.
    /// </summary>
    public required IReadOnlyDictionary<CoseLabel, CoseValue> UnprotectedHeaders { get; init; }

    /// <summary>
    /// Gets the signature, message authentication code or content encryption algorithm that verified or decrypted the
    /// message.
    /// </summary>
    public required CoseAlgorithm Algorithm { get; init; }

    /// <summary>
    /// Gets the key that verified the signature or the authentication code, or that decrypted the message: the direct
    /// key or the recipient key.
    /// </summary>
    public required SecretKey Key { get; init; }

    /// <summary>
    /// Gets the headers of the <c>COSE_Sign</c> signature that verified, or <c>null</c> for another message type.
    /// </summary>
    public CoseLayerHeaders? Signature { get; init; }

    /// <summary>
    /// Gets the headers of the recipient that yielded the key, or <c>null</c> when the message has no recipient.
    /// </summary>
    public CoseLayerHeaders? Recipient { get; init; }
}
