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

using System.Buffers.Binary;
using System.Security.Cryptography;
using NCode.Jose.Buffers;
using NCode.Jose.Exceptions;

namespace NCode.Jose.Algorithms.KeyManagement;

/// <summary>
/// Provides a default implementation for the <see cref="IAesKeyWrap"/> interface.
/// </summary>
/// <remarks>
/// The variables in the implementation are identical to the following RFC so that code can be easily compared.
/// https://datatracker.ietf.org/doc/html/rfc3394#section-2.2.1
/// </remarks>
public class AesKeyWrap : IAesKeyWrap
{
    /// <summary>
    /// Provides a default singleton instance for <see cref="AesKeyWrap"/>.
    /// </summary>
    public static AesKeyWrap Singleton { get; } = new();

    private const int ChunkBitCount = 64;
    internal const int ChunkByteCount = ChunkBitCount >> 3;

    private const int IntermediateBitCount = ChunkBitCount << 1; // i.e. 128
    internal const int IntermediateByteCount = IntermediateBitCount >> 3;

    private static IEnumerable<KeySizes> StaticLegalCekByteSizes { get; } = new[]
    {
        new KeySizes(minSize: IntermediateByteCount, maxSize: int.MaxValue, skipSize: ChunkByteCount)
    };

    // 0xA6A6A6A6A6A6A6A6
    // ReSharper disable once InconsistentNaming
    private static ReadOnlySpan<byte> DefaultIV =>
        new byte[] { 0xA6, 0xA6, 0xA6, 0xA6, 0xA6, 0xA6, 0xA6, 0xA6 };

    /// <inheritdoc />
    public IEnumerable<KeySizes> LegalCekByteSizes =>
        StaticLegalCekByteSizes;

    /// <inheritdoc />
    public int GetEncryptedContentKeySizeBytes(int contentKeySizeBytes) =>
        GetCipherTextSizeBytes(contentKeySizeBytes, out _);

    internal static int GetCipherTextSizeBytes(int contentKeySizeBytes, out int blocks)
    {
        if (contentKeySizeBytes < IntermediateByteCount)
        {
            throw new JoseException($"The CEK must be at least {IntermediateBitCount} bits.");
        }

        var (quotient, remainder) = Math.DivRem(contentKeySizeBytes, ChunkByteCount);
        if (remainder != 0)
        {
            throw new JoseException($"The CEK must be a multiple of {ChunkBitCount} bits.");
        }

        blocks = quotient;
        return contentKeySizeBytes + ChunkByteCount;
    }

    /// <inheritdoc />
    public bool TryWrapKey(
        ReadOnlySpan<byte> keyEncryptionKey,
        ReadOnlySpan<byte> contentKey,
        Span<byte> encryptedContentKey,
        out int bytesWritten)
    {
        /*
           Inputs:  Plaintext, n 64-bit values {P1, P2, ..., Pn}, and
                    Key, K (the KEK).
           Outputs: Ciphertext, (n+1) 64-bit values {C0, C1, ..., Cn}.
        */

        var expectedSizeBytes = GetCipherTextSizeBytes(contentKey.Length, out var n);
        if (encryptedContentKey.Length < expectedSizeBytes)
        {
            bytesWritten = 0;
            return false;
        }

        using var aes = Aes.Create();
        aes.Key = keyEncryptionKey.ToArray();
        aes.Mode = CipherMode.ECB;
        aes.Padding = PaddingMode.None;

        /*
           1) Initialize variables.

               Set A = IV, an initial value (see 2.2.3)
               For i = 1 to n
                   R[i] = P[i]
        */

        Span<byte> a = stackalloc byte[sizeof(long)];
        DefaultIV.CopyTo(a);

        using var lease = CryptoPool.Rent(contentKey.Length, isSensitive: true, out Memory<byte> leaseMemory);
        contentKey.CopyTo(leaseMemory.Span);
        ReadOnlyMemory<byte> keyMemory = leaseMemory;

        var r = Enumerable
            .Range(0, n)
            .Select(i => keyMemory.Slice(i * ChunkByteCount, ChunkByteCount))
            .ToArray();

        /*
           2) Calculate intermediate values.

               For j = 0 to 5
                   For i=1 to n
                       B = AES(K, A | R[i])
                       A = MSB(64, B) ^ t where t = (n*j)+i
                       R[i] = LSB(64, B)
        */

        Span<byte> encryptBuffer = stackalloc byte[IntermediateByteCount];

        for (var j = 0; j < 6; ++j)
        {
            for (var i = 0; i < n; ++i)
            {
                var t = n * j + i + 1u; // coerce as Int64

                Concat(a, r[i].Span, encryptBuffer);
                var b = aes.EncryptEcb(encryptBuffer, PaddingMode.None).AsMemory();

                b[..ChunkByteCount].Span.CopyTo(a);
                Xor(a, t, a);

                r[i] = b[ChunkByteCount..];
            }
        }

        /*
           3) Output the results.

               Set C[0] = A
               For i = 1 to n
                   C[i] = R[i]
        */

        Concat(a, r, encryptedContentKey);

        bytesWritten = expectedSizeBytes;
        return true;
    }

    /// <inheritdoc />
    public int GetContentKeySizeBytes(int encryptedContentKeySizeBytes) =>
        GetUnwrapKeySizeBytes(encryptedContentKeySizeBytes, out _);

    internal static int GetUnwrapKeySizeBytes(int encryptedContentKeySizeBytes, out int blocks)
    {
        if (encryptedContentKeySizeBytes < IntermediateByteCount)
        {
            throw new JoseException($"Encrypted KEK must be at least {IntermediateBitCount} bits.");
        }

        var (quotient, remainder) = Math.DivRem(encryptedContentKeySizeBytes, ChunkByteCount);
        if (remainder != 0)
        {
            throw new JoseException($"Encrypted KEK must be a multiple of {ChunkBitCount} bits.");
        }

        blocks = quotient - 1;
        return encryptedContentKeySizeBytes - ChunkByteCount;
    }

    /// <inheritdoc />
    public bool TryUnwrapKey(
        ReadOnlySpan<byte> keyEncryptionKey,
        ReadOnlySpan<byte> encryptedContentKey,
        Span<byte> contentKey,
        out int bytesWritten)
    {
        /*
           Inputs:  Ciphertext, (n+1) 64-bit values {C0, C1, ..., Cn}, and
                    Key, K (the KEK).
           Outputs: Plaintext, n 64-bit values {P0, P1, K, Pn}.
        */

        var expectedSizeBytes = GetUnwrapKeySizeBytes(encryptedContentKey.Length, out var n);
        if (contentKey.Length < expectedSizeBytes)
        {
            bytesWritten = 0;
            return false;
        }

        using var aes = Aes.Create();
        aes.Key = keyEncryptionKey.ToArray();
        aes.Mode = CipherMode.ECB;
        aes.Padding = PaddingMode.None;

        /*
           1) Initialize variables.

               Set A = C[0]
               For i = 1 to n
                   R[i] = C[i]
        */

        using var lease = CryptoPool.Rent(encryptedContentKey.Length, isSensitive: true, out Memory<byte> leaseMemory);
        encryptedContentKey.CopyTo(leaseMemory.Span);
        ReadOnlyMemory<byte> encryptedKeyMemory = leaseMemory;

        var a = encryptedKeyMemory[..ChunkByteCount];

        var r = Enumerable
            .Range(1, n) // skip first
            .Select(i => encryptedKeyMemory.Slice(i * ChunkByteCount, ChunkByteCount))
            .ToArray();

        /*
           2) Compute intermediate values.

               For j = 5 to 0
                   For i = n to 1
                       B = AES-1(K, (A ^ t) | R[i]) where t = n*j+i
                       A = MSB(64, B)
                       R[i] = LSB(64, B)
        */

        Span<byte> xorBuffer = stackalloc byte[sizeof(long)];
        Span<byte> decryptBuffer = stackalloc byte[IntermediateByteCount];

        for (var j = 5; j >= 0; --j)
        {
            for (var i = n - 1; i >= 0; --i)
            {
                var t = n * j + i + 1u; // coerce as Int64

                Xor(a.Span, t, xorBuffer);
                Concat(xorBuffer, r[i].Span, decryptBuffer);
                var b = aes.DecryptEcb(decryptBuffer, PaddingMode.None).AsMemory();

                a = b[..ChunkByteCount];
                r[i] = b[ChunkByteCount..];
            }
        }

        /*
           3) Output results.

           If A is an appropriate initial value (see 2.2.3),
           Then
               For i = 1 to n
                   P[i] = R[i]
           Else
               Return an error
        */

        if (!a.Span.SequenceEqual(DefaultIV))
            throw new JoseEncryptionException("Failed to decrypt the encrypted content encryption key (CEK). DefaultIV doesn't match.");

        Concat(r, contentKey);

        bytesWritten = expectedSizeBytes;
        return true;
    }

    private static void Xor(ReadOnlySpan<byte> xBuffer, long y, Span<byte> destination)
    {
        if (xBuffer.Length < sizeof(long))
            throw new InvalidOperationException();
        if (destination.Length < sizeof(long))
            throw new InvalidOperationException();

        var x = BinaryPrimitives.ReadInt64BigEndian(xBuffer);

        var result = x ^ y;

        BinaryPrimitives.WriteInt64BigEndian(destination, result);
    }

    private static void Concat(ReadOnlySpan<byte> a, ReadOnlySpan<byte> b, Span<byte> destination)
    {
        if (a.Length + b.Length > destination.Length)
            throw new InvalidOperationException();

        a.CopyTo(destination);
        b.CopyTo(destination[a.Length..]);
    }

    private static void Concat(ReadOnlySpan<byte> prepend, IEnumerable<ReadOnlyMemory<byte>> buffers, Span<byte> destination)
    {
        prepend.CopyTo(destination);
        destination = destination[prepend.Length..];

        Concat(buffers, destination);
    }

    private static void Concat(IEnumerable<ReadOnlyMemory<byte>> buffers, Span<byte> destination)
    {
        foreach (var buffer in buffers)
        {
            buffer.Span.CopyTo(destination);
            destination = destination[buffer.Length..];
        }
    }
}
