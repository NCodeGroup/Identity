// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;
using NCode.Identity.Cose.Algorithms;
using NCode.Identity.Secrets;
using NCode.Identity.Secrets.Keys;

namespace NCode.Identity.Cose.Tests.Messages;

public class CoseEncryptTests
{
    private const string Iv = "02D1F7E6F26C43D4868D87CE";

    private static byte[] Hex(string hex) => CoseTestKeys.Hex(hex);

    private static readonly byte[] Content = Hex(CoseTestKeys.Content);

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

    #region Working group examples

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

    #endregion

    #region Other recipients

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
}
