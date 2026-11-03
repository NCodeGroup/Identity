// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using NCode.Identity.Jose.Algorithms;
using NCode.Identity.Jose.Algorithms.KeyManagement;
using NCode.Identity.Jose.Exceptions;
using NCode.Identity.Secrets.Keys;

namespace NCode.Identity.Jose.Tests.Algorithms.KeyManagement;

public class DirectKeyManagementAlgorithmTests : BaseTests
{
    private static KeyManagementAlgorithm Algorithm => DirectKeyManagementAlgorithm.Singleton;

    [Fact]
    public void Code_ReturnsDir()
    {
        Assert.Equal("dir", Algorithm.Code);
    }

    [Fact]
    public void KeyType_ReturnsSymmetricSecretKey()
    {
        Assert.Equal(typeof(SymmetricSecretKey), Assert.Single(Algorithm.KeyTypes).KeyClrType);
    }

    [Fact]
    public void KeyBitSizes_ReturnsExpected()
    {
        var result = Assert.Single(Assert.Single(Algorithm.KeyTypes).KeyBitSizes);
        Assert.Equal(8, result.MinSize);
        Assert.Equal(int.MaxValue, result.MaxSize);
        Assert.Equal(8, result.SkipSize);
    }

    [Fact]
    public void GetLegalCekByteSizes_ReturnsExpected()
    {
        var kekSizeBits = Random.Shared.Next();
        var result = Assert.Single(Algorithm.GetLegalCekByteSizes(kekSizeBits));
        Assert.Equal(1, result.MinSize);
        Assert.Equal(int.MaxValue, result.MaxSize);
        Assert.Equal(1, result.SkipSize);
    }

    [Fact]
    public void GetEncryptedContentKeySizeBytes_ReturnsExpected()
    {
        var kekSizeBits = Random.Shared.Next();
        var cekSizeBytes = Random.Shared.Next();
        var result = Algorithm.GetEncryptedContentKeySizeBytes(kekSizeBits, cekSizeBytes);
        Assert.Equal(0, result);
    }

    [Fact]
    public void NewKey_CopiesKeyToDestination()
    {
        var kekSizeBytes = Random.Shared.Next(32, 512);
        Span<byte> kek = new byte[kekSizeBytes];
        Span<byte> cek = new byte[kekSizeBytes];
        RandomNumberGenerator.Fill(kek);
        var secretKey = SecretKeyFactory.CreateSymmetric(default, kek);

        var header = new Dictionary<string, object>();

        Algorithm.NewKey(secretKey, header, cek);

        Assert.Equal(kek.ToArray(), cek.ToArray());
    }

    [Fact]
    public void NewKey_WhenDestinationInvalidSize_Throws()
    {
        var kekSizeBytes = Random.Shared.Next(32, 512);
        Span<byte> kek = new byte[kekSizeBytes];
        RandomNumberGenerator.Fill(kek);
        var secretKey = SecretKeyFactory.CreateSymmetric(default, kek);

        var header = new Dictionary<string, object>();

        var exception = Assert.Throws<JoseException>(() =>
        {
            Span<byte> cek = new byte[kekSizeBytes + 1];
            Algorithm.NewKey(secretKey, header, cek);
        });

        Assert.Equal(
            "The size of the destination buffer for CEK must identical to the KEK size.",
            exception.Message
        );
    }

    [Fact]
    public void WrapKey_WhenCalled_Throws()
    {
        Span<byte> kek = new byte[1];
        var secretKey = SecretKeyFactory.CreateSymmetric(default, kek);

        var header = new Dictionary<string, object>();

        var exception = Assert.Throws<JoseException>(() =>
        {
            Span<byte> cek = new byte[1];
            var encryptedCekWriter = new ArrayBufferWriter<byte>(1);
            Algorithm.WrapKey(secretKey, header, cek, encryptedCekWriter);
        });

        Assert.Equal(
            "The direct key management algorithm does not support using an existing CEK.",
            exception.Message
        );
    }

    [Fact]
    public void TryUnwrapKey_WhenGivenValidInput_UnwrapsKey()
    {
        var kekSizeBytes = Random.Shared.Next(32, 512);
        Span<byte> kek = new byte[kekSizeBytes];
        Span<byte> cek = new byte[kekSizeBytes];
        Span<byte> encryptedCek = Array.Empty<byte>();
        RandomNumberGenerator.Fill(kek);
        var secretKey = SecretKeyFactory.CreateSymmetric(default, kek);

        var header = new JsonObject();
        var headerForUnwrap = header.Deserialize<JsonElement>();

        var unwrapResult = Algorithm.TryUnwrapKey(
            secretKey,
            headerForUnwrap,
            encryptedCek,
            cek,
            out var bytesWritten
        );
        Assert.True(unwrapResult);
        Assert.Equal(kekSizeBytes, bytesWritten);
        Assert.Equal(kek.ToArray(), cek.ToArray());
    }

    [Fact]
    public void TryUnwrapKey_WhenEncryptedCekInvalidSize_Throws()
    {
        var kekSizeBytes = Random.Shared.Next(32, 512);
        Span<byte> kek = new byte[kekSizeBytes];
        RandomNumberGenerator.Fill(kek);
        var secretKey = SecretKeyFactory.CreateSymmetric(default, kek);

        var header = new JsonObject();
        var headerForUnwrap = header.Deserialize<JsonElement>();

        var exception = Assert.Throws<JoseException>(() =>
        {
            Span<byte> cek = new byte[kekSizeBytes];
            Span<byte> encryptedCek = new byte[1];

            Algorithm.TryUnwrapKey(secretKey, headerForUnwrap, encryptedCek, cek, out _);
        });

        Assert.Equal(
            "The encrypted content encryption key (CEK) does not have a valid size for this cryptographic algorithm.",
            exception.Message
        );
    }
}
