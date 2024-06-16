#region Copyright Preamble

//
//    Copyright @ 2023 NCode Group
//
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
//
//        http://www.apache.org/licenses/LICENSE-2.0
//
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.

#endregion

using System.Numerics;
using System.Text;
using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Logic;

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
    /// <param name="binaryEncodingType">Specifies how to encode the binary data as a string.</param>
    /// <returns>The binary data encoded as a string.</returns>
    string EncodeBinary(
        ReadOnlySpan<byte> data,
        BinaryEncodingType binaryEncodingType);

    /// <summary>
    /// Generates a random key of the specified length and returns an encoded string of the key.
    /// </summary>
    /// <param name="byteLength">Specifies the number of random bytes to generate.</param>
    /// <param name="binaryEncodingType">Specifies how to encode the binary data as a string.</param>
    /// <returns>The newly generated random bytes encoded as a string.</returns>
    string GenerateKey(
        int byteLength,
        BinaryEncodingType binaryEncodingType);

    /// <summary>
    /// Hashes binary data and returns an encoded string of the hash.
    /// </summary>
    /// <param name="data">The binary data to hash.</param>
    /// <param name="hashAlgorithmType">Specifies which hashing algorithm to use.</param>
    /// <param name="binaryEncodingType">Specifies how to encode the hashed data as a string.</param>
    /// <returns>The hash of the input value encoded as a string.</returns>
    string HashValue(
        ReadOnlySpan<byte> data,
        HashAlgorithmType hashAlgorithmType,
        BinaryEncodingType binaryEncodingType);

    /// <summary>
    /// Hashes a string value and returns an encoded string of the hash.
    /// </summary>
    /// <param name="data">The <see cref="string"/> to hash.</param>
    /// <param name="hashAlgorithmType">Specifies which hashing algorithm to use.</param>
    /// <param name="binaryEncodingType">Specifies how to encode the hashed data as a string.</param>
    /// <param name="encoding">Specifies the encoding to use when converting the string to bytes.
    /// If <c>null</c>, a secure implementation of UTF-8 will be used that throws when invalid bytes are encountered.</param>
    /// <returns>The hash of the input value encoded as a string.</returns>
    string HashValue(
        string data,
        HashAlgorithmType hashAlgorithmType,
        BinaryEncodingType binaryEncodingType,
        Encoding? encoding = null);

    /// <summary>
    /// Determine the equality of two sequences in an amount of time which depends on the length of the sequences,
    /// but not the values.
    /// </summary>
    /// <param name="left">The first buffer to compare.</param>
    /// <param name="right">The second buffer to compare.</param>
    /// <returns>
    ///   <c>true</c> if <paramref name="left"/> and <paramref name="right"/> have the same
    ///   values for <see cref="ReadOnlySpan{T}.Length"/> and the same contents, <c>false</c>
    ///   otherwise.
    /// </returns>
    /// <remarks>
    ///   This method compares two buffers' contents for equality in a manner which does not
    ///   leak timing information, making it ideal for use within cryptographic routines.
    ///   This method will short-circuit and return <c>false</c> only if <paramref name="left"/>
    ///   and <paramref name="right"/> have different lengths.
    ///
    ///   Fixed-time behavior is guaranteed in all other cases, including if <paramref name="left"/>
    ///   and <paramref name="right"/> reference the same address.
    ///
    ///   This method was adapted from the .NET Core source code and credit goes to the .NET Core team.
    /// </remarks>
    bool FixedTimeEquals<T>(ReadOnlySpan<T> left, ReadOnlySpan<T> right)
        where T : IEqualityOperators<T, T, bool>;
}
