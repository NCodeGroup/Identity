// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers;
using System.Security.Cryptography;
using NCode.Identity.Cose.Algorithms;
using NCode.Identity.Cose.Cbor;
using NCode.Identity.Cose.Messages;
using NCode.Identity.Jose.Algorithms;
using NCode.Identity.Secrets;
using NCode.Identity.Secrets.Keys;

namespace NCode.Identity.Cose.Tests.Messages;

public class CoseEncryptTests
{
    private const string Iv = "02D1F7E6F26C43D4868D87CE";

    private static byte[] Hex(string hex) => CoseTestKeys.Hex(hex);

    private static byte[] Content { get; } = Hex(CoseTestKeys.Content);

    private static CoseDecryptionParameters Parameters(
        int[] keyDistribution,
        params SecretKey[] keys
    ) =>
        new()
        {
            ValidAlgorithms = [CoseAlgorithmIdentifiers.A128Gcm],
            ValidKeyDistributionAlgorithms = keyDistribution,
            Keys = keys,
        };

    private static string FailureCode(Action action) =>
        Assert.Throws<CoseException>(action).FailureCode;

    private static (byte[] Encoded, SecretKey Rsa) EncodeTwoRecipients(CoseTestHost host)
    {
        var aes = CoseTestKeys.CreateSymmetric(host.KeyFactory, CoseTestKeys.AesKey, "aes");
        var rsa = host.KeyFactory.CreateRsa(new KeyMetadata { KeyId = "rsa" }, RSA.Create(2048));
        var encoded = host.Serializer.EncodeEncrypt(
            Content,
            new CoseRecipients(
                CoseAlgorithms.A128Gcm,
                [
                    new CoseRecipient(new CoseCredentials(aes, CoseAlgorithms.A128Kw)),
                    new CoseRecipient(new CoseCredentials(rsa, CoseAlgorithms.RsaesOaepSha256)),
                ]
            ),
            new CoseEncodingOptions()
        );
        return (encoded, rsa);
    }

    private static byte[] BodyWith(CoseLayerEntry recipient) =>
        CoseMessageWriter.Encrypt(
            CoseTags.Encrypt,
            CborMapWriter.EncodeProtected(
                new Dictionary<CoseLabel, CoseValue> { [1] = CoseValue.FromInt64(1) }
            ),
            new Dictionary<CoseLabel, CoseValue> { [5] = CoseValue.FromByteString(Hex(Iv)) },
            new byte[32],
            [recipient]
        );

    private static byte[] WrapA256Kw(CoseTestHost host, SecretKey kek, byte[] contentKey)
    {
        Assert.True(host.Algorithms.TryGet(CoseAlgorithmIdentifiers.A256Kw, out var binding));
        var writer = new ArrayBufferWriter<byte>();
        ((KeyManagementAlgorithm)binding.JoseAlgorithm).WrapKey(
            kek,
            new Dictionary<string, object>(),
            contentKey,
            writer
        );
        return writer.WrittenSpan.ToArray();
    }

    #region EncodeEncrypt Tests

    [Fact]
    public async Task EncodeEncrypt_DirectRecipientKeyOfAnotherSize_Throws()
    {
        await using var host = new CoseTestHost();
        var key = CoseTestKeys.CreateSymmetric(host.KeyFactory, CoseTestKeys.HmacKey);

        Assert.Throws<ArgumentException>(() =>
            host.Serializer.EncodeEncrypt(
                Content,
                new CoseRecipients(
                    CoseAlgorithms.A128Gcm,
                    [new CoseRecipient(new CoseCredentials(key, CoseAlgorithms.Direct))]
                ),
                new CoseEncodingOptions()
            )
        );
    }

    [Fact]
    public async Task EncodeEncrypt_DirectRecipient_ProducesTheVectorBytesAndDecrypts()
    {
        var random = new QueueRandomSource();
        random.Enqueue(Iv);
        await using var host = new CoseTestHost(randomSource: random);
        var key = CoseTestKeys.CreateSymmetric(host.KeyFactory, CoseTestKeys.AesKey, "our-secret");

        var encoded = host.Serializer.EncodeEncrypt(
            Content,
            new CoseRecipients(
                CoseAlgorithms.A128Gcm,
                [new CoseRecipient(new CoseCredentials(key, CoseAlgorithms.Direct))]
            ),
            new CoseEncodingOptions()
        );

        Assert.Equal(CoseVectors.V12Encrypt, Convert.ToHexString(encoded));
        var decrypted = host.Serializer.Decrypt(
            encoded,
            Parameters([CoseAlgorithmIdentifiers.Direct], key)
        );
        Assert.Equal(Content, decrypted.Content.ToArray());
        Assert.NotNull(decrypted.Recipient);
        Assert.Same(key, decrypted.Key);
    }

    [Fact]
    public async Task EncodeEncrypt_KeyWrapRecipient_ProducesTheVectorBytesAndDecrypts()
    {
        var random = new QueueRandomSource();
        random.Enqueue("7A1B4CF78F4B8C6E9AB68198C43D22F3");
        random.Enqueue("DDDC08972DF9BE62855291A1");
        await using var host = new CoseTestHost(randomSource: random);
        var key = CoseTestKeys.CreateSymmetric(host.KeyFactory, CoseTestKeys.AesKey, "our-secret");

        var encoded = host.Serializer.EncodeEncrypt(
            Content,
            new CoseRecipients(
                CoseAlgorithms.A128Gcm,
                [new CoseRecipient(new CoseCredentials(key, CoseAlgorithms.A128Kw))]
            ),
            new CoseEncodingOptions()
        );

        Assert.Equal(CoseVectors.V13EncryptKeyWrap, Convert.ToHexString(encoded));
        var decrypted = host.Serializer.Decrypt(
            encoded,
            Parameters([CoseAlgorithmIdentifiers.A128Kw], key)
        );
        Assert.Equal(Content, decrypted.Content.ToArray());
    }

    [Fact]
    public async Task EncodeEncrypt_RsaOaepRecipient_RoundTrips()
    {
        await using var host = new CoseTestHost();
        var rsa = host.KeyFactory.CreateRsa(new KeyMetadata { KeyId = "rsa" }, RSA.Create(2048));

        var encoded = host.Serializer.EncodeEncrypt(
            Content,
            new CoseRecipients(
                CoseAlgorithms.A128Gcm,
                [new CoseRecipient(new CoseCredentials(rsa, CoseAlgorithms.RsaesOaepSha256))]
            ),
            new CoseEncodingOptions()
        );

        var decrypted = host.Serializer.Decrypt(
            encoded,
            Parameters([CoseAlgorithmIdentifiers.RsaesOaepSha256], rsa)
        );
        Assert.Equal(Content, decrypted.Content.ToArray());
    }

    [Fact]
    public async Task EncodeEncrypt_MoreRecipientsThanTheLimit_Throws()
    {
        await using var host = new CoseTestHost(options => options.MaxRecipients = 1);

        Assert.Throws<ArgumentException>(() => EncodeTwoRecipients(host));
    }

    [Fact]
    public async Task EncodeEncrypt_DetachedCiphertext_RoundTrips()
    {
        var random = new QueueRandomSource();
        random.Enqueue(Iv);
        random.Enqueue(Iv);
        await using var host = new CoseTestHost(randomSource: random);
        var key = CoseTestKeys.CreateSymmetric(host.KeyFactory, CoseTestKeys.AesKey, "our-secret");
        var recipients = new CoseRecipients(
            CoseAlgorithms.A128Gcm,
            [new CoseRecipient(new CoseCredentials(key, CoseAlgorithms.Direct))]
        );
        var full = host.Serializer.EncodeEncrypt(Content, recipients, new CoseEncodingOptions());
        var detached = host.Serializer.EncodeEncrypt(
            Content,
            recipients,
            new CoseEncodingOptions { DetachContent = true }
        );

        Assert.True(host.Serializer.Inspect(detached, null).IsContentDetached);
        Assert.Equal(
            CoseFailureCodes.DetachedContent,
            FailureCode(() =>
                host.Serializer.Decrypt(
                    detached,
                    Parameters([CoseAlgorithmIdentifiers.Direct], key)
                )
            )
        );
        var decrypted = host.Serializer.Decrypt(
            detached,
            new CoseDecryptionParameters
            {
                ValidAlgorithms = [CoseAlgorithmIdentifiers.A128Gcm],
                ValidKeyDistributionAlgorithms = [CoseAlgorithmIdentifiers.Direct],
                Keys = [key],
                DetachedContent = CoseTestCbor.ReadContent(full),
            }
        );
        Assert.Equal(Content, decrypted.Content.ToArray());
    }

    #endregion

    #region Decrypt Tests

    [Fact]
    public async Task Decrypt_TwoRecipients_DecryptsThroughTheSecond()
    {
        await using var host = new CoseTestHost();
        var aes = CoseTestKeys.CreateSymmetric(host.KeyFactory, CoseTestKeys.AesKey, "aes");
        var rsa = host.KeyFactory.CreateRsa(new KeyMetadata { KeyId = "rsa" }, RSA.Create(2048));
        var encoded = host.Serializer.EncodeEncrypt(
            Content,
            new CoseRecipients(
                CoseAlgorithms.A128Gcm,
                [
                    new CoseRecipient(new CoseCredentials(aes, CoseAlgorithms.A128Kw)),
                    new CoseRecipient(new CoseCredentials(rsa, CoseAlgorithms.RsaesOaepSha256)),
                ]
            ),
            new CoseEncodingOptions()
        );

        var decrypted = host.Serializer.Decrypt(
            encoded,
            Parameters(
                [CoseAlgorithmIdentifiers.A128Kw, CoseAlgorithmIdentifiers.RsaesOaepSha256],
                rsa
            )
        );

        Assert.Equal(Content, decrypted.Content.ToArray());
        Assert.Same(rsa, decrypted.Key);
    }

    [Fact]
    public async Task Decrypt_MoreRecipientsThanTheLimit_ThrowsLimitExceeded()
    {
        await using var encoder = new CoseTestHost();
        var (encoded, rsa) = EncodeTwoRecipients(encoder);
        await using var host = new CoseTestHost(options => options.MaxRecipients = 1);

        Assert.Equal(
            CoseFailureCodes.LimitExceeded,
            FailureCode(() =>
                host.Serializer.Decrypt(
                    encoded,
                    Parameters(
                        [CoseAlgorithmIdentifiers.A128Kw, CoseAlgorithmIdentifiers.RsaesOaepSha256],
                        rsa
                    )
                )
            )
        );
    }

    [Fact]
    public async Task Decrypt_RecipientsAtTheLimit_Decrypts()
    {
        await using var encoder = new CoseTestHost();
        var (encoded, rsa) = EncodeTwoRecipients(encoder);
        await using var host = new CoseTestHost(options => options.MaxRecipients = 2);

        var decrypted = host.Serializer.Decrypt(
            encoded,
            Parameters(
                [CoseAlgorithmIdentifiers.A128Kw, CoseAlgorithmIdentifiers.RsaesOaepSha256],
                rsa
            )
        );

        Assert.Equal(Content, decrypted.Content.ToArray());
    }

    [Fact]
    public async Task Decrypt_RecipientKeyForSignatures_ThrowsKeyNotFound()
    {
        await using var host = new CoseTestHost();
        var key = host.KeyFactory.CreateSymmetric(
            new KeyMetadata { KeyId = "our-secret", Use = SecretKeyUses.Signature },
            Hex(CoseTestKeys.AesKey)
        );

        Assert.Equal(
            CoseFailureCodes.KeyNotFound,
            FailureCode(() =>
                host.Serializer.Decrypt(
                    Hex(CoseVectors.V12Encrypt),
                    Parameters([CoseAlgorithmIdentifiers.Direct], key)
                )
            )
        );
    }

    [Fact]
    public async Task Decrypt_WrongRecipientKey_ThrowsDecryptionFailed()
    {
        await using var host = new CoseTestHost();
        var key = CoseTestKeys.CreateSymmetric(
            host.KeyFactory,
            "00112233445566778899AABBCCDDEEFF",
            "our-secret"
        );

        Assert.Equal(
            CoseFailureCodes.DecryptionFailed,
            FailureCode(() =>
                host.Serializer.Decrypt(
                    Hex(CoseVectors.V12Encrypt),
                    Parameters([CoseAlgorithmIdentifiers.Direct], key)
                )
            )
        );
    }

    [Fact]
    public async Task Decrypt_RecipientAlgorithmNotPermitted_ThrowsAlgorithmNotPermitted()
    {
        await using var host = new CoseTestHost();
        var key = CoseTestKeys.CreateSymmetric(host.KeyFactory, CoseTestKeys.AesKey, "aes");
        var encoded = host.Serializer.EncodeEncrypt(
            Content,
            new CoseRecipients(
                CoseAlgorithms.A128Gcm,
                [new CoseRecipient(new CoseCredentials(key, CoseAlgorithms.A128Kw))]
            ),
            new CoseEncodingOptions()
        );

        Assert.Equal(
            CoseFailureCodes.AlgorithmNotPermitted,
            FailureCode(() =>
                host.Serializer.Decrypt(encoded, Parameters([CoseAlgorithmIdentifiers.Direct], key))
            )
        );
    }

    [Fact]
    public async Task Decrypt_RecipientAlgorithmOfAnotherKind_ThrowsUnsupportedAlgorithm()
    {
        await using var host = new CoseTestHost();
        var key = CoseTestKeys.CreateSymmetric(host.KeyFactory, CoseTestKeys.AesKey);
        var message = BodyWith(
            new CoseLayerEntry(
                [],
                new Dictionary<CoseLabel, CoseValue> { [1] = CoseValue.FromInt64(1) },
                []
            )
        );

        Assert.Equal(
            CoseFailureCodes.UnsupportedAlgorithm,
            FailureCode(() => host.Serializer.Decrypt(message, Parameters([1], key)))
        );
    }

    [Fact]
    public async Task Decrypt_DirectRecipientWithCiphertext_ThrowsInvalidMessage()
    {
        await using var host = new CoseTestHost();
        var key = CoseTestKeys.CreateSymmetric(host.KeyFactory, CoseTestKeys.AesKey);
        var message = BodyWith(
            new CoseLayerEntry(
                [],
                new Dictionary<CoseLabel, CoseValue> { [1] = CoseValue.FromInt64(-6) },
                [0x00]
            )
        );

        Assert.Equal(
            CoseFailureCodes.InvalidMessage,
            FailureCode(() =>
                host.Serializer.Decrypt(message, Parameters([CoseAlgorithmIdentifiers.Direct], key))
            )
        );
    }

    [Fact]
    public async Task Decrypt_UnwrappedKeyOfAnotherSize_ThrowsDecryptionFailed()
    {
        await using var host = new CoseTestHost();
        var kek = CoseTestKeys.CreateSymmetric(host.KeyFactory, CoseTestKeys.HmacKey, "kw");
        var message = BodyWith(
            new CoseLayerEntry(
                [],
                new Dictionary<CoseLabel, CoseValue>
                {
                    [1] = CoseValue.FromInt64(-5),
                    [4] = CoseValue.FromByteString("kw"u8),
                },
                WrapA256Kw(host, kek, Hex(CoseTestKeys.HmacCwtKey))
            )
        );

        Assert.Equal(
            CoseFailureCodes.DecryptionFailed,
            FailureCode(() =>
                host.Serializer.Decrypt(message, Parameters([CoseAlgorithmIdentifiers.A256Kw], kek))
            )
        );
    }

    #endregion
}
