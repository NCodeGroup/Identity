// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Cose;

/// <summary>
/// Describes a COSE message by its type and headers only. It carries no content, signature, authentication code or
/// ciphertext, so the content is readable only from verification or decryption. The headers are returned as received
/// and nothing has authenticated them yet, the unprotected bucket in particular, so a caller uses them, such as
/// <c>kid</c>, only to choose the keys that verification then tries.
/// </summary>
// See ADR-1409.
[PublicAPI]
public sealed class CoseMessageInfo
{
    /// <summary>
    /// Gets the type of the message.
    /// </summary>
    public required CoseMessageType MessageType { get; init; }

    /// <summary>
    /// Gets a value indicating whether the message carries its COSE tag.
    /// </summary>
    public required bool IsTagged { get; init; }

    /// <summary>
    /// Gets a value indicating whether the content, or the ciphertext, is detached from the message.
    /// </summary>
    public required bool IsContentDetached { get; init; }

    /// <summary>
    /// Gets the protected header bucket of the message body.
    /// </summary>
    public required IReadOnlyDictionary<CoseLabel, CoseValue> ProtectedHeaders { get; init; }

    /// <summary>
    /// Gets the unprotected header bucket of the message body.
    /// </summary>
    public required IReadOnlyDictionary<CoseLabel, CoseValue> UnprotectedHeaders { get; init; }

    /// <summary>
    /// Gets the headers of each signature of a <c>COSE_Sign</c> message, and no entry for any other message type.
    /// </summary>
    public required IReadOnlyList<CoseLayerHeaders> Signatures { get; init; }

    /// <summary>
    /// Gets the headers of each recipient of a <c>COSE_Mac</c> or <c>COSE_Encrypt</c> message, and no entry for any
    /// other message type.
    /// </summary>
    public required IReadOnlyList<CoseLayerHeaders> Recipients { get; init; }
}
