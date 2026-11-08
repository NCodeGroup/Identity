// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers.Text;
using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using NCode.Extensions.DataProtection;
using NCode.Identity.Secrets.KeyMaterial;
using NCode.Identity.Secrets.Keys;
using NCode.Identity.Secrets.RemoteKeys;

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

    #region CreateRemoteKey Tests

    [Fact]
    public void CreateRemoteKey_WhenTheRowMatches_ReturnsARemoteKeyOnTheRowsCurve()
    {
        var provider = new EphemeralDataProtectionProvider();
        var mockFactory = CreateStrictMock<IDataProtectorFactory<SecretKey>>();
        mockFactory
            .Setup(x => x.CreateDataProtector())
            .Returns(provider.CreateProtector("test"))
            .Verifiable();
        var catalog = new DefaultEccCurveCatalog([]);
        var descriptor = new EccSecretKeyTypeDescriptor(
            new DefaultSecretKeyFactory(mockFactory.Object, catalog),
            catalog
        );
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var binding = new RemoteKeyBinding
        {
            Provider = CreateStrictMock<IRemoteKeyProvider>().Object,
            Reference = new RemoteKeyReference(RemoteKeyProviderNames.AwsKms, "key-1"),
            Algorithm = new RemoteSigningAlgorithm
            {
                Provider = RemoteKeyProviderNames.AwsKms,
                Algorithm = "ES256",
                SecretType = SecretKeyTypes.Ecc,
                KeySizeBits = 256,
                Curve = "P-256",
                KeySpec = "ECC_NIST_P256",
                SigningAlgorithm = "ECDSA_SHA_256",
                DigestAlgorithm = HashAlgorithmName.SHA256,
                IsDerEncodedSignature = true,
            },
        };

        var key = descriptor.CreateRemoteKey(
            new KeyMetadata { KeyId = "kid" },
            ecdsa.ExportSubjectPublicKeyInfo(),
            binding
        );

        var remoteKey = Assert.IsAssignableFrom<IRemoteSecretKey>(key);
        Assert.Same(binding, remoteKey.Binding);
        var eccKey = Assert.IsAssignableFrom<EccSecretKey>(key);
        Assert.Equal("P-256", eccKey.CurveName);
        Assert.Equal(256, eccKey.KeySizeBits);
    }

    #endregion
}
