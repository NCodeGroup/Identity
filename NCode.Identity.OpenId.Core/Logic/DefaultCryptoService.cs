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

using System.Diagnostics;
using System.Security.Cryptography;
using NCode.Encoders;
using NCode.Identity.Jose.Algorithms;
using NCode.Jose.Buffers;
using NCode.Jose.Infrastructure;

namespace NCode.Identity.OpenId.Logic;

/// <summary>
/// Provides a default implementation for the <see cref="ICryptoService"/> abstraction.
/// </summary>
public class DefaultCryptoService : ICryptoService
{
    private const int MaxStackAlloc = 512 >> 3;

    /// <inheritdoc />
    public void GenerateBytes(Span<byte> destination) =>
        RandomNumberGenerator.Fill(destination);

    /// <inheritdoc />
    public string EncodeBinary(ReadOnlySpan<byte> data, BinaryEncodingType binaryEncodingType) =>
        binaryEncodingType switch
        {
            BinaryEncodingType.Base64Url => Base64Url.Encode(data),
            BinaryEncodingType.Base64 => Convert.ToBase64String(data),
            BinaryEncodingType.Hex => Convert.ToHexString(data),
            _ => throw new ArgumentException("Unsupported encoding", nameof(binaryEncodingType))
        };

    /// <inheritdoc />
    public string GenerateKey(int byteLength, BinaryEncodingType binaryEncodingType)
    {
        var lease = EmptyDisposable.Singleton;
        try
        {
            Span<byte> bytes = stackalloc byte[0];

            if (byteLength <= MaxStackAlloc)
            {
                bytes = stackalloc byte[byteLength];
            }
            else
            {
                lease = CryptoPool.Rent(byteLength, isSensitive: false, out bytes);
            }

            GenerateBytes(bytes);

            return EncodeBinary(bytes, binaryEncodingType);
        }
        finally
        {
            lease.Dispose();
        }
    }

    private static HashFunctionDelegate GetHashFunction(HashAlgorithmType hashAlgorithmType) =>
        hashAlgorithmType switch
        {
            HashAlgorithmType.Sha1 => SHA1.TryHashData,
            HashAlgorithmType.Sha256 => SHA256.TryHashData,
            HashAlgorithmType.Sha512 => SHA512.TryHashData,
            _ => throw new ArgumentException("Unsupported algorithm", nameof(hashAlgorithmType))
        };

    /// <inheritdoc />
    public string HashValue(ReadOnlySpan<byte> data, HashAlgorithmType hashAlgorithmType, BinaryEncodingType binaryEncodingType)
    {
        var tryComputeHash = GetHashFunction(hashAlgorithmType);

        var lease = EmptyDisposable.Singleton;
        try
        {
            Span<byte> hashBytes = stackalloc byte[0];

            var hashByteLength = (int)hashAlgorithmType;
            if (hashByteLength <= MaxStackAlloc)
            {
                hashBytes = stackalloc byte[hashByteLength];
            }
            else
            {
                lease = CryptoPool.Rent(hashByteLength, isSensitive: false, out hashBytes);
            }

            var result = tryComputeHash(data, hashBytes, out var bytesWritten);
            Debug.Assert(result && bytesWritten == hashByteLength);

            return EncodeBinary(hashBytes, binaryEncodingType);
        }
        finally
        {
            lease.Dispose();
        }
    }

    /// <inheritdoc />
    public string HashValue(string data, HashAlgorithmType hashAlgorithmType, BinaryEncodingType binaryEncodingType)
    {
        var lease = EmptyDisposable.Singleton;
        try
        {
            Span<byte> dataBytes = stackalloc byte[0];

            var dataByteLength = SecureEncoding.Utf8.GetByteCount(data);
            if (dataByteLength <= MaxStackAlloc)
            {
                dataBytes = stackalloc byte[dataByteLength];
            }
            else
            {
                lease = CryptoPool.Rent(dataByteLength, isSensitive: false, out dataBytes);
            }

            var bytesWritten = SecureEncoding.Utf8.GetBytes(data, dataBytes);
            Debug.Assert(bytesWritten == dataByteLength);

            return HashValue(dataBytes, hashAlgorithmType, binaryEncodingType);
        }
        finally
        {
            lease.Dispose();
        }
    }
}
