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
    // and the signature is ECDSA with SHA-256 over the JWS signing input, encoded as R || S (RFC 7518 §3.4). The script
    // below prints x, y and d, then a JWS; ECDSA signing is randomized, so a rerun prints another valid signature over
    // the same signing input.
    //
    //   import base64, hashlib, json
    //   from cryptography.hazmat.primitives import hashes
    //   from cryptography.hazmat.primitives.asymmetric import ec
    //   from cryptography.hazmat.primitives.asymmetric.utils import decode_dss_signature
    //   b64u = lambda b: base64.urlsafe_b64encode(b).rstrip(b"=").decode()
    //   compact = lambda o: b64u(json.dumps(o, separators=(",", ":")).encode())
    //   n = 0xFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFEBAAEDCE6AF48A03BBFD25E8CD0364141
    //   d = int.from_bytes(hashlib.sha256(b"NCode.Identity ES256K test key").digest(), "big") % n
    //   key = ec.derive_private_key(d, ec.SECP256K1())
    //   pub = key.public_key().public_numbers()
    //   print(b64u(pub.x.to_bytes(32, "big")), b64u(pub.y.to_bytes(32, "big")), b64u(d.to_bytes(32, "big")))
    //   header = compact({"alg": "ES256K", "kid": "secp256k1-test"})
    //   signing_input = header + "." + compact({"iss": "https://issuer.example", "sub": "alice"})
    //   r, s = decode_dss_signature(key.sign(signing_input.encode(), ec.ECDSA(hashes.SHA256())))
    //   print(signing_input + "." + b64u(r.to_bytes(32, "big") + s.to_bytes(32, "big")))
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

    #region Encode Tests

    [Secp256k1Fact]
    public void Encode_WhenSecp256k1Key_IsVerifiedWithItsPublicJwk()
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

    #endregion

    #region TrySign Tests

    [Secp256k1Fact]
    public void TrySign_WhenSecp256k1Key_WritesTheFixedSizeSignatureOfRfc7518()
    {
        var privateKey = Services.GenerateKey("secp256k1");
        var signature = new byte[Es256K.GetSignatureSizeBytes(privateKey.KeySizeBits)];

        Assert.True(Es256K.TrySign(privateKey, "data"u8, signature, out var bytesWritten));

        Assert.Equal(64, bytesWritten);
        Assert.True(Es256K.Verify(privateKey, "data"u8, signature));
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

    #endregion

    #region Decode Tests

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
    public void Decode_WhenSignedWithTheKnownPrivateKey_VerifiesWithItsPublicKey()
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

    [Secp256k1Fact]
    public void Decode_WhenEs256KJwsIsVerifiedWithAP256Key_Throws()
    {
        var p256Key = Services.GenerateKey("P-256");

        Assert.Throws<ArgumentException>(() => Services.JoseSerializer.Decode(KnownJws, p256Key));
    }

    #endregion

    #region IsKeyCompatible Tests

    [Secp256k1Fact]
    public void IsKeyCompatible_WhenEs256KAndSecp256k1Key_ReturnsTrue()
    {
        var secp256k1Key = Services.GenerateKey("secp256k1");

        Assert.True(Es256K.IsKeyCompatible(secp256k1Key));
    }

    [Secp256k1Fact]
    public void IsKeyCompatible_WhenEs256KAndP256KeyOfTheSameSize_ReturnsFalse()
    {
        // The key is 256 bits, the only size ES256K accepts, so only the curve tells it apart.
        var p256Key = Services.GenerateKey("P-256");

        Assert.Equal(256, p256Key.KeySizeBits);
        Assert.False(Es256K.IsKeyCompatible(p256Key));
    }

    [Secp256k1Fact]
    public void IsKeyCompatible_WhenEs256AndP256Key_ReturnsTrue()
    {
        var p256Key = Services.GenerateKey("P-256");

        Assert.True(Es256.IsKeyCompatible(p256Key));
    }

    [Secp256k1Fact]
    public void IsKeyCompatible_WhenEs256AndSecp256k1Key_ReturnsFalse()
    {
        var secp256k1Key = Services.GenerateKey("secp256k1");

        Assert.False(Es256.IsKeyCompatible(secp256k1Key));
    }

    #endregion

    #region KeyTypes Property Tests

    [Secp256k1Fact]
    public void KeyTypes_WhenEs256K_NamesTheSecp256k1Curve()
    {
        var keyType = Assert.Single(Es256K.KeyTypes);

        Assert.Equal(typeof(EccSecretKey), keyType.KeyClrType);
        var curve = Assert.Single(keyType.Curves);
        Assert.Equal("secp256k1", curve.Name);
        Assert.Equal(256, curve.KeySizeBits);
        Assert.True(keyType.IsLegalSize(256));
    }

    #endregion

    #region GetTokenHashFunction Tests

    [Secp256k1Fact]
    public void GetTokenHashFunction_WhenSecp256k1Key_HashesWithSha256()
    {
        var secp256k1Key = Services.GenerateKey("secp256k1");

        Es256K.GetTokenHashFunction(secp256k1Key, out var hashSizeBytes);

        Assert.Equal(32, hashSizeBytes);
    }

    #endregion

    #region ExportPublicKey Tests

    [Secp256k1Fact]
    public void ExportPublicKey_WhenSecp256k1Key_WritesCrvSecp256k1()
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
    public void ExportPublicKey_WhenKnownKeyWasImported_RoundTripsItsJwk()
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

    #endregion

    #region ImportPublicKeyMaterial Tests

    [Secp256k1Fact]
    public void ImportPublicKeyMaterial_WhenPointIsNotOnSecp256k1_Throws()
    {
        // The known point is on secp256k1; with y changed it lies on no curve the JWK names.
        var jwk = KnownPublicJwk();
        jwk["y"] = KnownX;

        Assert.Throws<ArgumentException>(() => Services.ImportPublicJwk(jwk));
    }

    #endregion

    #region GenerateKeyMaterial Tests

    [Secp256k1Fact]
    public void GenerateKeyMaterial_WhenNoCurve_SelectsP256BySize()
    {
        var key = Services.GenerateKey(curve: null);

        Assert.True(Es256.IsKeyCompatible(key));
        Assert.False(Es256K.IsKeyCompatible(key));
    }

    #endregion
}
