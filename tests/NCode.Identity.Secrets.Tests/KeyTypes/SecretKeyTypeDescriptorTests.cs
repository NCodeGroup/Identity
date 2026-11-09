// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers.Text;
using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using NCode.Extensions.DataProtection;
using NCode.Identity.Jwk;
using NCode.Identity.Secrets.KeyMaterial;
using NCode.Identity.Secrets.Keys;

namespace NCode.Identity.Secrets.KeyTypes;

public class SecretKeyTypeDescriptorTests : BaseTests
{
    private static SecretKeyMaterialProtector Protect { get; } = _ => "protected";

    private static EccCurveSpecification Brainpool256 { get; } =
        new()
        {
            CurveName = "brainpoolP256r1",
            CurveSizeBits = 256,
            Curve = ECCurve.NamedCurves.brainpoolP256r1,
            Domain = EccCurveDomain.FromCurve(ECCurve.NamedCurves.brainpoolP256r1),
        };

    // The descriptors under test generate and validate keys without their factories, which a strict mock proves.
    private ISecretKeyFactory Factory { get; }

    public SecretKeyTypeDescriptorTests()
    {
        Factory = CreateStrictMock<ISecretKeyFactory>().Object;
    }

    private SymmetricSecretKeyTypeDescriptor Symmetric() => new(Factory);

    private RsaSecretKeyTypeDescriptor Rsa() => new(Factory);

    private EccSecretKeyTypeDescriptor Ecc(
        params IEnumerable<EccCurveSpecification> additionalCurves
    ) => new(Factory, new DefaultEccCurveCatalog(additionalCurves));

    private AkpSecretKeyTypeDescriptor Akp()
    {
        var protector = CreateStrictMock<IDataProtector>().Object;
        var factory = CreateStrictMock<IDataProtectorFactory<SecretKey>>();
        factory.Setup(x => x.CreateDataProtector()).Returns(protector).Verifiable();
        return new AkpSecretKeyTypeDescriptor(factory.Object);
    }

    private static SecretKeyGenerationParameters Params(int keySizeBits, string? curve = null) =>
        new() { KeySizeBits = keySizeBits, Curve = curve };

    [Fact]
    public void Symmetric_IsNotAsymmetricAndHasNoPublicKey()
    {
        var descriptor = Symmetric();
        Assert.Equal(SecretKeyTypes.Symmetric, descriptor.KeyType);
        Assert.Equal(typeof(SymmetricSecretKey), descriptor.KeyClrType);
        Assert.False(descriptor.IsAsymmetric);
        Assert.False(descriptor.SupportsPublicKeyExport);
    }

    [Fact]
    public void Rsa_IsAsymmetricAndExportsPublicKey()
    {
        var descriptor = Rsa();
        Assert.True(descriptor.IsAsymmetric);
        Assert.True(descriptor.SupportsPublicKeyExport);
    }

    [Fact]
    public void Ecc_IsAsymmetricAndExportsPublicKey()
    {
        var descriptor = Ecc();
        Assert.True(descriptor.IsAsymmetric);
        Assert.True(descriptor.SupportsPublicKeyExport);
    }

    [Fact]
    public void Symmetric_JwkKeyType_IsOct()
    {
        Assert.Same(JsonWebKeyTypeDescriptors.Oct, Symmetric().JwkKeyType);
    }

    [Fact]
    public void Rsa_JwkKeyType_IsRsa()
    {
        Assert.Same(JsonWebKeyTypeDescriptors.Rsa, Rsa().JwkKeyType);
    }

    [Fact]
    public void Ecc_JwkKeyType_IsEc()
    {
        Assert.Same(JsonWebKeyTypeDescriptors.Ec, Ecc().JwkKeyType);
    }

    [Theory]
    [InlineData(128, true)]
    [InlineData(256, true)]
    [InlineData(64, false)]
    [InlineData(100, false)]
    public void Symmetric_ValidateGeneration_EnforcesKeySize(int keySizeBits, bool valid)
    {
        Assert.Equal(valid, Symmetric().ValidateGeneration(Params(keySizeBits)) is null);
    }

    [Theory]
    [InlineData(2048, true)]
    [InlineData(4096, true)]
    [InlineData(1024, false)]
    [InlineData(2047, false)]
    public void Rsa_ValidateGeneration_EnforcesKeySize(int keySizeBits, bool valid)
    {
        Assert.Equal(valid, Rsa().ValidateGeneration(Params(keySizeBits)) is null);
    }

    [Fact]
    public void NonEcc_ValidateGeneration_RejectsCurve()
    {
        Assert.NotNull(Symmetric().ValidateGeneration(Params(256, curve: "P-256")));
        Assert.NotNull(Rsa().ValidateGeneration(Params(2048, curve: "P-256")));
    }

    [Theory]
    [InlineData(256, null, true)]
    [InlineData(384, null, true)]
    [InlineData(255, null, false)]
    public void Ecc_ValidateGeneration_BySize(int keySizeBits, string? curve, bool valid)
    {
        Assert.Equal(valid, Ecc().ValidateGeneration(Params(keySizeBits, curve)) is null);
    }

    [Fact]
    public void Ecc_ValidateGeneration_WhenNoCurveHasTheSize_ReturnsError()
    {
        Assert.Equal(
            "The ECC key size '255' bits is invalid; expected 'P-256' (256 bits), 'P-384' (384 bits), 'P-521' "
                + "(521 bits).",
            Ecc().ValidateGeneration(Params(255))
        );
    }

    // See ADR-1398: P-256 and brainpoolP256r1 are both 256 bits, so the size selects neither.
    [Fact]
    public void Ecc_ValidateGeneration_WhenTwoCurvesHaveTheSize_ReturnsError()
    {
        Assert.Equal(
            "The ECC key size '256' bits does not select a curve; 'P-256', 'brainpoolP256r1' have that size, so name "
                + "the curve.",
            Ecc(Brainpool256).ValidateGeneration(Params(256))
        );
    }

    [Theory]
    [InlineData("P-256")]
    [InlineData("brainpoolP256r1")]
    public void Ecc_ValidateGeneration_WhenTheCurveSelectsAmongCurvesOfOneSize_ReturnsNull(
        string curve
    )
    {
        Assert.Null(Ecc(Brainpool256).ValidateGeneration(Params(256, curve)));
    }

    [Fact]
    public void Ecc_ValidateGeneration_ByCurveName()
    {
        Assert.Null(Ecc().ValidateGeneration(Params(384, curve: "P-384")));
        Assert.NotNull(Ecc().ValidateGeneration(Params(256, curve: "not-a-curve")));
    }

    // A curve fixes the size of its keys, so a named curve is refused for any other requested size.
    [Theory]
    [InlineData(0, "P-384", 384)]
    [InlineData(256, "P-384", 384)]
    [InlineData(384, "P-256", 256)]
    [InlineData(512, "P-521", 521)]
    public void Ecc_ValidateGeneration_WhenTheSizeIsNotTheCurves_ReturnsError(
        int keySizeBits,
        string curve,
        int curveSizeBits
    )
    {
        Assert.Equal(
            $"The ECC key size '{keySizeBits}' bits is invalid for the curve '{curve}', whose keys are "
                + $"{curveSizeBits} bits.",
            Ecc().ValidateGeneration(Params(keySizeBits, curve))
        );
    }

    [Theory]
    [InlineData(10496, "ML-DSA-44")]
    [InlineData(15616, "ML-DSA-65")]
    [InlineData(20736, "ML-DSA-87")]
    public void Akp_ValidateGeneration_ByParameterSetName(int keySizeBits, string name)
    {
        Assert.Null(Akp().ValidateGeneration(Params(keySizeBits, name)));
    }

    // A parameter set fixes the size of its keys, so a named parameter set is refused for any other requested size.
    [Theory]
    [InlineData(0, "ML-DSA-65", 15616)]
    [InlineData(10496, "ML-DSA-65", 15616)]
    [InlineData(20736, "ML-DSA-44", 10496)]
    public void Akp_ValidateGeneration_WhenTheSizeIsNotTheParameterSets_ReturnsError(
        int keySizeBits,
        string name,
        int parameterSetSizeBits
    )
    {
        Assert.Equal(
            $"The ML-DSA key size '{keySizeBits}' bits is invalid for the parameter set '{name}', whose keys are "
                + $"{parameterSetSizeBits} bits.",
            Akp().ValidateGeneration(Params(keySizeBits, name))
        );
    }

    [Fact]
    public void Ecc_GenerateKeyMaterial_WhenTheSizeIsNotTheCurves_Throws()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            Ecc().GenerateKeyMaterial(Params(256, curve: "P-384"), Protect)
        );

        Assert.StartsWith(
            "The ECC key size '256' bits is invalid for the curve 'P-384', whose keys are 384 bits.",
            exception.Message,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void Ecc_GenerateKeyMaterial_WhenTwoCurvesHaveTheSize_Throws()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            Ecc(Brainpool256).GenerateKeyMaterial(Params(256), Protect)
        );

        Assert.StartsWith(
            "The ECC key size '256' bits does not select a curve; 'P-256', 'brainpoolP256r1' have that size, so name "
                + "the curve.",
            exception.Message,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void Ecc_GenerateKeyMaterial_WhenTheCurveSelectsAmongCurvesOfOneSize_GeneratesOnIt()
    {
        byte[]? pkcs8 = null;
        SecretKeyMaterialProtector protect = bytes =>
        {
            pkcs8 = bytes.ToArray();
            return "protected";
        };

        var material = Ecc(Brainpool256)
            .GenerateKeyMaterial(Params(256, curve: "brainpoolP256r1"), protect);

        using var ecdsa = ECDsa.Create();
        ecdsa.ImportPkcs8PrivateKey(pkcs8, out _);
        Assert.Equal(256, material.KeySizeBits);
        Assert.True(
            EccCurveSpecification.IsSameNamedCurve(
                ecdsa.ExportParameters(includePrivateParameters: false).Curve,
                ECCurve.NamedCurves.brainpoolP256r1
            )
        );
    }

    [Fact]
    public void Symmetric_GenerateKeyMaterial_RevealsMaterial()
    {
        var material = Symmetric().GenerateKeyMaterial(Params(256), Protect);
        Assert.Equal("protected", material.ProtectedKeyMaterial);
        Assert.Equal(256, material.KeySizeBits);
        Assert.False(string.IsNullOrEmpty(material.SecretMaterial));
    }

    [Fact]
    public void Ecc_GenerateKeyMaterial_ByCurveName_UsesCurveSize()
    {
        var material = Ecc().GenerateKeyMaterial(Params(384, curve: "P-384"), Protect);
        Assert.Equal("protected", material.ProtectedKeyMaterial);
        Assert.Equal(384, material.KeySizeBits);
        Assert.Null(material.SecretMaterial);
    }

    [Fact]
    public void Rsa_GenerateKeyMaterial_StoresThePrivateKeysSubjectPublicKeyInfo()
    {
        byte[]? pkcs8 = null;
        SecretKeyMaterialProtector protect = bytes =>
        {
            pkcs8 = bytes.ToArray();
            return "protected";
        };

        var material = Rsa().GenerateKeyMaterial(Params(2048), protect);

        using var rsa = RSA.Create();
        rsa.ImportPkcs8PrivateKey(pkcs8, out _);
        Assert.NotNull(material.PublicKey);
        Assert.Equal(
            rsa.ExportSubjectPublicKeyInfo(),
            Base64Url.DecodeFromChars(material.PublicKey)
        );
    }

    [Fact]
    public void Ecc_GenerateKeyMaterial_StoresThePrivateKeysSubjectPublicKeyInfo()
    {
        byte[]? pkcs8 = null;
        SecretKeyMaterialProtector protect = bytes =>
        {
            pkcs8 = bytes.ToArray();
            return "protected";
        };

        var material = Ecc().GenerateKeyMaterial(Params(256, curve: "P-256"), protect);

        using var ecdsa = ECDsa.Create();
        ecdsa.ImportPkcs8PrivateKey(pkcs8, out _);
        Assert.NotNull(material.PublicKey);
        Assert.Equal(
            ecdsa.ExportSubjectPublicKeyInfo(),
            Base64Url.DecodeFromChars(material.PublicKey)
        );
    }

    [Fact]
    public void Symmetric_GenerateKeyMaterial_HasNoPublicKey()
    {
        Assert.Null(Symmetric().GenerateKeyMaterial(Params(256), Protect).PublicKey);
    }
}
