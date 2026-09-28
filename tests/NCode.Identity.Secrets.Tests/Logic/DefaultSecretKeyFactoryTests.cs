#region Copyright Preamble

//
//    Copyright @ 2025 NCode Group
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

using System.Buffers;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.DataProtection;
using NCode.Extensions.DataProtection;
using NCode.Identity.Secrets.Keys;

namespace NCode.Identity.Secrets.Logic;

public class DefaultSecretKeyFactoryTests : BaseTests
{
    private DefaultSecretKeyFactory CreateSecretKeyFactory()
    {
        var provider = new EphemeralDataProtectionProvider();
        var protector = provider.CreateProtector("test");

        var mockFactory = CreateStrictMock<IDataProtectorFactory<SecretKey>>();
        mockFactory.Setup(x => x.CreateDataProtector()).Returns(protector).Verifiable();

        return new DefaultSecretKeyFactory(mockFactory.Object);
    }

    #region Empty Property Tests

    [Fact]
    public void Empty_ReturnsSingleton()
    {
        var factory = CreateSecretKeyFactory();

        Assert.Same(EmptySecretKey.Singleton, factory.Empty);
    }

    #endregion

    #region Symmetric Tests

    [Fact]
    public void CreateSymmetric_FromBytes_RoundTripsExportedKey()
    {
        var factory = CreateSecretKeyFactory();
        byte[] keyMaterial = [1, 2, 3, 4, 5, 6, 7, 8];

        var key = factory.CreateSymmetric(new KeyMetadata { KeyId = "sym" }, keyMaterial);

        Assert.Equal(keyMaterial.Length, key.KeySizeBytes);
        Assert.Equal(keyMaterial.Length * 8, key.KeySizeBits);

        var writer = new ArrayBufferWriter<byte>();
        key.ExportPrivateKey(ref writer);

        Assert.Equal(keyMaterial, writer.WrittenSpan.ToArray());
    }

    [Fact]
    public void CreateSymmetric_FromPassword_RoundTripsUtf8Bytes()
    {
        var factory = CreateSecretKeyFactory();
        const string password = "correct horse battery staple";
        byte[] expected = System.Text.Encoding.UTF8.GetBytes(password);

        var key = factory.CreateSymmetric(new KeyMetadata(), password);

        var writer = new ArrayBufferWriter<byte>();
        key.ExportPrivateKey(ref writer);

        Assert.Equal(expected, writer.WrittenSpan.ToArray());
    }

    #endregion

    #region RSA Tests

    [Fact]
    public void CreateRsa_FromKey_RoundTripsExportedKey()
    {
        var factory = CreateSecretKeyFactory();
        using var rsa = RSA.Create(2048);
        var expectedModulus = rsa.ExportParameters(false).Modulus;

        var key = factory.CreateRsa(new KeyMetadata { KeyId = "rsa" }, rsa);

        Assert.Equal(2048, key.KeySizeBits);
        Assert.False(key.HasCertificate);
        Assert.Null(key.ExportCertificate());

        using var exported = key.ExportRSA();
        Assert.Equal(expectedModulus, exported.ExportParameters(false).Modulus);
    }

    [Fact]
    public void CreateRsaPem_RoundTripsExportedKey()
    {
        var factory = CreateSecretKeyFactory();
        using var rsa = RSA.Create(2048);
        var pem = rsa.ExportPkcs8PrivateKeyPem();
        var expectedModulus = rsa.ExportParameters(false).Modulus;

        var key = factory.CreateRsaPem(new KeyMetadata(), pem);

        using var exported = key.ExportRSA();
        Assert.Equal(expectedModulus, exported.ExportParameters(false).Modulus);
    }

    [Fact]
    public void CreateRsaPkcs8_RoundTripsExportedKey()
    {
        var factory = CreateSecretKeyFactory();
        using var rsa = RSA.Create(2048);
        var pkcs8 = rsa.ExportPkcs8PrivateKey();
        var expectedModulus = rsa.ExportParameters(false).Modulus;

        var key = factory.CreateRsaPkcs8(new KeyMetadata(), pkcs8);

        using var exported = key.ExportRSA();
        Assert.Equal(expectedModulus, exported.ExportParameters(false).Modulus);
    }

    #endregion

    #region ECC Tests

    [Theory]
    [InlineData(256)]
    [InlineData(384)]
    [InlineData(521)]
    public void CreateEcc_FromKey_RoundTripsAndReportsCurve(int expectedSizeBits)
    {
        var factory = CreateSecretKeyFactory();
        var curve = expectedSizeBits switch
        {
            256 => ECCurve.NamedCurves.nistP256,
            384 => ECCurve.NamedCurves.nistP384,
            _ => ECCurve.NamedCurves.nistP521,
        };
        using var ecdsa = ECDsa.Create(curve);

        var key = factory.CreateEcc(new KeyMetadata { KeyId = "ecc" }, ecdsa);

        Assert.Equal(expectedSizeBits, key.KeySizeBits);
        Assert.Equal(curve.Oid.Value, key.GetECCurve().Oid.Value);

        using var exportedDsa = key.ExportECDsa();
        Assert.Equal(expectedSizeBits, exportedDsa.KeySize);

        using var exportedDh = key.ExportECDiffieHellman();
        Assert.Equal(expectedSizeBits, exportedDh.KeySize);
    }

    [Fact]
    public void CreateEccPem_RoundTripsExportedKey()
    {
        var factory = CreateSecretKeyFactory();
        using var ecdh = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
        var pem = ecdh.ExportPkcs8PrivateKeyPem();

        var key = factory.CreateEccPem(new KeyMetadata(), pem);

        using var exported = key.ExportECDiffieHellman();
        Assert.Equal(256, exported.KeySize);
    }

    [Fact]
    public void CreateEccPkcs8_RoundTripsExportedKey()
    {
        var factory = CreateSecretKeyFactory();
        using var ecdh = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
        var pkcs8 = ecdh.ExportPkcs8PrivateKey();

        var key = factory.CreateEccPkcs8(new KeyMetadata(), pkcs8);

        using var exported = key.ExportECDiffieHellman();
        Assert.Equal(256, exported.KeySize);
    }

    #endregion

    #region Create From Certificate Tests

    [Fact]
    public void Create_FromRsaCertificate_HasCertificateAndDefaultsMetadata()
    {
        var factory = CreateSecretKeyFactory();
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=test",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1
        );
        using var certificate = request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow.AddDays(1)
        );

        var key = Assert.IsAssignableFrom<RsaSecretKey>(
            factory.Create(new KeyMetadata(), certificate)
        );

        Assert.True(key.HasCertificate);
        Assert.Equal(certificate.Thumbprint, key.KeyId);

        using var exportedCertificate = key.ExportCertificate();
        Assert.NotNull(exportedCertificate);
        Assert.Equal(certificate.Thumbprint, exportedCertificate.Thumbprint);
    }

    [Fact]
    public void Create_FromEcdsaCertificate_ReturnsEccSecretKey()
    {
        var factory = CreateSecretKeyFactory();
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var request = new CertificateRequest("CN=test", ecdsa, HashAlgorithmName.SHA256);
        using var certificate = request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow.AddDays(1)
        );

        var key = Assert.IsAssignableFrom<EccSecretKey>(
            factory.Create(new KeyMetadata(), certificate)
        );

        Assert.True(key.HasCertificate);
    }

    [Fact]
    public void Create_WhenCertificateHasNoPrivateKey_ThrowsArgumentException()
    {
        var factory = CreateSecretKeyFactory();
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=test",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1
        );
        using var withPrivateKey = request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow.AddDays(1)
        );
        using var publicOnly = X509CertificateLoader.LoadCertificate(
            withPrivateKey.Export(X509ContentType.Cert)
        );

        var exception = Assert.Throws<ArgumentException>(() =>
            factory.Create(new KeyMetadata(), publicOnly)
        );

        Assert.Equal("certificate", exception.ParamName);
    }

    #endregion
}
