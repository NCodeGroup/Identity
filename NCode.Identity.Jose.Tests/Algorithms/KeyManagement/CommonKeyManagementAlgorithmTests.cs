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
using NCode.Identity.DataProtection;
using NCode.Identity.Jose.Algorithms;
using NCode.Identity.Jose.Exceptions;
using NCode.Identity.Secrets;

namespace NCode.Jose.Tests.Algorithms.KeyManagement;

public class CommonKeyManagementAlgorithmTests : BaseTests
{
    private DefaultSecretKeyFactory SecretKeyFactory { get; } = new(NoneSecureDataProtector.Singleton);
    private Mock<KeyManagementAlgorithm> MockAlgorithm { get; }
    private DummyCommonKeyManagementAlgorithm Algorithm { get; }

    public CommonKeyManagementAlgorithmTests()
    {
        MockAlgorithm = CreatePartialMock<KeyManagementAlgorithm>();
        Algorithm = new DummyCommonKeyManagementAlgorithm(MockAlgorithm.Object);
    }

    [Fact]
    public void Type_Valid()
    {
        Assert.Equal(AlgorithmType.KeyManagement, Algorithm.Type);
    }

    [Fact]
    public void NewKey_Valid()
    {
        const int kekSizeBytes = 32;
        const int kekSizeBits = kekSizeBytes << 3;
        const int cekSizeBytes = 64;

        Span<byte> kek = new byte[kekSizeBytes];
        RandomNumberGenerator.Fill(kek);
        var secretKey = SecretKeyFactory.CreateSymmetric(default, kek);

        Span<byte> cek = new byte[cekSizeBytes];
        cek.Fill(0);

        var header = new Dictionary<string, object>();

        MockAlgorithm
            .Setup(x => x.GetLegalCekByteSizes(kekSizeBits))
            .Returns(new[] { new KeySizes(cekSizeBytes, cekSizeBytes, 0) })
            .Verifiable();

        Algorithm.NewKey(secretKey, header, cek);

        Assert.NotEqual(0, cek.ToArray().Sum(b => b));
    }

    [Fact]
    public void ValidateContentKeySize_Valid()
    {
        const int kekSizeBytes = 32;
        const int kekSizeBits = kekSizeBytes << 3;
        const int cekSizeBytes = 64;

        MockAlgorithm
            .Setup(x => x.GetLegalCekByteSizes(kekSizeBits))
            .Returns(new[] { new KeySizes(cekSizeBytes, cekSizeBytes, 0) })
            .Verifiable();

        Algorithm.ValidateContentKeySize(kekSizeBits, cekSizeBytes);
    }

    [Fact]
    public void ValidateContentKeySize_InvalidSize()
    {
        const int kekSizeBytes = 32;
        const int kekSizeBits = kekSizeBytes << 3;
        const int cekSizeBytes = 64;

        MockAlgorithm
            .Setup(x => x.GetLegalCekByteSizes(kekSizeBits))
            .Returns(new[] { new KeySizes(cekSizeBytes + 1, cekSizeBytes + 1, 0) })
            .Verifiable();

        var exception = Assert.Throws<JoseException>(() =>
            Algorithm.ValidateContentKeySize(kekSizeBits, cekSizeBytes));

        Assert.Equal("The content encryption key (CEK) does not have a valid size for this cryptographic algorithm.", exception.Message);
    }
}
