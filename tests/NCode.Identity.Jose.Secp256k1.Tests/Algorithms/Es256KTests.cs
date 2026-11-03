// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using NCode.Buffers;
using NCode.Identity.Jose.Algorithms;
using NCode.Identity.Jose.Credentials;
using NCode.Identity.Jose.Exceptions;
using NCode.Identity.Jose.Secp256k1.KeyTypes;
using NCode.Identity.Secrets.Keys;
using Xunit;

namespace NCode.Identity.Jose.Secp256k1.Tests.Algorithms;

/// <summary>
/// Tests the <c>ES256K</c> algorithm (RFC 8812) end to end: signing and verification through the <c>JOSE</c>
/// serializer, a JWS signed by an independent implementation, the binding of <c>ES256K</c> and <c>ES256</c> to their
/// curves, and <c>JWK</c> import and export with <c>crv</c> <c>secp256k1</c>.
/// </summary>
public sealed class Es256KTests : IAsyncDisposable
{
    // RFC 8812 publishes no test vectors. This key and JWS were made with Python's `cryptography` library (OpenSSL), an
    // implementation independent of this package: the private scalar is SHA-256("NCode.Identity ES256K test key") mod n,
    // and the signature is ECDSA with SHA-256 over the JWS signing input, encoded as R || S (RFC 7518 §3.4).
    private const string KnownX = "M6OK6DCIZD8csw9Fn_s_BFQHcUQT3SgSvddFnL-7X-0";
    private const string KnownY = "15rv8RHVzqiowf7c2PfjYH8CA4sGYg_xqHu8sbqMoug";
    private const string KnownD = "c2RvzFdi4LWhKdVV0ZD8Urc6RxAyDYijkRpyt-TPxNk";

    private const string KnownJws =
        "eyJhbGciOiJFUzI1NksiLCJraWQiOiJzZWNwMjU2azEtdGVzdCJ9"
        + ".eyJpc3MiOiJodHRwczovL2lzc3Vlci5leGFtcGxlIiwic3ViIjoiYWxpY2UifQ"
        + ".ifLaEsi9aFTIYQ9LYn8cDsyfOZt_uvdA-NNOAuCgvXzUPeirHntmtcV2D3jDBEXRpSdoAVuAml1ddhavYjWfzA";

    private const string KnownPayload = """{"iss":"https://issuer.example","sub":"alice"}""";

    private Secp256k1TestServices Services { get; } = new();

    /// <inheritdoc />
    public async ValueTask DisposeAsync() => await Services.DisposeAsync();

    #region Helpers

    private static JsonObject KnownPublicJwk() =>
        new()
        {
            ["kty"] = "EC",
            ["crv"] = "secp256k1",
            ["x"] = KnownX,
            ["y"] = KnownY,
        };

    private SignatureAlgorithm Es256K => Services.GetSignatureAlgorithm("ES256K");

    private SignatureAlgorithm Es256 => Services.GetSignatureAlgorithm("ES256");

    private string Sign(SecretKey secretKey, SignatureAlgorithm algorithm, object payload) =>
        Services
            .JoseSerializer.CreateEncoder(
                new JoseSigningOptions(new JoseSigningCredentials(secretKey, algorithm))
            )
            .Encode(payload);

    #endregion

    #region Sign and verify

    [Secp256k1Fact]
    public void Encode_WithASecp256k1Key_IsVerifiedWithItsPublicJwk()
    {
        var privateKey = Services.GenerateKey("secp256k1");
        var exported = Services.Descriptor.ExportPublicKey(privateKey);
        Assert.NotNull(exported);
        var publicKey = Services.ImportPublicJwk(
            Assert.IsType<JsonObject>(JsonNode.Parse(exported.Jwk.AsElement().GetRawText()))
        );
        var payload = new Dictionary<string, object> { ["sub"] = "alice" };

        var token = Sign(privateKey, Es256K, payload);
        var decoded = Services.JoseSerializer.Decode(token, publicKey);

        Assert.Equal(JsonSerializer.Serialize(payload), decoded);
        var header = token[..token.IndexOf('.', StringComparison.Ordinal)];
        Assert.Contains(
            "\"alg\":\"ES256K\"",
            SecureEncoding.UTF8.GetString(Base64Url.DecodeFromChars(header)),
            StringComparison.Ordinal
        );
    }

    [Secp256k1Fact]
    public void TrySign_WritesTheFixedSizeSignatureOfRfc7518()
    {
        var privateKey = Services.GenerateKey("secp256k1");
        var signature = new byte[Es256K.GetSignatureSizeBytes(privateKey.KeySizeBits)];

        Assert.True(Es256K.TrySign(privateKey, "data"u8, signature, out var bytesWritten));

        Assert.Equal(64, bytesWritten);
        Assert.True(Es256K.Verify(privateKey, "data"u8, signature));
    }

    [Secp256k1Fact]
    public void Decode_WhenJwsWasSignedByAnIndependentImplementation_ReturnsThePayload()
    {
        var publicKey = Services.ImportPublicJwk(KnownPublicJwk());

        var decoded = Services.JoseSerializer.Decode(KnownJws, publicKey);

        Assert.Equal(KnownPayload, decoded);
    }

    [Secp256k1Fact]
    public void Decode_WhenKnownJwsPayloadIsAltered_Throws()
    {
        var publicKey = Services.ImportPublicJwk(KnownPublicJwk());
        var parts = KnownJws.Split('.');
        var altered = Base64Url.EncodeToString(
            SecureEncoding.UTF8.GetBytes("""{"iss":"https://issuer.example","sub":"mallory"}""")
        );

        Assert.Throws<JoseIntegrityException>(() =>
            Services.JoseSerializer.Decode($"{parts[0]}.{altered}.{parts[2]}", publicKey)
        );
    }

    [Secp256k1Fact]
    public void Decode_WithTheKnownPrivateKey_VerifiesItsOwnSignature()
    {
        var privateKey = Services.CreatePrivateKey(
            new ECParameters
            {
                Curve = Secp256k1Curve.Specification.Curve,
                D = Base64Url.DecodeFromChars(KnownD),
                Q = new ECPoint
                {
                    X = Base64Url.DecodeFromChars(KnownX),
                    Y = Base64Url.DecodeFromChars(KnownY),
                },
            }
        );
        var publicKey = Services.ImportPublicJwk(KnownPublicJwk());
        var payload = new Dictionary<string, object> { ["sub"] = "alice" };

        var token = Sign(privateKey, Es256K, payload);

        Assert.Equal(
            JsonSerializer.Serialize(payload),
            Services.JoseSerializer.Decode(token, publicKey)
        );
    }

    #endregion

    #region Curve binding

    [Secp256k1Fact]
    public void IsKeyCompatible_Es256KAcceptsOnlySecp256k1Keys()
    {
        // Both keys are 256 bits, the only size ES256K accepts, so only the curve tells them apart.
        var secp256k1Key = Services.GenerateKey("secp256k1");
        var p256Key = Services.GenerateKey("P-256");
        Assert.Equal(p256Key.KeySizeBits, secp256k1Key.KeySizeBits);

        Assert.True(Es256K.IsKeyCompatible(secp256k1Key));
        Assert.False(Es256K.IsKeyCompatible(p256Key));
    }

    [Secp256k1Fact]
    public void IsKeyCompatible_Es256RefusesASecp256k1Key()
    {
        var secp256k1Key = Services.GenerateKey("secp256k1");
        var p256Key = Services.GenerateKey("P-256");

        Assert.True(Es256.IsKeyCompatible(p256Key));
        Assert.False(Es256.IsKeyCompatible(secp256k1Key));
    }

    [Secp256k1Fact]
    public void TrySign_WhenEs256KIsGivenAP256Key_Throws()
    {
        var p256Key = Services.GenerateKey("P-256");
        var signature = new byte[64];

        Assert.Throws<ArgumentException>(() => Es256K.TrySign(p256Key, "data"u8, signature, out _));
    }

    [Secp256k1Fact]
    public void TrySign_WhenEs256IsGivenASecp256k1Key_Throws()
    {
        var secp256k1Key = Services.GenerateKey("secp256k1");
        var signature = new byte[64];

        Assert.Throws<ArgumentException>(() =>
            Es256.TrySign(secp256k1Key, "data"u8, signature, out _)
        );
    }

    [Secp256k1Fact]
    public void Decode_WhenEs256KJwsIsVerifiedWithAP256Key_Throws()
    {
        var p256Key = Services.GenerateKey("P-256");

        Assert.Throws<ArgumentException>(() => Services.JoseSerializer.Decode(KnownJws, p256Key));
    }

    [Secp256k1Fact]
    public void KeyTypes_NamesTheSecp256k1Curve()
    {
        var keyType = Assert.Single(Es256K.KeyTypes);

        Assert.Equal(typeof(EccSecretKey), keyType.KeyType);
        Assert.Equal("secp256k1", keyType.Curve);
        Assert.True(keyType.IsLegalSize(256));
    }

    [Secp256k1Fact]
    public void GetTokenHashFunction_HashesWithSha256()
    {
        var secp256k1Key = Services.GenerateKey("secp256k1");

        Es256K.GetTokenHashFunction(secp256k1Key, out var hashSizeBytes);

        Assert.Equal(32, hashSizeBytes);
    }

    #endregion

    #region JWK

    [Secp256k1Fact]
    public void ExportPublicKey_WritesCrvSecp256k1()
    {
        var privateKey = Services.GenerateKey("secp256k1");

        var exported = Services.Descriptor.ExportPublicKey(privateKey);

        Assert.NotNull(exported);
        var jwk = exported.Jwk.AsElement();
        Assert.Equal("EC", jwk.GetProperty("kty").GetString());
        Assert.Equal("secp256k1", jwk.GetProperty("crv").GetString());
        Assert.Equal(32, Base64Url.DecodeFromChars(jwk.GetProperty("x").GetString()).Length);
        Assert.Equal(32, Base64Url.DecodeFromChars(jwk.GetProperty("y").GetString()).Length);
        Assert.False(jwk.TryGetProperty("d", out _));
    }

    [Secp256k1Fact]
    public void ImportPublicJwk_ThenExport_RoundTripsTheKnownKey()
    {
        var publicKey = Services.ImportPublicJwk(KnownPublicJwk());

        var exported = Services.Descriptor.ExportPublicKey(publicKey);

        Assert.NotNull(exported);
        var jwk = exported.Jwk.AsElement();
        Assert.Equal("secp256k1", jwk.GetProperty("crv").GetString());
        Assert.Equal(KnownX, jwk.GetProperty("x").GetString());
        Assert.Equal(KnownY, jwk.GetProperty("y").GetString());
        Assert.True(Es256K.IsKeyCompatible(publicKey));
    }

    [Secp256k1Fact]
    public void ImportPublicJwk_WhenPointIsNotOnSecp256k1_Throws()
    {
        // The known point is on secp256k1; with y changed it lies on no curve the JWK names.
        var jwk = KnownPublicJwk();
        jwk["y"] = KnownX;

        Assert.Throws<ArgumentException>(() => Services.ImportPublicJwk(jwk));
    }

    [Secp256k1Fact]
    public void GenerateKey_WithoutACurve_SelectsP256BySize()
    {
        var key = Services.GenerateKey(curve: null);

        Assert.True(Es256.IsKeyCompatible(key));
        Assert.False(Es256K.IsKeyCompatible(key));
    }

    #endregion
}
