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
using System.Text.Json;
using System.Text.Json.Nodes;
using NCode.Jose.Algorithms;
using NCode.Jose.Algorithms.KeyManagement;
using NCode.Jose.Exceptions;
using NCode.Jose.SecretKeys;

namespace NCode.Jose.Tests.Algorithms.KeyManagement;

public class DirectKeyManagementAlgorithmTests
{
    private static KeyManagementAlgorithm Algorithm => DirectKeyManagementAlgorithm.Singleton;

    [Fact]
    public void Code_Valid()
    {
        Assert.Equal("dir", Algorithm.Code);
    }

    [Fact]
    public void KeyType_Valid()
    {
        Assert.Equal(typeof(SymmetricSecretKey), Algorithm.KeyType);
    }

    [Fact]
    public void KeyBitSizes_Valid()
    {
        var result = Assert.Single(Algorithm.KeyBitSizes);
        Assert.Equal(8, result.MinSize);
        Assert.Equal(int.MaxValue, result.MaxSize);
        Assert.Equal(8, result.SkipSize);
    }

    [Fact]
    public void GetLegalCekByteSizes_Valid()
    {
        var kekSizeBits = Random.Shared.Next();
        var result = Assert.Single(Algorithm.GetLegalCekByteSizes(kekSizeBits));
        Assert.Equal(1, result.MinSize);
        Assert.Equal(int.MaxValue, result.MaxSize);
        Assert.Equal(1, result.SkipSize);
    }

    [Fact]
    public void GetEncryptedContentKeySizeBytes_Valid()
    {
        var kekSizeBits = Random.Shared.Next();
        var cekSizeBytes = Random.Shared.Next();
        var result = Algorithm.GetEncryptedContentKeySizeBytes(kekSizeBits, cekSizeBytes);
        Assert.Equal(0, result);
    }

    [Fact]
    public void NewKey_Valid()
    {
        var kekSizeBytes = Random.Shared.Next(32, 512);
        Span<byte> kek = new byte[kekSizeBytes];
        Span<byte> cek = new byte[kekSizeBytes];
        RandomNumberGenerator.Fill(kek);
        using var secretKey = new DefaultSymmetricSecretKey(default, kek);

        var header = new Dictionary<string, object>();

        Algorithm.NewKey(secretKey, header, cek);

        Assert.Equal(kek.ToArray(), cek.ToArray());
    }

    [Fact]
    public void NewKey_InvalidDestinationSize()
    {
        var kekSizeBytes = Random.Shared.Next(32, 512);
        Span<byte> kek = new byte[kekSizeBytes];
        RandomNumberGenerator.Fill(kek);
        using var secretKey = new DefaultSymmetricSecretKey(default, kek);

        var header = new Dictionary<string, object>();

        var exception = Assert.Throws<JoseException>(() =>
        {
            Span<byte> cek = new byte[kekSizeBytes + 1];
            Algorithm.NewKey(secretKey, header, cek);
        });

        Assert.Equal("The size of the destination buffer for CEK must identical to the KEK size.", exception.Message);
    }

    [Fact]
    public void TryWrapKey_Valid()
    {
        Span<byte> kek = new byte[1];
        using var secretKey = new DefaultSymmetricSecretKey(default, kek);

        var header = new Dictionary<string, object>();

        var exception = Assert.Throws<JoseException>(() =>
        {
            Span<byte> cek = new byte[1];
            Span<byte> encryptedCek = new byte[1];
            Algorithm.TryWrapKey(secretKey, header, cek, encryptedCek, out _);
        });

        Assert.Equal("The direct key management algorithm does not support using an existing CEK.", exception.Message);
    }

    [Fact]
    public void TryUnwrapKey_Valid()
    {
        var kekSizeBytes = Random.Shared.Next(32, 512);
        Span<byte> kek = new byte[kekSizeBytes];
        Span<byte> cek = new byte[kekSizeBytes];
        Span<byte> encryptedCek = Array.Empty<byte>();
        RandomNumberGenerator.Fill(kek);
        using var secretKey = new DefaultSymmetricSecretKey(default, kek);

        var header = new JsonObject();
        var headerForUnwrap = header.Deserialize<JsonElement>();

        var unwrapResult = Algorithm.TryUnwrapKey(secretKey, headerForUnwrap, encryptedCek, cek, out var bytesWritten);
        Assert.True(unwrapResult);
        Assert.Equal(kekSizeBytes, bytesWritten);
        Assert.Equal(kek.ToArray(), cek.ToArray());
    }

    [Fact]
    public void TryUnwrapKey_InvalidSize()
    {
        var kekSizeBytes = Random.Shared.Next(32, 512);
        Span<byte> kek = new byte[kekSizeBytes];
        RandomNumberGenerator.Fill(kek);
        using var secretKey = new DefaultSymmetricSecretKey(default, kek);

        var header = new JsonObject();
        var headerForUnwrap = header.Deserialize<JsonElement>();

        var exception = Assert.Throws<JoseException>(() =>
        {
            Span<byte> cek = new byte[kekSizeBytes];
            Span<byte> encryptedCek = new byte[1];

            Algorithm.TryUnwrapKey(secretKey, headerForUnwrap, encryptedCek, cek, out _);
        });

        Assert.Equal("The encrypted content encryption key (CEK) does not have a valid size for this cryptographic algorithm.", exception.Message);
    }
}
