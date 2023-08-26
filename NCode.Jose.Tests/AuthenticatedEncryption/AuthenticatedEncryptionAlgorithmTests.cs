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
using NCode.Jose.AuthenticatedEncryption;

namespace NCode.Jose.Tests.AuthenticatedEncryption;

public class AuthenticatedEncryptionAlgorithmTests : BaseTests
{
    private Mock<AuthenticatedEncryptionAlgorithm> MockAuthenticatedEncryptionAlgorithm { get; }
    private AuthenticatedEncryptionAlgorithm AuthenticatedEncryptionAlgorithm { get; }

    public AuthenticatedEncryptionAlgorithmTests()
    {
        MockAuthenticatedEncryptionAlgorithm = CreatePartialMock<AuthenticatedEncryptionAlgorithm>();
        AuthenticatedEncryptionAlgorithm = MockAuthenticatedEncryptionAlgorithm.Object;
    }

    [Fact]
    public void Type_Valid()
    {
        var actual = AuthenticatedEncryptionAlgorithm.Type;
        Assert.Equal(AlgorithmType.AuthenticatedEncryption, actual);
    }

    [Fact]
    public void KeyType_Valid()
    {
        var actual = AuthenticatedEncryptionAlgorithm.KeyType;
        Assert.Equal(typeof(ReadOnlySpan<byte>), actual);
    }

    [Fact]
    public void KeyBitSizes_Valid()
    {
        const int contentKeySizeBytes = 7;
        const int contentKeySizeBits = contentKeySizeBytes << 3;

        MockAuthenticatedEncryptionAlgorithm
            .Setup(_ => _.ContentKeySizeBytes)
            .Returns(contentKeySizeBytes)
            .Verifiable();

        var results = AuthenticatedEncryptionAlgorithm.KeyBitSizes;
        var result = Assert.Single(results);
        Assert.Equal(contentKeySizeBits, result.MinSize);
        Assert.Equal(contentKeySizeBits, result.MaxSize);
        Assert.Equal(0, result.SkipSize);
    }

    [Fact]
    public void ContentKeySizeBits_Valid()
    {
        const int contentKeySizeBytes = 7;
        const int contentKeySizeBits = contentKeySizeBytes << 3;

        MockAuthenticatedEncryptionAlgorithm
            .Setup(_ => _.ContentKeySizeBytes)
            .Returns(contentKeySizeBytes)
            .Verifiable();

        var result = AuthenticatedEncryptionAlgorithm.ContentKeySizeBits;
        Assert.Equal(contentKeySizeBits, result);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ValidateParameters_Valid(bool encrypt)
    {
        const int contentKeySizeBytes = 7;
        const int nonceSizeBytes = 8;
        const int plainTextSizeBytes = 9;
        const int cipherTextSizeBytes = 10;
        const int authenticationTagSizeBytes = 11;

        Span<byte> cek = new byte[contentKeySizeBytes];
        Span<byte> nonce = new byte[nonceSizeBytes];
        Span<byte> plainText = new byte[plainTextSizeBytes];
        Span<byte> cipherText = new byte[cipherTextSizeBytes];
        Span<byte> authenticationTag = new byte[authenticationTagSizeBytes];

        RandomNumberGenerator.Fill(cek);
        RandomNumberGenerator.Fill(nonce);
        RandomNumberGenerator.Fill(plainText);
        RandomNumberGenerator.Fill(cipherText);
        RandomNumberGenerator.Fill(authenticationTag);

        MockAuthenticatedEncryptionAlgorithm
            .Setup(_ => _.ContentKeySizeBytes)
            .Returns(contentKeySizeBytes)
            .Verifiable();

        MockAuthenticatedEncryptionAlgorithm
            .Setup(_ => _.NonceSizeBytes)
            .Returns(nonceSizeBytes)
            .Verifiable();

        if (encrypt)
        {
            MockAuthenticatedEncryptionAlgorithm
                .Setup(_ => _.GetCipherTextSizeBytes(plainTextSizeBytes))
                .Returns(cipherTextSizeBytes)
                .Verifiable();
        }

        MockAuthenticatedEncryptionAlgorithm
            .Setup(_ => _.AuthenticationTagSizeBytes)
            .Returns(authenticationTagSizeBytes)
            .Verifiable();

        AuthenticatedEncryptionAlgorithm.ValidateParameters(
            encrypt,
            cek,
            nonce,
            plainText,
            cipherText,
            authenticationTag);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ValidateParameters_Invalid_Cek(bool encrypt)
    {
        const int contentKeySizeBytes = 7;
        const int nonceSizeBytes = 8;
        const int plainTextSizeBytes = 9;
        const int cipherTextSizeBytes = 10;
        const int authenticationTagSizeBytes = 11;

        MockAuthenticatedEncryptionAlgorithm
            .Setup(_ => _.ContentKeySizeBytes)
            .Returns(contentKeySizeBytes)
            .Verifiable();

        var exception = Assert.Throws<ArgumentException>(() =>
        {
            Span<byte> cek = new byte[contentKeySizeBytes + 1];
            Span<byte> nonce = new byte[nonceSizeBytes];
            Span<byte> plainText = new byte[plainTextSizeBytes];
            Span<byte> cipherText = new byte[cipherTextSizeBytes];
            Span<byte> authenticationTag = new byte[authenticationTagSizeBytes];

            RandomNumberGenerator.Fill(cek);
            RandomNumberGenerator.Fill(nonce);
            RandomNumberGenerator.Fill(plainText);
            RandomNumberGenerator.Fill(cipherText);
            RandomNumberGenerator.Fill(authenticationTag);

            AuthenticatedEncryptionAlgorithm.ValidateParameters(
                encrypt,
                cek,
                nonce,
                plainText,
                cipherText,
                authenticationTag);
        });

        Assert.Equal("The specified content encryption key (CEK) does not have a valid size for this cryptographic algorithm. (Parameter 'cek')", exception.Message);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ValidateParameters_Invalid_Nonce(bool encrypt)
    {
        const int contentKeySizeBytes = 7;
        const int nonceSizeBytes = 8;
        const int plainTextSizeBytes = 9;
        const int cipherTextSizeBytes = 10;
        const int authenticationTagSizeBytes = 11;

        MockAuthenticatedEncryptionAlgorithm
            .Setup(_ => _.ContentKeySizeBytes)
            .Returns(contentKeySizeBytes)
            .Verifiable();

        MockAuthenticatedEncryptionAlgorithm
            .Setup(_ => _.NonceSizeBytes)
            .Returns(nonceSizeBytes)
            .Verifiable();

        var exception = Assert.Throws<ArgumentException>(() =>
        {
            Span<byte> cek = new byte[contentKeySizeBytes];
            Span<byte> nonce = new byte[nonceSizeBytes + 1];
            Span<byte> plainText = new byte[plainTextSizeBytes];
            Span<byte> cipherText = new byte[cipherTextSizeBytes];
            Span<byte> authenticationTag = new byte[authenticationTagSizeBytes];

            RandomNumberGenerator.Fill(cek);
            RandomNumberGenerator.Fill(nonce);
            RandomNumberGenerator.Fill(plainText);
            RandomNumberGenerator.Fill(cipherText);
            RandomNumberGenerator.Fill(authenticationTag);

            AuthenticatedEncryptionAlgorithm.ValidateParameters(
                encrypt,
                cek,
                nonce,
                plainText,
                cipherText,
                authenticationTag);
        });

        Assert.Equal("The specified nonce does not have a valid size for this cryptographic algorithm. (Parameter 'nonce')", exception.Message);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ValidateParameters_Invalid_PlainText(bool encrypt)
    {
        const int contentKeySizeBytes = 7;
        const int nonceSizeBytes = 8;
        const int plainTextSizeBytes = 9;
        const int cipherTextSizeBytes = 10;
        const int authenticationTagSizeBytes = 11;

        MockAuthenticatedEncryptionAlgorithm
            .Setup(_ => _.ContentKeySizeBytes)
            .Returns(contentKeySizeBytes)
            .Verifiable();

        MockAuthenticatedEncryptionAlgorithm
            .Setup(_ => _.NonceSizeBytes)
            .Returns(nonceSizeBytes)
            .Verifiable();

        if (encrypt)
        {
            MockAuthenticatedEncryptionAlgorithm
                .Setup(_ => _.GetCipherTextSizeBytes(plainTextSizeBytes))
                .Returns(cipherTextSizeBytes)
                .Verifiable();
        }
        else
        {
            MockAuthenticatedEncryptionAlgorithm
                .Setup(_ => _.AuthenticationTagSizeBytes)
                .Returns(authenticationTagSizeBytes)
                .Verifiable();
        }

        if (encrypt)
        {
            var exception = Assert.Throws<ArgumentException>(() =>
            {
                Span<byte> cek = new byte[contentKeySizeBytes];
                Span<byte> nonce = new byte[nonceSizeBytes];
                Span<byte> plainText = new byte[plainTextSizeBytes + 1];
                Span<byte> cipherText = new byte[cipherTextSizeBytes];
                Span<byte> authenticationTag = new byte[authenticationTagSizeBytes];

                RandomNumberGenerator.Fill(cek);
                RandomNumberGenerator.Fill(nonce);
                RandomNumberGenerator.Fill(plainText);
                RandomNumberGenerator.Fill(cipherText);
                RandomNumberGenerator.Fill(authenticationTag);

                AuthenticatedEncryptionAlgorithm.ValidateParameters(
                    encrypt,
                    cek,
                    nonce,
                    plainText,
                    cipherText,
                    authenticationTag);
            });

            Assert.Equal("The specified plain text and cipher text do not have a valid size for this cryptographic algorithm. (Parameter 'cipherText')", exception.Message);
        }
        else
        {
            Span<byte> cek = new byte[contentKeySizeBytes];
            Span<byte> nonce = new byte[nonceSizeBytes];
            Span<byte> plainText = new byte[plainTextSizeBytes + 1];
            Span<byte> cipherText = new byte[cipherTextSizeBytes];
            Span<byte> authenticationTag = new byte[authenticationTagSizeBytes];

            RandomNumberGenerator.Fill(cek);
            RandomNumberGenerator.Fill(nonce);
            RandomNumberGenerator.Fill(plainText);
            RandomNumberGenerator.Fill(cipherText);
            RandomNumberGenerator.Fill(authenticationTag);

            AuthenticatedEncryptionAlgorithm.ValidateParameters(
                encrypt,
                cek,
                nonce,
                plainText,
                cipherText,
                authenticationTag);
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ValidateParameters_Invalid_AuthenticationTag(bool encrypt)
    {
        const int contentKeySizeBytes = 7;
        const int nonceSizeBytes = 8;
        const int plainTextSizeBytes = 9;
        const int cipherTextSizeBytes = 10;
        const int authenticationTagSizeBytes = 11;

        MockAuthenticatedEncryptionAlgorithm
            .Setup(_ => _.ContentKeySizeBytes)
            .Returns(contentKeySizeBytes)
            .Verifiable();

        MockAuthenticatedEncryptionAlgorithm
            .Setup(_ => _.NonceSizeBytes)
            .Returns(nonceSizeBytes)
            .Verifiable();

        if (encrypt)
        {
            MockAuthenticatedEncryptionAlgorithm
                .Setup(_ => _.GetCipherTextSizeBytes(plainTextSizeBytes))
                .Returns(cipherTextSizeBytes)
                .Verifiable();
        }

        MockAuthenticatedEncryptionAlgorithm
            .Setup(_ => _.AuthenticationTagSizeBytes)
            .Returns(authenticationTagSizeBytes)
            .Verifiable();

        var exception = Assert.Throws<ArgumentException>(() =>
        {
            Span<byte> cek = new byte[contentKeySizeBytes];
            Span<byte> nonce = new byte[nonceSizeBytes];
            Span<byte> plainText = new byte[plainTextSizeBytes];
            Span<byte> cipherText = new byte[cipherTextSizeBytes];
            Span<byte> authenticationTag = new byte[authenticationTagSizeBytes + 1];

            RandomNumberGenerator.Fill(cek);
            RandomNumberGenerator.Fill(nonce);
            RandomNumberGenerator.Fill(plainText);
            RandomNumberGenerator.Fill(cipherText);
            RandomNumberGenerator.Fill(authenticationTag);

            AuthenticatedEncryptionAlgorithm.ValidateParameters(
                encrypt,
                cek,
                nonce,
                plainText,
                cipherText,
                authenticationTag);
        });

        Assert.Equal("The specified authentication tag does not have a valid size for this cryptographic algorithm. (Parameter 'authenticationTag')", exception.Message);
    }
}
