// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.DependencyInjection;
using NCode.Identity.Cose.Algorithms;
using NCode.Identity.Cose.Cbor;
using NCode.Identity.Cose.Keys;
using NCode.Identity.Cose.Options;
using NCode.Identity.Jose;
using NCode.Identity.Jose.Algorithms;
using NCode.Identity.Jose.EdDsa;
using NCode.Identity.Secrets;
using NCode.Identity.Secrets.KeyMaterial;
using NCode.Identity.Secrets.Keys;
using NCode.Identity.Secrets.KeyTypes;

namespace NCode.Identity.Cose.Tests.Keys;

public class DefaultCoseKeyConverterTests
{
    private static readonly CoseKeyTypeDescriptor[] KeyTypes =
    [
        CoseKeyTypeDescriptors.Okp,
        CoseKeyTypeDescriptors.Ec2,
        CoseKeyTypeDescriptors.Rsa,
        CoseKeyTypeDescriptors.Symmetric,
        CoseKeyTypeDescriptors.Akp,
    ];

    private static readonly CoseEllipticCurve[] Curves =
    [
        CoseEllipticCurves.P256,
        CoseEllipticCurves.P384,
        CoseEllipticCurves.P521,
        CoseEllipticCurves.X25519,
        CoseEllipticCurves.X448,
        CoseEllipticCurves.Ed25519,
        CoseEllipticCurves.Ed448,
        CoseEllipticCurves.Secp256k1,
    ];

    private static readonly ServiceProvider Provider = CreateProvider();

    private static ServiceProvider CreateProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSecretsLibrary().AddJoseLibrary().AddEdDsa();
        return services.BuildServiceProvider();
    }

    private static ISecretKeyFactory Factory => Provider.GetRequiredService<ISecretKeyFactory>();

    private static ISecretKeyTypeCatalog KeyTypeCatalog =>
        Provider.GetRequiredService<ISecretKeyTypeCatalog>();

    private static DefaultCoseKeyConverter CreateConverter()
    {
        var algorithms = new DefaultCoseAlgorithmCatalog(
            [
                CoseAlgorithms.Es256,
                CoseAlgorithms.Ed25519,
                CoseAlgorithms.MlDsa44,
                CoseAlgorithms.Ps256,
            ],
            Provider.GetRequiredService<IAlgorithmCollectionProvider>()
        );
        return new DefaultCoseKeyConverter(
            new DefaultCoseKeyTypeCatalog(KeyTypes, Curves),
            algorithms,
            KeyTypeCatalog,
            Microsoft.Extensions.Options.Options.Create(new CoseOptions())
        );
    }

    private static byte[] Bytes(string hex) => CoseTestKeys.Hex(hex);

    private static CoseValue Bstr(string hex) => CoseValue.FromByteString(Bytes(hex));

    private static byte[] EcMap(
        Action<Dictionary<CoseLabel, CoseValue>>? customize = null,
        bool includeKeyId = false
    )
    {
        var map = new Dictionary<CoseLabel, CoseValue>
        {
            [1] = CoseValue.FromInt64(2),
            [-1] = CoseValue.FromInt64(1),
            [-2] = Bstr(CoseTestKeys.EcP256X),
            [-3] = Bstr(CoseTestKeys.EcP256Y),
        };
        if (includeKeyId)
            map[2] = Bstr("3131");

        customize?.Invoke(map);
        return CborMapWriter.EncodeMap(map);
    }

    private static string ExportedMember(SecretKey key, string member)
    {
        Assert.True(KeyTypeCatalog.TryGet(key.KeyType, out var descriptor));
        var jwk = descriptor.ExportPublicKey(key)!.Jwk;
        return jwk.GetMember(member)!;
    }

    private static string FailureCodeOf(Action action) =>
        Assert.Throws<CoseException>(action).FailureCode;

    #region WritePublicKey and ReadPublicKey

    [Fact]
    public void WritePublicKey_P256Key_UsesTheDeterministicLabelOrder()
    {
        var converter = CreateConverter();
        var key = CoseTestKeys.CreateEcP256(
            Factory,
            CoseTestKeys.EcP256X,
            CoseTestKeys.EcP256Y,
            keyId: "11"
        );

        var encoded = converter.WritePublicKey(key);

        Assert.Equal(
            "A5010202423131200121"
                + "5820"
                + CoseTestKeys.EcP256X
                + "225820"
                + CoseTestKeys.EcP256Y,
            Convert.ToHexString(encoded)
        );
    }

    [Fact]
    public void ReadPublicKey_WrittenP256Key_RoundTrips()
    {
        var converter = CreateConverter();
        var key = CoseTestKeys.CreateEcP256(
            Factory,
            CoseTestKeys.EcP256X,
            CoseTestKeys.EcP256Y,
            CoseTestKeys.EcP256D,
            "11"
        );

        var read = converter.ReadPublicKey(converter.WritePublicKey(key));

        Assert.Equal("ecc", read.KeyType);
        Assert.Equal("11", read.KeyId);
        Assert.Equal(ExportedMember(key, "x"), ExportedMember(read, "x"));
        Assert.Equal(ExportedMember(key, "y"), ExportedMember(read, "y"));
    }

    [Fact]
    public void ReadPublicKey_WrittenEd25519Key_RoundTrips()
    {
        var converter = CreateConverter();
        var key = CoseTestKeys.CreateEd25519(KeyTypeCatalog, CoseTestKeys.Ed25519Seed, "11");

        var encoded = converter.WritePublicKey(key);
        var read = converter.ReadPublicKey(encoded);

        Assert.Equal(
            "A40101024231312006215820" + CoseTestKeys.Ed25519Public,
            Convert.ToHexString(encoded)
        );
        Assert.Equal("okp", read.KeyType);
        Assert.Equal("11", read.KeyId);
        Assert.Equal(ExportedMember(key, "x"), ExportedMember(read, "x"));
    }

    [Fact]
    public void ReadPublicKey_WrittenRsaKey_RoundTrips()
    {
        var converter = CreateConverter();
        var key = CoseTestKeys.CreateRsaPublic(Factory, CoseTestKeys.RsaN, CoseTestKeys.RsaE);

        var read = converter.ReadPublicKey(converter.WritePublicKey(key));

        Assert.Equal("rsa", read.KeyType);
        Assert.Equal(ExportedMember(key, "n"), ExportedMember(read, "n"));
        Assert.Equal(ExportedMember(key, "e"), ExportedMember(read, "e"));
    }

    [Fact]
    public void WritePublicKey_SymmetricKey_Throws()
    {
        var converter = CreateConverter();
        var key = CoseTestKeys.CreateSymmetric(Factory, CoseTestKeys.HmacKey);

        Assert.Throws<ArgumentException>(() => converter.WritePublicKey(key));
    }

    [Fact]
    public void ReadPublicKey_PrivateParameter_ThrowsInvalidKey()
    {
        var converter = CreateConverter();
        var encoded = EcMap(map => map[-4] = Bstr(CoseTestKeys.EcP256D));

        Assert.Equal(
            CoseFailureCodes.InvalidKey,
            FailureCodeOf(() => converter.ReadPublicKey(encoded))
        );
    }

    [Fact]
    public void ReadPublicKey_SymmetricKeyType_ThrowsInvalidKey()
    {
        var converter = CreateConverter();

        Assert.Equal(
            CoseFailureCodes.InvalidKey,
            FailureCodeOf(() => converter.ReadPublicKey(Bytes("A20104" + "2041" + "00")))
        );
    }

    [Fact]
    public void ReadPublicKey_TextKeyType_ThrowsUnsupportedKeyType()
    {
        var converter = CreateConverter();

        Assert.Equal(
            CoseFailureCodes.UnsupportedKeyType,
            FailureCodeOf(() => converter.ReadPublicKey(Bytes("A1016178")))
        );
    }

    [Fact]
    public void ReadPublicKey_UnregisteredCurve_ThrowsUnsupportedKeyType()
    {
        var converter = CreateConverter();
        var encoded = EcMap(map => map[-1] = CoseValue.FromInt64(99));

        Assert.Equal(
            CoseFailureCodes.UnsupportedKeyType,
            FailureCodeOf(() => converter.ReadPublicKey(encoded))
        );
    }

    [Fact]
    public void ReadPublicKey_CompressedPointSign_ThrowsInvalidKey()
    {
        var converter = CreateConverter();
        var encoded = EcMap(map => map[-3] = CoseValue.FromEncodedValue(Bytes("F5")));

        Assert.Equal(
            CoseFailureCodes.InvalidKey,
            FailureCodeOf(() => converter.ReadPublicKey(encoded))
        );
    }

    [Fact]
    public void ReadPublicKey_BaseIv_ThrowsInvalidKey()
    {
        var converter = CreateConverter();
        var encoded = EcMap(map => map[5] = Bstr("00"));

        Assert.Equal(
            CoseFailureCodes.InvalidKey,
            FailureCodeOf(() => converter.ReadPublicKey(encoded))
        );
    }

    [Fact]
    public void ReadPublicKey_BinaryKeyId_NamesTheKeyInDiagnosticNotation()
    {
        var converter = CreateConverter();
        var encoded = EcMap(map => map[2] = Bstr("FFFF"));

        var read = converter.ReadPublicKey(encoded);

        Assert.Equal("h'ffff'", read.KeyId);
    }

    [Fact]
    public void WritePublicKey_BinaryKeyId_WritesTheOriginalBytes()
    {
        var converter = CreateConverter();
        var read = converter.ReadPublicKey(EcMap(map => map[2] = Bstr("FFFF")));

        var written = converter.WritePublicKey(read);

        var map = CborGuard.ReadRootMap(written, new CoseOptions());
        Assert.True(map[2].TryGetByteString(out var kid));
        Assert.Equal(Bytes("FFFF"), kid.ToArray());
    }

    [Fact]
    public void ReadPublicKey_VerifyKeyOperation_SetsTheSignatureUse()
    {
        var converter = CreateConverter();
        var encoded = EcMap(map => map[4] = CoseValue.FromEncodedValue(Bytes("8102")));

        var key = converter.ReadPublicKey(encoded);

        Assert.Equal(SecretKeyUses.Signature, key.Metadata.Use);
    }

    [Fact]
    public void ReadPublicKey_MacKeyOperation_ThrowsInvalidKey()
    {
        var converter = CreateConverter();
        var encoded = EcMap(map => map[4] = CoseValue.FromEncodedValue(Bytes("810A")));

        Assert.Equal(
            CoseFailureCodes.InvalidKey,
            FailureCodeOf(() => converter.ReadPublicKey(encoded))
        );
    }

    [Fact]
    public void ReadPublicKey_Algorithm_SetsTheJoseAlgorithm()
    {
        var converter = CreateConverter();
        var encoded = EcMap(map => map[3] = CoseValue.FromInt64(-7));

        var key = converter.ReadPublicKey(encoded);

        Assert.Equal("ES256", key.Metadata.Algorithm);
    }

    [Fact]
    public void ReadPublicKey_UnknownLabel_IsIgnored()
    {
        var converter = CreateConverter();
        var encoded = EcMap(map => map[100] = CoseValue.FromInt64(1));

        var key = converter.ReadPublicKey(encoded);

        Assert.Equal("ecc", key.KeyType);
    }

    [Fact]
    public void ReadPublicKey_DuplicateLabel_ThrowsInvalidMessage()
    {
        var converter = CreateConverter();

        Assert.Equal(
            CoseFailureCodes.InvalidMessage,
            FailureCodeOf(() => converter.ReadPublicKey(Bytes("A201020102")))
        );
    }

    #endregion

    #region ReadPublicKeySet

    [Fact]
    public void ReadPublicKeySet_UnregisteredKeyType_SkipsThatKey()
    {
        var converter = CreateConverter();
        var set = new List<byte>(Bytes("82"));
        set.AddRange(Bytes("A10105"));
        set.AddRange(EcMap());

        var keys = converter.ReadPublicKeySet(set.ToArray());

        Assert.Single(keys);
        Assert.Equal("ecc", keys[0].KeyType);
    }

    [Fact]
    public void ReadPublicKeySet_BinaryKeyId_ReadsTheKey()
    {
        var converter = CreateConverter();
        var set = new List<byte>(Bytes("82"));
        set.AddRange(EcMap(map => map[2] = Bstr("A1B2C3D4E5F60718")));
        set.AddRange(EcMap(includeKeyId: true));

        var keys = converter.ReadPublicKeySet(set.ToArray());

        Assert.Equal(["h'a1b2c3d4e5f60718'", "11"], keys.Select(key => key.KeyId ?? "").ToArray());
    }

    [Fact]
    public void WritePublicKeySet_Keys_ReadsBack()
    {
        var converter = CreateConverter();
        var ec = CoseTestKeys.CreateEcP256(Factory, CoseTestKeys.EcP256X, CoseTestKeys.EcP256Y);
        var ed = CoseTestKeys.CreateEd25519(KeyTypeCatalog, CoseTestKeys.Ed25519Seed);

        var keys = converter.ReadPublicKeySet(converter.WritePublicKeySet([ec, ed]));

        Assert.Equal(["ecc", "okp"], keys.Select(key => key.KeyType).ToArray());
    }

    [Fact]
    public void ReadPublicKeySet_NotAnArray_ThrowsInvalidMessage()
    {
        var converter = CreateConverter();

        Assert.Equal(
            CoseFailureCodes.InvalidMessage,
            FailureCodeOf(() => converter.ReadPublicKeySet(Bytes("A0")))
        );
    }

    #endregion

    #region AKP

    [MlDsaFact]
    public void WritePublicKey_MlDsaKey_CarriesTheAlgorithm()
    {
        var converter = CreateConverter();
        Assert.True(KeyTypeCatalog.TryGet(SecretKeyTypes.Akp, out var descriptor));
        var generated = descriptor.GenerateKeyMaterial(
            new SecretKeyGenerationParameters { KeySizeBits = 1312 * 8, Curve = "ML-DSA-44" },
            bytes => Convert.ToBase64String(bytes)
        );
        var key = descriptor.DeserializeKeyMaterial(
            new KeyMetadata { KeyId = "k" },
            SecretEncodings.Pkcs8,
            Convert.FromBase64String(generated.ProtectedKeyMaterial)
        );

        var encoded = converter.WritePublicKey(key);
        var read = converter.ReadPublicKey(encoded);

        var map = CborGuard.ReadRootMap(encoded, new CoseOptions());
        Assert.True(map[3].TryGetInt64(out var algorithm));
        Assert.Equal(-48, algorithm);
        Assert.Equal("akp", read.KeyType);
    }

    #endregion
}
