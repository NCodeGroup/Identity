// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.Cryptography;

/// <summary>
/// Encodes binary data as uppercase hexadecimal under the <see cref="BinaryEncodings.Hex"/> name.
/// </summary>
internal sealed class HexBinaryEncoder : IBinaryEncoder
{
    /// <inheritdoc />
    public string Name => BinaryEncodings.Hex;

    /// <inheritdoc />
    public string Encode(ReadOnlySpan<byte> data) => Convert.ToHexString(data);
}
