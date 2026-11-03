// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Jwk;

/// <summary>
/// Defines the values of the <c>key_ops</c> member of a <c>JSON Web Key</c> (RFC 7517 §4.3). This is an extensible
/// vocabulary of string constants, not a closed enumeration, so a custom operation can be contributed without
/// editing this package.
/// </summary>
/// <seealso href="https://datatracker.ietf.org/doc/html/rfc7517#section-4.3">RFC 7517 - The "key_ops" (Key Operations) Parameter</seealso>
[PublicAPI]
public static class JsonWebKeyOperations
{
    /// <summary>The <c>sign</c> value, to compute a signature or MAC (RFC 7517 §4.3).</summary>
    public const string Sign = "sign";

    /// <summary>The <c>verify</c> value, to verify a signature or MAC (RFC 7517 §4.3).</summary>
    public const string Verify = "verify";

    /// <summary>The <c>encrypt</c> value, to encrypt content (RFC 7517 §4.3).</summary>
    public const string Encrypt = "encrypt";

    /// <summary>The <c>decrypt</c> value, to decrypt content and validate decryption (RFC 7517 §4.3).</summary>
    public const string Decrypt = "decrypt";

    /// <summary>The <c>wrapKey</c> value, to encrypt a key (RFC 7517 §4.3).</summary>
    public const string WrapKey = "wrapKey";

    /// <summary>The <c>unwrapKey</c> value, to decrypt a key and validate decryption (RFC 7517 §4.3).</summary>
    public const string UnwrapKey = "unwrapKey";

    /// <summary>The <c>deriveKey</c> value, to derive a key (RFC 7517 §4.3).</summary>
    public const string DeriveKey = "deriveKey";

    /// <summary>The <c>deriveBits</c> value, to derive bits not to be used as a key (RFC 7517 §4.3).</summary>
    public const string DeriveBits = "deriveBits";
}
