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
using Jose;
using Jose.keys;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NCode.Cryptography.Keys;
using NCode.Jose.Extensions;
using NCode.Jose.Signature;

namespace NCode.Jose.Tests;

public class JoseSerializerTests : BaseTests
{
    private ServiceProvider ServiceProvider { get; }
    private JoseOptions JoseOptions { get; } = new();
    private Mock<JoseSerializer> MockJoseSerializer { get; }
    private JoseSerializer JoseSerializer { get; }

    public JoseSerializerTests()
    {
        var services = new ServiceCollection();
        ConfigureServices(services);
        ServiceProvider = services.BuildServiceProvider();

        var algorithmProvider = ServiceProvider.GetRequiredService<IAlgorithmProvider>();
        MockJoseSerializer = CreatePartialMock<JoseSerializer>(Options.Create(JoseOptions), algorithmProvider);
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

    private static ISignatureAlgorithm CreateSignatureAlgorithm(JwsAlgorithm jwsAlgorithm) =>
        jwsAlgorithm switch
        {
            JwsAlgorithm.none => new NoneSignatureAlgorithm(),
            JwsAlgorithm.HS256 => new KeyedHashSignatureAlgorithm("HS256", 256, HMACSHA256.TryHashData),
            JwsAlgorithm.HS384 => new KeyedHashSignatureAlgorithm("HS384", 384, HMACSHA384.TryHashData),
            JwsAlgorithm.HS512 => new KeyedHashSignatureAlgorithm("HS512", 512, HMACSHA512.TryHashData),
            JwsAlgorithm.RS256 => new RsaSignatureAlgorithm("RS256", HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1),
            JwsAlgorithm.RS384 => new RsaSignatureAlgorithm("RS384", HashAlgorithmName.SHA384, RSASignaturePadding.Pkcs1),
            JwsAlgorithm.RS512 => new RsaSignatureAlgorithm("RS512", HashAlgorithmName.SHA512, RSASignaturePadding.Pkcs1),
            JwsAlgorithm.PS256 => new RsaSignatureAlgorithm("PS256", HashAlgorithmName.SHA256, RSASignaturePadding.Pss),
            JwsAlgorithm.PS384 => new RsaSignatureAlgorithm("PS384", HashAlgorithmName.SHA384, RSASignaturePadding.Pss),
            JwsAlgorithm.PS512 => new RsaSignatureAlgorithm("PS512", HashAlgorithmName.SHA512, RSASignaturePadding.Pss),
            JwsAlgorithm.ES256 => new EccSignatureAlgorithm("ES256", HashAlgorithmName.SHA256),
            JwsAlgorithm.ES384 => new EccSignatureAlgorithm("ES384", HashAlgorithmName.SHA384),
            JwsAlgorithm.ES512 => new EccSignatureAlgorithm("ES512", HashAlgorithmName.SHA512),
            _ => throw new ArgumentOutOfRangeException(nameof(jwsAlgorithm), jwsAlgorithm, null)
        };

    private static (object controlKey, SecretKey secretKey) CreateRandomRsaKey(string keyId)
    {
        var nativeKey = RSA.Create();
        var secretKey = RsaSecretKey.Create(keyId, nativeKey);
        return (nativeKey, secretKey);
    }

    private static (object controlKey, SecretKey secretKey) CreateRandomEccKey(string keyId, ECCurve curve)
    {
        using var eccKey = ECDiffieHellman.Create(curve);
        var secretKey = EccSecretKey.Create(keyId, eccKey);
        var parameters = eccKey.ExportParameters(true);
        var nativeKey = EccKey.New(parameters.Q.X, parameters.Q.Y, parameters.D, CngKeyUsages.KeyAgreement);
        return (nativeKey, secretKey);
    }

    private static (object controlKey, SecretKey secretKey) CreateRandomEccKey(string keyId, JweEncryption jweEncryption)
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

    private static (object controlKey, SecretKey secretKey) CreateRandomSymmetricKey(string keyId, int bitCount)
    {
        var byteCount = bitCount >> 3;
        var bytes = new byte[byteCount];
        RandomNumberGenerator.Fill(bytes);
        var secretKey = new SymmetricSecretKey(keyId, bytes);
        return (bytes, secretKey);
    }

    private static (object controlKey, SecretKey secretKey) CreateRandomPassword(string keyId)
    {
        var password = Guid.NewGuid().ToString("N");
        var secretKey = new SymmetricSecretKey(keyId, password);
        return (password, secretKey);
    }

    private static (object? controlKey, SecretKey secretKey) CreateRandomKey(string keyId, JwsAlgorithm jwsAlgorithm)
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

    private static (object controlKey, SecretKey secretKey) CreateRandomKey(
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

    public static IEnumerable<object?[]> DecodeJweTestData
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
    [MemberData(nameof(DecodeJweTestData))]
    public void Decode_Jwe(JweAlgorithm jweAlgorithm, JweEncryption jweEncryption, JweCompression? jweCompression)
    {
        const string keyId = nameof(keyId);
        var (controlKey, secretKey) = CreateRandomKey(keyId, jweAlgorithm, jweEncryption);

        using var disposableNativeKey = controlKey as IDisposable ?? DummyDisposable.Singleton;
        using var disposableSecretKey = secretKey;

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
            ["kid"] = keyId
        };
        var jwtSettings = new JwtSettings();
        var originalToken = JWT.Encode(originalPayload, controlKey, jweAlgorithm, jweEncryption, jweCompression, originalExtraHeaders, jwtSettings);

        var jsonPayload = JWT.Decode(originalToken, controlKey, jwtSettings);
        Assert.Equal(JsonSerializer.Serialize(originalPayload), jsonPayload);

        var actualPayload = JoseSerializer.Deserialize<Dictionary<string, object>>(originalToken, secretKey, out var header);
        Assert.Equal(originalPayload, actualPayload);

        var alg = Assert.IsType<string>(Assert.Contains("alg", header));
        Assert.Equal(jwtSettings.JwaHeaderValue(jweAlgorithm), alg);

        var enc = Assert.IsType<string>(Assert.Contains("enc", header));
        Assert.Equal(jwtSettings.JweHeaderValue(jweEncryption), enc);

        if (jweCompression.HasValue)
        {
            var zip = Assert.IsType<string>(Assert.Contains("zip", header));
            Assert.Equal(jwtSettings.CompressionHeader(jweCompression.Value), zip);
        }
        else
        {
            Assert.DoesNotContain("zip", header);
        }

        var kid = Assert.IsType<string>(Assert.Contains("kid", header));
        Assert.Equal(keyId, kid);
    }

    public static IEnumerable<object[]> DecodeJwsTestData =>
        Enum.GetValues<JwsAlgorithm>().Select(algorithmType =>
            new object[] { algorithmType });

    [Theory]
    [MemberData(nameof(DecodeJwsTestData))]
    public void Decode_Jws(JwsAlgorithm jwsAlgorithm)
    {
        const string keyId = nameof(keyId);

        var (controlKey, secretKey) = CreateRandomKey(keyId, jwsAlgorithm);

        using var disposableNativeKey = controlKey as IDisposable ?? DummyDisposable.Singleton;
        using var disposableSecretKey = secretKey;

        var originalPayload = new Dictionary<string, object>
        {
            ["key1"] = 1234,
            ["key2"] = 12.34M,
            ["key3"] = "foo",
            ["key4"] = true,
            ["key5"] = DateTimeOffset.Now
        };
        var originalExtraHeaders = new Dictionary<string, object>
        {
            ["kid"] = keyId
        };
        var jwtSettings = new JwtSettings();

        var originalToken = JWT.Encode(originalPayload, controlKey, jwsAlgorithm, originalExtraHeaders, jwtSettings);
        var jsonPayload = JWT.Decode(originalToken, controlKey, jwtSettings);
        Assert.Equal(JsonSerializer.Serialize(originalPayload), jsonPayload);

        var actualPayload = JoseSerializer.Deserialize<Dictionary<string, object>>(originalToken, secretKey, out var header);
        Assert.Equal(originalPayload, actualPayload);

        var alg = Assert.IsType<string>(Assert.Contains("alg", header));
        Assert.Equal(jwtSettings.JwsHeaderValue(jwsAlgorithm), alg);

        var kid = Assert.IsType<string>(Assert.Contains("kid", header));
        Assert.Equal(keyId, kid);
    }

    public static IEnumerable<object[]> GetEncodeJwsTestData()
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
    [MemberData(nameof(GetEncodeJwsTestData))]
    public void Encode_Jws(JwsAlgorithm jwsAlgorithm, bool encodePayload, bool detachPayload)
    {
        const string keyId = nameof(keyId);

        var (controlKey, secretKey) = CreateRandomKey(keyId, jwsAlgorithm);
        var algorithm = CreateSignatureAlgorithm(jwsAlgorithm);

        var payload = new Dictionary<string, object>
        {
            ["key1"] = "p-value"
        };
        var extraHeaders = new Dictionary<string, object>
        {
            ["header1"] = "h-value"
        };

        var options = new JwsOptions
        {
            EncodePayload = encodePayload,
            DetachPayload = detachPayload
        };

        var token = JoseSerializer.EncodeJws(payload, secretKey, algorithm, extraHeaders, options);

        var json = JsonSerializer.Serialize(payload, JoseOptions.JsonSerializerOptions);
        var token2 = JoseSerializer.EncodeJws(json, secretKey, algorithm, extraHeaders, options);

        IReadOnlyDictionary<string, object> deserializedHeaders;
        if (detachPayload)
        {
            JoseSerializer.VerifyJws(token, secretKey, payload, out deserializedHeaders);
            JoseSerializer.VerifyJws(token, secretKey, json, out var deserializedHeaders2);

            JoseSerializer.VerifyJws(token2, secretKey, payload, out var deserializedHeaders3);
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

        if (secretKey.KeySizeBits > 0)
        {
            var kidHeader = Assert.Contains("kid", deserializedHeaders);
            Assert.Equal(secretKey.KeyId, kidHeader);
        }

        if (encodePayload)
        {
            Assert.DoesNotContain("b64", deserializedHeaders);
            Assert.DoesNotContain("crit", deserializedHeaders);
        }
        else
        {
            var b64Header = Assert.Contains("b64", deserializedHeaders);
            Assert.Equal(false, b64Header);

            var critHeader = Assert.Contains("crit", deserializedHeaders);
            Assert.Equal(new[] { "b64" }, critHeader);
        }

        var typHeader = Assert.Contains("typ", deserializedHeaders);
        Assert.Equal("JWT", typHeader);

        var algHeader = Assert.Contains("alg", deserializedHeaders);
        Assert.Equal(algorithm.Code, algHeader);
    }
}
