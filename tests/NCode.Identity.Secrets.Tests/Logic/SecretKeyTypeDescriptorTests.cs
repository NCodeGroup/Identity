#region Copyright Preamble

// Copyright @ 2026 NCode Group
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

using Moq;
using NCode.Identity.Secrets.Keys;
using NCode.Identity.Secrets.Logic;

namespace NCode.Identity.Secrets.Tests.Logic;

public class SecretKeyTypeDescriptorTests
{
    private static readonly SecretKeyMaterialProtector Protect = _ => "protected";

    private static readonly ISecretKeyFactory Factory = Mock.Of<ISecretKeyFactory>();

    private static SymmetricSecretKeyTypeDescriptor Symmetric() => new(Factory);

    private static RsaSecretKeyTypeDescriptor Rsa() => new(Factory);

    private static EccSecretKeyTypeDescriptor Ecc() => new(Factory, new DefaultEccCurveCatalog([]));

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
    public void Ecc_ValidateGeneration_ByCurveName()
    {
        Assert.Null(Ecc().ValidateGeneration(Params(0, curve: "P-384")));
        Assert.NotNull(Ecc().ValidateGeneration(Params(256, curve: "not-a-curve")));
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
        var material = Ecc().GenerateKeyMaterial(Params(0, curve: "P-384"), Protect);
        Assert.Equal("protected", material.ProtectedKeyMaterial);
        Assert.Equal(384, material.KeySizeBits);
        Assert.Null(material.SecretMaterial);
    }
}
