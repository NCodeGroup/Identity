// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Jose;
using NCode.Buffers;
using NCode.Identity.Jose.Algorithms;
using NCode.Identity.Jose.Algorithms.KeyManagement;
using NCode.Identity.Secrets.Keys;
using Base64Url = NCode.Encoders.Base64Url;
using JoseEncryptionException = NCode.Identity.Jose.Exceptions.JoseEncryptionException;
using JoseException = NCode.Identity.Jose.Exceptions.JoseException;

namespace NCode.Identity.Jose.Tests.Algorithms.KeyManagement;

public class Pbes2KeyManagementAlgorithmTests : BaseTests
{
    private Mock<IAesKeyWrap> MockAesKeyWrap { get; }

    public Pbes2KeyManagementAlgorithmTests()
    {
        MockAesKeyWrap = CreateStrictMock<IAesKeyWrap>();
    }

    private Pbes2KeyManagementAlgorithm Create(
        IAesKeyWrap? aesKeyWrap = null,
        string? code = null,
        int? keySizeBits = null,
        int? maxIterationCount = null
    )
    {
        var actualKeySizeBits = keySizeBits ?? 128;
        var hashAlgorithmName = actualKeySizeBits switch
        {
            128 => HashAlgorithmName.SHA256,
            192 => HashAlgorithmName.SHA384,
            256 => HashAlgorithmName.SHA512,
            _ => throw new ArgumentOutOfRangeException(nameof(keySizeBits)),
        };
        return new Pbes2KeyManagementAlgorithm(
            aesKeyWrap ?? MockAesKeyWrap.Object,
            code ?? "code",
            hashAlgorithmName,
            actualKeySizeBits,
            maxIterationCount ?? 310000
        );
    }

    [Fact]
    public void Code_ReturnsCode()
    {
        var algorithm = Create();
        Assert.Equal("code", algorithm.Code);
    }

    [Fact]
    public void KeyType_ReturnsSymmetricSecretKey()
    {
        var algorithm = Create();
        Assert.Equal(typeof(SymmetricSecretKey), Assert.Single(algorithm.KeyTypes).KeyClrType);
    }

    [Fact]
    public void KekBitSizes_ReturnsExpected()
    {
        var algorithm = Create();
        var kekBitSizes = Assert.Single(Assert.Single(algorithm.KeyTypes).KeyBitSizes);
        Assert.Equal(8, kekBitSizes.MinSize);
        Assert.Equal(int.MaxValue, kekBitSizes.MaxSize);
        Assert.Equal(8, kekBitSizes.SkipSize);
    }

    [Theory]
    [InlineData(128, 16)]
    [InlineData(192, 24)]
    [InlineData(256, 32)]
    public void KeySizeBytes_WhenGivenKeySizeBits_ReturnsExpected(int keySizeBits, int keySizeBytes)
    {
        var algorithm = Create(keySizeBits: keySizeBits);
        Assert.Equal(keySizeBytes, algorithm.KeySizeBytes);
    }

    [Fact]
    public void GetLegalCekByteSizes_ReturnsExpected()
    {
        var expected = Random.Shared.Next();
        var kekSizeBits = Random.Shared.Next();
        var cekSizeBytes = Random.Shared.Next();

        MockAesKeyWrap
            .Setup(x => x.GetEncryptedContentKeySizeBytes(cekSizeBytes))
            .Returns(expected)
            .Verifiable();

        var algorithm = Create();
        var result = algorithm.GetEncryptedContentKeySizeBytes(kekSizeBits, cekSizeBytes);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void GetEncryptedContentKeySizeBytes_WhenGivenKeySizeBits_ReturnsExpected()
    {
        var expected = Enumerable.Empty<KeySizes>();

        MockAesKeyWrap.Setup(x => x.LegalCekByteSizes).Returns(expected).Verifiable();

        var algorithm = Create();
        var kekSizeBits = Random.Shared.Next();
        Assert.Same(expected, algorithm.GetLegalCekByteSizes(kekSizeBits));
    }

    [Fact]
    public void WrapKey_GivenMissingAlgHeader_ThenThrows()
    {
        const string password = nameof(password);

        var algorithm = Create();

        var secretKey = SecretKeyFactory.CreateSymmetric(default, password);

        var header = new Dictionary<string, object>();
        var contentKey = Array.Empty<byte>();
        var encryptedContentKeyWriter = new ArrayBufferWriter<byte>();

        var exception = Assert.Throws<JoseException>(() =>
            algorithm.WrapKey(secretKey, header, contentKey, encryptedContentKeyWriter)
        );

        Assert.Equal("The JWT header is missing the 'alg' field.", exception.Message);
    }

    [Fact]
    public void WrapKey_GivenTooSmallIterationCount_ThenThrows()
    {
        const string password = nameof(password);

        var algorithm = Create();

        var secretKey = SecretKeyFactory.CreateSymmetric(default, password);

        var header = new Dictionary<string, object> { ["alg"] = "anything", ["p2c"] = 1 };

        var contentKey = Array.Empty<byte>();
        var encryptedContentKeyWriter = new ArrayBufferWriter<byte>();

        var exception = Assert.Throws<JoseException>(() =>
            algorithm.WrapKey(secretKey, header, contentKey, encryptedContentKeyWriter)
        );

        Assert.Equal("The 'p2c' field in the JWT header must be at least 1000", exception.Message);
    }

    [Fact]
    public void WrapKey_GivenTooLargeIterationCount_ThenThrows()
    {
        const string password = nameof(password);

        var algorithm = Create();

        var secretKey = SecretKeyFactory.CreateSymmetric(default, password);

        var header = new Dictionary<string, object>
        {
            ["alg"] = "anything",
            ["p2c"] = int.MaxValue,
        };

        var contentKey = Array.Empty<byte>();
        var encryptedContentKeyWriter = new ArrayBufferWriter<byte>();

        var exception = Assert.Throws<JoseException>(() =>
            algorithm.WrapKey(secretKey, header, contentKey, encryptedContentKeyWriter)
        );

        Assert.Equal("The 'p2c' field in the JWT header must be at most 310000", exception.Message);
    }

    [Theory]
    [InlineData(128, 384)]
    [InlineData(128, 512)]
    [InlineData(192, 384)]
    [InlineData(192, 512)]
    [InlineData(256, 384)]
    [InlineData(256, 512)]
    public void RoundTrip_EncryptsAndDecrypts(int keySizeBits, int cekSizeBits)
    {
        const string keyId = nameof(keyId);
        const string alg = nameof(alg);

        var password = Guid.NewGuid().ToString("N");
        var keyBytes = SecureEncoding.UTF8.GetBytes(password);
        var cekSizeBytes = cekSizeBits >> 3;

        var algorithm = Create(keySizeBits: keySizeBits, aesKeyWrap: DefaultAesKeyWrap.Singleton);

        var metadata = new KeyMetadata { KeyId = keyId };
        var secretKey = SecretKeyFactory.CreateSymmetric(metadata, keyBytes);

        // ReSharper disable once InconsistentNaming
        var p2c = Random.Shared.Next(Pbes2KeyManagementAlgorithm.MinIterationCount, 310000);
        var saltSize = alg.Length + 1 + Pbes2KeyManagementAlgorithm.SaltInputSizeBytes;

        var header = new Dictionary<string, object> { [nameof(alg)] = alg, [nameof(p2c)] = p2c };

        var contentKey = new byte[cekSizeBytes];
        RandomNumberGenerator.Fill(contentKey);

        var encryptedContentKeyWriter = new ArrayBufferWriter<byte>(cekSizeBytes + 8);
        var decryptedContentKey = new byte[cekSizeBytes];

        algorithm.WrapKey(secretKey, header, contentKey, encryptedContentKeyWriter);
        Assert.Equal(cekSizeBytes + 8, encryptedContentKeyWriter.WrittenCount);
        Assert.Equal(p2c, Assert.IsType<int>(Assert.Contains(nameof(p2c), header)));

        var encodedSaltInput = Assert.IsType<string>(Assert.Contains("p2s", header));
        Assert.Equal(saltSize, encodedSaltInput.Length);

        var controlAlgorithm = new Pbse2HmacShaKeyManagementWithAesKeyWrap(
            keySizeBits,
            new AesKeyWrapManagement(keySizeBits)
        );

        var expectedControl = controlAlgorithm.Unwrap(
            encryptedContentKeyWriter.WrittenSpan.ToArray(),
            password,
            cekSizeBits,
            header
        );
        Assert.Equal(expectedControl, contentKey.ToArray());

        var headerForUnwrap = JsonSerializer.SerializeToElement(header);
        var unwrapResult = algorithm.TryUnwrapKey(
            secretKey,
            headerForUnwrap,
            encryptedContentKeyWriter.WrittenSpan,
            decryptedContentKey,
            out var unwrapBytesWritten
        );
        Assert.True(unwrapResult);
        Assert.Equal(contentKey.Length, unwrapBytesWritten);
        Assert.Equal(contentKey, decryptedContentKey);
    }

    #region TryUnwrapKey Tests

    private const string Password = "correct horse battery staple";

    // A header, encrypted content key and content key from a real wrap, which each test alters in one way.
    private static (JsonObject Header, byte[] EncryptedContentKey, byte[] ContentKey) Wrap(
        Pbes2KeyManagementAlgorithm algorithm,
        string password = Password
    )
    {
        var secretKey = SecretKeyFactory.CreateSymmetric(default, password);
        var header = new Dictionary<string, object>
        {
            ["alg"] = "PBES2-HS256+A128KW",
            ["p2c"] = Pbes2KeyManagementAlgorithm.MinIterationCount,
        };

        var contentKey = new byte[32];
        RandomNumberGenerator.Fill(contentKey);

        var encryptedContentKeyWriter = new ArrayBufferWriter<byte>();
        algorithm.WrapKey(secretKey, header, contentKey, encryptedContentKeyWriter);

        var headerNode = Assert.IsType<JsonObject>(JsonSerializer.SerializeToNode(header));
        return (headerNode, encryptedContentKeyWriter.WrittenSpan.ToArray(), contentKey);
    }

    private static bool TryUnwrap(
        Pbes2KeyManagementAlgorithm algorithm,
        JsonObject header,
        byte[] encryptedContentKey,
        Span<byte> contentKey,
        out int bytesWritten,
        string password = Password
    ) =>
        algorithm.TryUnwrapKey(
            SecretKeyFactory.CreateSymmetric(default, password),
            JsonSerializer.SerializeToElement(header),
            encryptedContentKey,
            contentKey,
            out bytesWritten
        );

    // The strict key wrap mock fails any test that reaches the key wrap, so each header check is shown to refuse the
    // header before a key encryption key is derived from it.
    private JoseException AssertUnwrapRefusesHeader(JsonObject header)
    {
        var algorithm = Create();
        var contentKey = new byte[32];
        return Assert.Throws<JoseException>(() =>
            TryUnwrap(algorithm, header, new byte[40], contentKey, out _)
        );
    }

    private static JsonObject CreateHeader() =>
        new()
        {
            ["alg"] = "PBES2-HS256+A128KW",
            ["p2c"] = Pbes2KeyManagementAlgorithm.MinIterationCount,
            ["p2s"] = Base64Url.Encode(new byte[Pbes2KeyManagementAlgorithm.SaltInputSizeBytes]),
        };

    [Theory]
    [InlineData("alg")]
    [InlineData("p2c")]
    [InlineData("p2s")]
    public void TryUnwrapKey_GivenHeaderWithoutAMember_ThenThrows(string member)
    {
        var header = CreateHeader();
        header.Remove(member);

        var exception = AssertUnwrapRefusesHeader(header);

        Assert.Equal($"The JWT header is missing the '{member}' field.", exception.Message);
    }

    [Theory]
    [InlineData(0, "The 'p2c' field in the JWT header must be at least 1000")]
    [InlineData(999, "The 'p2c' field in the JWT header must be at least 1000")]
    [InlineData(310001, "The 'p2c' field in the JWT header must be at most 310000")]
    public void TryUnwrapKey_GivenIterationCountOutOfRange_ThenThrows(
        int iterationCount,
        string expected
    )
    {
        var header = CreateHeader();
        header["p2c"] = iterationCount;

        var exception = AssertUnwrapRefusesHeader(header);

        Assert.Equal(expected, exception.Message);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(8)]
    [InlineData(11)]
    [InlineData(13)]
    [InlineData(16)]
    public void TryUnwrapKey_GivenSaltInputOfAnotherSize_ThenThrows(int saltInputSizeBytes)
    {
        var header = CreateHeader();
        header["p2s"] = Base64Url.Encode(new byte[saltInputSizeBytes]);

        var exception = AssertUnwrapRefusesHeader(header);

        Assert.Equal(
            "The salt input ('p2s') does not have a valid size for this cryptographic algorithm.",
            exception.Message
        );
    }

    // A length that no base64url value has, which a decoder refuses before it decodes.
    [Fact]
    public void TryUnwrapKey_GivenSaltInputOfAnImpossibleLength_ThenThrows()
    {
        var header = CreateHeader();
        header["p2s"] = new string('A', 17);

        var exception = AssertUnwrapRefusesHeader(header);

        Assert.Equal(
            "The salt input ('p2s') does not have a valid size for this cryptographic algorithm.",
            exception.Message
        );
    }

    [Theory]
    [InlineData("!!!!!!!!!!!!!!!!")]
    [InlineData("AAAAAAAAAAAAAAA=")]
    [InlineData("AAAAAAAA+AAAAAAA")]
    [InlineData("AAAAAAAA/AAAAAAA")]
    [InlineData("AAAAAAAA AAAAAAA")]
    public void TryUnwrapKey_GivenSaltInputThatIsNotBase64Url_ThenThrows(string saltInput)
    {
        var header = CreateHeader();
        header["p2s"] = saltInput;

        var exception = AssertUnwrapRefusesHeader(header);

        Assert.Equal(
            "The salt input ('p2s') in the JWT header is not valid base64url.",
            exception.Message
        );
    }

    [Theory]
    [InlineData(0)]
    [InlineData(8)]
    [InlineData(16)]
    [InlineData(23)]
    [InlineData(25)]
    [InlineData(31)]
    public void TryUnwrapKey_GivenEncryptedContentKeyOfAnInvalidSize_ThenThrows(
        int encryptedContentKeySizeBytes
    )
    {
        var algorithm = Create();
        var contentKey = new byte[64];

        var exception = Assert.Throws<JoseException>(() =>
            TryUnwrap(
                algorithm,
                CreateHeader(),
                new byte[encryptedContentKeySizeBytes],
                contentKey,
                out _
            )
        );

        Assert.Equal(
            "The encrypted content encryption key (CEK) does not have a valid size for this cryptographic algorithm.",
            exception.Message
        );
    }

    [Fact]
    public void TryUnwrapKey_GivenContentKeyDestinationTooSmall_ThenReturnsFalse()
    {
        var algorithm = Create();
        var contentKey = new byte[31];

        var result = TryUnwrap(
            algorithm,
            CreateHeader(),
            new byte[40],
            contentKey,
            out var bytesWritten
        );

        Assert.False(result);
        Assert.Equal(0, bytesWritten);
    }

    [Fact]
    public void TryUnwrapKey_GivenKeyOfASizeOutsideTheKeyBitSizes_ThenThrows()
    {
        var algorithm = Create();
        var secretKey = SecretKeyFactory.CreateSymmetric(default, Array.Empty<byte>());
        var contentKey = new byte[32];

        var exception = Assert.Throws<ArgumentException>(() =>
            algorithm.TryUnwrapKey(
                secretKey,
                JsonSerializer.SerializeToElement(CreateHeader()),
                new byte[40],
                contentKey,
                out _
            )
        );

        Assert.StartsWith(
            "The secret key does not have a valid size for this cryptographic algorithm.",
            exception.Message,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void TryUnwrapKey_GivenKeyOfAnotherType_ThenThrows()
    {
        var algorithm = Create();
        using var rsa = RSA.Create(2048);
        var secretKey = SecretKeyFactory.CreateRsa(default, rsa);
        var contentKey = new byte[32];

        Assert.Throws<ArgumentException>(() =>
            algorithm.TryUnwrapKey(
                secretKey,
                JsonSerializer.SerializeToElement(CreateHeader()),
                new byte[40],
                contentKey,
                out _
            )
        );
    }

    [Fact]
    public void TryUnwrapKey_GivenTheWrappedKey_ThenUnwrapsIt()
    {
        var algorithm = Create(aesKeyWrap: DefaultAesKeyWrap.Singleton);
        var (header, encryptedContentKey, expected) = Wrap(algorithm);
        var contentKey = new byte[expected.Length];

        var result = TryUnwrap(algorithm, header, encryptedContentKey, contentKey, out var written);

        Assert.True(result);
        Assert.Equal(expected.Length, written);
        Assert.Equal(expected, contentKey);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(8)]
    [InlineData(39)]
    public void TryUnwrapKey_GivenTamperedEncryptedContentKey_ThenFailsTheIntegrityCheck(
        int tamperedIndex
    )
    {
        var algorithm = Create(aesKeyWrap: DefaultAesKeyWrap.Singleton);
        var (header, encryptedContentKey, _) = Wrap(algorithm);
        encryptedContentKey[tamperedIndex] ^= 0x01;
        var contentKey = new byte[32];

        Assert.Throws<JoseEncryptionException>(() =>
            TryUnwrap(algorithm, header, encryptedContentKey, contentKey, out _)
        );
    }

    [Fact]
    public void TryUnwrapKey_GivenTamperedSaltInput_ThenFailsTheIntegrityCheck()
    {
        var algorithm = Create(aesKeyWrap: DefaultAesKeyWrap.Singleton);
        var (header, encryptedContentKey, _) = Wrap(algorithm);
        header["p2s"] = Base64Url.Encode(new byte[Pbes2KeyManagementAlgorithm.SaltInputSizeBytes]);
        var contentKey = new byte[32];

        Assert.Throws<JoseEncryptionException>(() =>
            TryUnwrap(algorithm, header, encryptedContentKey, contentKey, out _)
        );
    }

    [Fact]
    public void TryUnwrapKey_GivenWrongPassword_ThenFailsTheIntegrityCheck()
    {
        var algorithm = Create(aesKeyWrap: DefaultAesKeyWrap.Singleton);
        var (header, encryptedContentKey, _) = Wrap(algorithm);
        var contentKey = new byte[32];

        Assert.Throws<JoseEncryptionException>(() =>
            TryUnwrap(
                algorithm,
                header,
                encryptedContentKey,
                contentKey,
                out _,
                password: "wrong password"
            )
        );
    }

    #endregion
}
