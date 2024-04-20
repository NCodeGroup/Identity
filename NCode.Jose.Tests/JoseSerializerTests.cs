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
using System.Text;
using System.Text.Json;
using Jose;
using Jose.keys;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NCode.Jose.Algorithms;
using NCode.Jose.Credentials;
using NCode.Jose.DataProtection;
using NCode.Jose.Extensions;
using NCode.Jose.Infrastructure;
using NCode.Jose.SecretKeys;

namespace NCode.Jose.Tests;

public class JoseSerializerTests : BaseTests
{
    private DefaultSecretKeyFactory SecretKeyFactory { get; } = new(NoneSecureDataProtector.Singleton);
    private JsonSerializerOptions JsonSerializerOptions { get; } = new(JsonSerializerDefaults.Web);
    private ServiceProvider ServiceProvider { get; }
    private JoseSerializerOptions JoseSerializerOptions { get; } = new();
    private IAlgorithmProvider AlgorithmProvider { get; }
    private Mock<JoseSerializer> MockJoseSerializer { get; }
    private JoseSerializer JoseSerializer { get; }

    public JoseSerializerTests()
    {
        var services = new ServiceCollection();
        ConfigureServices(services);
        ServiceProvider = services.BuildServiceProvider();

        AlgorithmProvider = ServiceProvider.GetRequiredService<IAlgorithmProvider>();
        MockJoseSerializer = CreatePartialMock<JoseSerializer>(Options.Create(JoseSerializerOptions), AlgorithmProvider);
        JoseSerializer = MockJoseSerializer.Object;
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        services.AddJose();
    }

    protected override async ValueTask DisposeAsyncCore()
    {
        await ServiceProvider.DisposeAsync();
    }

    private (object controlKey, SecretKey secretKey) CreateRandomRsaKey(string keyId)
    {
        var metadata = new KeyMetadata { KeyId = keyId };
        var nativeKey = RSA.Create();
        var secretKey = SecretKeyFactory.CreateRsa(metadata, nativeKey);
        return (nativeKey, secretKey);
    }

    private (object controlKey, SecretKey secretKey) CreateRandomEccKey(string keyId, ECCurve curve)
    {
        var metadata = new KeyMetadata { KeyId = keyId };
        using var eccKey = ECDiffieHellman.Create(curve);
        var secretKey = SecretKeyFactory.CreateEcc(metadata, eccKey);
        var parameters = eccKey.ExportParameters(true);
        var nativeKey = EccKey.New(parameters.Q.X, parameters.Q.Y, parameters.D, CngKeyUsages.KeyAgreement);
        return (nativeKey, secretKey);
    }

    private (object controlKey, SecretKey secretKey) CreateRandomEccKey(string keyId, JweEncryption jweEncryption)
    {
        var curve = jweEncryption switch
        {
            JweEncryption.A128GCM => ECCurve.NamedCurves.nistP256,
            JweEncryption.A192GCM => ECCurve.NamedCurves.nistP256,
            JweEncryption.A256GCM => ECCurve.NamedCurves.nistP256,
            JweEncryption.A128CBC_HS256 => ECCurve.NamedCurves.nistP256,
            JweEncryption.A192CBC_HS384 => ECCurve.NamedCurves.nistP384,
            JweEncryption.A256CBC_HS512 => ECCurve.NamedCurves.nistP521,
            _ => throw new ArgumentOutOfRangeException(nameof(jweEncryption), jweEncryption, null)
        };
        return CreateRandomEccKey(keyId, curve);
    }

    private (object controlKey, SecretKey secretKey) CreateRandomSymmetricKey(string keyId, int bitCount)
    {
        var byteCount = bitCount >> 3;
        var bytes = new byte[byteCount];
        RandomNumberGenerator.Fill(bytes);
        var metadata = new KeyMetadata { KeyId = keyId };
        var secretKey = SecretKeyFactory.CreateSymmetric(metadata, bytes);
        return (bytes, secretKey);
    }

    private (object controlKey, SecretKey secretKey) CreateRandomPassword(string keyId)
    {
        var metadata = new KeyMetadata { KeyId = keyId };
        var password = Guid.NewGuid().ToString("N");
        var keyBytes = Encoding.UTF8.GetBytes(password);
        var secretKey = SecretKeyFactory.CreateSymmetric(metadata, keyBytes);
        return (password, secretKey);
    }

    private (object? controlKey, SecretKey secretKey) CreateRandomKey(string keyId, JwsAlgorithm jwsAlgorithm)
    {
        switch (jwsAlgorithm)
        {
            case JwsAlgorithm.none:
                return (null, EmptySecretKey.Singleton);

            case JwsAlgorithm.HS256:
                return CreateRandomSymmetricKey(keyId, 256);

            case JwsAlgorithm.HS384:
                return CreateRandomSymmetricKey(keyId, 384);

            case JwsAlgorithm.HS512:
                return CreateRandomSymmetricKey(keyId, 512);

            case JwsAlgorithm.RS256:
            case JwsAlgorithm.RS384:
            case JwsAlgorithm.RS512:
            case JwsAlgorithm.PS256:
            case JwsAlgorithm.PS384:
            case JwsAlgorithm.PS512:
                return CreateRandomRsaKey(keyId);

            case JwsAlgorithm.ES256:
                return CreateRandomEccKey(keyId, ECCurve.NamedCurves.nistP256);

            case JwsAlgorithm.ES384:
                return CreateRandomEccKey(keyId, ECCurve.NamedCurves.nistP384);

            case JwsAlgorithm.ES512:
                return CreateRandomEccKey(keyId, ECCurve.NamedCurves.nistP521);

            default:
                throw new ArgumentOutOfRangeException(nameof(jwsAlgorithm), jwsAlgorithm, null);
        }
    }

    private (object controlKey, SecretKey secretKey) CreateRandomKey(
        string keyId,
        JweAlgorithm jweAlgorithm,
        JweEncryption jweEncryption) =>
        jweAlgorithm switch
        {
            JweAlgorithm.DIR => CreateRandomSymmetricKey(keyId, GetBitCount(jweEncryption)),
            JweAlgorithm.RSA1_5 => CreateRandomRsaKey(keyId),
            JweAlgorithm.RSA_OAEP => CreateRandomRsaKey(keyId),
            JweAlgorithm.RSA_OAEP_256 => CreateRandomRsaKey(keyId),
            JweAlgorithm.ECDH_ES => CreateRandomEccKey(keyId, jweEncryption),
            JweAlgorithm.ECDH_ES_A128KW => CreateRandomEccKey(keyId, jweEncryption),
            JweAlgorithm.ECDH_ES_A192KW => CreateRandomEccKey(keyId, jweEncryption),
            JweAlgorithm.ECDH_ES_A256KW => CreateRandomEccKey(keyId, jweEncryption),
            JweAlgorithm.PBES2_HS256_A128KW => CreateRandomPassword(keyId),
            JweAlgorithm.PBES2_HS384_A192KW => CreateRandomPassword(keyId),
            JweAlgorithm.PBES2_HS512_A256KW => CreateRandomPassword(keyId),
            JweAlgorithm.A128KW => CreateRandomSymmetricKey(keyId, 128),
            JweAlgorithm.A128GCMKW => CreateRandomSymmetricKey(keyId, 128),
            JweAlgorithm.A192KW => CreateRandomSymmetricKey(keyId, 192),
            JweAlgorithm.A192GCMKW => CreateRandomSymmetricKey(keyId, 192),
            JweAlgorithm.A256KW => CreateRandomSymmetricKey(keyId, 256),
            JweAlgorithm.A256GCMKW => CreateRandomSymmetricKey(keyId, 256),
            _ => throw new ArgumentOutOfRangeException(nameof(jweAlgorithm), jweAlgorithm, null)
        };

    private static int GetBitCount(JweEncryption jweEncryption) =>
        jweEncryption switch
        {
            JweEncryption.A128GCM => 128,
            JweEncryption.A192GCM => 192,
            JweEncryption.A256GCM => 256,
            JweEncryption.A128CBC_HS256 => 256,
            JweEncryption.A192CBC_HS384 => 386,
            JweEncryption.A256CBC_HS512 => 512,
            _ => throw new ArgumentOutOfRangeException(nameof(jweEncryption), jweEncryption, null)
        };

    public static IEnumerable<object?[]> EncodeDecodeJweTestData
    {
        get
        {
            var algorithmTypes = Enum.GetValues<JweAlgorithm>();
            var encryptionTypes = Enum.GetValues<JweEncryption>();
            var compressionTypes = new JweCompression?[] { null, JweCompression.DEF };

            foreach (var algorithmType in algorithmTypes)
            foreach (var encryptionType in encryptionTypes)
            foreach (var compressionType in compressionTypes)
                yield return new object?[] { algorithmType, encryptionType, compressionType };
        }
    }

    [Theory]
    [MemberData(nameof(EncodeDecodeJweTestData))]
    public void Encode_Jwe(JweAlgorithm jweAlgorithm, JweEncryption jweEncryption, JweCompression? jweCompression)
    {
        const string keyId = nameof(keyId);

        var controlSettings = new JwtSettings();
        var (controlKey, secretKey) = CreateRandomKey(keyId, jweAlgorithm, jweEncryption);
        using var disposableNativeKey = controlKey as IDisposable ?? EmptyDisposable.Singleton;

        var keyManagementAlgorithmCode = controlSettings.JwaHeaderValue(jweAlgorithm);
        var encryptionAlgorithmCode = controlSettings.JweHeaderValue(jweEncryption);
        var compressionAlgorithmCode = jweCompression.HasValue ? controlSettings.CompressionHeader(jweCompression.Value) : null;

        var originalPayload = new Dictionary<string, object>
        {
            ["key1"] = 1234,
            ["key2"] = 12.34m,
            ["key3"] = "foo",
            ["key4"] = true,
            ["key5"] = DateTimeOffset.Now
        };

        var originalExtraHeaders = new Dictionary<string, object>
        {
            ["customHeader"] = "customValue"
        };

        if (!AlgorithmProvider.Collection.TryGetKeyManagementAlgorithm(
                keyManagementAlgorithmCode,
                out var keyManagementAlgorithm))
            throw new InvalidOperationException();

        if (!AlgorithmProvider.Collection.TryGetAuthenticatedEncryptionAlgorithm(
                encryptionAlgorithmCode,
                out var encryptionAlgorithm))
            throw new InvalidOperationException();

        if (string.IsNullOrEmpty(compressionAlgorithmCode) ||
            !AlgorithmProvider.Collection.TryGetCompressionAlgorithm(
                compressionAlgorithmCode,
                out var compressionAlgorithm))
            compressionAlgorithm = null;

        var encryptingCredentials = new JoseEncryptionCredentials(
            secretKey,
            keyManagementAlgorithm,
            encryptionAlgorithm,
            compressionAlgorithm);

        var encryptingOptions = new JoseEncryptionOptions(encryptingCredentials);

        var token = JoseSerializer.Encode(
            originalPayload,
            encryptingOptions,
            JsonSerializerOptions,
            originalExtraHeaders);

        var originalJson = JsonSerializer.Serialize(originalPayload, JoseSerializerOptions.JsonSerializerOptions);
        var deserializedJson = JWT.Decode(token, controlKey);
        var deserializedHeaders = JWT.Headers(token);
        Assert.Equal(originalJson, deserializedJson);

        var customHeader = Assert.Contains("customHeader", deserializedHeaders);
        Assert.Equal("customValue", customHeader);

        var typHeader = Assert.Contains("typ", deserializedHeaders);
        Assert.Equal("JWT", typHeader);

        var algHeader = Assert.Contains("alg", deserializedHeaders);
        Assert.Equal(keyManagementAlgorithmCode, algHeader);

        var encHeader = Assert.Contains("enc", deserializedHeaders);
        Assert.Equal(encryptionAlgorithmCode, encHeader);

        if (jweCompression is null)
        {
            Assert.DoesNotContain("zip", deserializedHeaders);
        }
        else
        {
            var zipHeader = Assert.Contains("zip", deserializedHeaders);
            Assert.Equal(compressionAlgorithmCode, zipHeader);
        }

        if (secretKey.KeySizeBits > 0)
        {
            var kidHeader = Assert.Contains("kid", deserializedHeaders);
            Assert.Equal(secretKey.KeyId, kidHeader);
        }
        else
        {
            Assert.DoesNotContain("kid", deserializedHeaders);
        }

        var decodedPayload = JoseSerializer.Decode(token, secretKey, out var decodedHeaders);
        Assert.Equal(JsonSerializer.Serialize(originalPayload), decodedPayload);
        Assert.Equal(JsonSerializer.Serialize(deserializedHeaders), JsonSerializer.Serialize(decodedHeaders));

        var deserializedPayload = JoseSerializer.Deserialize<IReadOnlyDictionary<string, object>>(token, secretKey, out var deserializedHeaders2);
        Assert.Equal(JsonSerializer.Serialize(originalPayload), JsonSerializer.Serialize(deserializedPayload));
        Assert.Equal(JsonSerializer.Serialize(deserializedHeaders), JsonSerializer.Serialize(deserializedHeaders2));
    }

    [Theory]
    [MemberData(nameof(EncodeDecodeJweTestData))]
    public void Decode_Jwe(JweAlgorithm jweAlgorithm, JweEncryption jweEncryption, JweCompression? jweCompression)
    {
        const string keyId = nameof(keyId);
        var (controlKey, secretKey) = CreateRandomKey(keyId, jweAlgorithm, jweEncryption);

        using var disposableNativeKey = controlKey as IDisposable ?? EmptyDisposable.Singleton;

        var originalPayload = new Dictionary<string, object>
        {
            ["key1"] = 1234,
            ["key2"] = 12.34,
            ["key3"] = "foo",
            ["key4"] = true,
            ["key5"] = DateTimeOffset.Now
        };
        var originalExtraHeaders = new Dictionary<string, object>
        {
            ["typ"] = "JWT",
            ["kid"] = keyId
        };
        var jwtSettings = new JwtSettings();
        var originalToken = JWT.Encode(originalPayload, controlKey, jweAlgorithm, jweEncryption, jweCompression, originalExtraHeaders, jwtSettings);

        var jsonPayload = JWT.Decode(originalToken, controlKey, jwtSettings);
        Assert.Equal(JsonSerializer.Serialize(originalPayload), jsonPayload);

        var actualPayload = JoseSerializer.Deserialize<Dictionary<string, object>>(originalToken, secretKey, out var header);
        Assert.Equal(originalPayload, actualPayload);

        var headerToVerify = header.Deserialize<Dictionary<string, object?>>(JoseSerializerOptions.JsonSerializerOptions);
        Assert.NotNull(headerToVerify);

        var typ = Assert.IsType<string>(Assert.Contains("typ", headerToVerify));
        Assert.Equal("JWT", typ);

        var alg = Assert.IsType<string>(Assert.Contains("alg", headerToVerify));
        Assert.Equal(jwtSettings.JwaHeaderValue(jweAlgorithm), alg);

        var enc = Assert.IsType<string>(Assert.Contains("enc", headerToVerify));
        Assert.Equal(jwtSettings.JweHeaderValue(jweEncryption), enc);

        if (jweCompression.HasValue)
        {
            var zip = Assert.IsType<string>(Assert.Contains("zip", headerToVerify));
            Assert.Equal(jwtSettings.CompressionHeader(jweCompression.Value), zip);
        }
        else
        {
            Assert.DoesNotContain("zip", headerToVerify);
        }

        var kid = Assert.IsType<string>(Assert.Contains("kid", headerToVerify));
        Assert.Equal(keyId, kid);
    }

    public static IEnumerable<object[]> GetEncodeDecodeJwsTestData()
    {
        var jwsAlgorithms = Enum.GetValues<JwsAlgorithm>();
        foreach (var jwsAlgorithm in jwsAlgorithms)
        {
            yield return new object[] { jwsAlgorithm, true, false };
            yield return new object[] { jwsAlgorithm, true, true };
            yield return new object[] { jwsAlgorithm, false, true };
        }
    }

    [Theory]
    [MemberData(nameof(GetEncodeDecodeJwsTestData))]
    public void Decode_Jws(JwsAlgorithm jwsAlgorithm, bool encodePayload, bool detachPayload)
    {
        const string keyId = nameof(keyId);

        var (controlKey, secretKey) = CreateRandomKey(keyId, jwsAlgorithm);

        using var disposableNativeKey = controlKey as IDisposable ?? EmptyDisposable.Singleton;

        var originalPayload = new Dictionary<string, object>
        {
            ["key1"] = 1234,
            ["key2"] = 12.34,
            ["key3"] = "foo",
            ["key4"] = true,
            ["key5"] = DateTimeOffset.Now
        };

        var originalExtraHeaders = new Dictionary<string, object>
        {
            ["typ"] = "JWT",
            ["kid"] = keyId
        };

        var jwtSettings = new JwtSettings();
        var jwtOptions = new JwtOptions
        {
            EncodePayload = encodePayload,
            DetachPayload = detachPayload
        };

        var originalToken = JWT.Encode(originalPayload, controlKey, jwsAlgorithm, originalExtraHeaders, jwtSettings, jwtOptions);

        var originalPayloadJson = JsonSerializer.Serialize(originalPayload, JoseSerializerOptions.JsonSerializerOptions);
        var detachedPayload = detachPayload ? originalPayloadJson : null;

        var jsonPayload = JWT.Decode(originalToken, controlKey, jwtSettings, detachedPayload);
        Assert.Equal(JsonSerializer.Serialize(originalPayload), jsonPayload);

        JsonElement header;
        if (detachPayload)
        {
            JoseSerializer.VerifyJws(
                originalToken,
                secretKey,
                originalPayload,
                JsonSerializerOptions,
                out header);
        }
        else
        {
            var actualPayload = JoseSerializer.Deserialize<Dictionary<string, object>>(originalToken, secretKey, out header);
            Assert.Equal(originalPayload, actualPayload);
        }

        var headerToVerify = header.Deserialize<Dictionary<string, object?>>(JoseSerializerOptions.JsonSerializerOptions);
        Assert.NotNull(headerToVerify);

        var typ = Assert.IsType<string>(Assert.Contains("typ", headerToVerify));
        Assert.Equal("JWT", typ);

        var alg = Assert.IsType<string>(Assert.Contains("alg", headerToVerify));
        Assert.Equal(jwtSettings.JwsHeaderValue(jwsAlgorithm), alg);

        var kid = Assert.IsType<string>(Assert.Contains("kid", headerToVerify));
        Assert.Equal(keyId, kid);
    }

    [Theory]
    [MemberData(nameof(GetEncodeDecodeJwsTestData))]
    public void Encode_Jws(JwsAlgorithm jwsAlgorithm, bool encodePayload, bool detachPayload)
    {
        const string keyId = nameof(keyId);

        var controlSettings = new JwtSettings();
        var (controlKey, secretKey) = CreateRandomKey(keyId, jwsAlgorithm);
        var signatureAlgorithmCode = controlSettings.JwsHeaderValue(jwsAlgorithm);

        if (!AlgorithmProvider.Collection.TryGetSignatureAlgorithm(
                signatureAlgorithmCode,
                out var signatureAlgorithm))
            throw new InvalidOperationException();

        var payload = new Dictionary<string, object>
        {
            ["key1"] = "p-value"
        };
        var extraHeaders = new Dictionary<string, object>
        {
            ["header1"] = "h-value"
        };

        var signingCredentials = new JoseSigningCredentials(
            secretKey,
            signatureAlgorithm);

        var signingOptions = new JoseSigningOptions(signingCredentials)
        {
            EncodePayload = encodePayload,
            DetachPayload = detachPayload
        };

        var token = JoseSerializer.Encode(
            payload,
            signingOptions,
            JsonSerializerOptions,
            extraHeaders);

        var json = JsonSerializer.Serialize(payload, JoseSerializerOptions.JsonSerializerOptions);
        var token2 = JoseSerializer.Encode(json, signingOptions, extraHeaders);

        JsonElement deserializedHeaders;
        if (detachPayload)
        {
            JoseSerializer.VerifyJws(token, secretKey, payload, JsonSerializerOptions, out deserializedHeaders);
            JoseSerializer.VerifyJws(token, secretKey, json, out var deserializedHeaders2);

            JoseSerializer.VerifyJws(token2, secretKey, payload, JsonSerializerOptions, out var deserializedHeaders3);
            JoseSerializer.VerifyJws(token2, secretKey, json, out var deserializedHeaders4);

            Assert.Equal(JsonSerializer.Serialize(deserializedHeaders), JsonSerializer.Serialize(deserializedHeaders2));
            Assert.Equal(JsonSerializer.Serialize(deserializedHeaders), JsonSerializer.Serialize(deserializedHeaders3));
            Assert.Equal(JsonSerializer.Serialize(deserializedHeaders), JsonSerializer.Serialize(deserializedHeaders4));
        }
        else
        {
            var deserialized = JoseSerializer.Deserialize<IReadOnlyDictionary<string, object>>(token, secretKey, out deserializedHeaders);
            Assert.Equal(JsonSerializer.Serialize(payload), JsonSerializer.Serialize(deserialized));

            var deserialized2 = JoseSerializer.Deserialize<IReadOnlyDictionary<string, object>>(token2, secretKey, out var deserializedHeaders2);
            Assert.Equal(JsonSerializer.Serialize(payload), JsonSerializer.Serialize(deserialized2));
            Assert.Equal(JsonSerializer.Serialize(deserializedHeaders), JsonSerializer.Serialize(deserializedHeaders2));

            var controlPayload = JWT.Decode<Dictionary<string, object>>(token, controlKey);
            Assert.Equal(JsonSerializer.Serialize(payload), JsonSerializer.Serialize(controlPayload));
        }

        var headerToVerify = deserializedHeaders.Deserialize<Dictionary<string, object?>>(JoseSerializerOptions.JsonSerializerOptions);
        Assert.NotNull(headerToVerify);

        if (secretKey.KeySizeBits > 0)
        {
            var kidHeader = Assert.Contains("kid", headerToVerify);
            Assert.Equal(secretKey.KeyId, kidHeader);
        }

        if (encodePayload)
        {
            Assert.DoesNotContain("b64", headerToVerify);
            Assert.DoesNotContain("crit", headerToVerify);
        }
        else
        {
            var b64Header = Assert.Contains("b64", headerToVerify);
            Assert.Equal(false, b64Header);

            var criticalHeader = Assert.Contains("crit", headerToVerify);
            Assert.Equal(new[] { "b64" }, criticalHeader);
        }

        var typHeader = Assert.Contains("typ", headerToVerify);
        Assert.Equal("JWT", typHeader);

        var algHeader = Assert.Contains("alg", headerToVerify);
        Assert.Equal(signatureAlgorithmCode, algHeader);
    }
}
