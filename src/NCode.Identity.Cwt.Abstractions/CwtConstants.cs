// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Cwt;

/// <summary>
/// Contains the constants of CBOR Web Tokens (CWT, RFC 8392).
/// </summary>
[PublicAPI]
public static class CwtConstants
{
    /// <summary>
    /// The CBOR tag that identifies a CWT (RFC 8392 §6).
    /// </summary>
    public const ulong Tag = 61;

    /// <summary>
    /// The media type of a CWT (RFC 8392 §9.5).
    /// </summary>
    public const string MediaType = "application/cwt";
}
