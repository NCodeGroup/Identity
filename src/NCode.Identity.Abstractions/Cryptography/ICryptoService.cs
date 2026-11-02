// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;
using System.Text;
using JetBrains.Annotations;

namespace NCode.Identity.Cryptography;

/// <summary>
/// Provides the ability to generate random bytes, encode binary data as a string, generate random keys, and hash values.
/// </summary>
[PublicAPI]
public interface ICryptoService
{
    /// <summary>
    /// Generates a sequence of cryptographically strong random bytes.
    /// </summary>
    /// <param name="destination">The span to fill with cryptographically strong random bytes.</param>
    void GenerateBytes(Span<byte> destination);

    /// <summary>
    /// Encodes binary data as a string using the specified encoding.
    /// </summary>
    /// <param name="data">The binary data to encode.</param>
    /// <param name="binaryEncoding">The name of the registered <see cref="IBinaryEncoder"/> that encodes the binary data
    /// as a string, such as a <see cref="BinaryEncodings"/> name.</param>
    /// <returns>The binary data encoded as a string.</returns>
    string EncodeBinary(ReadOnlySpan<byte> data, string binaryEncoding);

    /// <summary>
    /// Generates a random key of the specified length and returns an encoded string of the key.
    /// </summary>
    /// <param name="byteLength">Specifies the number of random bytes to generate.</param>
    /// <param name="binaryEncoding">The name of the registered <see cref="IBinaryEncoder"/> that encodes the binary data
    /// as a string, such as a <see cref="BinaryEncodings"/> name.</param>
    /// <returns>The newly generated random bytes encoded as a string.</returns>
    string GenerateKey(int byteLength, string binaryEncoding);

    /// <summary>
    /// Hashes binary data and returns an encoded string of the hash.
    /// </summary>
    /// <param name="data">The binary data to hash.</param>
    /// <param name="hashAlgorithm">The hash algorithm, such as <see cref="HashAlgorithmName.SHA256"/>.</param>
    /// <param name="binaryEncoding">The name of the registered <see cref="IBinaryEncoder"/> that encodes the hash as a
    /// string, such as a <see cref="BinaryEncodings"/> name.</param>
    /// <returns>The hash of the input value encoded as a string.</returns>
    string HashValue(
        ReadOnlySpan<byte> data,
        HashAlgorithmName hashAlgorithm,
        string binaryEncoding
    );

    /// <summary>
    /// Hashes a string value and returns an encoded string of the hash.
    /// </summary>
    /// <param name="data">The <see cref="string"/> to hash.</param>
    /// <param name="hashAlgorithm">The hash algorithm, such as <see cref="HashAlgorithmName.SHA256"/>.</param>
    /// <param name="binaryEncoding">The name of the registered <see cref="IBinaryEncoder"/> that encodes the hash as a
    /// string, such as a <see cref="BinaryEncodings"/> name.</param>
    /// <param name="encoding">Specifies the encoding to use when converting the string to bytes.
    /// If <c>null</c>, a secure implementation of UTF-8 will be used that throws when invalid bytes are encountered.</param>
    /// <returns>The hash of the input value encoded as a string.</returns>
    string HashValue(
        string data,
        HashAlgorithmName hashAlgorithm,
        string binaryEncoding,
        Encoding? encoding = null
    );
}
