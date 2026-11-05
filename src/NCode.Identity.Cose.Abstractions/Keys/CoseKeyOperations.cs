// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Cose.Keys;

/// <summary>
/// Contains the values of the <c>key_ops</c> parameter of a COSE_Key (RFC 9052 §7.1).
/// </summary>
[PublicAPI]
public static class CoseKeyOperations
{
    /// <summary>
    /// The key creates signatures.
    /// </summary>
    public const int Sign = 1;

    /// <summary>
    /// The key verifies signatures.
    /// </summary>
    public const int Verify = 2;

    /// <summary>
    /// The key encrypts content.
    /// </summary>
    public const int Encrypt = 3;

    /// <summary>
    /// The key decrypts content.
    /// </summary>
    public const int Decrypt = 4;

    /// <summary>
    /// The key wraps keys.
    /// </summary>
    public const int WrapKey = 5;

    /// <summary>
    /// The key unwraps keys.
    /// </summary>
    public const int UnwrapKey = 6;

    /// <summary>
    /// The key derives keys.
    /// </summary>
    public const int DeriveKey = 7;

    /// <summary>
    /// The key derives bits not to be used as a key.
    /// </summary>
    public const int DeriveBits = 8;

    /// <summary>
    /// The key creates message authentication codes.
    /// </summary>
    public const int MacCreate = 9;

    /// <summary>
    /// The key verifies message authentication codes.
    /// </summary>
    public const int MacVerify = 10;
}
