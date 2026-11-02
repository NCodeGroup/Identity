// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;
using Jose;
using NCode.Identity.Jose.Algorithms.AuthenticatedEncryption;

namespace NCode.Identity.Jose.Tests.Algorithms.AuthenticatedEncryption;

public class AesGcmAuthenticatedEncryptionAlgorithmTests
{
    [Theory]
    [InlineData(1, 1)]
    [InlineData(7, 7)]
    [InlineData(9, 9)]
    public void GetCipherTextSizeBytes_WhenGivenSizes_ReturnsExpected(
        int plainTextSizeBytes,
        int expected
    )
    {
        const int cekSizeBits = -1; // dont care

        var algorithm = new AesGcmAuthenticatedEncryptionAlgorithm("code", cekSizeBits);

        var actual = algorithm.GetCipherTextSizeBytes(plainTextSizeBytes);
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(7, 7)]
    [InlineData(9, 9)]
    public void GetMaxPlainTextSizeBytes_WhenGivenSizes_ReturnsExpected(
        int cipherTextSizeBytes,
        int expected
    )
    {
        const int cekSizeBits = -1; // dont care

        var algorithm = new AesGcmAuthenticatedEncryptionAlgorithm("code", cekSizeBits);

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
    public void RoundTrip_EncryptsAndDecrypts(
        int cekSizeBits,
        int plainTextSizeBytes,
        int aadSizeBytes
    )
    {
        var cekSizeBytes = cekSizeBits >> 3;

        Span<byte> cek = new byte[cekSizeBytes];
        Span<byte> plainText = new byte[plainTextSizeBytes];
        Span<byte> associatedData = new byte[aadSizeBytes];

        RandomNumberGenerator.Fill(cek);
        RandomNumberGenerator.Fill(plainText);
        RandomNumberGenerator.Fill(associatedData);

        var controlAlgorithm = new AesGcmEncryption(cekSizeBits);
        var expectedResult = controlAlgorithm.Encrypt(
            associatedData.ToArray(),
            plainText.ToArray(),
            cek.ToArray()
        );
        if (expectedResult is not [var nonce, var expectedCipherText, var expectedAuthTag])
        {
            throw new InvalidOperationException();
        }

        var algorithm = new AesGcmAuthenticatedEncryptionAlgorithm("code", cekSizeBits);

        Span<byte> cipherText = new byte[plainTextSizeBytes];
        Span<byte> authenticationTag = new byte[16];

        algorithm.Encrypt(cek, nonce, plainText, associatedData, cipherText, authenticationTag);

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
            out var decryptBytesWritten
        );

        Assert.True(decryptResult);
        Assert.Equal(plainTextSizeBytes, decryptBytesWritten);
        Assert.Equal(plainText.ToArray(), plainTextOutput.ToArray());
    }
}
