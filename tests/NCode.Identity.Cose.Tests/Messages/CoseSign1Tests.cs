// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;
using NCode.Identity.Cose.Algorithms;
using NCode.Identity.Cose.Cbor;
using NCode.Identity.Secrets;
using NCode.Identity.Secrets.KeyMaterial;
using NCode.Identity.Secrets.Keys;

namespace NCode.Identity.Cose.Tests.Messages;

public class CoseSign1Tests
{
    private static byte[] Hex(string hex) => CoseTestKeys.Hex(hex);

    private static byte[] Content { get; } = Hex(CoseTestKeys.Content);

    private static CoseVerificationParameters Parameters(
        int[] algorithms,
        params SecretKey[] keys
    ) => new() { ValidAlgorithms = algorithms, Keys = keys };

    private static SecretKey EcPublic(CoseTestHost host, string? keyId = "11") =>
        CoseTestKeys.CreateEcP256(
            host.KeyFactory,
            CoseTestKeys.EcP256X,
            CoseTestKeys.EcP256Y,
            keyId: keyId
        );

    private static SecretKey EcPrivate(CoseTestHost host, string? keyId = "11") =>
        CoseTestKeys.CreateEcP256(
            host.KeyFactory,
            CoseTestKeys.EcP256X,
            CoseTestKeys.EcP256Y,
            CoseTestKeys.EcP256D,
            keyId
        );

    private static string FailureCode(Action action) =>
        Assert.Throws<CoseException>(action).FailureCode;

    #region Verify Tests

    [Fact]
    public async Task Verify_WithExternalAad_ReturnsTheContent()
    {
        await using var host = new CoseTestHost();
        var parameters = new CoseVerificationParameters
        {
            ValidAlgorithms = [CoseAlgorithmIdentifiers.Es256],
            Keys = [EcPublic(host)],
            ExternalAad = Hex(CoseVectors.V2ExternalAad),
        };

        var verified = host.Serializer.Verify(Hex(CoseVectors.V2Sign1ExternalAad), parameters);

        Assert.Equal(Content, verified.Content.ToArray());
        Assert.Equal(CoseMessageType.Sign1, verified.MessageType);
        Assert.Equal(CoseAlgorithmIdentifiers.Es256, verified.Algorithm.Identifier);
    }

    [Fact]
    public async Task Verify_ClockSkewAtTheMaximum_SelectsTheKey()
    {
        await using var host = new CoseTestHost();
        var parameters = new CoseVerificationParameters
        {
            ValidAlgorithms = [CoseAlgorithmIdentifiers.Es256],
            Keys = [EcPublic(host)],
            ExternalAad = Hex(CoseVectors.V2ExternalAad),
            ClockSkew = TimeSpan.MaxValue,
        };

        var verified = host.Serializer.Verify(Hex(CoseVectors.V2Sign1ExternalAad), parameters);

        Assert.Equal(Content, verified.Content.ToArray());
    }

    [Fact]
    public async Task Verify_WithoutTheExternalAad_ThrowsInvalidSignature()
    {
        await using var host = new CoseTestHost();
        var parameters = Parameters([CoseAlgorithmIdentifiers.Es256], EcPublic(host));

        Assert.Equal(
            CoseFailureCodes.InvalidSignature,
            FailureCode(() =>
                host.Serializer.Verify(Hex(CoseVectors.V2Sign1ExternalAad), parameters)
            )
        );
    }

    [Fact]
    public async Task Verify_Untagged_RequiresTheMessageType()
    {
        await using var host = new CoseTestHost();
        var key = EcPublic(host);

        Assert.Equal(
            CoseFailureCodes.UntaggedMessage,
            FailureCode(() =>
                host.Serializer.Verify(
                    Hex(CoseVectors.V3Untagged),
                    Parameters([CoseAlgorithmIdentifiers.Es256], key)
                )
            )
        );

        var verified = host.Serializer.Verify(
            Hex(CoseVectors.V3Untagged),
            new CoseVerificationParameters
            {
                ValidAlgorithms = [CoseAlgorithmIdentifiers.Es256],
                Keys = [key],
                UntaggedMessageType = CoseMessageType.Sign1,
            }
        );
        Assert.Equal(Content, verified.Content.ToArray());
    }

    [Fact]
    public async Task Verify_SignedCwt_ReturnsTheClaimsSet()
    {
        await using var host = new CoseTestHost();
        var key = CoseTestKeys.CreateEcP256(host.KeyFactory, CoseTestKeys.CwtX, CoseTestKeys.CwtY);

        var verified = host.Serializer.Verify(
            Hex(CoseVectors.V14SignedCwt),
            Parameters([CoseAlgorithmIdentifiers.Es256], key)
        );

        Assert.Equal(Hex(CoseVectors.ClaimsA1), verified.Content.ToArray());
    }

    [Theory]
    [InlineData(CoseVectors.V18ChangedContent)]
    [InlineData(CoseVectors.V20AddedProtectedHeader)]
    public async Task Verify_TamperedMessage_ThrowsInvalidSignature(string hex)
    {
        await using var host = new CoseTestHost();
        var parameters = Parameters([CoseAlgorithmIdentifiers.Es256], EcPublic(host));

        Assert.Equal(
            CoseFailureCodes.InvalidSignature,
            FailureCode(() => host.Serializer.Verify(Hex(hex), parameters))
        );
    }

    [Fact]
    public async Task Verify_UnknownAlgorithm_DistinguishesNotPermittedFromUnsupported()
    {
        await using var host = new CoseTestHost();
        var key = EcPublic(host);

        Assert.Equal(
            CoseFailureCodes.AlgorithmNotPermitted,
            FailureCode(() =>
                host.Serializer.Verify(
                    Hex(CoseVectors.V19UnknownAlgorithm),
                    Parameters([CoseAlgorithmIdentifiers.Es256], key)
                )
            )
        );
        Assert.Equal(
            CoseFailureCodes.UnsupportedAlgorithm,
            FailureCode(() =>
                host.Serializer.Verify(
                    Hex(CoseVectors.V19UnknownAlgorithm),
                    Parameters([CoseAlgorithmIdentifiers.Es256, -999], key)
                )
            )
        );
    }

    [Fact]
    public async Task Verify_AlgorithmOnlyUnprotected_ThrowsUnprotectedAlgorithm()
    {
        await using var host = new CoseTestHost();
        var parameters = Parameters([CoseAlgorithmIdentifiers.Es256], EcPublic(host));

        Assert.Equal(
            CoseFailureCodes.UnprotectedAlgorithm,
            FailureCode(() =>
                host.Serializer.Verify(Hex(CoseVectors.V1UnprotectedAlgorithm), parameters)
            )
        );
    }

    [Fact]
    public async Task Verify_UnknownTag_ThrowsUnsupportedMessageType()
    {
        await using var host = new CoseTestHost();
        var parameters = Parameters([CoseAlgorithmIdentifiers.Es256], EcPublic(host));

        Assert.Equal(
            CoseFailureCodes.UnsupportedMessageType,
            FailureCode(() => host.Serializer.Verify(Hex(CoseVectors.V17UnknownTag), parameters))
        );
    }

    [Fact]
    public async Task Verify_EncryptMessage_ThrowsUnsupportedMessageType()
    {
        await using var host = new CoseTestHost();
        var parameters = Parameters([CoseAlgorithmIdentifiers.Es256], EcPublic(host));

        Assert.Equal(
            CoseFailureCodes.UnsupportedMessageType,
            FailureCode(() => host.Serializer.Verify(Hex(CoseVectors.V11Encrypt0), parameters))
        );
    }

    [Fact]
    public async Task Verify_BinaryKeyId_SelectsTheKeyReadFromACoseKey()
    {
        await using var host = new CoseTestHost();

        var encoded = await host.Serializer.EncodeSign1Async(
            Content,
            new CoseCredentials(EcPrivate(host, "h'a1b2c3d4'"), CoseAlgorithms.Es256),
            new CoseEncodingOptions(),
            CancellationToken.None
        );

        var info = host.Serializer.Inspect(encoded, null);
        Assert.True(info.UnprotectedHeaders[CoseHeaderLabels.KeyId].TryGetByteString(out var kid));
        Assert.Equal(Hex("A1B2C3D4"), kid.ToArray());

        var coseKey = CborMapWriter.EncodeMap(
            new Dictionary<CoseLabel, CoseValue>
            {
                [1] = CoseValue.FromInt64(2),
                [2] = CoseValue.FromByteString(Hex("A1B2C3D4")),
                [-1] = CoseValue.FromInt64(1),
                [-2] = CoseValue.FromByteString(Hex(CoseTestKeys.EcP256X)),
                [-3] = CoseValue.FromByteString(Hex(CoseTestKeys.EcP256Y)),
            }
        );
        var converted = host.KeyConverter.ReadPublicKey(coseKey);
        var verified = host.Serializer.Verify(
            encoded,
            Parameters([CoseAlgorithmIdentifiers.Es256], converted, EcPublic(host, "other"))
        );

        Assert.Same(converted, verified.Key);
    }

    [Fact]
    public async Task Verify_ContentAndDetachedContent_ThrowsDetachedContent()
    {
        await using var host = new CoseTestHost();
        var encoded = await host.Serializer.EncodeSign1Async(
            Content,
            new CoseCredentials(EcPrivate(host), CoseAlgorithms.Es256),
            new CoseEncodingOptions(),
            CancellationToken.None
        );

        Assert.Equal(
            CoseFailureCodes.DetachedContent,
            FailureCode(() =>
                host.Serializer.Verify(
                    encoded,
                    new CoseVerificationParameters
                    {
                        ValidAlgorithms = [CoseAlgorithmIdentifiers.Es256],
                        Keys = [EcPublic(host)],
                        DetachedContent = Content,
                    }
                )
            )
        );
    }

    [Fact]
    public async Task Verify_CriticalHeaderNotUnderstood_Throws()
    {
        await using var host = new CoseTestHost();
        var options = new CoseEncodingOptions
        {
            ProtectedHeaders = new Dictionary<CoseLabel, CoseValue>
            {
                [CoseHeaderLabels.Critical] = CoseValue.FromLabels([100]),
                [100] = CoseValue.FromInt64(1),
            },
        };
        var encoded = await host.Serializer.EncodeSign1Async(
            Content,
            new CoseCredentials(EcPrivate(host), CoseAlgorithms.Es256),
            options,
            CancellationToken.None
        );
        var key = EcPublic(host);

        Assert.Equal(
            CoseFailureCodes.CriticalHeaderNotUnderstood,
            FailureCode(() =>
                host.Serializer.Verify(encoded, Parameters([CoseAlgorithmIdentifiers.Es256], key))
            )
        );

        var verified = host.Serializer.Verify(
            encoded,
            new CoseVerificationParameters
            {
                ValidAlgorithms = [CoseAlgorithmIdentifiers.Es256],
                Keys = [key],
                UnderstoodCriticalHeaders = [100],
            }
        );
        Assert.Equal(Content, verified.Content.ToArray());
    }

    [Fact]
    public async Task Verify_KeysThatMustNotBeTried_ThrowKeyNotFound()
    {
        await using var host = new CoseTestHost();
        var encoded = await host.Serializer.EncodeSign1Async(
            Content,
            new CoseCredentials(EcPrivate(host), CoseAlgorithms.Es256),
            new CoseEncodingOptions(),
            CancellationToken.None
        );
        var encryptionOnly = CoseTestKeys.CreateEcP256(
            host.KeyFactory,
            CoseTestKeys.EcP256X,
            CoseTestKeys.EcP256Y,
            new KeyMetadata { KeyId = "11", Use = SecretKeyUses.Encryption }
        );
        var expired = CoseTestKeys.CreateEcP256(
            host.KeyFactory,
            CoseTestKeys.EcP256X,
            CoseTestKeys.EcP256Y,
            new KeyMetadata
            {
                KeyId = "11",
                ExpiresWhen = CoseTestHost.StartTime - TimeSpan.FromMinutes(6),
            }
        );
        var otherKeyId = EcPublic(host, "other");

        Assert.Equal(
            CoseFailureCodes.KeyNotFound,
            FailureCode(() =>
                host.Serializer.Verify(
                    encoded,
                    Parameters(
                        [CoseAlgorithmIdentifiers.Es256],
                        encryptionOnly,
                        expired,
                        otherKeyId
                    )
                )
            )
        );
    }

    [Fact]
    public async Task Verify_KeyWithoutKeyId_IsTriedAfterTheMatchingKeys()
    {
        await using var host = new CoseTestHost();
        var encoded = await host.Serializer.EncodeSign1Async(
            Content,
            new CoseCredentials(EcPrivate(host), CoseAlgorithms.Es256),
            new CoseEncodingOptions(),
            CancellationToken.None
        );
        var unidentified = EcPublic(host, keyId: null);
        var matching = EcPublic(host);

        var verified = host.Serializer.Verify(
            encoded,
            Parameters([CoseAlgorithmIdentifiers.Es256], unidentified, matching)
        );

        Assert.Same(matching, verified.Key);
    }

    [Fact]
    public async Task Verify_KeyWithinTheClockSkewOfExpiry_IsTried()
    {
        await using var host = new CoseTestHost();
        var encoded = await host.Serializer.EncodeSign1Async(
            Content,
            new CoseCredentials(EcPrivate(host), CoseAlgorithms.Es256),
            new CoseEncodingOptions(),
            CancellationToken.None
        );
        var recentlyExpired = CoseTestKeys.CreateEcP256(
            host.KeyFactory,
            CoseTestKeys.EcP256X,
            CoseTestKeys.EcP256Y,
            new KeyMetadata
            {
                KeyId = "11",
                ExpiresWhen = CoseTestHost.StartTime - TimeSpan.FromMinutes(1),
            }
        );

        var verified = host.Serializer.Verify(
            encoded,
            Parameters([CoseAlgorithmIdentifiers.Es256], recentlyExpired)
        );

        Assert.Same(recentlyExpired, verified.Key);
    }

    [Fact]
    public async Task Verify_NoAlgorithms_Throws()
    {
        await using var host = new CoseTestHost();

        Assert.Throws<ArgumentException>(() =>
            host.Serializer.Verify(Hex(CoseVectors.V2Sign1ExternalAad), Parameters([]))
        );
    }

    [Fact]
    public async Task Verify_NegativeClockSkew_Throws()
    {
        await using var host = new CoseTestHost();
        var parameters = new CoseVerificationParameters
        {
            ValidAlgorithms = [CoseAlgorithmIdentifiers.Es256],
            Keys = [],
            ClockSkew = TimeSpan.FromSeconds(-1),
        };

        Assert.Throws<ArgumentException>(() =>
            host.Serializer.Verify(Hex(CoseVectors.V2Sign1ExternalAad), parameters)
        );
    }

    #endregion

    #region EncodeSign1 Tests

    [Fact]
    public async Task EncodeSign1_EdDsaVector_ProducesTheVectorBytes()
    {
        await using var host = new CoseTestHost();
        var key = CoseTestKeys.CreateEd25519(host.SecretKeyTypes, CoseTestKeys.Ed25519Seed, "11");
        var options = new CoseEncodingOptions
        {
            ProtectedHeaders = new Dictionary<CoseLabel, CoseValue>
            {
                [CoseHeaderLabels.ContentType] = CoseValue.FromInt64(0),
            },
        };

        var encoded = await host.Serializer.EncodeSign1Async(
            Content,
            new CoseCredentials(key, CoseAlgorithms.EdDsa),
            options,
            CancellationToken.None
        );

        Assert.Equal(CoseVectors.V5EdDsa, Convert.ToHexString(encoded));
        var verified = host.Serializer.Verify(
            encoded,
            Parameters([CoseAlgorithmIdentifiers.EdDsa], key)
        );
        Assert.Equal(Content, verified.Content.ToArray());
    }

    [Fact]
    public async Task EncodeSign1_Ed25519Algorithm_RoundTrips()
    {
        await using var host = new CoseTestHost();
        var key = CoseTestKeys.CreateEd25519(host.SecretKeyTypes, CoseTestKeys.Ed25519Seed);

        var encoded = await host.Serializer.EncodeSign1Async(
            Content,
            new CoseCredentials(key, CoseAlgorithms.Ed25519),
            new CoseEncodingOptions(),
            CancellationToken.None
        );

        var verified = host.Serializer.Verify(
            encoded,
            Parameters([CoseAlgorithmIdentifiers.Ed25519], key)
        );
        Assert.Equal(Content, verified.Content.ToArray());
    }

    [Fact]
    public async Task EncodeSign1_Es256_RoundTripsAndWritesTheKeyId()
    {
        await using var host = new CoseTestHost();

        var encoded = await host.Serializer.EncodeSign1Async(
            Content,
            new CoseCredentials(EcPrivate(host), CoseAlgorithms.Es256),
            new CoseEncodingOptions(),
            CancellationToken.None
        );

        var info = host.Serializer.Inspect(encoded, null);
        Assert.True(info.UnprotectedHeaders[CoseHeaderLabels.KeyId].TryGetByteString(out var kid));
        Assert.Equal("11"u8.ToArray(), kid.ToArray());
        var verified = host.Serializer.Verify(
            encoded,
            Parameters([CoseAlgorithmIdentifiers.Es256], EcPublic(host))
        );
        Assert.Equal(Content, verified.Content.ToArray());
    }

    [Fact]
    public async Task EncodeSign1_Ps256_RoundTrips()
    {
        await using var host = new CoseTestHost();
        var rsa = RSA.Create(2048);
        var key = host.KeyFactory.CreateRsa(new KeyMetadata { KeyId = "rsa" }, rsa);

        var encoded = await host.Serializer.EncodeSign1Async(
            Content,
            new CoseCredentials(key, CoseAlgorithms.Ps256),
            new CoseEncodingOptions(),
            CancellationToken.None
        );

        var verified = host.Serializer.Verify(
            encoded,
            Parameters([CoseAlgorithmIdentifiers.Ps256], key)
        );
        Assert.Equal(Content, verified.Content.ToArray());
    }

    [MlDsaFact]
    public async Task EncodeSign1_MlDsa44_RoundTrips()
    {
        await using var host = new CoseTestHost();
        Assert.True(host.SecretKeyTypes.TryGet(SecretKeyTypes.Akp, out var descriptor));
        var generated = descriptor.GenerateKeyMaterial(
            new SecretKeyGenerationParameters { KeySizeBits = 1312 * 8, Curve = "ML-DSA-44" },
            bytes => Convert.ToBase64String(bytes)
        );
        var key = descriptor.DeserializeKeyMaterial(
            new KeyMetadata(),
            SecretEncodings.Pkcs8,
            Convert.FromBase64String(generated.ProtectedKeyMaterial)
        );

        var encoded = await host.Serializer.EncodeSign1Async(
            Content,
            new CoseCredentials(key, CoseAlgorithms.MlDsa44),
            new CoseEncodingOptions(),
            CancellationToken.None
        );

        var verified = host.Serializer.Verify(
            encoded,
            Parameters([CoseAlgorithmIdentifiers.MlDsa44], key)
        );
        Assert.Equal(Content, verified.Content.ToArray());
    }

    [Fact]
    public async Task EncodeSign1_DetachedContent_RequiresTheContentToVerify()
    {
        await using var host = new CoseTestHost();
        var encoded = await host.Serializer.EncodeSign1Async(
            Content,
            new CoseCredentials(EcPrivate(host), CoseAlgorithms.Es256),
            new CoseEncodingOptions { DetachContent = true },
            CancellationToken.None
        );
        var key = EcPublic(host);

        Assert.True(host.Serializer.Inspect(encoded, null).IsContentDetached);
        Assert.Equal(
            CoseFailureCodes.DetachedContent,
            FailureCode(() =>
                host.Serializer.Verify(encoded, Parameters([CoseAlgorithmIdentifiers.Es256], key))
            )
        );

        var verified = host.Serializer.Verify(
            encoded,
            new CoseVerificationParameters
            {
                ValidAlgorithms = [CoseAlgorithmIdentifiers.Es256],
                Keys = [key],
                DetachedContent = Content,
            }
        );
        Assert.Equal(Content, verified.Content.ToArray());
    }

    [Fact]
    public async Task EncodeSign1_OmitTag_WritesAnUntaggedMessage()
    {
        await using var host = new CoseTestHost();

        var encoded = await host.Serializer.EncodeSign1Async(
            Content,
            new CoseCredentials(EcPrivate(host), CoseAlgorithms.Es256),
            new CoseEncodingOptions { OmitTag = true },
            CancellationToken.None
        );

        Assert.False(host.Serializer.Inspect(encoded, CoseMessageType.Sign1).IsTagged);
        Assert.Equal(0x84, encoded[0]);
    }

    [Fact]
    public async Task EncodeSign1_MacAlgorithm_Throws()
    {
        await using var host = new CoseTestHost();
        var key = CoseTestKeys.CreateSymmetric(host.KeyFactory, CoseTestKeys.HmacKey);

        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await host.Serializer.EncodeSign1Async(
                Content,
                new CoseCredentials(key, CoseAlgorithms.HmacSha256),
                new CoseEncodingOptions(),
                CancellationToken.None
            )
        );
    }

    [Fact]
    public async Task EncodeSign1_KeyOfTheWrongType_Throws()
    {
        await using var host = new CoseTestHost();
        var key = CoseTestKeys.CreateSymmetric(host.KeyFactory, CoseTestKeys.HmacKey);

        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await host.Serializer.EncodeSign1Async(
                Content,
                new CoseCredentials(key, CoseAlgorithms.Es256),
                new CoseEncodingOptions(),
                CancellationToken.None
            )
        );
    }

    [Fact]
    public async Task EncodeSign1_CallerAlgorithmHeader_Throws()
    {
        await using var host = new CoseTestHost();
        var options = new CoseEncodingOptions
        {
            ProtectedHeaders = new Dictionary<CoseLabel, CoseValue>
            {
                [CoseHeaderLabels.Algorithm] = CoseValue.FromInt64(-7),
            },
        };

        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await host.Serializer.EncodeSign1Async(
                Content,
                new CoseCredentials(EcPrivate(host), CoseAlgorithms.Es256),
                options,
                CancellationToken.None
            )
        );
    }

    #endregion
}
