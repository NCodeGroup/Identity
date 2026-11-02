// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Jose;

/// <summary>
/// Specifies the wire serialization used to represent a JOSE (JWS or JWE) token.
/// </summary>
[PublicAPI]
public enum JoseSerializationFormat
{
    /// <summary>
    /// The URL-safe, dot-separated compact serialization defined by <c>RFC 7515</c> §7.1 (JWS) and
    /// <c>RFC 7516</c> §7.1 (JWE). This representation supports exactly one signature/recipient and cannot
    /// carry unprotected header members.
    /// </summary>
    Compact = 0,

    /// <summary>
    /// The flattened JSON serialization defined by <c>RFC 7515</c> §7.2.2 (JWS) and <c>RFC 7516</c> §7.2.2 (JWE).
    /// This representation is optimized for the single-signature/single-recipient case and may carry unprotected
    /// header members.
    /// </summary>
    Flattened = 1,

    /// <summary>
    /// The general JSON serialization defined by <c>RFC 7515</c> §7.2.1 (JWS) and <c>RFC 7516</c> §7.2.1 (JWE).
    /// This representation supports multiple signatures (JWS) / multiple recipients (JWE) and may carry
    /// unprotected header members.
    /// </summary>
    General = 2,
}
