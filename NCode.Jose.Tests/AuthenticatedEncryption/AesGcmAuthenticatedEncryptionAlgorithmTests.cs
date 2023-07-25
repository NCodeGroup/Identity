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

public class AesGcmAuthenticatedEncryptionAlgorithmTests
{
    [Theory]
    [InlineData(1, 1)]
    [InlineData(7, 7)]
    [InlineData(9, 9)]
    public void GetCipherTextSizeBytes_Valid(int plainTextSizeBytes, int expected)
    {
        const int cekSizeBits = -1; // dont care

        var algorithm = new AesGcmAuthenticatedEncryptionAlgorithm(
            "code",
            cekSizeBits);

        var actual = algorithm.GetCipherTextSizeBytes(plainTextSizeBytes);
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(7, 7)]
    [InlineData(9, 9)]
    public void GetMaxPlainTextSizeBytes_Valid(int cipherTextSizeBytes, int expected)
    {
        const int cekSizeBits = -1; // dont care

        var algorithm = new AesGcmAuthenticatedEncryptionAlgorithm(
            "code",
            cekSizeBits);

        var actual = algorithm.GetMaxPlainTextSizeBytes(cipherTextSizeBytes);
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(128, 32, 4)]
    [InlineData(128, 33, 6)]
    [InlineData(192, 34, 8)]
    [InlineData(192, 35, 10)]
    [InlineData(256, 36, 12)]
    [InlineData(256, 37, 14)]
    [InlineData(256, 38, 16)]
    public void RoundTrip_Valid(int cekSizeBits, int plainTextSizeBytes, int aadSizeBytes)
    {
        var cekSizeBytes = cekSizeBits >> 3;

        Span<byte> cek = new byte[cekSizeBytes];
        Span<byte> plainText = new byte[plainTextSizeBytes];
        Span<byte> associatedData = new byte[aadSizeBytes];

        RandomNumberGenerator.Fill(cek);
        RandomNumberGenerator.Fill(plainText);
        RandomNumberGenerator.Fill(associatedData);

        var controlAlgorithm = new AesGcmEncryption(cekSizeBits);
        var expectedResult = controlAlgorithm.Encrypt(associatedData.ToArray(), plainText.ToArray(), cek.ToArray());
        if (expectedResult is not [var nonce, var expectedCipherText, var expectedAuthTag])
        {
            throw new InvalidOperationException();
        }

        var algorithm = new AesGcmAuthenticatedEncryptionAlgorithm(
            "code",
            cekSizeBits);

        Span<byte> cipherText = new byte[plainTextSizeBytes];
        Span<byte> authenticationTag = new byte[16];

        algorithm.Encrypt(
            cek,
            nonce,
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
