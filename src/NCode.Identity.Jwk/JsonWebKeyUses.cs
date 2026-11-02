// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Jwk;

/// <summary>
/// Defines the values of the <c>use</c> member of a <c>JSON Web Key</c> (RFC 7517 §4.2). This is an extensible
/// vocabulary of string constants, not a closed enumeration, so a custom use can be contributed without a breaking
/// change.
/// </summary>
/// <seealso href="https://datatracker.ietf.org/doc/html/rfc7517#section-4.2">RFC 7517 - The "use" (Public Key Use) Parameter</seealso>
[PublicAPI]
public static class JsonWebKeyUses
{
    /// <summary>The <c>sig</c> value, indicating the key is used to verify or create signatures (RFC 7517 §4.2).</summary>
    public const string Signature = "sig";

    /// <summary>The <c>enc</c> value, indicating the key is used to encrypt or decrypt data (RFC 7517 §4.2).</summary>
    public const string Encryption = "enc";
}
