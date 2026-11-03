// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers;
using System.Security.Cryptography;
using System.Text.Json;
using Jose;
using Microsoft.Extensions.DependencyInjection;
using NCode.Identity.Jose.Algorithms;
using NCode.Identity.Jose.Algorithms.KeyManagement;
using NCode.Identity.Secrets;
using NCode.Identity.Secrets.Keys;
using NCode.Identity.Secrets.KeyTypes;
using Base64Url = NCode.Encoders.Base64Url;

namespace NCode.Identity.Jose.Tests.Algorithms.KeyManagement;

public class EcdhWithAesKeyManagementAlgorithmTests : BaseTests
{
    private Mock<IAesKeyWrap> MockAesKeyWrap { get; }

    private ServiceProvider ServiceProvider { get; }

    private ISecretKeyTypeCatalog SecretKeyTypeCatalog { get; }

    public EcdhWithAesKeyManagementAlgorithmTests()
    {
        MockAesKeyWrap = CreateStrictMock<IAesKeyWrap>();

        var serviceCollection = new ServiceCollection();
        serviceCollection.AddSecretsLibrary().AddJoseLibrary();
        ServiceProvider = serviceCollection.BuildServiceProvider();
        SecretKeyTypeCatalog = ServiceProvider.GetRequiredService<ISecretKeyTypeCatalog>();
    }

    protected override async ValueTask DisposeAsyncCore()
    {
        await ServiceProvider.DisposeAsync();
        await base.DisposeAsyncCore();
    }

    private EcdhWithAesKeyManagementAlgorithm CreateAlgorithm(
        int? kekSizeBits = null,
        IAesKeyWrap? aesKeyWrap = null
    ) =>
        new(
            SecretKeyTypeCatalog,
            [new EccEcdhKeyAgreement()],
            aesKeyWrap ?? MockAesKeyWrap.Object,
            "code",
            kekSizeBits ?? Random.Shared.Next()
        );

    [Fact]
    public void GetEncryptedContentKeySizeBytes_ReturnsExpected()
    {
        var kekSizeBits = Random.Shared.Next();
        var cekSizeBytes = Random.Shared.Next();
        var encryptedCekSizeBytes = Random.Shared.Next();

        MockAesKeyWrap
            .Setup(x => x.GetEncryptedContentKeySizeBytes(cekSizeBytes))
            .Returns(encryptedCekSizeBytes)
            .Verifiable();

        var algorithm = CreateAlgorithm();
        var result = algorithm.GetEncryptedContentKeySizeBytes(kekSizeBits, cekSizeBytes);
        Assert.Equal(encryptedCekSizeBytes, result);
    }

    public static IEnumerable<object[]> RoundTripTestData
    {
        get
        {
            var curves = new[]
            {
                ECCurve.NamedCurves.nistP256,
                ECCurve.NamedCurves.nistP384,
                ECCurve.NamedCurves.nistP521,
            };

            var keySizes = new[] { 16, 24, 32 };

            foreach (var curve in curves)
            foreach (var keySize in keySizes)
                yield return new object[] { curve, keySize };
        }
    }

    [Theory]
    [MemberData(nameof(RoundTripTestData))]
    public void RoundTrip_EncryptsAndDecrypts(ECCurve curve, int cekSizeBytes)
    {
        const string keyId = nameof(keyId);
        const string alg = nameof(alg);

        var metadata = new KeyMetadata { KeyId = keyId };
        using var key = ECDiffieHellman.Create(curve);
        var secretKey = SecretKeyFactory.CreateEcc(metadata, key);

        var cekSizeBits = cekSizeBytes << 3;
        var algorithm = CreateAlgorithm(cekSizeBits, DefaultAesKeyWrap.Singleton);

        var encryptedCekSizeBytes = algorithm.GetEncryptedContentKeySizeBytes(
            cekSizeBits,
            cekSizeBytes
        );

        Span<byte> apu = new byte[32];
        Span<byte> apv = new byte[32];

        RandomNumberGenerator.Fill(apu);
        RandomNumberGenerator.Fill(apv);

        Span<byte> cek = new byte[cekSizeBytes];
        Span<byte> decryptedCek = new byte[cekSizeBytes];
        var encryptedCekWriter = new ArrayBufferWriter<byte>(encryptedCekSizeBytes);

        var header = new Dictionary<string, object>
        {
            ["alg"] = alg,
            ["apu"] = Base64Url.Encode(apu),
            ["apv"] = Base64Url.Encode(apv),
        };

        algorithm.WrapKey(secretKey, header, cek, encryptedCekWriter);
        Assert.Equal(encryptedCekSizeBytes, encryptedCekWriter.WrittenCount);

        var controlAlgorithm = CreateControlAlgorithm(cekSizeBits);
        var controlResult = controlAlgorithm.Unwrap(
            encryptedCekWriter.WrittenSpan.ToArray(),
            key,
            cekSizeBits,
            header
        );
        Assert.Equal(controlResult, cek.ToArray());

        var headerForUnwrap = JsonSerializer.SerializeToElement(header);
        var unwrapResult = algorithm.TryUnwrapKey(
            secretKey,
            headerForUnwrap,
            encryptedCekWriter.WrittenSpan,
            decryptedCek,
            out var unwrapBytesWritten
        );
        Assert.True(unwrapResult);
        Assert.Equal(cekSizeBytes, unwrapBytesWritten);
        Assert.Equal(controlResult, decryptedCek.ToArray());
    }

    [Fact]
    public void WrapNewKey_WrapsARandomContentKeyThatTheRecipientUnwraps()
    {
        const int kekSizeBits = 128;
        const int cekSizeBytes = 32;

        using var key = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
        var secretKey = SecretKeyFactory.CreateEcc(new KeyMetadata { KeyId = "recipient" }, key);
        var algorithm = CreateAlgorithm(kekSizeBits, DefaultAesKeyWrap.Singleton);

        var header = new Dictionary<string, object> { ["alg"] = "ECDH-ES+A128KW" };
        var cek = new byte[cekSizeBytes];
        var encryptedCekWriter = new ArrayBufferWriter<byte>();
        algorithm.WrapNewKey(secretKey, header, cek, encryptedCekWriter);

        Assert.Contains(cek, value => value != 0);
        var controlResult = CreateControlAlgorithm(kekSizeBits)
            .Unwrap(encryptedCekWriter.WrittenSpan.ToArray(), key, cekSizeBytes << 3, header);
        Assert.Equal(cek, controlResult);

        var decryptedCek = new byte[cekSizeBytes];
        var result = algorithm.TryUnwrapKey(
            secretKey,
            JsonSerializer.SerializeToElement(header),
            encryptedCekWriter.WrittenSpan,
            decryptedCek,
            out var bytesWritten
        );
        Assert.True(result);
        Assert.Equal(cekSizeBytes, bytesWritten);
        Assert.Equal(cek, decryptedCek);
    }

    [Theory]
    [InlineData(128, 32)]
    [InlineData(192, 48)]
    [InlineData(256, 64)]
    public void TryUnwrapKey_WhenAnIndependentSenderWrapsAKey_UnwrapsIt(
        int kekSizeBits,
        int cekSizeBytes
    )
    {
        // The recipient never derives the content key in key agreement with key wrapping mode; it unwraps whatever key
        // the sender chose under the key agreed with the header's epk (RFC 7518 §4.6). A token is therefore decrypted
        // the same way whether its sender generated the content key at random or derived it otherwise.
        using var key = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
        var secretKey = SecretKeyFactory.CreateEcc(new KeyMetadata { KeyId = "recipient" }, key);
        var algorithm = CreateAlgorithm(kekSizeBits, DefaultAesKeyWrap.Singleton);

        var header = new Dictionary<string, object> { ["alg"] = $"ECDH-ES+A{kekSizeBits}KW" };
        var wrapped = CreateControlAlgorithm(kekSizeBits)
            .WrapNewKey(cekSizeBytes << 3, key, header);

        var contentKey = new byte[cekSizeBytes];
        var result = algorithm.TryUnwrapKey(
            secretKey,
            JsonSerializer.SerializeToElement(header),
            wrapped[1],
            contentKey,
            out var bytesWritten
        );

        Assert.True(result);
        Assert.Equal(cekSizeBytes, bytesWritten);
        Assert.Equal(wrapped[0], contentKey);
    }

    // The BCL-backed (ECDiffieHellman) control implementation; despite the name it is the cross-platform one jose-jwt
    // uses on every platform for managed keys, and the CNG-backed Win variant delegates straight to it for such keys.
    private static EcdhKeyManagementUnixWithAesKeyWrap CreateControlAlgorithm(int cekSizeBits)
    {
        var aesKw = new AesKeyWrapManagement(cekSizeBits);
        return new EcdhKeyManagementUnixWithAesKeyWrap(cekSizeBits, aesKw);
    }
}
