﻿#region Copyright Preamble

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
using System.Text.Json;
using Jose;
using NCode.Identity.DataProtection;
using NCode.Identity.Jose.Algorithms;
using NCode.Identity.Jose.Algorithms.KeyManagement;
using NCode.Identity.Secrets;

namespace NCode.Jose.Tests.Algorithms.KeyManagement;

public class AesKeyManagementAlgorithmTests : BaseTests
{
    private DefaultSecretKeyFactory SecretKeyFactory { get; } = new(NoneSecureDataProtector.Singleton);
    private Mock<IAesKeyWrap> MockAesKeyWrap { get; }

    public AesKeyManagementAlgorithmTests()
    {
        MockAesKeyWrap = CreateStrictMock<IAesKeyWrap>();
    }

    private AesKeyManagementAlgorithm CreateAlgorithm(int? kekSizeBits = null, IAesKeyWrap? aesKeyWrap = null) => new(
        aesKeyWrap ?? MockAesKeyWrap.Object,
        "code",
        kekSizeBits ?? 128);

    [Fact]
    public void Code_Valid()
    {
        var algorithm = CreateAlgorithm();
        Assert.Equal("code", algorithm.Code);
    }

    [Fact]
    public void KeyType_Valid()
    {
        var algorithm = CreateAlgorithm();
        Assert.Equal(typeof(SymmetricSecretKey), algorithm.KeyType);
    }

    [Fact]
    public void KeyBitSizes_Valid()
    {
        var kekSizeBits = Random.Shared.Next();
        var algorithm = CreateAlgorithm(kekSizeBits);
        var result = Assert.Single(algorithm.KeyBitSizes);
        Assert.Equal(kekSizeBits, result.MinSize);
        Assert.Equal(kekSizeBits, result.MaxSize);
        Assert.Equal(0, result.SkipSize);
    }

    [Fact]
    public void GetLegalCekByteSizes_Valid()
    {
        var kekSizeBits = Random.Shared.Next();
        var legalCekByteSizes = new KeySizes[] { new(-1, -1, 0) };

        MockAesKeyWrap
            .Setup(x => x.LegalCekByteSizes)
            .Returns(legalCekByteSizes)
            .Verifiable();

        var algorithm = CreateAlgorithm();
        Assert.Same(legalCekByteSizes, algorithm.GetLegalCekByteSizes(kekSizeBits));
    }

    [Fact]
    public void GetEncryptedContentKeySizeBytes_Valid()
    {
        var kekSizeBits = Random.Shared.Next();
        var cekSizeBytes = Random.Shared.Next();
        var expected = Random.Shared.Next();

        MockAesKeyWrap
            .Setup(x => x.GetEncryptedContentKeySizeBytes(cekSizeBytes))
            .Returns(expected)
            .Verifiable();

        var algorithm = CreateAlgorithm();
        var result = algorithm.GetEncryptedContentKeySizeBytes(kekSizeBits, cekSizeBytes);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(128, 128)]
    [InlineData(128, 256)]
    [InlineData(192, 128)]
    [InlineData(192, 256)]
    [InlineData(256, 128)]
    [InlineData(256, 256)]
    public void RoundTrip_Valid(int kekSizeBits, int cekSizeBits)
    {
        const string keyId = nameof(keyId);

        var aesKeyWrap = new DefaultAesKeyWrap();

        var kekSizeBytes = kekSizeBits >> 3;
        Span<byte> kek = new byte[kekSizeBytes];

        var metadata = new KeyMetadata { KeyId = keyId };
        var secretKey = SecretKeyFactory.CreateSymmetric(metadata, kek);

        var cekSizeBytes = cekSizeBits >> 3;
        var encryptedCekSizeBytes = aesKeyWrap.GetEncryptedContentKeySizeBytes(cekSizeBytes);
        Span<byte> cek = new byte[cekSizeBytes];
        Span<byte> encryptedCek = new byte[encryptedCekSizeBytes];

        var headerForWrap = new Dictionary<string, object>();
        var algorithm = CreateAlgorithm(kekSizeBits, DefaultAesKeyWrap.Singleton);

        var wrapResult = algorithm.TryWrapKey(secretKey, headerForWrap, cek, encryptedCek, out var wrapBytesWritten);
        Assert.True(wrapResult);
        Assert.Equal(encryptedCekSizeBytes, wrapBytesWritten);

        var controlAlgorithm = new AesKeyWrapManagement(kekSizeBits);
        var controlResult = controlAlgorithm.Unwrap(encryptedCek.ToArray(), kek.ToArray(), cekSizeBits, headerForWrap);
        Assert.Equal(controlResult, cek.ToArray());

        var headerForUnwrap = JsonSerializer.SerializeToElement(headerForWrap);
        var unwrapResult = algorithm.TryUnwrapKey(secretKey, headerForUnwrap, encryptedCek, cek, out var unwrapBytesWritten);
        Assert.True(unwrapResult);
        Assert.Equal(cekSizeBytes, unwrapBytesWritten);
        Assert.Equal(controlResult, cek.ToArray());
    }
}
