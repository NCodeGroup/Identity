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

// Advanced Encryption Standard (AES) Key Wrap Algorithm
// https://datatracker.ietf.org/doc/html/rfc3394

// These variables are identical to RFC so that code can be easily compared
// https://datatracker.ietf.org/doc/html/rfc3394#section-2.2.1

internal class AesKeyWrapProvider : KeyWrapProvider
{
    private const int BitsPerByte = 8;

    private const int ChunkBitCount = 64;
    private const int ChunkByteCount = ChunkBitCount / BitsPerByte;

    private const int IntermediateBitCount = ChunkBitCount * 2; // i.e. 128
    private const int IntermediateByteCount = IntermediateBitCount / BitsPerByte;

    // 0xA6A6A6A6A6A6A6A6
    // ReSharper disable once InconsistentNaming
    private static readonly ReadOnlyMemory<byte> DefaultIV = new byte[] { 0xA6, 0xA6, 0xA6, 0xA6, 0xA6, 0xA6, 0xA6, 0xA6 };

    private AesSecretKey AesSecretKey { get; }
    private AesKeyWrapAlgorithmDescriptor Descriptor { get; }

    public AesKeyWrapProvider(AesSecretKey secretKey, AesKeyWrapAlgorithmDescriptor descriptor) :
        base(secretKey, descriptor)
    {
        AesSecretKey = secretKey;
        Descriptor = descriptor;
    }

    public override ReadOnlySequence<byte> WrapKey(KeyWrapParameters parameters)
    {
        /*
           Inputs:  Plaintext, n 64-bit values {P1, P2, ..., Pn}, and
                    Key, K (the KEK).
           Outputs: Ciphertext, (n+1) 64-bit values {C0, C1, ..., Cn}.
        */

        if (parameters is not ContentKeyWrapParameters typedParameters)
        {
            // TODO: unit tests
            throw new InvalidOperationException();
        }

        var plainText = typedParameters.PlainText;
        if (plainText.Length < IntermediateByteCount)
        {
            // TODO: unit tests
            // min size
            throw new InvalidOperationException();
        }

        if (plainText.Length % ChunkByteCount != 0)
        {
            // TODO: unit tests
            // 64bit chunks
            throw new InvalidOperationException();
        }

        /*
           1) Initialize variables.

               Set A = IV, an initial value (see 2.2.3)
               For i = 1 to n
                   R[i] = P[i]
        */

        var n = plainText.Length / ChunkByteCount;

        Span<byte> a = stackalloc byte[sizeof(long)];
        DefaultIV.Span.CopyTo(a);

        var r = Enumerable
            .Range(0, n)
            .Select(i => plainText.Slice(i * ChunkByteCount, ChunkByteCount))
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
                var b = Encrypt(encryptBuffer);

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

    public override ReadOnlySequence<byte> UnwrapKey(KeyUnwrapParameters parameters)
    {
        /*
           Inputs:  Ciphertext, (n+1) 64-bit values {C0, C1, ..., Cn}, and
                    Key, K (the KEK).
           Outputs: Plaintext, n 64-bit values {P0, P1, K, Pn}.
        */

        if (parameters is not ContentKeyUnwrapParameters typedParameters)
        {
            // TODO: unit tests
            throw new InvalidOperationException();
        }

        var cipherText = typedParameters.CipherText;
        if (cipherText.Length < IntermediateByteCount)
        {
            // TODO: unit tests
            // min size
            throw new InvalidOperationException();
        }

        if (cipherText.Length % ChunkByteCount != 0)
        {
            // TODO: unit tests
            // 64bit chunks
            throw new InvalidOperationException();
        }

        /*
           1) Initialize variables.

               Set A = C[0]
               For i = 1 to n
                   R[i] = C[i]
        */

        var n = cipherText.Length / ChunkByteCount - 1;

        var a = cipherText[..ChunkByteCount];

        var r = Enumerable
            .Range(1, n) // skip first
            .Select(i => cipherText.Slice(i * ChunkByteCount, ChunkByteCount))
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
                var b = Decrypt(decryptBuffer);

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
            throw new InvalidOperationException();

        return BinaryUtility.Concat(r);
    }

    private ReadOnlyMemory<byte> Encrypt(ReadOnlySpan<byte> plainText) =>
        AesSecretKey.Key.EncryptEcb(plainText, PaddingMode.None);

    private ReadOnlyMemory<byte> Decrypt(ReadOnlySpan<byte> cipherText) =>
        AesSecretKey.Key.DecryptEcb(cipherText, PaddingMode.None);
}
