// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.Cose.Messages;

/// <summary>
/// Holds a parsed COSE message of any of the six types.
/// </summary>
internal sealed class ParsedCoseMessage
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
    /// Gets the message body: its two header buckets.
    /// </summary>
    public required ParsedCoseLayer Body { get; init; }

    /// <summary>
    /// Gets the payload or the ciphertext, or <c>null</c> when it is detached.
    /// </summary>
    public ReadOnlyMemory<byte>? Content { get; init; }

    /// <summary>
    /// Gets the signature of a <c>COSE_Sign1</c> message or the authentication code of a <c>COSE_Mac0</c> or
    /// <c>COSE_Mac</c> message.
    /// </summary>
    public ReadOnlyMemory<byte> Authenticator { get; init; }

    /// <summary>
    /// Gets the signatures of a <c>COSE_Sign</c> message.
    /// </summary>
    public IReadOnlyList<ParsedCoseLayer> Signatures { get; init; } = [];

    /// <summary>
    /// Gets the recipients of a <c>COSE_Mac</c> or <c>COSE_Encrypt</c> message.
    /// </summary>
    public IReadOnlyList<ParsedCoseLayer> Recipients { get; init; } = [];
}
