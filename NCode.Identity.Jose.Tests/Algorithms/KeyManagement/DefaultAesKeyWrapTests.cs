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

using NCode.Identity.Jose.Algorithms.KeyManagement;
using NCode.Identity.Jose.Exceptions;

namespace NCode.Jose.Tests.Algorithms.KeyManagement;

public class DefaultAesKeyWrapTests
{
    private DefaultAesKeyWrap AesKeyWrap { get; } = new();

    [Fact]
    public void LegalCekByteSizes_Valid()
    {
        var result = Assert.Single(AesKeyWrap.LegalCekByteSizes);
        Assert.Equal(16, result.MinSize);
        Assert.Equal(int.MaxValue, result.MaxSize);
        Assert.Equal(8, result.SkipSize);
    }

    [Theory]
    [InlineData(16, 24, 2)]
    [InlineData(16 + 8, 24 + 8, 3)]
    [InlineData(16 + 16, 24 + 16, 4)]
    [InlineData(16 + 24, 24 + 24, 5)]
    public void GetCipherTextSizeBytes_Valid(int contentKeySizeBytes, int expectedResult, int expectedBlocks)
    {
        var result = DefaultAesKeyWrap.GetCipherTextSizeBytes(contentKeySizeBytes, out var blocks);
        Assert.Equal(expectedResult, result);
        Assert.Equal(expectedBlocks, blocks);
    }

    [Fact]
    public void GetCipherTextSizeBytes_CekTooSmall()
    {
        const int contentKeySizeBytes = 16 - 1;

        var exception = Assert.Throws<JoseException>(() =>
            DefaultAesKeyWrap.GetCipherTextSizeBytes(contentKeySizeBytes, out _));

        Assert.Equal("The CEK must be at least 128 bits.", exception.Message);
    }

    [Fact]
    public void GetCipherTextSizeBytes_CekInvalid()
    {
        const int contentKeySizeBytes = 16 + 1;

        var exception = Assert.Throws<JoseException>(() =>
            DefaultAesKeyWrap.GetCipherTextSizeBytes(contentKeySizeBytes, out _));

        Assert.Equal("The CEK must be a multiple of 64 bits.", exception.Message);
    }

    [Theory]
    [InlineData(16, 8, 1)]
    [InlineData(16 + 8, 8 + 8, 2)]
    [InlineData(16 + 16, 8 + 16, 3)]
    [InlineData(16 + 24, 8 + 24, 4)]
    public void GetUnwrapKeySizeBytes_Valid(int encryptedContentKeySizeBytes, int expectedResult, int expectedBlocks)
    {
        var result = DefaultAesKeyWrap.GetUnwrapKeySizeBytes(encryptedContentKeySizeBytes, out var blocks);
        Assert.Equal(expectedResult, result);
        Assert.Equal(expectedBlocks, blocks);
    }

    [Theory]
    [InlineData(16, 16)]
    [InlineData(16, 16 + 8)]
    [InlineData(16, 16 + 16)]
    [InlineData(16, 16 + 24)]
    [InlineData(24, 16)]
    [InlineData(24, 16 + 8)]
    [InlineData(24, 16 + 16)]
    [InlineData(24, 16 + 24)]
    [InlineData(32, 16)]
    [InlineData(32, 16 + 8)]
    [InlineData(32, 16 + 16)]
    [InlineData(32, 16 + 24)]
    public void RoundTrip_Valid(int kekSizeBytes, int cekSizeBytes)
    {
        Span<byte> kek = new byte[kekSizeBytes];
        Span<byte> cek = new byte[cekSizeBytes];
        Span<byte> encryptedCek = new byte[cekSizeBytes + 8];

        var wrapResult = AesKeyWrap.TryWrapKey(kek, cek, encryptedCek, out var wrapBytesWritten);
        Assert.True(wrapResult);
        Assert.Equal(cekSizeBytes + 8, wrapBytesWritten);

        var controlResult = global::Jose.AesKeyWrap.Unwrap(encryptedCek.ToArray(), kek.ToArray());
        Assert.Equal(controlResult, cek.ToArray());

        var unwrapResult = AesKeyWrap.TryUnwrapKey(kek, encryptedCek, cek, out var unwrapBytesWritten);
        Assert.True(unwrapResult);
        Assert.Equal(cekSizeBytes, unwrapBytesWritten);
        Assert.Equal(controlResult, cek.ToArray());
    }
}
