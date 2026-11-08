// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using NCode.Identity.Jose.Algorithms;
using NCode.Identity.Jose.Credentials;
using NCode.Identity.Jose.EdDsa.Algorithms;
using NCode.Identity.Jose.EdDsa.Keys;
using NCode.Identity.Jose.EdDsa.KeyTypes;
using NCode.Identity.Jose.Exceptions;
using NCode.Identity.Secrets;
using NCode.Identity.Secrets.KeyMaterial;
using NCode.Identity.Secrets.Keys;
using NCode.Identity.Secrets.KeyTypes;
using Xunit;
using Base64Url = System.Buffers.Text.Base64Url;

namespace NCode.Identity.Jose.EdDsa.Tests.Algorithms;

/// <summary>
/// Composes the core <c>ECDH-ES</c> algorithms with the opt-in package's X25519 and X448 agreement (RFC 8037 §3.2):
/// the RFC 8037 Appendix A.6 example, a JSON Web Encryption (JWE) round trip for each curve and key wrap, and the
/// refusals of a degenerate shared secret, of an <c>epk</c> that carries a private member, and of a key whose curve is
/// for the other use.
/// </summary>
// See ADR-1390.
public sealed class OkpEcdhKeyAgreementTests : IAsyncDisposable
{
    private const string Enc = "A128GCM";

    // RFC 8037 Appendix A.6, which reuses the keys of RFC 7748 §6.1: Bob is the recipient, and Alice's key is the
    // ephemeral key.
    private const string BobPrivateKey = "XasIfmJKikt54X-Lg4AO5m87sSkmGLb9HC-LJ_-I4Os";
    private const string BobPublicKey = "3p7bfXt9wbTTW2HC7OQ1Nz-DQ8hbeGdNrfx-FG-IK08";
    private const string EphemeralPublicKey = "hSDwCYkwp1R0i33ctD73Wg2_Og0mOBr066SpjqqbTmo";

    private ServiceProvider ServiceProvider { get; }
    private ISecretKeyTypeDescriptor Descriptor { get; }
    private IAlgorithmCollection AlgorithmCollection { get; }
    private IJoseSerializer JoseSerializer { get; }

    public OkpEcdhKeyAgreementTests()
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddSecretsLibrary().AddJoseLibrary().AddEdDsa();
        ServiceProvider = serviceCollection.BuildServiceProvider();

        var catalog = ServiceProvider.GetRequiredService<ISecretKeyTypeCatalog>();
        Assert.True(catalog.TryGet(OkpSecretKey.TypeName, out var descriptor));
        Descriptor = descriptor;
        AlgorithmCollection = ServiceProvider
            .GetRequiredService<IAlgorithmCollectionProvider>()
            .Collection;
        JoseSerializer = ServiceProvider.GetRequiredService<IJoseSerializer>();
    }

    public async ValueTask DisposeAsync()
    {
        await ServiceProvider.DisposeAsync();
    }

    #region Scaffolding

    private KeyManagementAlgorithm GetAlgorithm(string code)
    {
        Assert.True(AlgorithmCollection.TryGetKeyManagementAlgorithm(code, out var algorithm));
        return algorithm;
    }

    private OkpSecretKey GenerateKey(string curve, int keySizeBits)
    {
        byte[] keyMaterial = [];
        var generated = Descriptor.GenerateKeyMaterial(
            new SecretKeyGenerationParameters { KeySizeBits = keySizeBits, Curve = curve },
            bytes =>
            {
                keyMaterial = bytes.ToArray();
                return string.Empty;
            }
        );
        var secretKey = Descriptor.DeserializeKeyMaterial(
            new KeyMetadata { KeyId = "recipient", Use = SecretKeyUses.Encryption },
            generated.SecretEncoding,
            keyMaterial
        );
        return Assert.IsType<OkpSecretKey>(secretKey, exactMatch: false);
    }

    private OkpSecretKey CreatePrivateKey(OkpCurve curve, string privateKey) =>
        Assert.IsType<OkpSecretKey>(
            Descriptor.DeserializeKeyMaterial(
                new KeyMetadata { KeyId = "recipient" },
                SecretEncodings.Pkcs8,
                OkpKeyEncoder.BuildPkcs8(curve, Base64Url.DecodeFromChars(privateKey))
            ),
            exactMatch: false
        );

    private OkpSecretKey ImportPublicKey(string curve, string x)
    {
        var imported = Descriptor.ImportPublicKeyMaterial(
            new SecretKeyImportParameters
            {
                Format = SecretKeyImportFormats.Jwk,
                KeyMaterial = new JsonObject
                {
                    ["kty"] = "OKP",
                    ["crv"] = curve,
                    ["x"] = x,
                }.ToJsonString(),
            },
            static publicKey => Convert.ToBase64String(publicKey)
        );
        return Assert.IsType<OkpSecretKey>(
            Descriptor.DeserializeKeyMaterial(
                new KeyMetadata { KeyId = "recipient" },
                imported.SecretEncoding,
                Convert.FromBase64String(imported.ProtectedKeyMaterial)
            ),
            exactMatch: false
        );
    }

    private static JsonElement CreateHeader(string field, string value, JsonObject epk) =>
        new JsonObject { [field] = value, ["epk"] = epk }.Deserialize<JsonElement>();

    private static string UnsupportedCurveMessage(string curve) =>
        $"The OKP curve '{curve}' is not valid for the ECDH-ES algorithms; expected 'X25519' or 'X448'. "
        + "(Parameter 'recipientKey')";

    // An OKP key that declares a curve and a size of its own choosing, as a key of another package could.
    private sealed class StubOkpSecretKey(string curve, int keySizeBits)
        : OkpSecretKey(
            new KeyMetadata { KeyId = "stub" },
            curve,
            keySizeBits,
            new byte[keySizeBits / 8],
            certificateRawData: null
        )
    {
        public override bool HasPrivateKey => true;

        public override void ExportPrivateKey<TWriter>(ref TWriter destination) =>
            throw new NotSupportedException();
    }

    private OkpEcdhKeyAgreement KeyAgreement { get; } = new();

    #endregion

    #region GetSharedSecretSizeBytes Tests

    [Theory]
    [InlineData(OkpCurves.X25519Name, 256, 32)]
    [InlineData(OkpCurves.X448Name, 448, 56)]
    public void GetSharedSecretSizeBytes_WhenKeyAgreementCurve_ReturnsTheCurvesSize(
        string curve,
        int keySizeBits,
        int expected
    )
    {
        Assert.Equal(
            expected,
            KeyAgreement.GetSharedSecretSizeBytes(GenerateKey(curve, keySizeBits))
        );
    }

    // Ed25519 has X25519's size, so only the curve's use refuses it.
    [Fact]
    public void GetSharedSecretSizeBytes_WhenSigningCurveOfTheSameSize_Throws()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            KeyAgreement.GetSharedSecretSizeBytes(GenerateKey(OkpCurves.Ed25519Name, 256))
        );

        Assert.Equal("recipientKey", exception.ParamName);
        Assert.Equal(UnsupportedCurveMessage(OkpCurves.Ed25519Name), exception.Message);
    }

    // The key names X25519 but declares X448's size, which the agreement accepts for X448 keys only.
    [Fact]
    public void GetSharedSecretSizeBytes_WhenKeySizeIsNotItsCurves_Throws()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            KeyAgreement.GetSharedSecretSizeBytes(new StubOkpSecretKey(OkpCurves.X25519Name, 448))
        );

        Assert.Equal("recipientKey", exception.ParamName);
        Assert.Equal(UnsupportedCurveMessage(OkpCurves.X25519Name), exception.Message);
    }

    #endregion

    #region AgreeWithEphemeralKey Tests

    [Theory]
    [InlineData(OkpCurves.X25519Name, 256, 31)]
    [InlineData(OkpCurves.X448Name, 448, 32)]
    public void AgreeWithEphemeralKey_WhenSharedSecretHasTheWrongSize_ThrowsAndWritesNoEphemeralKey(
        string curve,
        int keySizeBits,
        int sharedSecretSize
    )
    {
        var ephemeralPublicKey = new ArrayBufferWriter<byte>();

        var exception = Assert.Throws<ArgumentException>(() =>
            KeyAgreement.AgreeWithEphemeralKey(
                GenerateKey(curve, keySizeBits),
                ephemeralPublicKey,
                new byte[sharedSecretSize]
            )
        );

        Assert.Equal("sharedSecret", exception.ParamName);
        Assert.Equal(
            $"The shared secret of an {curve} agreement is {keySizeBits / 8} bytes. (Parameter 'sharedSecret')",
            exception.Message
        );
        Assert.Equal(0, ephemeralPublicKey.WrittenCount);
    }

    #endregion

    #region KeyTypes Property Tests

    [Theory]
    [InlineData("ECDH-ES")]
    [InlineData("ECDH-ES+A128KW")]
    [InlineData("ECDH-ES+A192KW")]
    [InlineData("ECDH-ES+A256KW")]
    public void KeyTypes_WhenEdDsaPackageIsAdded_ListsOkpAfterEc(string code)
    {
        var algorithm = GetAlgorithm(code);

        Assert.Equal(
            [typeof(EccSecretKey), typeof(OkpSecretKey)],
            algorithm.KeyTypes.Select(keyType => keyType.KeyClrType)
        );
    }

    #endregion

    #region IsKeyCompatible Tests

    [Theory]
    [InlineData(OkpCurves.X25519Name, 256)]
    [InlineData(OkpCurves.X448Name, 448)]
    public void IsKeyCompatible_WhenKeyAgreementCurve_ReturnsTrue(string curve, int keySizeBits)
    {
        Assert.True(GetAlgorithm("ECDH-ES").IsKeyCompatible(GenerateKey(curve, keySizeBits)));
    }

    // Ed25519 has X25519's size, so only the curve's use refuses it.
    [Fact]
    public void IsKeyCompatible_WhenSigningCurveOfTheSameSize_ReturnsFalse()
    {
        var algorithm = GetAlgorithm("ECDH-ES");
        var secretKey = GenerateKey(OkpCurves.Ed25519Name, 256);

        Assert.True(algorithm.TryGetKeyType(secretKey.GetType(), out var keyType));
        Assert.True(keyType.IsLegalSize(secretKey.KeySizeBits));
        Assert.False(algorithm.IsKeyCompatible(secretKey));
    }

    [Theory]
    [InlineData("EdDSA", OkpCurves.X25519Name, 256)]
    [InlineData("Ed25519", OkpCurves.X25519Name, 256)]
    [InlineData("EdDSA", OkpCurves.X448Name, 448)]
    [InlineData("Ed448", OkpCurves.X448Name, 448)]
    public void IsKeyCompatible_WhenSignatureAlgorithmIsGivenAKeyAgreementKey_ReturnsFalse(
        string code,
        string curve,
        int keySizeBits
    )
    {
        Assert.True(AlgorithmCollection.TryGetSignatureAlgorithm(code, out var algorithm));

        Assert.False(algorithm.IsKeyCompatible(GenerateKey(curve, keySizeBits)));
    }

    #endregion

    #region Agree Tests

    [Fact]
    public void Agree_WhenRfc8037AppendixA6_ReturnsTheSharedSecret()
    {
        var recipientKey = CreatePrivateKey(OkpCurves.X25519, BobPrivateKey);
        var keyAgreement = ServiceProvider
            .GetServices<IEcdhKeyAgreement>()
            .Single(candidate => candidate.AlgorithmKeyType.IsKeyCompatible(recipientKey));
        var sharedSecret = new byte[keyAgreement.GetSharedSecretSizeBytes(recipientKey)];

        keyAgreement.Agree(
            recipientKey,
            ImportPublicKey(OkpCurves.X25519Name, EphemeralPublicKey),
            sharedSecret
        );

        Assert.Equal(
            Convert.FromHexString(
                "4a5d9d5ba4ce2de1728e3bf480350f25e07e21c947d19e3376f09b3c1e161742"
            ),
            sharedSecret
        );
    }

    [Fact]
    public void Agree_WhenSharedSecretHasTheWrongSize_Throws()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            KeyAgreement.Agree(
                CreatePrivateKey(OkpCurves.X25519, BobPrivateKey),
                ImportPublicKey(OkpCurves.X25519Name, EphemeralPublicKey),
                new byte[56]
            )
        );

        Assert.Equal("sharedSecret", exception.ParamName);
    }

    // The RFC 8037 Appendix A.6 agreement, with Bob's public key in place of his private key.
    [Fact]
    public void Agree_WhenRecipientKeyHoldsOnlyItsPublicHalf_Throws()
    {
        var sharedSecret = new byte[32];

        var exception = Assert.Throws<InvalidOperationException>(() =>
            KeyAgreement.Agree(
                ImportPublicKey(OkpCurves.X25519Name, BobPublicKey),
                ImportPublicKey(OkpCurves.X25519Name, EphemeralPublicKey),
                sharedSecret
            )
        );

        Assert.Equal(
            "The OKP secret key holds only the public half and cannot agree keys as a recipient.",
            exception.Message
        );
        Assert.All(sharedSecret, value => Assert.Equal(0, value));
    }

    [Fact]
    public void Agree_WhenRecipientKeyIsOnASigningCurve_Throws()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            KeyAgreement.Agree(
                GenerateKey(OkpCurves.Ed25519Name, 256),
                ImportPublicKey(OkpCurves.X25519Name, EphemeralPublicKey),
                new byte[32]
            )
        );

        Assert.Equal("recipientKey", exception.ParamName);
        Assert.Equal(UnsupportedCurveMessage(OkpCurves.Ed25519Name), exception.Message);
    }

    #endregion

    #region ExportPublicKey Tests

    [Fact]
    public void ExportPublicKey_WhenRfc8037AppendixA6RecipientKey_WritesBobsPublicKey()
    {
        var exported = Descriptor.ExportPublicKey(
            CreatePrivateKey(OkpCurves.X25519, BobPrivateKey)
        );

        Assert.NotNull(exported);
        Assert.Equal(BobPublicKey, exported.Jwk.AsElement().GetProperty("x").GetString());
    }

    #endregion

    #region DeserializeKeyMaterial Tests

    // RFC 8410 publishes no X25519 private key, so the input is the bytes of its §10.3 Ed25519 private key wrapped under
    // the X25519 OID (1.3.101.110) instead of the Ed25519 one; the X25519 public key of those bytes was computed
    // independently of this library. It is not a published conformance vector.
    [Fact]
    public void DeserializeKeyMaterial_WhenX25519Pkcs8_DerivesItsPublicKey()
    {
        var secretKey = Assert.IsType<OkpSecretKey>(
            Descriptor.DeserializeKeyMaterial(
                default,
                SecretEncodings.Pkcs8,
                Convert.FromBase64String(
                    "MC4CAQAwBQYDK2VuBCIEINTuctv5E1hK1bbY8fdp+K06/nwoy/HU++CXqI9EdVhC"
                )
            ),
            exactMatch: false
        );
        var publicKey = new ArrayBufferWriter<byte>();
        secretKey.ExportPublicKey(ref publicKey);

        Assert.Equal(OkpCurves.X25519Name, secretKey.CurveName);
        Assert.Equal(256, secretKey.KeySizeBits);
        Assert.Equal(
            Convert.FromHexString(
                "226ad8f8f62b7b2637247e6b21ffb5723d6330c4d5dd5a8d11e7e7de65c1c501"
            ),
            publicKey.WrittenSpan.ToArray()
        );
    }

    #endregion

    #region NewKey Tests

    [Fact]
    public void NewKey_WhenSigningCurve_Throws()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            GetAlgorithm("ECDH-ES")
                .NewKey(
                    GenerateKey(OkpCurves.Ed25519Name, 256),
                    new Dictionary<string, object> { ["enc"] = Enc },
                    new byte[16]
                )
        );

        Assert.Equal("secretKey", exception.ParamName);
    }

    [Theory]
    [InlineData(OkpCurves.X25519Name, 256, 32)]
    [InlineData(OkpCurves.X448Name, 448, 56)]
    public void NewKey_WhenKeyAgreementCurve_WritesOnlyThePublicMembersOfTheEpk(
        string curve,
        int keySizeBits,
        int publicKeySize
    )
    {
        var header = new Dictionary<string, object> { ["enc"] = Enc };

        GetAlgorithm("ECDH-ES").NewKey(GenerateKey(curve, keySizeBits), header, new byte[16]);

        var epk = Assert.IsType<Dictionary<string, object>>(Assert.Contains("epk", header));
        Assert.Equal(["crv", "kty", "x"], epk.Keys.Order(StringComparer.Ordinal));
        Assert.Equal("OKP", epk["kty"]);
        Assert.Equal(curve, epk["crv"]);
        Assert.Equal(
            publicKeySize,
            Base64Url.DecodeFromChars(Assert.IsType<string>(epk["x"])).Length
        );
    }

    [Theory]
    [InlineData(OkpCurves.X25519Name, "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    [InlineData(
        OkpCurves.X448Name,
        "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA"
    )]
    public void NewKey_WhenRecipientKeyHasSmallOrder_Throws(string curve, string x)
    {
        var recipientKey = ImportPublicKey(curve, x);

        var exception = Assert.Throws<JoseException>(() =>
            GetAlgorithm("ECDH-ES")
                .NewKey(
                    recipientKey,
                    new Dictionary<string, object> { ["enc"] = Enc },
                    new byte[16]
                )
        );

        Assert.Equal(
            "The recipient's public key is a point of small order, which yields an all-zero shared secret.",
            exception.Message
        );
    }

    #endregion

    #region TryUnwrapKey Tests

    // The content encryption key is the Concat KDF of RFC 7518 §4.6.2 over the RFC 8037 Appendix A.6 shared secret,
    // computed independently of this library.
    [Fact]
    public void TryUnwrapKey_WhenRfc8037AppendixA6DirectAgreement_DerivesTheContentKey()
    {
        var header = CreateHeader(
            "enc",
            Enc,
            new JsonObject
            {
                ["kty"] = "OKP",
                ["crv"] = "X25519",
                ["x"] = EphemeralPublicKey,
            }
        );
        var contentKey = new byte[16];

        var result = GetAlgorithm("ECDH-ES")
            .TryUnwrapKey(
                CreatePrivateKey(OkpCurves.X25519, BobPrivateKey),
                header,
                [],
                contentKey,
                out var bytesWritten
            );

        Assert.True(result);
        Assert.Equal(16, bytesWritten);
        Assert.Equal(Convert.FromHexString("ea3ff64f86e83c4e53d05e804aee7a5e"), contentKey);
    }

    // RFC 8037 Appendix A.6 uses ECDH-ES+A128KW; the wrapped key is the content key 00..0f wrapped with AES Key Wrap
    // (RFC 3394) under the Concat KDF output, both computed independently of this library.
    [Fact]
    public void TryUnwrapKey_WhenRfc8037AppendixA6KeyWrap_UnwrapsTheContentKey()
    {
        var header = CreateHeader(
            "alg",
            "ECDH-ES+A128KW",
            new JsonObject
            {
                ["kty"] = "OKP",
                ["crv"] = "X25519",
                ["x"] = EphemeralPublicKey,
            }
        );
        var contentKey = new byte[16];

        var result = GetAlgorithm("ECDH-ES+A128KW")
            .TryUnwrapKey(
                CreatePrivateKey(OkpCurves.X25519, BobPrivateKey),
                header,
                Convert.FromHexString("108e0d21dd822b2a72d07362ed6eced57be9ba8608f7771c"),
                contentKey,
                out var bytesWritten
            );

        Assert.True(result);
        Assert.Equal(16, bytesWritten);
        Assert.Equal(Enumerable.Range(0, 16).Select(value => (byte)value), contentKey);
    }

    [Theory]
    [InlineData(OkpCurves.X25519Name, 256, "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    [InlineData(OkpCurves.X25519Name, 256, "AQAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    [InlineData(
        OkpCurves.X448Name,
        448,
        "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA"
    )]
    public void TryUnwrapKey_WhenEpkHasSmallOrder_Throws(string curve, int keySizeBits, string x)
    {
        var header = CreateHeader(
            "enc",
            Enc,
            new JsonObject
            {
                ["kty"] = "OKP",
                ["crv"] = curve,
                ["x"] = x,
            }
        );

        var exception = Assert.Throws<JoseException>(() =>
            GetAlgorithm("ECDH-ES")
                .TryUnwrapKey(GenerateKey(curve, keySizeBits), header, [], new byte[16], out _)
        );

        Assert.Equal(
            "The ephemeral public key is a point of small order, which yields an all-zero shared secret.",
            exception.Message
        );
    }

    [Fact]
    public void TryUnwrapKey_WhenEpkCarriesPrivateKey_Throws()
    {
        var header = CreateHeader(
            "enc",
            Enc,
            new JsonObject
            {
                ["kty"] = "OKP",
                ["crv"] = "X25519",
                ["x"] = EphemeralPublicKey,
                ["d"] = "dwdtCnMYpX08FsFyUbJmRd9ML4frwJkqsXf7pR25LCo",
            }
        );

        var exception = Assert.Throws<JoseException>(() =>
            GetAlgorithm("ECDH-ES")
                .TryUnwrapKey(
                    CreatePrivateKey(OkpCurves.X25519, BobPrivateKey),
                    header,
                    [],
                    new byte[16],
                    out _
                )
        );

        Assert.Equal(
            "The 'epk' header must contain only public key parameters, but it contains 'd'.",
            exception.Message
        );
    }

    // A signing curve's key and a key on the other key-agreement curve are both well-formed OKP keys.
    [Theory]
    [InlineData(OkpCurves.Ed25519Name, 256)]
    [InlineData(OkpCurves.X448Name, 448)]
    public void TryUnwrapKey_WhenEpkIsOnAnotherCurve_Throws(string epkCurve, int epkKeySizeBits)
    {
        var epkKey = Descriptor.ExportPublicKey(GenerateKey(epkCurve, epkKeySizeBits));
        Assert.NotNull(epkKey);
        var header = CreateHeader(
            "enc",
            Enc,
            new JsonObject
            {
                ["kty"] = "OKP",
                ["crv"] = epkCurve,
                ["x"] = epkKey.Jwk.AsElement().GetProperty("x").GetString(),
            }
        );

        var exception = Assert.Throws<JoseException>(() =>
            GetAlgorithm("ECDH-ES")
                .TryUnwrapKey(
                    GenerateKey(OkpCurves.X25519Name, 256),
                    header,
                    [],
                    new byte[16],
                    out _
                )
        );

        Assert.Equal(
            "The ephemeral public key is not on the curve of the recipient's key.",
            exception.Message
        );
    }

    [Theory]
    [InlineData("ECDH-ES", "enc", OkpCurves.X25519Name, 256)]
    [InlineData("ECDH-ES+A128KW", "alg", OkpCurves.X25519Name, 256)]
    [InlineData("ECDH-ES", "enc", OkpCurves.X448Name, 448)]
    [InlineData("ECDH-ES+A256KW", "alg", OkpCurves.X448Name, 448)]
    public void TryUnwrapKey_WhenSenderAgreed_ReturnsTheSameKey(
        string code,
        string field,
        string curve,
        int keySizeBits
    )
    {
        var algorithm = GetAlgorithm(code);
        var recipientKey = GenerateKey(curve, keySizeBits);
        var header = new Dictionary<string, object> { [field] = field == "enc" ? Enc : code };
        var contentKey = new byte[16];
        var encryptedContentKey = new ArrayBufferWriter<byte>();
        algorithm.WrapNewKey(recipientKey, header, contentKey, encryptedContentKey);

        var unwrapped = new byte[16];
        var result = algorithm.TryUnwrapKey(
            recipientKey,
            JsonSerializer.SerializeToElement(header),
            encryptedContentKey.WrittenSpan,
            unwrapped,
            out var bytesWritten
        );

        Assert.True(result);
        Assert.Equal(16, bytesWritten);
        Assert.Equal(contentKey, unwrapped);
    }

    #endregion

    #region Decode Tests

    [Theory]
    [InlineData("ECDH-ES", OkpCurves.X25519Name, 256)]
    [InlineData("ECDH-ES+A128KW", OkpCurves.X25519Name, 256)]
    [InlineData("ECDH-ES+A192KW", OkpCurves.X25519Name, 256)]
    [InlineData("ECDH-ES+A256KW", OkpCurves.X25519Name, 256)]
    [InlineData("ECDH-ES", OkpCurves.X448Name, 448)]
    [InlineData("ECDH-ES+A128KW", OkpCurves.X448Name, 448)]
    [InlineData("ECDH-ES+A192KW", OkpCurves.X448Name, 448)]
    [InlineData("ECDH-ES+A256KW", OkpCurves.X448Name, 448)]
    public async Task Decode_WhenJweIsEncryptedToOkpKey_ReturnsThePayload(
        string code,
        string curve,
        int keySizeBits
    )
    {
        var privateKey = GenerateKey(curve, keySizeBits);
        var exported = Descriptor.ExportPublicKey(privateKey);
        Assert.NotNull(exported);
        var recipientKey = ImportPublicKey(
            curve,
            exported.Jwk.AsElement().GetProperty("x").GetString() ?? string.Empty
        );
        Assert.True(AlgorithmCollection.TryGetAuthenticatedEncryptionAlgorithm(Enc, out var enc));
        var options = new JoseEncryptionOptions(
            new JoseEncryptionCredentials(recipientKey, GetAlgorithm(code), enc)
        );
        var payload = new Dictionary<string, object> { ["sub"] = "alice" };

        var token = await JoseSerializer
            .CreateEncoder(options)
            .EncodeAsync(payload, CancellationToken.None);
        var decoded = JoseSerializer.Decode(JoseSerializer.Parse(token), privateKey);

        Assert.Equal(JsonSerializer.Serialize(payload), decoded);
    }

    // An Ed25519 key has the type and size of an X25519 key; configuring it to decrypt is the caller's error, reported as
    // such rather than as a failed authentication tag.
    [Fact]
    public async Task Decode_WhenDecryptionKeyIsOnASigningCurve_ThrowsTheConfigurationError()
    {
        var privateKey = GenerateKey(OkpCurves.X25519Name, 256);
        Assert.True(AlgorithmCollection.TryGetAuthenticatedEncryptionAlgorithm(Enc, out var enc));
        var options = new JoseEncryptionOptions(
            new JoseEncryptionCredentials(privateKey, GetAlgorithm("ECDH-ES"), enc)
        );
        var token = await JoseSerializer
            .CreateEncoder(options)
            .EncodeAsync(
                new Dictionary<string, object> { ["sub"] = "alice" },
                CancellationToken.None
            );
        var signingKey = GenerateKey(OkpCurves.Ed25519Name, 256);

        var exception = Assert.Throws<ArgumentException>(() =>
            JoseSerializer.Decode(JoseSerializer.Parse(token), signingKey)
        );

        Assert.Equal("secretKey", exception.ParamName);
        Assert.Contains("curve", exception.Message, StringComparison.Ordinal);
    }

    #endregion
}
