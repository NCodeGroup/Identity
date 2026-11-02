// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Encoders;

namespace NCode.Identity.Cryptography;

/// <summary>
/// Encodes binary data as URL-safe Base64 without padding under the <see cref="BinaryEncodings.Base64Url"/> name.
/// </summary>
internal sealed class Base64UrlBinaryEncoder : IBinaryEncoder
{
    /// <inheritdoc />
    public string Name => BinaryEncodings.Base64Url;

    /// <inheritdoc />
    public string Encode(ReadOnlySpan<byte> data) => Base64Url.Encode(data);
}
