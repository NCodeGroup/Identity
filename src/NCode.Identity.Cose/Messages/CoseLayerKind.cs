// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.Cose.Messages;

/// <summary>
/// Names the layer structures of a COSE message (RFC 9052 §2) that share one parser.
/// </summary>
// See ADR-1409.
internal enum CoseLayerKind
{
    /// <summary>
    /// The body of a single-layer message.
    /// </summary>
    Body,

    /// <summary>
    /// The body of a <c>COSE_Sign</c> message, which carries no algorithm because each signature has its own.
    /// </summary>
    SignBody,

    /// <summary>
    /// A <c>COSE_Signature</c>.
    /// </summary>
    Signature,

    /// <summary>
    /// A <c>COSE_Recipient</c>.
    /// </summary>
    Recipient,
}
