// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Jose;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NCode.Identity.Jose.Algorithms;
using NCode.Identity.Jose.Algorithms.KeyManagement;
using NCode.Identity.Jwk;
using NCode.Identity.Secrets;
using NCode.Identity.Secrets.KeyMaterial;
using NCode.Identity.Secrets.Keys;
using NCode.Identity.Secrets.KeyTypes;
using Base64Url = NCode.Encoders.Base64Url;
using JoseException = NCode.Identity.Jose.Exceptions.JoseException;

namespace NCode.Identity.Jose.Tests.Algorithms.KeyManagement;

public class EcdhKeyManagementAlgorithmTests : BaseTests
{
    private const string Enc = "A128GCM";

    private static EccCurveSpecification BrainpoolCurve { get; } =
        new()
        {
            CurveName = "brainpoolP256r1",
            CurveSizeBits = 256,
            Curve = ECCurve.NamedCurves.brainpoolP256r1,
        };

    private ServiceProvider ServiceProvider { get; }

    private ISecretKeyTypeCatalog SecretKeyTypeCatalog { get; }

    private EcdhKeyManagementAlgorithm Algorithm { get; }

    public EcdhKeyManagementAlgorithmTests()
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddSecretsLibrary().AddJoseLibrary();
        ServiceProvider = serviceCollection.BuildServiceProvider();

        SecretKeyTypeCatalog = ServiceProvider.GetRequiredService<ISecretKeyTypeCatalog>();
        Algorithm = new EcdhKeyManagementAlgorithm(
            SecretKeyTypeCatalog,
            [new EccEcdhKeyAgreement()]
        );
    }

    protected override async ValueTask DisposeAsyncCore()
    {
        await ServiceProvider.DisposeAsync();
        await base.DisposeAsyncCore();
    }

    private static ECCurve GetCurve(int curveSizeBits) =>
        curveSizeBits switch
        {
            256 => ECCurve.NamedCurves.nistP256,
            384 => ECCurve.NamedCurves.nistP384,
            521 => ECCurve.NamedCurves.nistP521,
            _ => throw new NotSupportedException(),
        };

    private static EccSecretKey CreateRecipientKey(int curveSizeBits)
    {
        using var key = ECDiffieHellman.Create(GetCurve(curveSizeBits));
        return SecretKeyFactory.CreateEcc(new KeyMetadata { KeyId = "recipient" }, key);
    }

    private static JsonObject CreateEpk(int curveSizeBits, bool includePrivateKey = false)
    {
        using var key = ECDiffieHellman.Create(GetCurve(curveSizeBits));
        var parameters = key.ExportParameters(includePrivateKey);
        var epk = new JsonObject
        {
            ["kty"] = "EC",
            ["crv"] = $"P-{curveSizeBits}",
            ["x"] = Base64Url.Encode(parameters.Q.X),
            ["y"] = Base64Url.Encode(parameters.Q.Y),
        };
        if (includePrivateKey)
        {
            epk["d"] = Base64Url.Encode(parameters.D);
        }

        return epk;
    }

    private JoseException ValidateHeaderForUnwrapThrows(SecretKey recipientKey, JsonObject header)
    {
        var headerForUnwrap = header.Deserialize<JsonElement>();
        return Assert.Throws<JoseException>(() =>
            Algorithm.ValidateHeaderForUnwrap(recipientKey, headerForUnwrap, out _, out _, out _)
        );
    }

    [Fact]
    public void Code_ReturnsEcdhEs()
    {
        Assert.Equal("ECDH-ES", Algorithm.Code);
    }

    [Fact]
    public void KeyTypes_ReturnsTheKeyTypeOfEachAgreement()
    {
        var keyType = Assert.Single(Algorithm.KeyTypes);
        Assert.Equal(typeof(EccSecretKey), keyType.KeyClrType);
        Assert.Collection(
            keyType.KeyBitSizes,
            size => Assert.Equal((256, 256, 0), (size.MinSize, size.MaxSize, size.SkipSize)),
            size => Assert.Equal((384, 384, 0), (size.MinSize, size.MaxSize, size.SkipSize)),
            size => Assert.Equal((521, 521, 0), (size.MinSize, size.MaxSize, size.SkipSize))
        );
        Assert.Equal(
            [("P-256", 256), ("P-384", 384), ("P-521", 521)],
            keyType.Curves.Select(curve => (curve.Name, curve.KeySizeBits))
        );
    }

    // An agreement over another EC curve, as a package would contribute one; it only decides which keys it accepts.
    private sealed class BrainpoolEcdhKeyAgreement : IEcdhKeyAgreement
    {
        public AlgorithmKeyType AlgorithmKeyType { get; } =
            AlgorithmKeyType.OnCurves(
                typeof(EccSecretKey),
                [new AlgorithmCurve(BrainpoolCurve.CurveName, BrainpoolCurve.CurveSizeBits)]
            );

        public int GetSharedSecretSizeBytes(SecretKey secretKey) => 32;

        public void AgreeWithEphemeralKey(
            SecretKey recipientKey,
            IBufferWriter<byte> ephemeralPublicKeyWriter,
            Span<byte> sharedSecret
        ) => throw new NotSupportedException();

        public void Agree(
            SecretKey recipientKey,
            SecretKey ephemeralPublicKey,
            Span<byte> sharedSecret
        ) => throw new NotSupportedException();
    }

    // An agreement over EC keys of one size, 256 bits unless named, that names no curve, so it accepts every curve of
    // that size.
    private sealed class SizedEccEcdhKeyAgreement(int keySizeBits = 256) : IEcdhKeyAgreement
    {
        public AlgorithmKeyType AlgorithmKeyType { get; } =
            new(
                typeof(EccSecretKey),
                [new KeySizes(minSize: keySizeBits, maxSize: keySizeBits, skipSize: 0)]
            );

        public int GetSharedSecretSizeBytes(SecretKey secretKey) => 32;

        public void AgreeWithEphemeralKey(
            SecretKey recipientKey,
            IBufferWriter<byte> ephemeralPublicKeyWriter,
            Span<byte> sharedSecret
        ) => throw new NotSupportedException();

        public void Agree(
            SecretKey recipientKey,
            SecretKey ephemeralPublicKey,
            Span<byte> sharedSecret
        ) => throw new NotSupportedException();
    }

    // A key on a curve the application registers in the elliptic-curve catalog, which names it.
    private static EccSecretKey CreateBrainpoolKey()
    {
        var factory = new DefaultSecretKeyFactory(
            new DefaultSecretKeyDataProtectorFactory(NullLoggerFactory.Instance),
            new DefaultEccCurveCatalog([BrainpoolCurve])
        );
        using var key = ECDiffieHellman.Create(BrainpoolCurve.Curve);
        return factory.CreateEcc(new KeyMetadata { KeyId = "brainpool" }, key);
    }

    [Fact]
    public void KeyTypes_WhenTwoAgreementsServeTheSameKeyType_MergesTheirSizesAndCurves()
    {
        var algorithm = new EcdhKeyManagementAlgorithm(
            SecretKeyTypeCatalog,
            [new BrainpoolEcdhKeyAgreement(), new EccEcdhKeyAgreement()]
        );

        var keyType = Assert.Single(algorithm.KeyTypes);
        Assert.Equal(typeof(EccSecretKey), keyType.KeyClrType);
        Assert.Equal(
            ["brainpoolP256r1", "P-256", "P-384", "P-521"],
            keyType.Curves.Select(curve => curve.Name)
        );
        Assert.True(keyType.IsLegalSize(521));
    }

    // A merged key type without curves would accept every curve of its sizes and list none.
    [Fact]
    public void Constructor_WhenAgreementsOfOneKeyTypeMixCurvesAndSizesOnly_Throws()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            new EcdhKeyManagementAlgorithm(
                SecretKeyTypeCatalog,
                [new EccEcdhKeyAgreement(), new SizedEccEcdhKeyAgreement()]
            )
        );

        Assert.Contains(
            typeof(EccSecretKey).FullName!,
            exception.Message,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void IsKeyCompatible_WhenAnotherAgreementServesTheKeysCurve_ReturnsTrue()
    {
        var algorithm = new EcdhKeyManagementAlgorithm(
            SecretKeyTypeCatalog,
            [new EccEcdhKeyAgreement(), new BrainpoolEcdhKeyAgreement()]
        );

        Assert.True(algorithm.IsKeyCompatible(CreateBrainpoolKey()));
    }

    [Fact]
    public void IsKeyCompatible_WhenNoAgreementServesTheKeysCurve_ReturnsFalse()
    {
        Assert.False(Algorithm.IsKeyCompatible(CreateBrainpoolKey()));
    }

    [Fact]
    public void IsKeyCompatible_WhenTwoAgreementsAcceptTheKey_ReturnsFalse()
    {
        var algorithm = new EcdhKeyManagementAlgorithm(
            SecretKeyTypeCatalog,
            [new EccEcdhKeyAgreement(), new EccEcdhKeyAgreement()]
        );

        Assert.False(algorithm.IsKeyCompatible(CreateRecipientKey(256)));
    }

    [Fact]
    public void NewKey_WhenTwoAgreementsAcceptTheKey_Throws()
    {
        var algorithm = new EcdhKeyManagementAlgorithm(
            SecretKeyTypeCatalog,
            [new EccEcdhKeyAgreement(), new EccEcdhKeyAgreement()]
        );

        var exception = Assert.Throws<ArgumentException>(() =>
            algorithm.NewKey(
                CreateRecipientKey(256),
                new Dictionary<string, object> { ["enc"] = Enc },
                new byte[16]
            )
        );

        Assert.Contains("both accept", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(256)]
    [InlineData(384)]
    [InlineData(521)]
    public void IsKeyCompatible_WhenEccKeyOnASupportedCurve_ReturnsTrue(int curveSizeBits)
    {
        Assert.True(Algorithm.IsKeyCompatible(CreateRecipientKey(curveSizeBits)));
    }

    [Fact]
    public void IsKeyCompatible_WhenKeyTypeHasNoAgreement_ReturnsFalse()
    {
        using var rsa = RSA.Create(2048);
        var secretKey = SecretKeyFactory.CreateRsa(new KeyMetadata { KeyId = "rsa" }, rsa);

        Assert.False(Algorithm.IsKeyCompatible(secretKey));
    }

    [Fact]
    public void NewKey_WhenKeyTypeHasNoAgreement_Throws()
    {
        using var rsa = RSA.Create(2048);
        var secretKey = SecretKeyFactory.CreateRsa(new KeyMetadata { KeyId = "rsa" }, rsa);
        var header = new Dictionary<string, object> { ["enc"] = Enc };

        var exception = Assert.Throws<ArgumentException>(() =>
            Algorithm.NewKey(secretKey, header, new byte[16])
        );

        Assert.Contains(
            "has no registered ECDH key agreement",
            exception.Message,
            StringComparison.Ordinal
        );
        Assert.DoesNotContain("epk", header.Keys);
    }

    [Theory]
    [InlineData(256)]
    [InlineData(384)]
    [InlineData(521)]
    public void NewKey_WritesOnlyTheEphemeralPublicKeyToTheHeader(int curveSizeBits)
    {
        var recipientKey = CreateRecipientKey(curveSizeBits);
        var header = new Dictionary<string, object> { ["enc"] = Enc };

        Algorithm.NewKey(recipientKey, header, new byte[16]);

        var epk = Assert.IsType<Dictionary<string, object>>(Assert.Contains("epk", header));

        // RFC 7518 §4.6.1.1: the ephemeral private key never leaves the sender, and the epk holds only the members the
        // EC key type requires.
        Assert.Equal(["crv", "kty", "x", "y"], epk.Keys.Order(StringComparer.Ordinal));
        Assert.Equal("EC", epk["kty"]);
        Assert.Equal($"P-{curveSizeBits}", epk["crv"]);

        var coordinateSizeBytes = (curveSizeBits + 7) >> 3;
        Assert.Equal(coordinateSizeBytes, Base64Url.Decode(Assert.IsType<string>(epk["x"])).Length);
        Assert.Equal(coordinateSizeBytes, Base64Url.Decode(Assert.IsType<string>(epk["y"])).Length);
    }

    [Fact]
    public void ValidateHeaderForUnwrap_WhenTheEphemeralKeyCarriesItsPrivateKey_Throws()
    {
        var header = new JsonObject
        {
            ["enc"] = Enc,
            ["epk"] = CreateEpk(256, includePrivateKey: true),
        };

        var exception = ValidateHeaderForUnwrapThrows(CreateRecipientKey(256), header);

        Assert.Equal(
            "The 'epk' header must contain only public key parameters, but it contains 'd'.",
            exception.Message
        );
    }

    [Theory]
    [InlineData("k")]
    [InlineData("p")]
    [InlineData("priv")]
    public void ValidateHeaderForUnwrap_WhenTheEphemeralKeyCarriesAnotherTypesPrivateMember_Throws(
        string privateMember
    )
    {
        var epk = CreateEpk(256);
        epk[privateMember] = Base64Url.Encode(new byte[32]);
        var header = new JsonObject { ["enc"] = Enc, ["epk"] = epk };

        var exception = ValidateHeaderForUnwrapThrows(CreateRecipientKey(256), header);

        Assert.Equal(
            "The 'epk' header must contain only public key parameters.",
            exception.Message
        );
    }

    [Fact]
    public void ValidateHeaderForUnwrap_WhenValidHeader_ReturnsTheEphemeralPublicKey()
    {
        var epk = CreateEpk(256);
        var apu = Base64Url.Encode(RandomNumberGenerator.GetBytes(32));
        var apv = Base64Url.Encode(RandomNumberGenerator.GetBytes(32));
        var header = new JsonObject
        {
            ["enc"] = Enc,
            ["epk"] = epk,
            ["apu"] = apu,
            ["apv"] = apv,
        };

        var ephemeralKey = Algorithm.ValidateHeaderForUnwrap(
            CreateRecipientKey(256),
            header.Deserialize<JsonElement>(),
            out var algorithm,
            out var actualApu,
            out var actualApv
        );

        Assert.Equal(Enc, algorithm);
        Assert.Equal(apu, actualApu);
        Assert.Equal(apv, actualApv);

        var eccKey = Assert.IsType<EccSecretKey>(ephemeralKey, exactMatch: false);
        using var ecdh = eccKey.ExportECDiffieHellman();
        var parameters = ecdh.ExportParameters(includePrivateParameters: false);
        var x = Assert.IsType<JsonValue>(epk["x"], exactMatch: false);
        var y = Assert.IsType<JsonValue>(epk["y"], exactMatch: false);
        Assert.Equal(x.GetValue<string>(), Base64Url.Encode(parameters.Q.X));
        Assert.Equal(y.GetValue<string>(), Base64Url.Encode(parameters.Q.Y));
    }

    [Fact]
    public void ValidateHeaderForUnwrap_IgnoresMembersTheKeyTypeDoesNotRequire()
    {
        var epk = CreateEpk(256);
        epk["kid"] = "ignored";
        epk["use"] = "enc";
        var header = new JsonObject { ["enc"] = Enc, ["epk"] = epk };

        var ephemeralKey = Algorithm.ValidateHeaderForUnwrap(
            CreateRecipientKey(256),
            header.Deserialize<JsonElement>(),
            out _,
            out _,
            out _
        );

        Assert.IsType<EccSecretKey>(ephemeralKey, exactMatch: false);
    }

    [Fact]
    public void ValidateHeaderForUnwrap_WhenMissingAlgorithm_Throws()
    {
        var exception = ValidateHeaderForUnwrapThrows(CreateRecipientKey(256), new JsonObject());

        Assert.Equal("The JWT header is missing the 'enc' field.", exception.Message);
    }

    [Fact]
    public void ValidateHeaderForUnwrap_WhenMissingKey_Throws()
    {
        var exception = ValidateHeaderForUnwrapThrows(
            CreateRecipientKey(256),
            new JsonObject { ["enc"] = Enc }
        );

        Assert.Equal("The JWT header is missing the 'epk' field.", exception.Message);
    }

    [Fact]
    public void ValidateHeaderForUnwrap_WhenKeyIsNotAnObject_Throws()
    {
        var exception = ValidateHeaderForUnwrapThrows(
            CreateRecipientKey(256),
            new JsonObject { ["enc"] = Enc, ["epk"] = "not-a-key" }
        );

        Assert.Equal("The 'epk' header must be a JSON object.", exception.Message);
    }

    [Fact]
    public void ValidateHeaderForUnwrap_WhenMissingKeyType_Throws()
    {
        var exception = ValidateHeaderForUnwrapThrows(
            CreateRecipientKey(256),
            new JsonObject { ["enc"] = Enc, ["epk"] = new JsonObject() }
        );

        Assert.Equal("The 'epk' header is missing the 'kty' field.", exception.Message);
    }

    [Fact]
    public void ValidateHeaderForUnwrap_WhenKeyTypeIsNotTheRecipientKeys_Throws()
    {
        var exception = ValidateHeaderForUnwrapThrows(
            CreateRecipientKey(256),
            new JsonObject
            {
                ["enc"] = Enc,
                ["epk"] = new JsonObject { ["kty"] = "OKP" },
            }
        );

        Assert.Equal("The 'kty' field was expected to be 'EC'.", exception.Message);
    }

    [Theory]
    [InlineData("crv")]
    [InlineData("x")]
    [InlineData("y")]
    public void ValidateHeaderForUnwrap_WhenMissingARequiredMember_Throws(string member)
    {
        var epk = CreateEpk(256);
        epk.Remove(member);

        var exception = ValidateHeaderForUnwrapThrows(
            CreateRecipientKey(256),
            new JsonObject { ["enc"] = Enc, ["epk"] = epk }
        );

        Assert.Equal($"The 'epk' header is missing the '{member}' field.", exception.Message);
    }

    [Fact]
    public void ValidateHeaderForUnwrap_WhenThePointIsNotOnTheCurve_Throws()
    {
        var epk = CreateEpk(256);
        var yMember = Assert.IsType<JsonValue>(epk["y"], exactMatch: false);
        var y = Base64Url.Decode(yMember.GetValue<string>());
        y[^1] ^= 0x01;
        epk["y"] = Base64Url.Encode(y);

        var exception = ValidateHeaderForUnwrapThrows(
            CreateRecipientKey(256),
            new JsonObject { ["enc"] = Enc, ["epk"] = epk }
        );

        Assert.Equal("The 'epk' header is not a valid 'EC' public key.", exception.Message);
    }

    [Fact]
    public void ValidateHeaderForUnwrap_WhenMissingApuAndApv_Succeeds()
    {
        var header = new JsonObject { ["enc"] = Enc, ["epk"] = CreateEpk(256) };

        Algorithm.ValidateHeaderForUnwrap(
            CreateRecipientKey(256),
            header.Deserialize<JsonElement>(),
            out var algorithm,
            out var apu,
            out var apv
        );

        Assert.Equal(Enc, algorithm);
        Assert.Null(apu);
        Assert.Null(apv);
    }

    [Fact]
    public void TryUnwrapKey_WhenTheEphemeralKeyIsOnAnotherCurve_Throws()
    {
        var header = new JsonObject { ["enc"] = Enc, ["epk"] = CreateEpk(384) };

        var exception = Assert.Throws<JoseException>(() =>
            Algorithm.TryUnwrapKey(
                CreateRecipientKey(256),
                header.Deserialize<JsonElement>(),
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

    [Fact]
    public void TryUnwrapKey_WhenApuIsNotBase64Url_Throws()
    {
        var header = new JsonObject
        {
            ["enc"] = Enc,
            ["epk"] = CreateEpk(256),
            ["apu"] = "not base64url!",
        };

        var exception = Assert.Throws<JoseException>(() =>
            Algorithm.TryUnwrapKey(
                CreateRecipientKey(256),
                header.Deserialize<JsonElement>(),
                [],
                new byte[16],
                out _
            )
        );

        Assert.Equal("The 'apu' header is not valid base64url.", exception.Message);
    }

    /// <summary>
    /// The example of RFC 7518 Appendix C: Bob's key, Alice's ephemeral key and the <c>apu</c> and <c>apv</c> of the
    /// example agree on the content encryption key <c>VqqN6vgjbSBcIijNcacQGg</c> for <c>A128GCM</c>.
    /// </summary>
    [Fact]
    public void TryUnwrapKey_WhenRfc7518AppendixCExample_DerivesTheExampleKey()
    {
        var bobParameters = new ECParameters
        {
            Curve = ECCurve.NamedCurves.nistP256,
            Q = new ECPoint
            {
                X = Base64Url.Decode("weNJy2HscCSM6AEDTDg04biOvhFhyyWvOHQfeF_PxMQ"),
                Y = Base64Url.Decode("e8lnCO-AlStT-NJVX-crhB7QRYhiix03illJOVAOyck"),
            },
            D = Base64Url.Decode("VEmDZpDXXK8p8N0Cndsxs924q6nS1RXFASRl6BfUqdw"),
        };
        using var bob = ECDiffieHellman.Create(bobParameters);
        var bobKey = SecretKeyFactory.CreateEcc(new KeyMetadata { KeyId = "bob" }, bob);

        var header = new JsonObject
        {
            ["alg"] = "ECDH-ES",
            ["enc"] = "A128GCM",
            ["apu"] = "QWxpY2U",
            ["apv"] = "Qm9i",
            ["epk"] = new JsonObject
            {
                ["kty"] = "EC",
                ["crv"] = "P-256",
                ["x"] = "gI0GAILBdu7T53akrFmMyGcsF3n5dO7MmwNBHKW5SV0",
                ["y"] = "SLW_xSffzlPWrHEVI30DHM_4egVwt3NQqeUD7nMFpps",
            },
        };

        var cek = new byte[16];
        var result = Algorithm.TryUnwrapKey(
            bobKey,
            header.Deserialize<JsonElement>(),
            [],
            cek,
            out var bytesWritten
        );

        Assert.True(result);
        Assert.Equal(16, bytesWritten);
        Assert.Equal("VqqN6vgjbSBcIijNcacQGg", Base64Url.Encode(cek));
    }

    [Fact]
    public void GetLegalCekByteSizes_ReturnsExpected()
    {
        var kekSizeBits = Random.Shared.Next();
        var results = Algorithm.GetLegalCekByteSizes(kekSizeBits);
        var result = Assert.Single(results);
        Assert.Equal(1, result.MinSize);
        Assert.Equal(int.MaxValue, result.MaxSize);
        Assert.Equal(1, result.SkipSize);
    }

    [Fact]
    public void GetEncryptedContentKeySizeBytes_ReturnsExpected()
    {
        var kekSizeBits = Random.Shared.Next();
        var cekSizeBytes = Random.Shared.Next();
        var result = Algorithm.GetEncryptedContentKeySizeBytes(kekSizeBits, cekSizeBytes);
        Assert.Equal(0, result);
    }

    [Fact]
    public void WrapKey_WhenCalled_Throws()
    {
        Assert.Throws<JoseException>(() =>
            Algorithm.WrapKey(null!, null!, Span<byte>.Empty, new ArrayBufferWriter<byte>())
        );
    }

    public static TheoryData<ECCurve, int> RoundTripTestData
    {
        get
        {
            var data = new TheoryData<ECCurve, int>();
            foreach (
                var curve in new[]
                {
                    ECCurve.NamedCurves.nistP256,
                    ECCurve.NamedCurves.nistP384,
                    ECCurve.NamedCurves.nistP521,
                }
            )
            foreach (var keySize in new[] { 16, 24, 32 })
                data.Add(curve, keySize);
            return data;
        }
    }

    [Theory]
    [MemberData(nameof(RoundTripTestData))]
    public void RoundTrip_EncryptsAndDecrypts(ECCurve curve, int keySizeBytes)
    {
        const string enc = "dir";

        var apu = RandomNumberGenerator.GetBytes(16);
        var apv = RandomNumberGenerator.GetBytes(32);

        // the sender agrees on a key from the recipient's public key and an ephemeral key of its own

        using var recipientKey = ECDiffieHellman.Create(curve);
        var recipientSecretKey = SecretKeyFactory.CreateEcc(
            new KeyMetadata { KeyId = "recipient" },
            recipientKey
        );

        var header = new Dictionary<string, object>
        {
            ["enc"] = enc,
            ["apu"] = Base64Url.Encode(apu),
            ["apv"] = Base64Url.Encode(apv),
        };

        var cek1 = new byte[keySizeBytes];
        Algorithm.NewKey(recipientSecretKey, header, cek1);

        // the recipient agrees on the same key from its private key and the ephemeral public key

        var cek2 = new byte[keySizeBytes];
        var result = Algorithm.TryUnwrapKey(
            recipientSecretKey,
            JsonSerializer.SerializeToElement(header),
            Array.Empty<byte>(),
            cek2,
            out var bytesWritten
        );

        Assert.True(result);
        Assert.Equal(keySizeBytes, bytesWritten);
        Assert.Equal(cek1, cek2);

        // control
        //
        // EcdhKeyManagementUnix is the BCL-backed (ECDiffieHellman) control implementation; despite the name it is the
        // cross-platform one jose-jwt uses on every platform for managed keys. The CNG-backed EcdhKeyManagementWin
        // delegates straight to it whenever the key is an ECDiffieHellman/ECDsa/Jwk, so driving it directly keeps the
        // same third-party oracle without a Windows-only key type.

        var keySizeBits = keySizeBytes << 3;
        var controlAlgorithm = new EcdhKeyManagementUnix(true);
        var controlResult = controlAlgorithm.Unwrap([], recipientKey, keySizeBits, header);
        Assert.Equal(controlResult, cek1);
    }

    #region Refusal Scaffolding

    private const string NoJsonWebKeyFormMessage =
        "The key type 'ecc' has no registered JSON Web Key form, so it cannot carry an ephemeral public key.";

    // An agreement over P-256 that writes an ephemeral public key its key type cannot read, as a faulty package could.
    private sealed class UnreadableEphemeralKeyAgreement : IEcdhKeyAgreement
    {
        public AlgorithmKeyType AlgorithmKeyType { get; } =
            AlgorithmKeyType.OnCurves(typeof(EccSecretKey), [new AlgorithmCurve("P-256", 256)]);

        public int GetSharedSecretSizeBytes(SecretKey secretKey) => 32;

        public void AgreeWithEphemeralKey(
            SecretKey recipientKey,
            IBufferWriter<byte> ephemeralPublicKeyWriter,
            Span<byte> sharedSecret
        ) => ephemeralPublicKeyWriter.Write<byte>([0x30, 0x03, 0x02, 0x01, 0x00]);

        public void Agree(
            SecretKey recipientKey,
            SecretKey ephemeralPublicKey,
            Span<byte> sharedSecret
        ) => throw new NotSupportedException();
    }

    // The algorithm over the real EC agreement, with a key type catalog that answers for the 'ecc' key type only.
    private EcdhKeyManagementAlgorithm CreateAlgorithmWithCatalog(
        bool registered,
        ISecretKeyTypeDescriptor? descriptor
    )
    {
        var mockCatalog = CreateStrictMock<ISecretKeyTypeCatalog>();
        mockCatalog
            .Setup(x => x.TryGet(SecretKeyTypes.Ecc, out descriptor))
            .Returns(registered)
            .Verifiable();
        return new EcdhKeyManagementAlgorithm(mockCatalog.Object, [new EccEcdhKeyAgreement()]);
    }

    #endregion

    #region KeyTypes Property Tests

    [Fact]
    public void KeyTypes_WhenTwoAgreementsOfOneKeyTypeNameOnlySizes_MergesTheirSizes()
    {
        var algorithm = new EcdhKeyManagementAlgorithm(
            SecretKeyTypeCatalog,
            [new SizedEccEcdhKeyAgreement(256), new SizedEccEcdhKeyAgreement(384)]
        );

        var keyType = Assert.Single(algorithm.KeyTypes);
        Assert.Equal(typeof(EccSecretKey), keyType.KeyClrType);
        Assert.Empty(keyType.Curves);
        Assert.Equal(
            [(256, 256, 0), (384, 384, 0)],
            keyType.KeyBitSizes.Select(size => (size.MinSize, size.MaxSize, size.SkipSize))
        );
    }

    #endregion

    #region SupportsKeyWrapping Property Tests

    // RFC 7518 §4.6: in direct key agreement mode the agreed key is the content encryption key, so nothing is wrapped.
    [Fact]
    public void SupportsKeyWrapping_WhenDirectKeyAgreement_ReturnsFalse()
    {
        Assert.False(Algorithm.SupportsKeyWrapping);
    }

    #endregion

    #region NewKey Tests

    // RFC 7518 §4.6.2: the Concat KDF's AlgorithmID is the 'enc' value in direct key agreement mode.
    [Fact]
    public void NewKey_WhenTheHeaderHasNoEnc_Throws()
    {
        var header = new Dictionary<string, object>();

        var exception = Assert.Throws<JoseException>(() =>
            Algorithm.NewKey(CreateRecipientKey(256), header, new byte[16])
        );

        Assert.Equal("The JWT header is missing the 'enc' field.", exception.Message);
        Assert.DoesNotContain("epk", header.Keys);
    }

    // The brainpool key has the size of P-256, so the agreement's curve, not its size, refuses it.
    [Fact]
    public void NewKey_WhenNoAgreementAcceptsTheKeysCurve_Throws()
    {
        var header = new Dictionary<string, object> { ["enc"] = Enc };

        var exception = Assert.Throws<ArgumentException>(() =>
            Algorithm.NewKey(CreateBrainpoolKey(), header, new byte[16])
        );

        Assert.Equal("secretKey", exception.ParamName);
        Assert.Equal(
            "The secret key's size or curve is not valid for this cryptographic algorithm. (Parameter 'secretKey')",
            exception.Message
        );
        Assert.DoesNotContain("epk", header.Keys);
    }

    [Fact]
    public void NewKey_WhenTheKeyTypeIsNotRegistered_Throws()
    {
        var algorithm = CreateAlgorithmWithCatalog(registered: false, descriptor: null);
        var header = new Dictionary<string, object> { ["enc"] = Enc };

        var exception = Assert.Throws<JoseException>(() =>
            algorithm.NewKey(CreateRecipientKey(256), header, new byte[16])
        );

        Assert.Equal(NoJsonWebKeyFormMessage, exception.Message);
        Assert.DoesNotContain("epk", header.Keys);
    }

    [Fact]
    public void NewKey_WhenTheKeyTypeHasNoJsonWebKeyType_Throws()
    {
        var mockDescriptor = CreateStrictMock<ISecretKeyTypeDescriptor>();
        mockDescriptor
            .Setup(x => x.JwkKeyType)
            .Returns((JsonWebKeyTypeDescriptor?)null)
            .Verifiable();
        var algorithm = CreateAlgorithmWithCatalog(registered: true, mockDescriptor.Object);
        var header = new Dictionary<string, object> { ["enc"] = Enc };

        var exception = Assert.Throws<JoseException>(() =>
            algorithm.NewKey(CreateRecipientKey(256), header, new byte[16])
        );

        Assert.Equal(NoJsonWebKeyFormMessage, exception.Message);
        Assert.DoesNotContain("epk", header.Keys);
    }

    // The recipient reads the epk as a JSON Web Key, so a key type that cannot import one cannot carry the epk.
    [Fact]
    public void NewKey_WhenTheKeyTypeCannotImportAJsonWebKey_Throws()
    {
        var mockDescriptor = CreateStrictMock<ISecretKeyTypeDescriptor>();
        mockDescriptor.Setup(x => x.JwkKeyType).Returns(JsonWebKeyTypeDescriptors.Ec).Verifiable();
        mockDescriptor
            .Setup(x => x.PublicKeyImportFormats)
            .Returns([SecretKeyImportFormats.Pem])
            .Verifiable();
        var algorithm = CreateAlgorithmWithCatalog(registered: true, mockDescriptor.Object);
        var header = new Dictionary<string, object> { ["enc"] = Enc };

        var exception = Assert.Throws<JoseException>(() =>
            algorithm.NewKey(CreateRecipientKey(256), header, new byte[16])
        );

        Assert.Equal(NoJsonWebKeyFormMessage, exception.Message);
        Assert.DoesNotContain("epk", header.Keys);
    }

    [Fact]
    public void NewKey_WhenTheAgreementWritesAnEphemeralKeyItsKeyTypeCannotRead_Throws()
    {
        var algorithm = new EcdhKeyManagementAlgorithm(
            SecretKeyTypeCatalog,
            [new UnreadableEphemeralKeyAgreement()]
        );
        var header = new Dictionary<string, object> { ["enc"] = Enc };

        var exception = Assert.Throws<JoseException>(() =>
            algorithm.NewKey(CreateRecipientKey(256), header, new byte[16])
        );

        Assert.Equal("The ephemeral public key cannot be read by its key type.", exception.Message);
        Assert.IsAssignableFrom<CryptographicException>(exception.InnerException);
        Assert.DoesNotContain("epk", header.Keys);
    }

    #endregion

    #region TryUnwrapKey Tests

    // A length of five characters leaves a single trailing character, which no base64url value has.
    [Theory]
    [InlineData("apu")]
    [InlineData("apv")]
    public void TryUnwrapKey_WhenPartyInfoHasAnInvalidLength_Throws(string name)
    {
        var header = new JsonObject
        {
            ["enc"] = Enc,
            ["epk"] = CreateEpk(256),
            [name] = "AAAAA",
        };

        var exception = Assert.Throws<JoseException>(() =>
            Algorithm.TryUnwrapKey(
                CreateRecipientKey(256),
                header.Deserialize<JsonElement>(),
                [],
                new byte[16],
                out _
            )
        );

        Assert.Equal($"The '{name}' header is not valid base64url.", exception.Message);
        Assert.IsType<FormatException>(exception.InnerException);
    }

    #endregion
}
