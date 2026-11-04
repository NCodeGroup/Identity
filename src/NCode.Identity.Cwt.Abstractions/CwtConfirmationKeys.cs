// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Cwt;

/// <summary>
/// Contains the integer keys of the members of the <c>cnf</c> claim this library knows (RFC 8747 §3.1).
/// </summary>
[PublicAPI]
public static class CwtConfirmationKeys
{
    /// <summary>
    /// The <c>COSE_Key</c> member, a public key.
    /// </summary>
    public const long CoseKey = 1;

    /// <summary>
    /// The <c>Encrypted_COSE_Key</c> member.
    /// </summary>
    public const long EncryptedCoseKey = 2;

    /// <summary>
    /// The <c>kid</c> member, a key identifier.
    /// </summary>
    public const long KeyId = 3;
}
