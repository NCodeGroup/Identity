#region Copyright Preamble

//
//    Copyright @ 2026 NCode Group
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
using Microsoft.Extensions.DependencyInjection;
using NCode.Buffers;
using NCode.Encoders;
using NCode.Identity.Jose.Algorithms;
using NCode.Identity.Jose.Credentials;
using NCode.Identity.Jose.DPoP;
using NCode.Identity.Secrets;
using NCode.Identity.Secrets.Keys;

namespace NCode.Identity.Jose.Tests.DPoP;

public class DefaultDPoPProofServiceTests : BaseTests
{
    private const string HttpMethod = "POST";
    private const string HttpUri = "https://op.test/token";

    private static readonly DateTimeOffset StartTime = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private DateTimeOffset Now { get; set; } = StartTime;
    private ServiceProvider ServiceProvider { get; }
    private IAlgorithmCollection AlgorithmCollection { get; }
    private IDPoPProofService ProofService { get; }

    public DefaultDPoPProofServiceTests()
    {
        var mockTimeProvider = CreateLooseMock<TimeProvider>();
        mockTimeProvider.Setup(x => x.GetUtcNow()).Returns(() => Now);

        var serviceCollection = new ServiceCollection();
        serviceCollection.AddLogging();
        serviceCollection.AddSingleton(mockTimeProvider.Object);
        serviceCollection.AddSecretsLibrary().AddJoseLibrary();
        ServiceProvider = serviceCollection.BuildServiceProvider();

        AlgorithmCollection = ServiceProvider
            .GetRequiredService<IAlgorithmCollectionProvider>()
            .Collection;
        ProofService = ServiceProvider.GetRequiredService<IDPoPProofService>();
    }

    protected override async ValueTask DisposeAsyncCore()
    {
        await ServiceProvider.DisposeAsync();
    }

    #region Scaffolding

    private JoseSigningCredentials CreateCredentials(string algorithmCode)
    {
        Assert.True(AlgorithmCollection.TryGetSignatureAlgorithm(algorithmCode, out var algorithm));
        SecretKey secretKey = algorithmCode.StartsWith("ES", StringComparison.Ordinal)
            ? SecretKeyFactory.CreateEcc(
                new KeyMetadata { KeyId = "dpop" },
                ECDsa.Create(ECCurve.NamedCurves.nistP256)
            )
            : SecretKeyFactory.CreateRsa(new KeyMetadata { KeyId = "dpop" }, RSA.Create(2048));
        return new JoseSigningCredentials(secretKey, algorithm);
    }

    private string CreateProof(
        JoseSigningCredentials credentials,
        string httpUri = HttpUri,
        string? accessToken = null,
        string? nonce = null
    ) =>
        ProofService.CreateProof(
            new DPoPProofCreationOptions
            {
                SigningCredentials = credentials,
                HttpMethod = HttpMethod,
                HttpUri = httpUri,
                AccessToken = accessToken,
                Nonce = nonce,
            }
        );

    private static DPoPProofValidationParameters CreateParameters(
        string httpMethod = HttpMethod,
        string httpUri = HttpUri,
        string? accessToken = null,
        params string[] allowedAlgorithms
    ) =>
        new()
        {
            HttpMethod = httpMethod,
            HttpUri = httpUri,
            AllowedAlgorithms =
                allowedAlgorithms.Length > 0 ? allowedAlgorithms : ["ES256", "PS256"],
            Lifetime = TimeSpan.FromSeconds(60),
            AccessToken = accessToken,
        };

    private static JsonElement DecodeSegment(string proof, int index) =>
        JsonDocument.Parse(Base64Url.Decode(proof.Split('.')[index])).RootElement.Clone();

    // Hand-builds an ES256-signed proof so a test can emit headers the service itself never produces.
    private static string BuildEs256Proof(ECDsa ecdsa, object header, object payload)
    {
        var encodedHeader = Base64Url.Encode(JsonSerializer.SerializeToUtf8Bytes(header));
        var encodedPayload = Base64Url.Encode(JsonSerializer.SerializeToUtf8Bytes(payload));
        var signingInput = $"{encodedHeader}.{encodedPayload}";
        var signature = ecdsa.SignData(
            SecureEncoding.ASCII.GetBytes(signingInput),
            HashAlgorithmName.SHA256,
            DSASignatureFormat.IeeeP1363FixedFieldConcatenation
        );
        return $"{signingInput}.{Base64Url.Encode(signature)}";
    }

    private static Dictionary<string, object> CreateEcJwk(ECDsa ecdsa, bool includePrivate)
    {
        var parameters = ecdsa.ExportParameters(includePrivateParameters: includePrivate);
        var jwk = new Dictionary<string, object>
        {
            ["kty"] = "EC",
            ["crv"] = "P-256",
            ["x"] = Base64Url.Encode(parameters.Q.X!),
            ["y"] = Base64Url.Encode(parameters.Q.Y!),
        };
        if (includePrivate)
            jwk["d"] = Base64Url.Encode(parameters.D!);
        return jwk;
    }

    private Dictionary<string, object> CreatePayload() =>
        new()
        {
            ["htm"] = HttpMethod,
            ["htu"] = HttpUri,
            ["iat"] = Now.ToUnixTimeSeconds(),
            ["jti"] = "jti-1",
        };

    private void AssertRejected(
        string proof,
        DPoPProofValidationParameters parameters,
        string expectedFragment
    )
    {
        var valid = ProofService.TryValidateProof(proof, parameters, out var result, out var error);

        Assert.False(valid);
        Assert.Null(result);
        Assert.Contains(expectedFragment, error, StringComparison.Ordinal);
    }

    #endregion

    #region CreateProof Tests

    [Theory]
    [InlineData("ES256")]
    [InlineData("PS256")]
    public void CreateProof_WhenValidated_RoundTripsWithThumbprint(string algorithmCode)
    {
        var credentials = CreateCredentials(algorithmCode);

        var proof = CreateProof(credentials, nonce: "server-nonce");

        Assert.True(
            ProofService.TryValidateProof(proof, CreateParameters(), out var result, out var error),
            error
        );
        var header = DecodeSegment(proof, 0);
        var expectedJwk = new Jwk.JsonWebKey(header.GetProperty("jwk"));
        Assert.Equal(algorithmCode, result.Algorithm);
        Assert.Equal(expectedJwk.ComputeThumbprint(), result.JwkThumbprint);
        Assert.False(result.PublicKey.ContainsPrivateKeyMaterial);
        Assert.Equal("server-nonce", result.Nonce);
        Assert.Equal(StartTime, result.IssuedAt);
        Assert.False(string.IsNullOrEmpty(result.Jti));
    }

    [Fact]
    public void CreateProof_WhenCalled_EmitsDPoPHeaderAndClaims()
    {
        var proof = CreateProof(
            CreateCredentials("ES256"),
            httpUri: "https://op.test/token?x=1#frag",
            accessToken: "access-token"
        );

        var header = DecodeSegment(proof, 0);
        var payload = DecodeSegment(proof, 1);
        Assert.Equal("dpop+jwt", header.GetProperty("typ").GetString());
        Assert.Equal("ES256", header.GetProperty("alg").GetString());
        Assert.False(header.TryGetProperty("kid", out _));
        Assert.False(header.GetProperty("jwk").TryGetProperty("d", out _));
        Assert.Equal(HttpMethod, payload.GetProperty("htm").GetString());
        Assert.Equal(HttpUri, payload.GetProperty("htu").GetString());
        Assert.Equal(StartTime.ToUnixTimeSeconds(), payload.GetProperty("iat").GetInt64());
        Assert.Equal(
            Base64Url.Encode(SHA256.HashData(SecureEncoding.ASCII.GetBytes("access-token"))),
            payload.GetProperty("ath").GetString()
        );
        Assert.False(payload.TryGetProperty("nonce", out _));
    }

    [Fact]
    public void CreateProof_WhenCalledTwice_UsesUniqueJti()
    {
        var credentials = CreateCredentials("ES256");

        var first = DecodeSegment(CreateProof(credentials), 1).GetProperty("jti").GetString();
        var second = DecodeSegment(CreateProof(credentials), 1).GetProperty("jti").GetString();

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void CreateProof_WhenSymmetricKey_Throws()
    {
        Assert.True(AlgorithmCollection.TryGetSignatureAlgorithm("HS256", out var algorithm));
        var secretKey = SecretKeyFactory.CreateSymmetric(
            new KeyMetadata { KeyId = "hmac" },
            RandomNumberGenerator.GetBytes(32)
        );

        Assert.Throws<ArgumentException>(() =>
            CreateProof(new JoseSigningCredentials(secretKey, algorithm))
        );
    }

    [Fact]
    public void CreateProof_WhenRelativeUri_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            CreateProof(CreateCredentials("ES256"), httpUri: "/token")
        );
    }

    [Fact]
    public void CreateProof_WhenAccessTokenIsNotAscii_Throws()
    {
        Assert.ThrowsAny<ArgumentException>(() =>
            CreateProof(CreateCredentials("ES256"), accessToken: "accéss-token")
        );
    }

    #endregion

    #region TryValidateProof Tests

    [Fact]
    public void TryValidateProof_WhenAccessTokenMatches_Succeeds()
    {
        var proof = CreateProof(CreateCredentials("ES256"), accessToken: "access-token");

        Assert.True(
            ProofService.TryValidateProof(
                proof,
                CreateParameters(accessToken: "access-token"),
                out _,
                out var error
            ),
            error
        );
    }

    [Fact]
    public void TryValidateProof_WhenAccessTokenDiffers_Fails()
    {
        var proof = CreateProof(CreateCredentials("ES256"), accessToken: "access-token");

        AssertRejected(proof, CreateParameters(accessToken: "other-token"), "'ath'");
    }

    [Fact]
    public void TryValidateProof_WhenAccessTokenIsNotAscii_Fails()
    {
        var proof = CreateProof(CreateCredentials("ES256"), accessToken: "acc?ss-token");

        AssertRejected(proof, CreateParameters(accessToken: "accéss-token"), "'ath'");
    }

    [Fact]
    public void TryValidateProof_WhenRequestUriHasQuery_IgnoresIt()
    {
        var proof = CreateProof(CreateCredentials("ES256"));

        Assert.True(
            ProofService.TryValidateProof(
                proof,
                CreateParameters(httpUri: "https://op.test/token?x=1"),
                out _,
                out var error
            ),
            error
        );
    }

    [Fact]
    public void TryValidateProof_WhenMethodDiffers_Fails()
    {
        var proof = CreateProof(CreateCredentials("ES256"));

        AssertRejected(proof, CreateParameters(httpMethod: "GET"), "'htm'");
    }

    [Fact]
    public void TryValidateProof_WhenUriDiffers_Fails()
    {
        var proof = CreateProof(CreateCredentials("ES256"));

        AssertRejected(proof, CreateParameters(httpUri: "https://op.test/other"), "'htu'");
    }

    [Fact]
    public void TryValidateProof_WhenAlgorithmNotAllowed_Fails()
    {
        var proof = CreateProof(CreateCredentials("PS256"));

        AssertRejected(proof, CreateParameters(allowedAlgorithms: "ES256"), "'alg'");
    }

    [Theory]
    [InlineData(-61)]
    [InlineData(61)]
    public void TryValidateProof_WhenOutsideLifetime_Fails(int offsetSeconds)
    {
        var proof = CreateProof(CreateCredentials("ES256"));
        Now = StartTime.AddSeconds(offsetSeconds);

        AssertRejected(proof, CreateParameters(), "'iat'");
    }

    [Fact]
    public void TryValidateProof_WhenSignatureTampered_Fails()
    {
        var proof = CreateProof(CreateCredentials("ES256"));
        var lastDot = proof.LastIndexOf('.');
        var signature = proof[(lastDot + 1)..];
        var tampered = proof[..(lastDot + 1)] + (signature[0] == 'A' ? 'B' : 'A') + signature[1..];

        AssertRejected(tampered, CreateParameters(), "signature");
    }

    [Theory]
    [InlineData("not-a-jwt")]
    [InlineData("a.b.c.d.e")]
    [InlineData("!!!.@@@.###")]
    public void TryValidateProof_WhenMalformed_Fails(string proof)
    {
        Assert.False(
            ProofService.TryValidateProof(proof, CreateParameters(), out _, out var error)
        );
        Assert.NotNull(error);
    }

    [Fact]
    public void TryValidateProof_WhenWrongTyp_Fails()
    {
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var header = new Dictionary<string, object>
        {
            ["typ"] = "JWT",
            ["alg"] = "ES256",
            ["jwk"] = CreateEcJwk(ecdsa, includePrivate: false),
        };

        AssertRejected(
            BuildEs256Proof(ecdsa, header, CreatePayload()),
            CreateParameters(),
            "'typ'"
        );
    }

    [Fact]
    public void TryValidateProof_WhenCritHeader_Fails()
    {
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var header = new Dictionary<string, object>
        {
            ["typ"] = "dpop+jwt",
            ["alg"] = "ES256",
            ["jwk"] = CreateEcJwk(ecdsa, includePrivate: false),
            ["crit"] = new[] { "exp" },
        };

        AssertRejected(
            BuildEs256Proof(ecdsa, header, CreatePayload()),
            CreateParameters(),
            "'crit'"
        );
    }

    [Fact]
    public void TryValidateProof_WhenJwkHasPrivateKey_Fails()
    {
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var header = new Dictionary<string, object>
        {
            ["typ"] = "dpop+jwt",
            ["alg"] = "ES256",
            ["jwk"] = CreateEcJwk(ecdsa, includePrivate: true),
        };

        AssertRejected(
            BuildEs256Proof(ecdsa, header, CreatePayload()),
            CreateParameters(),
            "private key material"
        );
    }

    [Fact]
    public void TryValidateProof_WhenUnknownKeyTypeCarriesPrivateMember_ReportsUnsupportedKeyType()
    {
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var header = new Dictionary<string, object>
        {
            ["typ"] = "dpop+jwt",
            ["alg"] = "ES256",
            ["jwk"] = new Dictionary<string, object> { ["kty"] = "XYZ", ["d"] = "AAAA" },
        };

        AssertRejected(
            BuildEs256Proof(ecdsa, header, CreatePayload()),
            CreateParameters(),
            "'XYZ' is not supported"
        );
    }

    [Fact]
    public void TryValidateProof_WhenSymmetricAlgorithmAllowed_StillFails()
    {
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var header = new Dictionary<string, object>
        {
            ["typ"] = "dpop+jwt",
            ["alg"] = "HS256",
            ["jwk"] = CreateEcJwk(ecdsa, includePrivate: false),
        };

        AssertRejected(
            BuildEs256Proof(ecdsa, header, CreatePayload()),
            CreateParameters(allowedAlgorithms: "HS256"),
            "'alg'"
        );
    }

    [Fact]
    public void TryValidateProof_WhenAlgorithmDoesNotMatchKeyType_Fails()
    {
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var header = new Dictionary<string, object>
        {
            ["typ"] = "dpop+jwt",
            ["alg"] = "PS256",
            ["jwk"] = CreateEcJwk(ecdsa, includePrivate: false),
        };

        AssertRejected(
            BuildEs256Proof(ecdsa, header, CreatePayload()),
            CreateParameters(),
            "does not match"
        );
    }

    [Fact]
    public void TryValidateProof_WhenRsaKeyTooSmall_Fails()
    {
        using var rsa = RSA.Create(1024);
        var parameters = rsa.ExportParameters(includePrivateParameters: false);
        var header = new Dictionary<string, object>
        {
            ["typ"] = "dpop+jwt",
            ["alg"] = "RS256",
            ["jwk"] = new Dictionary<string, object>
            {
                ["kty"] = "RSA",
                ["n"] = Base64Url.Encode(parameters.Modulus!),
                ["e"] = Base64Url.Encode(parameters.Exponent!),
            },
        };
        var encodedHeader = Base64Url.Encode(JsonSerializer.SerializeToUtf8Bytes(header));
        var encodedPayload = Base64Url.Encode(JsonSerializer.SerializeToUtf8Bytes(CreatePayload()));
        var signingInput = $"{encodedHeader}.{encodedPayload}";
        var signature = rsa.SignData(
            SecureEncoding.ASCII.GetBytes(signingInput),
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1
        );
        var proof = $"{signingInput}.{Base64Url.Encode(signature)}";

        AssertRejected(proof, CreateParameters(allowedAlgorithms: "RS256"), "key size");
    }

    [Fact]
    public void TryValidateProof_WhenJtiMissing_Fails()
    {
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var header = new Dictionary<string, object>
        {
            ["typ"] = "dpop+jwt",
            ["alg"] = "ES256",
            ["jwk"] = CreateEcJwk(ecdsa, includePrivate: false),
        };
        var payload = CreatePayload();
        payload.Remove("jti");

        AssertRejected(BuildEs256Proof(ecdsa, header, payload), CreateParameters(), "'jti'");
    }

    #endregion
}
