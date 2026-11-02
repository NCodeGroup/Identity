// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Cryptography;

/// <summary>
/// Contains the names of the built-in encodings that convert binary data into text.
/// </summary>
/// <remarks>
/// The set is open: each name is the <see cref="IBinaryEncoder.Name"/> of a registered <see cref="IBinaryEncoder"/>, so
/// a package adds an encoding, such as Base32, by registering another encoder. Names compare ordinally.
/// </remarks>
// See ADR-1150.
[PublicAPI]
public static class BinaryEncodings
{
    /// <summary>
    /// The name of the Base64 encoding that is safe for URLs, without padding (RFC 4648 section 5).
    /// </summary>
    public const string Base64Url = "base64url";

    /// <summary>
    /// The name of the standard Base64 encoding, with padding (RFC 4648 section 4).
    /// </summary>
    public const string Base64 = "base64";

    /// <summary>
    /// The name of the uppercase hexadecimal encoding (RFC 4648 section 8).
    /// </summary>
    public const string Hex = "hex";
}
