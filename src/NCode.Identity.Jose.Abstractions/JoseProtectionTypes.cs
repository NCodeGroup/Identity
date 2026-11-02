// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Jose;

/// <summary>
/// Constants for <see cref="CompactJwt.ProtectionType"/> that can be used to indicate how the JWT is protected.
/// </summary>
[PublicAPI]
public static class JoseProtectionTypes
{
    /// <summary>
    /// A value that indicates the JWT is protected using <c>JWS</c> (signing).
    /// </summary>
    public const string Jws = "JWS";

    /// <summary>
    /// A value that indicates the JWT is protected using <c>JWE</c> (encryption).
    /// </summary>
    public const string Jwe = "JWE";
}
