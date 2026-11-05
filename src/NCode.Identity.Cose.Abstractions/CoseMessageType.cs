// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Cose;

/// <summary>
/// Describes the type of a COSE message (RFC 9052 §2).
/// </summary>
// The types are a closed set: RFC 9052 defines exactly these six message structures, each with its own tag, and a new
// structure needs a new specification and new processing here. See ADR-1409.
[PublicAPI]
public enum CoseMessageType
{
    /// <summary>
    /// A signed message with any number of signers, <c>COSE_Sign</c>.
    /// </summary>
    Sign,

    /// <summary>
    /// A signed message with a single signer, <c>COSE_Sign1</c>.
    /// </summary>
    Sign1,

    /// <summary>
    /// An encrypted message with any number of recipients, <c>COSE_Encrypt</c>.
    /// </summary>
    Encrypt,

    /// <summary>
    /// An encrypted message whose key is known to the recipient, <c>COSE_Encrypt0</c>.
    /// </summary>
    Encrypt0,

    /// <summary>
    /// A message authentication code message with any number of recipients, <c>COSE_Mac</c>.
    /// </summary>
    Mac,

    /// <summary>
    /// A message authentication code message whose key is known to the recipient, <c>COSE_Mac0</c>.
    /// </summary>
    Mac0,
}
