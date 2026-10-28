#region Copyright Preamble

// Copyright @ 2026 NCode Group
//
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
//
//        http://www.apache.org/licenses/LICENSE-2.0
//
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.

#endregion

using JetBrains.Annotations;

namespace NCode.Identity.Jwk;

/// <summary>
/// Defines the values of the <c>key_ops</c> member of a <c>JSON Web Key</c> (RFC 7517 §4.3). This is an extensible
/// vocabulary of string constants, not a closed enumeration, so a custom operation can be contributed without a
/// breaking change.
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
