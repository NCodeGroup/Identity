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

using System.Security.Cryptography;
using Jose;
using NCode.Jose.AuthenticatedEncryption;

namespace NCode.Jose.Tests.AuthenticatedEncryption;

public class AesCbcHmacAuthenticatedEncryptionAlgorithmTests
{
    private static bool KeyedHashFunctionNotImplemented(
        ReadOnlySpan<byte> key,
        ReadOnlySpan<byte> source,
        Span<byte> destination,
        out int bytesWritten)
    {
        throw new NotImplementedException();
    }

    [Theory]
    [InlineData(1, 16)]
    [InlineData(15, 16)]
    [InlineData(16, 32)]
    [InlineData(17, 32)]
    [InlineData(30, 32)]
    [InlineData(31, 32)]
    [InlineData(32, 48)]
    [InlineData(33, 48)]
    [InlineData(46, 48)]
    [InlineData(47, 48)]
    [InlineData(48, 64)]
    [InlineData(63, 64)]
    [InlineData(64, 80)]
    public void GetCipherTextSizeBytes_Valid(int plainTextSizeBytes, int expected)
    {
        const int cekSizeBits = -1; // don't care

        var algorithm = new AesCbcHmacAuthenticatedEncryptionAlgorithm(
            "code",
            KeyedHashFunctionNotImplemented,
            cekSizeBits);

        var actual = algorithm.GetCipherTextSizeBytes(plainTextSizeBytes);
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(256, 256)]
    [InlineData(384, 384)]
    [InlineData(512, 512)]
    public void GetMaxPlainTextSizeBytes_Valid(int cipherTextSizeBytes, int expected)
    {
        const int cekSizeBits = -1; // don't care

        var algorithm = new AesCbcHmacAuthenticatedEncryptionAlgorithm(
            "code",
            KeyedHashFunctionNotImplemented,
            cekSizeBits);

        var actual = algorithm.GetMaxPlainTextSizeBytes(cipherTextSizeBytes);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void CreateAes_Valid()
    {
        Span<byte> key = new byte[256 >> 3];
        RandomNumberGenerator.Fill(key);

        using var aes = AesCbcHmacAuthenticatedEncryptionAlgorithm.CreateAes(key);
        Assert.True(aes.Key.AsSpan().SequenceEqual(key));
    }

    public static IEnumerable<object[]> RoundTripTestData
    {
        get
        {
            var plainTextSizeBytesArray = new[] { 1, 15, 16, 17, 31, 32, 33 };
            yield return new object[]
            {
                256,
                (KeyedHashFunctionDelegate)HMACSHA256.TryHashData,
                new HmacUsingSha("SHA256"),
                plainTextSizeBytesArray
            };
            yield return new object[]
            {
                384,
                (KeyedHashFunctionDelegate)HMACSHA384.TryHashData,
                new HmacUsingSha("SHA384"),
                plainTextSizeBytesArray
            };
            yield return new object[]
            {
                512,
                (KeyedHashFunctionDelegate)HMACSHA512.TryHashData,
                new HmacUsingSha("SHA512"),
                plainTextSizeBytesArray
            };
        }
    }

    [Theory]
    [MemberData(nameof(RoundTripTestData))]
    public void RoundTrip_Valid(int cekSizeBits, KeyedHashFunctionDelegate keyedHashFunction, IJwsAlgorithm jwsAlgorithm, int[] plainTextSizeBytesArray)
    {
        foreach (var plainTextSizeBytes in plainTextSizeBytesArray)
        {
            const int blockSizeBytes = 128 >> 3;

            var cekSizeBytes = cekSizeBits >> 3;
            var componentSizeBytes = cekSizeBytes >> 1;

            Span<byte> cek = new byte[cekSizeBytes];
            Span<byte> plainText = new byte[plainTextSizeBytes];
            Span<byte> associatedData = new byte[blockSizeBytes];

            RandomNumberGenerator.Fill(cek);
            RandomNumberGenerator.Fill(plainText);
            RandomNumberGenerator.Fill(associatedData);

            var controlAlgorithm = new AesCbcHmacEncryption(jwsAlgorithm, cekSizeBits);
            var expectedResult = controlAlgorithm.Encrypt(associatedData.ToArray(), plainText.ToArray(), cek.ToArray());
            if (expectedResult is not [var nonce, var expectedCipherText, var expectedAuthTag])
            {
                throw new InvalidOperationException();
            }

            var algorithm = new AesCbcHmacAuthenticatedEncryptionAlgorithm(
                "code",
                keyedHashFunction,
                cekSizeBits);

            var cipherTextSizeBytes = algorithm.GetCipherTextSizeBytes(plainTextSizeBytes);
            Assert.Equal(expectedCipherText.Length, cipherTextSizeBytes);

            Span<byte> cipherText = new byte[cipherTextSizeBytes];
            Span<byte> authenticationTag = new byte[componentSizeBytes];

            algorithm.Encrypt(
                cek,
                nonce.AsSpan(),
                plainText,
                associatedData,
                cipherText,
                authenticationTag);

            Assert.Equal(expectedCipherText, cipherText.ToArray());
            Assert.Equal(expectedAuthTag, authenticationTag.ToArray());

            Span<byte> plainTextOutput = new byte[plainTextSizeBytes];

            var decryptResult = algorithm.TryDecrypt(
                cek,
                nonce.AsSpan(),
                cipherText,
                associatedData,
                authenticationTag,
                plainTextOutput,
                out var decryptBytesWritten);

            Assert.True(decryptResult);
            Assert.Equal(plainTextSizeBytes, decryptBytesWritten);
            Assert.Equal(plainText.ToArray(), plainTextOutput.ToArray());
        }
    }
}
