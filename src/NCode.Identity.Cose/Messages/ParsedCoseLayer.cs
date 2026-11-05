// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.Cose.Messages;

/// <summary>
/// Holds one parsed layer of a COSE message: the message body, a signature or a recipient.
/// </summary>
internal sealed class ParsedCoseLayer
{
    /// <summary>
    /// Gets the protected bucket exactly as received.
    /// </summary>
    public required ReadOnlyMemory<byte> ProtectedBytes { get; init; }

    /// <summary>
    /// Gets the decoded protected bucket.
    /// </summary>
    public required IReadOnlyDictionary<CoseLabel, CoseValue> Protected { get; init; }

    /// <summary>
    /// Gets the decoded unprotected bucket.
    /// </summary>
    public required IReadOnlyDictionary<CoseLabel, CoseValue> Unprotected { get; init; }

    /// <summary>
    /// Gets the signature of a signature layer, or the ciphertext of a recipient layer, and no bytes for a message body.
    /// </summary>
    public ReadOnlyMemory<byte> Third { get; init; }

    /// <summary>
    /// Gets the protected bucket as the signed, authenticated and encrypted structures carry it: no bytes when the
    /// bucket holds no parameter, otherwise the bytes as received.
    /// </summary>
    // See ADR-1409.
    public ReadOnlyMemory<byte> EffectiveProtected =>
        Protected.Count == 0 ? default : ProtectedBytes;

    /// <summary>
    /// Gets the key identifier of the layer from either bucket, or <c>null</c> when it has none.
    /// </summary>
    public ReadOnlyMemory<byte>? KeyId
    {
        get
        {
            if (
                Protected.TryGetValue(CoseHeaderLabels.KeyId, out var value)
                || Unprotected.TryGetValue(CoseHeaderLabels.KeyId, out value)
            )
                return value.TryGetByteString(out var bytes) ? bytes : null;

            return null;
        }
    }
}
