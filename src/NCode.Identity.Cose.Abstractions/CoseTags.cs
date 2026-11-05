// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Cose;

/// <summary>
/// Contains the CBOR tags that identify the COSE message types (RFC 9052 §2).
/// </summary>
[PublicAPI]
public static class CoseTags
{
    /// <summary>
    /// The tag of <c>COSE_Sign</c>.
    /// </summary>
    public const ulong Sign = 98;

    /// <summary>
    /// The tag of <c>COSE_Sign1</c>.
    /// </summary>
    public const ulong Sign1 = 18;

    /// <summary>
    /// The tag of <c>COSE_Encrypt</c>.
    /// </summary>
    public const ulong Encrypt = 96;

    /// <summary>
    /// The tag of <c>COSE_Encrypt0</c>.
    /// </summary>
    public const ulong Encrypt0 = 16;

    /// <summary>
    /// The tag of <c>COSE_Mac</c>.
    /// </summary>
    public const ulong Mac = 97;

    /// <summary>
    /// The tag of <c>COSE_Mac0</c>.
    /// </summary>
    public const ulong Mac0 = 17;
}
