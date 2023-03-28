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

using System.Buffers;
using System.Security.Cryptography;
using NIdentity.OpenId.Cryptography.CryptoProvider;

namespace NIdentity.OpenId.Cryptography.Aes;

/// <summary>
/// Defines cryptographic operations for the <c>Advanced Encryption Standard (AES) Key Wrap Algorithm</c>.
/// https://datatracker.ietf.org/doc/html/rfc3394
/// </summary>
public interface IAesKeyWrap
{
    /// <summary>
    /// Performs the cryptographic operation of encrypting key data using the AES key wrap algorithm.
    /// </summary>
    /// <param name="kek">Contains the key encryption key.</param>
    /// <param name="parameters">Contains the plain text to be encrypted.</param>
    /// <param name="keyBitLength">The expected length of <paramref name="kek"/> in bits.</param>
    /// <returns>The result of encrypting the plain text to cipher text.</returns>
    ReadOnlySequence<byte> WrapKey(
        ReadOnlySpan<byte> kek,
        ISupportPlainTextKey parameters,
        int keyBitLength);

    /// <summary>
    /// Performs the cryptographic operation of decrypting key data using the AES key wrap algorithm.
    /// </summary>
    /// <param name="kek">Contains the key encryption key.</param>
    /// <param name="parameters">Contains the cipher text to be decrypted.</param>
    /// <param name="keyBitLength">The expected length of <paramref name="kek"/> in bits.</param>
    /// <returns>The result of decrypting the cipher text to plain text.</returns>
    ReadOnlySequence<byte> UnwrapKey(
        ReadOnlySpan<byte> kek,
        ISupportCipherTextKey parameters,
        int keyBitLength);
}

/// <summary>
/// Provides the default implementation for <see cref="IAesKeyWrap"/>.
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
    public static IAesKeyWrap Default { get; } = new AesKeyWrap();

    private const int ChunkBitCount = 64;
    private const int ChunkByteCount = ChunkBitCount / BinaryUtility.BitsPerByte;

    private const int IntermediateBitCount = ChunkBitCount * 2; // i.e. 128
    private const int IntermediateByteCount = IntermediateBitCount / BinaryUtility.BitsPerByte;

    // 0xA6A6A6A6A6A6A6A6
    // ReSharper disable once InconsistentNaming
    private static readonly ReadOnlyMemory<byte> DefaultIV =
        new byte[] { 0xA6, 0xA6, 0xA6, 0xA6, 0xA6, 0xA6, 0xA6, 0xA6 };

    /// <inheritdoc />
    public ReadOnlySequence<byte> WrapKey(
        ReadOnlySpan<byte> kek,
        ISupportPlainTextKey parameters,
        int keyBitLength)
    {
        /*
           Inputs:  Plaintext, n 64-bit values {P1, P2, ..., Pn}, and
                    Key, K (the KEK).
           Outputs: Ciphertext, (n+1) 64-bit values {C0, C1, ..., Cn}.
        */

        // TODO: does this really need to be here?
        if (kek.Length * BinaryUtility.BitsPerByte != keyBitLength)
        {
            // TODO: unit tests
            throw new InvalidOperationException();
        }

        var plainTextKey = parameters.PlainTextKey;
        if (plainTextKey.Length < IntermediateByteCount)
        {
            // TODO: unit tests
            // min size
            throw new InvalidOperationException();
        }

        if (plainTextKey.Length % ChunkByteCount != 0)
        {
            // TODO: unit tests
            // 64bit chunks
            throw new InvalidOperationException();
        }

        using var aes = System.Security.Cryptography.Aes.Create();
        aes.Key = kek.ToArray();
        aes.Mode = CipherMode.ECB;
        aes.Padding = PaddingMode.None;

        /*
           1) Initialize variables.

               Set A = IV, an initial value (see 2.2.3)
               For i = 1 to n
                   R[i] = P[i]
        */

        var n = plainTextKey.Length / ChunkByteCount;

        Span<byte> a = stackalloc byte[sizeof(long)];
        DefaultIV.Span.CopyTo(a);

        var r = Enumerable
            .Range(0, n)
            .Select(i => plainTextKey.Slice(i * ChunkByteCount, ChunkByteCount))
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

                BinaryUtility.Concat(a, r[i].Span, encryptBuffer);
                var b = aes.EncryptEcb(encryptBuffer, PaddingMode.None).AsMemory();

                b[..ChunkByteCount].Span.CopyTo(a);
                BinaryUtility.Xor(a, t, a);

                r[i] = b[ChunkByteCount..];
            }
        }

        /*
           3) Output the results.

               Set C[0] = A
               For i = 1 to n
                   C[i] = R[i]
        */

        return BinaryUtility.Concat(a, r);
    }

    /// <inheritdoc />
    public ReadOnlySequence<byte> UnwrapKey(
        ReadOnlySpan<byte> kek,
        ISupportCipherTextKey parameters,
        int keyBitLength)
    {
        /*
           Inputs:  Ciphertext, (n+1) 64-bit values {C0, C1, ..., Cn}, and
                    Key, K (the KEK).
           Outputs: Plaintext, n 64-bit values {P0, P1, K, Pn}.
        */

        // TODO: does this really need to be here?
        if (kek.Length * BinaryUtility.BitsPerByte != keyBitLength)
        {
            // TODO: unit tests
            throw new InvalidOperationException();
        }

        var cipherTextKey = parameters.CipherTextKey;
        if (cipherTextKey.Length < IntermediateByteCount)
        {
            // TODO: unit tests
            // min size
            throw new InvalidOperationException();
        }

        if (cipherTextKey.Length % ChunkByteCount != 0)
        {
            // TODO: unit tests
            // 64bit chunks
            throw new InvalidOperationException();
        }

        using var aes = System.Security.Cryptography.Aes.Create();
        aes.Key = kek.ToArray();
        aes.Mode = CipherMode.ECB;
        aes.Padding = PaddingMode.None;

        /*
           1) Initialize variables.

               Set A = C[0]
               For i = 1 to n
                   R[i] = C[i]
        */

        var n = cipherTextKey.Length / ChunkByteCount - 1;

        var a = cipherTextKey[..ChunkByteCount];

        var r = Enumerable
            .Range(1, n) // skip first
            .Select(i => cipherTextKey.Slice(i * ChunkByteCount, ChunkByteCount))
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

                BinaryUtility.Xor(a.Span, t, xorBuffer);
                BinaryUtility.Concat(xorBuffer, r[i].Span, decryptBuffer);
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

        if (!a.Span.SequenceEqual(DefaultIV.Span))
            // TODO: unit tests
            // TODO: better exception
            throw new InvalidOperationException();

        return BinaryUtility.Concat(r);
    }
}
