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

using System.Collections.Frozen;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using NCode.Buffers;
using NCode.Disposables;

namespace NCode.Identity.Cryptography;

/// <summary>
/// Provides a default implementation for the <see cref="ICryptoService"/> abstraction.
/// </summary>
/// <remarks>
/// Encodings are the registered <see cref="IBinaryEncoder"/> strategies, resolved by name; hash algorithms are the
/// framework's <see cref="HashAlgorithmName"/> names, computed by <see cref="CryptographicOperations"/> (ADR-0150).
/// </remarks>
internal class DefaultCryptoService : ICryptoService
{
    private const int MaxStackAlloc = 512 >> 3;

    private FrozenDictionary<string, IBinaryEncoder> BinaryEncoders { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultCryptoService"/> class.
    /// </summary>
    /// <param name="binaryEncoders">The registered encoders, each under a distinct name.</param>
    /// <exception cref="InvalidOperationException">Two encoders are registered under the same name.</exception>
    public DefaultCryptoService(IEnumerable<IBinaryEncoder> binaryEncoders)
    {
        var encoders = new Dictionary<string, IBinaryEncoder>(StringComparer.Ordinal);
        foreach (var encoder in binaryEncoders)
        {
            if (!encoders.TryAdd(encoder.Name, encoder))
            {
                throw new InvalidOperationException(
                    $"The binary encoding '{encoder.Name}' is registered more than once."
                );
            }
        }

        BinaryEncoders = encoders.ToFrozenDictionary(StringComparer.Ordinal);
    }

    private IBinaryEncoder GetBinaryEncoder(string binaryEncoding) =>
        BinaryEncoders.TryGetValue(binaryEncoding, out var encoder)
            ? encoder
            : throw new ArgumentException(
                $"The binary encoding '{binaryEncoding}' is not registered.",
                nameof(binaryEncoding)
            );

    /// <inheritdoc />
    public void GenerateBytes(Span<byte> destination) => RandomNumberGenerator.Fill(destination);

    /// <inheritdoc />
    public string EncodeBinary(ReadOnlySpan<byte> data, string binaryEncoding) =>
        GetBinaryEncoder(binaryEncoding).Encode(data);

    /// <inheritdoc />
    public string GenerateKey(int byteLength, string binaryEncoding)
    {
        var encoder = GetBinaryEncoder(binaryEncoding);
        var lease = Disposable.Empty;
        try
        {
            // ReSharper disable once UseCollectionExpression
            Span<byte> bytes = stackalloc byte[0];

            if (byteLength <= MaxStackAlloc)
            {
                bytes = stackalloc byte[byteLength];
            }
            else
            {
                lease = BufferFactory.Rent(byteLength, isSensitive: false, out bytes);
            }

            GenerateBytes(bytes);

            return encoder.Encode(bytes);
        }
        finally
        {
            lease.Dispose();
        }
    }

    /// <inheritdoc />
    public string HashValue(
        ReadOnlySpan<byte> data,
        HashAlgorithmName hashAlgorithm,
        string binaryEncoding
    )
    {
        var encoder = GetBinaryEncoder(binaryEncoding);

        // A buffer the size of a SHA-512 digest fits every framework hash; a longer digest falls back to the heap.
        Span<byte> hashBytes = stackalloc byte[MaxStackAlloc];
        return CryptographicOperations.TryHashData(
            hashAlgorithm,
            data,
            hashBytes,
            out var bytesWritten
        )
            ? encoder.Encode(hashBytes[..bytesWritten])
            : encoder.Encode(CryptographicOperations.HashData(hashAlgorithm, data));
    }

    /// <inheritdoc />
    public string HashValue(
        string data,
        HashAlgorithmName hashAlgorithm,
        string binaryEncoding,
        Encoding? encoding = null
    )
    {
        var effectiveEncoding = encoding ?? SecureEncoding.UTF8;
        var lease = Disposable.Empty;
        try
        {
            // ReSharper disable once UseCollectionExpression
            Span<byte> dataBytes = stackalloc byte[0];

            var dataByteLength = effectiveEncoding.GetByteCount(data);
            if (dataByteLength <= MaxStackAlloc)
            {
                dataBytes = stackalloc byte[dataByteLength];
            }
            else
            {
                lease = BufferFactory.Rent(dataByteLength, isSensitive: false, out dataBytes);
            }

            var bytesWritten = effectiveEncoding.GetBytes(data, dataBytes);
            Debug.Assert(bytesWritten == dataByteLength);

            return HashValue(dataBytes, hashAlgorithm, binaryEncoding);
        }
        finally
        {
            lease.Dispose();
        }
    }
}
