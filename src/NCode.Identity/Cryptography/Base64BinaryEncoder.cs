// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.Cryptography;

/// <summary>
/// Encodes binary data as standard Base64 with padding under the <see cref="BinaryEncodings.Base64"/> name.
/// </summary>
internal sealed class Base64BinaryEncoder : IBinaryEncoder
{
    /// <inheritdoc />
    public string Name => BinaryEncodings.Base64;

    /// <inheritdoc />
    public string Encode(ReadOnlySpan<byte> data) => Convert.ToBase64String(data);
}
