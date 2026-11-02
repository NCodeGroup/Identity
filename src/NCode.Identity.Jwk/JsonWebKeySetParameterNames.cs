// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Jwk;

/// <summary>
/// Defines the member names of a <c>JSON Web Key Set</c> (RFC 7517 §5). This is an extensible vocabulary of string
/// constants, not a closed enumeration, so a custom member can be contributed without a breaking change.
/// </summary>
/// <seealso href="https://datatracker.ietf.org/doc/html/rfc7517#section-5">RFC 7517 - JWK Set Format</seealso>
[PublicAPI]
public static class JsonWebKeySetParameterNames
{
    /// <summary>The <c>keys</c> member, the REQUIRED array of JSON Web Keys (RFC 7517 §5.1).</summary>
    public const string Keys = "keys";
}
