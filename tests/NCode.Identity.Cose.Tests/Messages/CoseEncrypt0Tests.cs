// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.Cose.Algorithms;
using NCode.Identity.Secrets;
using NCode.Identity.Secrets.Keys;

namespace NCode.Identity.Cose.Tests.Messages;

public class CoseEncrypt0Tests
{
    private const string Iv = "02D1F7E6F26C43D4868D87CE";

    private static byte[] Hex(string hex) => CoseTestKeys.Hex(hex);

    private static readonly byte[] Content = Hex(CoseTestKeys.Content);

    private static CoseDecryptionParameters Parameters(params SecretKey[] keys) =>
        new() { ValidAlgorithms = [CoseAlgorithmIdentifiers.A128Gcm], Keys = keys };

    private static string FailureCode(Action action) =>
        Assert.Throws<CoseException>(action).FailureCode;

    #region EncodeEncrypt0 Tests

    [Fact]
    public async Task EncodeEncrypt0_AesGcmExample_ProducesTheVectorBytesAndDecrypts()
    {
        var random = new QueueRandomSource();
        random.Enqueue(Iv);
        await using var host = new CoseTestHost(randomSource: random);
        var key = CoseTestKeys.CreateSymmetric(host.KeyFactory, CoseTestKeys.AesKey);

        var encoded = host.Serializer.EncodeEncrypt0(
            Content,
            new CoseCredentials(key, CoseAlgorithms.A128Gcm),
            new CoseEncodingOptions()
        );

        Assert.Equal(CoseVectors.V11Encrypt0, Convert.ToHexString(encoded));
        var decrypted = host.Serializer.Decrypt(encoded, Parameters(key));
        Assert.Equal(Content, decrypted.Content.ToArray());
        Assert.Equal(CoseMessageType.Encrypt0, decrypted.MessageType);
        Assert.Same(key, decrypted.Key);
    }

    [Fact]
    public async Task EncodeEncrypt0_KeyOfAnotherSize_Throws()
    {
        await using var host = new CoseTestHost();
        var key = CoseTestKeys.CreateSymmetric(host.KeyFactory, CoseTestKeys.HmacKey);

        Assert.Throws<ArgumentException>(() =>
            host.Serializer.EncodeEncrypt0(
                Content,
                new CoseCredentials(key, CoseAlgorithms.A128Gcm),
                new CoseEncodingOptions()
            )
        );
    }

    [Fact]
    public async Task EncodeEncrypt0_DetachedCiphertext_RoundTrips()
    {
        var random = new QueueRandomSource();
        random.Enqueue(Iv);
        random.Enqueue(Iv);
        await using var host = new CoseTestHost(randomSource: random);
        var key = CoseTestKeys.CreateSymmetric(host.KeyFactory, CoseTestKeys.AesKey, "k");
        var credentials = new CoseCredentials(key, CoseAlgorithms.A128Gcm);
        var full = host.Serializer.EncodeEncrypt0(
            Content,
            credentials,
            new CoseEncodingOptions { ExternalAad = Hex("AABB") }
        );
        var detached = host.Serializer.EncodeEncrypt0(
            Content,
            credentials,
            new CoseEncodingOptions { DetachContent = true, ExternalAad = Hex("AABB") }
        );

        Assert.True(host.Serializer.Inspect(detached, null).IsContentDetached);
        Assert.Equal(
            CoseFailureCodes.DetachedContent,
            FailureCode(() => host.Serializer.Decrypt(detached, Parameters(key)))
        );
        var decrypted = host.Serializer.Decrypt(
            detached,
            new CoseDecryptionParameters
            {
                ValidAlgorithms = [CoseAlgorithmIdentifiers.A128Gcm],
                Keys = [key],
                ExternalAad = Hex("AABB"),
                DetachedContent = CoseTestCbor.ReadContent(full),
            }
        );
        Assert.Equal(Content, decrypted.Content.ToArray());
    }

    #endregion

    #region Decrypt Tests

    [Fact]
    public async Task Decrypt_UnknownTag_ThrowsUnsupportedMessageType()
    {
        await using var host = new CoseTestHost();
        var key = CoseTestKeys.CreateSymmetric(host.KeyFactory, CoseTestKeys.AesKey);

        Assert.Equal(
            CoseFailureCodes.UnsupportedMessageType,
            FailureCode(() =>
                host.Serializer.Decrypt(Hex(CoseVectors.V22Encrypt0UnknownTag), Parameters(key))
            )
        );
    }

    [Fact]
    public async Task Decrypt_SignedMessage_ThrowsUnsupportedMessageType()
    {
        await using var host = new CoseTestHost();
        var key = CoseTestKeys.CreateSymmetric(host.KeyFactory, CoseTestKeys.AesKey);

        Assert.Equal(
            CoseFailureCodes.UnsupportedMessageType,
            FailureCode(() =>
                host.Serializer.Decrypt(Hex(CoseVectors.V2Sign1ExternalAad), Parameters(key))
            )
        );
    }

    [Fact]
    public async Task Decrypt_ChangedCiphertext_ThrowsDecryptionFailed()
    {
        await using var host = new CoseTestHost();
        var key = CoseTestKeys.CreateSymmetric(host.KeyFactory, CoseTestKeys.AesKey);
        var message = Hex(CoseVectors.V11Encrypt0);
        message[^20] ^= 0x01;

        Assert.Equal(
            CoseFailureCodes.DecryptionFailed,
            FailureCode(() => host.Serializer.Decrypt(message, Parameters(key)))
        );
    }

    [Fact]
    public async Task Decrypt_WithoutTheExternalAad_ThrowsDecryptionFailed()
    {
        await using var host = new CoseTestHost();
        var key = CoseTestKeys.CreateSymmetric(host.KeyFactory, CoseTestKeys.AesKey);
        var encoded = host.Serializer.EncodeEncrypt0(
            Content,
            new CoseCredentials(key, CoseAlgorithms.A128Gcm),
            new CoseEncodingOptions { ExternalAad = Hex("AABB") }
        );

        Assert.Equal(
            CoseFailureCodes.DecryptionFailed,
            FailureCode(() => host.Serializer.Decrypt(encoded, Parameters(key)))
        );
        Assert.Equal(
            Content,
            host.Serializer.Decrypt(
                    encoded,
                    new CoseDecryptionParameters
                    {
                        ValidAlgorithms = [CoseAlgorithmIdentifiers.A128Gcm],
                        Keys = [key],
                        ExternalAad = Hex("AABB"),
                    }
                )
                .Content.ToArray()
        );
    }

    [Fact]
    public async Task Decrypt_MissingInitializationVector_ThrowsInvalidHeader()
    {
        await using var host = new CoseTestHost();
        var key = CoseTestKeys.CreateSymmetric(host.KeyFactory, CoseTestKeys.AesKey);

        Assert.Equal(
            CoseFailureCodes.InvalidHeader,
            FailureCode(() =>
                host.Serializer.Decrypt(
                    Hex("D08343A10101A0" + "5824" + new string('0', 72)),
                    Parameters(key)
                )
            )
        );
    }

    [Fact]
    public async Task Decrypt_InitializationVectorOfTheWrongSize_ThrowsInvalidHeader()
    {
        await using var host = new CoseTestHost();
        var key = CoseTestKeys.CreateSymmetric(host.KeyFactory, CoseTestKeys.AesKey);

        Assert.Equal(
            CoseFailureCodes.InvalidHeader,
            FailureCode(() =>
                host.Serializer.Decrypt(
                    Hex("D08343A10101A10550" + new string('0', 32) + "5824" + new string('0', 72)),
                    Parameters(key)
                )
            )
        );
    }

    [Fact]
    public async Task Decrypt_CiphertextShorterThanTheTag_ThrowsDecryptionFailed()
    {
        await using var host = new CoseTestHost();
        var key = CoseTestKeys.CreateSymmetric(host.KeyFactory, CoseTestKeys.AesKey);

        Assert.Equal(
            CoseFailureCodes.DecryptionFailed,
            FailureCode(() =>
                host.Serializer.Decrypt(
                    Hex("D08343A10101A1054C" + Iv + "4F" + new string('0', 30)),
                    Parameters(key)
                )
            )
        );
    }

    [Fact]
    public async Task Decrypt_KeyWithTheSignatureUse_ThrowsKeyNotFound()
    {
        await using var host = new CoseTestHost();
        var key = host.KeyFactory.CreateSymmetric(
            new KeyMetadata { Use = SecretKeyUses.Signature },
            Hex(CoseTestKeys.AesKey)
        );

        Assert.Equal(
            CoseFailureCodes.KeyNotFound,
            FailureCode(() =>
                host.Serializer.Decrypt(Hex(CoseVectors.V11Encrypt0), Parameters(key))
            )
        );
    }

    [Fact]
    public async Task Decrypt_AlgorithmNotPermitted_Throws()
    {
        await using var host = new CoseTestHost();
        var key = CoseTestKeys.CreateSymmetric(host.KeyFactory, CoseTestKeys.AesKey);

        Assert.Equal(
            CoseFailureCodes.AlgorithmNotPermitted,
            FailureCode(() =>
                host.Serializer.Decrypt(
                    Hex(CoseVectors.V11Encrypt0),
                    new CoseDecryptionParameters
                    {
                        ValidAlgorithms = [CoseAlgorithmIdentifiers.A256Gcm],
                        Keys = [key],
                    }
                )
            )
        );
    }

    #endregion
}
