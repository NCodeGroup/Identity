// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers.Text;
using System.Security.Cryptography;
using NCode.Identity.Secrets.KeyMaterial;

namespace NCode.Identity.Secrets.KeyTypes;

// See ADR-1427.
public class EccSecretKeyTypeDescriptorTests : BaseTests
{
    private EccSecretKeyTypeDescriptor Descriptor { get; }

    public EccSecretKeyTypeDescriptorTests()
    {
        Descriptor = new EccSecretKeyTypeDescriptor(
            CreateStrictMock<ISecretKeyFactory>().Object,
            new DefaultEccCurveCatalog([])
        );
    }

    private ImportedSecretKeyMaterial Import(string x, string y) =>
        Descriptor.ImportPublicKeyMaterial(
            new SecretKeyImportParameters
            {
                Format = SecretKeyImportFormats.Jwk,
                KeyMaterial = $$"""{"kty":"EC","crv":"P-256","x":"{{x}}","y":"{{y}}"}""",
            },
            _ => "protected"
        );

    private static (byte[] X, byte[] Y) CreateP256Point()
    {
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var parameters = ecdsa.ExportParameters(includePrivateParameters: false);
        return (parameters.Q.X!, parameters.Q.Y!);
    }

    #region ImportPublicKeyMaterial Tests

    [Fact]
    public void ImportPublicKeyMaterial_WhenPointIsOnTheCurve_ImportsTheKey()
    {
        var (x, y) = CreateP256Point();

        var material = Import(Base64Url.EncodeToString(x), Base64Url.EncodeToString(y));

        Assert.Equal(SecretEncodings.Spki, material.SecretEncoding);
        Assert.Equal(256, material.KeySizeBits);
    }

    [Fact]
    public void ImportPublicKeyMaterial_WhenPointIsNotOnTheCurve_Throws()
    {
        var (x, _) = CreateP256Point();
        var encodedX = Base64Url.EncodeToString(x);

        var exception = Assert.Throws<ArgumentException>(() => Import(encodedX, encodedX));

        Assert.Contains("'P-256'", exception.Message);
        Assert.Contains("not on the curve", exception.Message);
    }

    [Fact]
    public void ImportPublicKeyMaterial_WhenACoordinateIsShorterThanTheField_Throws()
    {
        var (x, y) = CreateP256Point();
        var shortX = Base64Url.EncodeToString(x.AsSpan(1));

        var exception = Assert.Throws<ArgumentException>(() =>
            Import(shortX, Base64Url.EncodeToString(y))
        );

        Assert.Contains("must each be 32 bytes", exception.Message);
        Assert.Contains("'P-256'", exception.Message);
    }

    [Fact]
    public void ImportPublicKeyMaterial_WhenACoordinateIsLongerThanTheField_Throws()
    {
        var (x, y) = CreateP256Point();
        var longY = Base64Url.EncodeToString([0, .. y]);

        var exception = Assert.Throws<ArgumentException>(() =>
            Import(Base64Url.EncodeToString(x), longY)
        );

        Assert.Contains("must each be 32 bytes", exception.Message);
    }

    #endregion
}
