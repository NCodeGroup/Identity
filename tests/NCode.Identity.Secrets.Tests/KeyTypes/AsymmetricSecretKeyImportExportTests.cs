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

using System.Buffers.Text;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.DataProtection;
using NCode.Extensions.DataProtection;
using NCode.Identity.Jwk;
using NCode.Identity.Secrets.KeyMaterial;
using NCode.Identity.Secrets.Keys;

namespace NCode.Identity.Secrets.KeyTypes;

// See ADR-0104 (public-key import) and ADR-0105 (descriptor first-class seam).
public class AsymmetricSecretKeyImportExportTests : BaseTests
{
    private static readonly KeyMetadata Metadata = new()
    {
        KeyId = "kid-1",
        Use = "sig",
        Algorithm = "RS256",
    };

    private readonly ISecretKeyFactory _factory;

    public AsymmetricSecretKeyImportExportTests()
    {
        var provider = new EphemeralDataProtectionProvider();
        var protector = provider.CreateProtector("test");

        var mockFactory = CreateStrictMock<IDataProtectorFactory<SecretKey>>();
        mockFactory.Setup(x => x.CreateDataProtector()).Returns(protector).Verifiable();

        _factory = new DefaultSecretKeyFactory(mockFactory.Object);
    }

    private RsaSecretKeyTypeDescriptor Rsa() => new(_factory);

    private EccSecretKeyTypeDescriptor Ecc() => new(_factory, new DefaultEccCurveCatalog([]));

    private SymmetricSecretKeyTypeDescriptor Symmetric() => new(_factory);

    // Reads a key-type-specific JWK member (for example 'n' or 'x') by name.
    private static string? Member(JsonWebKey jwk, string name) => jwk.GetMember(name);

    // A capturing protector that records the raw (unprotected) bytes so a test can feed them back into
    // DeserializeKeyMaterial, mirroring the persistence unprotect-then-deserialize path.
    private sealed class CapturingProtector
    {
        public byte[] Captured { get; private set; } = [];

        public string Protect(ReadOnlySpan<byte> material)
        {
            Captured = material.ToArray();
            return Convert.ToBase64String(material);
        }
    }

    #region RSA import

    [Fact]
    public void Rsa_ImportJwk_ProducesSpkiMaterial()
    {
        using var rsa = RSA.Create(2048);
        var parameters = rsa.ExportParameters(includePrivateParameters: false);
        var jwk = $$"""
            {"kty":"RSA","n":"{{Base64Url.EncodeToString(
                parameters.Modulus!
            )}}","e":"{{Base64Url.EncodeToString(parameters.Exponent!)}}"}
            """;

        var capture = new CapturingProtector();
        var material = Rsa()
            .ImportPublicKeyMaterial(
                new SecretKeyImportParameters
                {
                    Format = SecretKeyImportFormats.Jwk,
                    KeyMaterial = jwk,
                },
                capture.Protect
            );

        Assert.Equal(SecretEncodings.Spki, material.SecretEncoding);
        Assert.Equal(2048, material.KeySizeBits);
    }

    [Fact]
    public void Rsa_ImportPem_ProducesSpkiMaterial()
    {
        using var rsa = RSA.Create(2048);
        var pem = rsa.ExportSubjectPublicKeyInfoPem();

        var capture = new CapturingProtector();
        var material = Rsa()
            .ImportPublicKeyMaterial(
                new SecretKeyImportParameters
                {
                    Format = SecretKeyImportFormats.Pem,
                    KeyMaterial = pem,
                },
                capture.Protect
            );

        Assert.Equal(SecretEncodings.Spki, material.SecretEncoding);
        Assert.Equal(2048, material.KeySizeBits);
    }

    [Fact]
    public void Rsa_ImportCertificate_ProducesX509Material()
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=test",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1
        );
        using var certificate = request.CreateSelfSigned(
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow.AddDays(1)
        );
        var base64Der = Convert.ToBase64String(certificate.Export(X509ContentType.Cert));

        var capture = new CapturingProtector();
        var material = Rsa()
            .ImportPublicKeyMaterial(
                new SecretKeyImportParameters
                {
                    Format = SecretKeyImportFormats.Certificate,
                    KeyMaterial = base64Der,
                },
                capture.Protect
            );

        Assert.Equal(SecretEncodings.X509, material.SecretEncoding);
        Assert.Equal(2048, material.KeySizeBits);
    }

    [Fact]
    public void Rsa_ImportJwk_WithPrivateComponent_IsRejected()
    {
        using var rsa = RSA.Create(2048);
        var parameters = rsa.ExportParameters(includePrivateParameters: true);
        var jwk = $$"""
            {"kty":"RSA","n":"{{Base64Url.EncodeToString(
                parameters.Modulus!
            )}}","e":"{{Base64Url.EncodeToString(
                parameters.Exponent!
            )}}","d":"{{Base64Url.EncodeToString(parameters.D!)}}"}
            """;

        Assert.Throws<ArgumentException>(() =>
            Rsa()
                .ImportPublicKeyMaterial(
                    new SecretKeyImportParameters
                    {
                        Format = SecretKeyImportFormats.Jwk,
                        KeyMaterial = jwk,
                    },
                    _ => "protected"
                )
        );
    }

    [Fact]
    public void Rsa_ImportJwk_WithPrivatePrimeButNoPrivateExponent_IsRejected()
    {
        using var rsa = RSA.Create(2048);
        var parameters = rsa.ExportParameters(includePrivateParameters: true);
        var jwk = $$"""
            {"kty":"RSA","n":"{{Base64Url.EncodeToString(
                parameters.Modulus!
            )}}","e":"{{Base64Url.EncodeToString(
                parameters.Exponent!
            )}}","p":"{{Base64Url.EncodeToString(parameters.P!)}}"}
            """;

        Assert.Throws<ArgumentException>(() =>
            Rsa()
                .ImportPublicKeyMaterial(
                    new SecretKeyImportParameters
                    {
                        Format = SecretKeyImportFormats.Jwk,
                        KeyMaterial = jwk,
                    },
                    _ => "protected"
                )
        );
    }

    [Fact]
    public void Rsa_ImportPem_WithPrivateKey_IsRejected()
    {
        using var rsa = RSA.Create(2048);
        var privatePem = rsa.ExportPkcs8PrivateKeyPem();

        Assert.Throws<ArgumentException>(() =>
            Rsa()
                .ImportPublicKeyMaterial(
                    new SecretKeyImportParameters
                    {
                        Format = SecretKeyImportFormats.Pem,
                        KeyMaterial = privatePem,
                    },
                    _ => "protected"
                )
        );
    }

    #endregion

    #region RSA deserialize + export round-trip

    [Fact]
    public void Rsa_ImportDeserializeExport_RoundTripsPublicKey()
    {
        using var rsa = RSA.Create(2048);
        var original = rsa.ExportParameters(includePrivateParameters: false);
        var expectedModulus = Base64Url.EncodeToString(original.Modulus!);
        var expectedExponent = Base64Url.EncodeToString(original.Exponent!);
        var pem = rsa.ExportSubjectPublicKeyInfoPem();

        var descriptor = Rsa();
        var capture = new CapturingProtector();
        var material = descriptor.ImportPublicKeyMaterial(
            new SecretKeyImportParameters
            {
                Format = SecretKeyImportFormats.Pem,
                KeyMaterial = pem,
            },
            capture.Protect
        );

        var secretKey = (RsaSecretKey)
            descriptor.DeserializeKeyMaterial(Metadata, material.SecretEncoding, capture.Captured);

        var exported = descriptor.ExportPublicKey(secretKey);
        Assert.NotNull(exported);
        Assert.Equal("RSA", exported.Jwk.KeyType);
        Assert.Equal(expectedModulus, Member(exported.Jwk, "n"));
        Assert.Equal(expectedExponent, Member(exported.Jwk, "e"));
        Assert.Equal("kid-1", exported.Jwk.KeyId);
        Assert.Contains("-----BEGIN PUBLIC KEY-----", exported.Pem);
    }

    #endregion

    #region ECC import + round-trip

    [Fact]
    public void Ecc_ImportJwk_ProducesSpkiMaterial()
    {
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var parameters = ecdsa.ExportParameters(includePrivateParameters: false);
        var jwk = $$"""
            {"kty":"EC","crv":"P-256","x":"{{Base64Url.EncodeToString(
                parameters.Q.X!
            )}}","y":"{{Base64Url.EncodeToString(parameters.Q.Y!)}}"}
            """;

        var capture = new CapturingProtector();
        var material = Ecc()
            .ImportPublicKeyMaterial(
                new SecretKeyImportParameters
                {
                    Format = SecretKeyImportFormats.Jwk,
                    KeyMaterial = jwk,
                },
                capture.Protect
            );

        Assert.Equal(SecretEncodings.Spki, material.SecretEncoding);
        Assert.Equal(256, material.KeySizeBits);
    }

    [Fact]
    public void Ecc_ImportDeserializeExport_RoundTripsPublicKey()
    {
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var original = ecdsa.ExportParameters(includePrivateParameters: false);
        var expectedX = Base64Url.EncodeToString(original.Q.X!);
        var expectedY = Base64Url.EncodeToString(original.Q.Y!);
        var pem = ecdsa.ExportSubjectPublicKeyInfoPem();

        var descriptor = Ecc();
        var capture = new CapturingProtector();
        var material = descriptor.ImportPublicKeyMaterial(
            new SecretKeyImportParameters
            {
                Format = SecretKeyImportFormats.Pem,
                KeyMaterial = pem,
            },
            capture.Protect
        );

        var secretKey = (EccSecretKey)
            descriptor.DeserializeKeyMaterial(Metadata, material.SecretEncoding, capture.Captured);

        var exported = descriptor.ExportPublicKey(secretKey);
        Assert.NotNull(exported);
        Assert.Equal("EC", exported.Jwk.KeyType);
        Assert.Equal("P-256", Member(exported.Jwk, "crv"));
        Assert.Equal(expectedX, Member(exported.Jwk, "x"));
        Assert.Equal(expectedY, Member(exported.Jwk, "y"));
    }

    [Fact]
    public void Ecc_ImportJwk_WithWrongKeyType_IsRejected()
    {
        using var rsa = RSA.Create(2048);
        var parameters = rsa.ExportParameters(includePrivateParameters: false);
        var jwk = $$"""
            {"kty":"RSA","n":"{{Base64Url.EncodeToString(
                parameters.Modulus!
            )}}","e":"{{Base64Url.EncodeToString(parameters.Exponent!)}}"}
            """;

        Assert.Throws<ArgumentException>(() =>
            Ecc()
                .ImportPublicKeyMaterial(
                    new SecretKeyImportParameters
                    {
                        Format = SecretKeyImportFormats.Jwk,
                        KeyMaterial = jwk,
                    },
                    _ => "protected"
                )
        );
    }

    #endregion

    #region JWK x5c import

    private static X509Certificate2 CreateEccCertificate(ECDsa key) =>
        new CertificateRequest("CN=client", key, HashAlgorithmName.SHA256).CreateSelfSigned(
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow.AddDays(1)
        );

    private static string EccJwk(ECDsa key, string x5cMember)
    {
        var parameters = key.ExportParameters(includePrivateParameters: false);
        var x = Base64Url.EncodeToString(parameters.Q.X!);
        var y = Base64Url.EncodeToString(parameters.Q.Y!);
        return $$"""{"kty":"EC","crv":"P-256","x":"{{x}}","y":"{{y}}"{{x5cMember}}}""";
    }

    private ImportedSecretKeyMaterial ImportEccJwk(string jwk, CapturingProtector capture) =>
        Ecc()
            .ImportPublicKeyMaterial(
                new SecretKeyImportParameters
                {
                    Format = SecretKeyImportFormats.Jwk,
                    KeyMaterial = jwk,
                },
                capture.Protect
            );

    [Fact]
    public void Ecc_ImportJwk_WithX5c_ProducesLeafCertificate()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var leaf = CreateEccCertificate(key);
        using var issuer = CreateEccCertificate(ECDsa.Create(ECCurve.NamedCurves.nistP256));
        var x5c =
            $$""","x5c":["{{Convert.ToBase64String(leaf.RawData)}}","{{Convert.ToBase64String(
                issuer.RawData
            )}}"]""";

        var descriptor = Ecc();
        var capture = new CapturingProtector();
        var material = ImportEccJwk(EccJwk(key, x5c), capture);

        Assert.Equal(SecretEncodings.X509, material.SecretEncoding);
        Assert.Equal(256, material.KeySizeBits);
        Assert.Equal(leaf.RawData, capture.Captured);

        var secretKey = (AsymmetricSecretKey)
            descriptor.DeserializeKeyMaterial(Metadata, material.SecretEncoding, capture.Captured);
        Assert.True(secretKey.HasCertificate);
        Assert.Equal(leaf.RawData, secretKey.CertificateRawData.Value.ToArray());
    }

    [Fact]
    public void Ecc_ImportJwk_WithX5cOfAnotherKey_IsRejected()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var otherKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var other = CreateEccCertificate(otherKey);
        var x5c = $$""","x5c":["{{Convert.ToBase64String(other.RawData)}}"]""";

        var exception = Assert.Throws<ArgumentException>(() =>
            ImportEccJwk(EccJwk(key, x5c), new CapturingProtector())
        );

        Assert.Contains("does not carry the JWK's public key", exception.Message);
    }

    [Theory]
    [InlineData(""","x5c":[]""")]
    [InlineData(""","x5c":"AAAA" """)]
    [InlineData(""","x5c":[42]""")]
    public void Ecc_ImportJwk_WithEmptyOrNonArrayX5c_IsRejected(string x5c)
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);

        Assert.Throws<ArgumentException>(() =>
            ImportEccJwk(EccJwk(key, x5c), new CapturingProtector())
        );
    }

    [Theory]
    [InlineData(""","x5c":["not base64!"]""")]
    [InlineData(""","x5c":["AAAA"]""")]
    public void Ecc_ImportJwk_WithMalformedX5c_IsRejected(string x5c)
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);

        var exception = Assert.Throws<ArgumentException>(() =>
            ImportEccJwk(EccJwk(key, x5c), new CapturingProtector())
        );

        Assert.Contains("malformed", exception.Message);
    }

    #endregion

    #region Symmetric does not support public-key import or export

    [Fact]
    public void Symmetric_DoesNotSupportPublicKeyImportOrExport()
    {
        var descriptor = Symmetric();

        Assert.Empty(descriptor.PublicKeyImportFormats);
        Assert.False(descriptor.SupportsPublicKeyExport);
    }

    [Fact]
    public void Asymmetric_PublicKeyImportFormats_AreJwkPemAndCertificate()
    {
        string[] expected =
        [
            SecretKeyImportFormats.Jwk,
            SecretKeyImportFormats.Pem,
            SecretKeyImportFormats.Certificate,
        ];

        Assert.Equal(expected, Rsa().PublicKeyImportFormats);
        Assert.Equal(expected, Ecc().PublicKeyImportFormats);
    }

    [Theory]
    [InlineData("openssh")]
    [InlineData("JWK")]
    public void Rsa_ImportPublicKeyMaterial_WithAnUnknownFormat_IsRejected(string format)
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            Rsa()
                .ImportPublicKeyMaterial(
                    new SecretKeyImportParameters { Format = format, KeyMaterial = "{}" },
                    _ => "protected"
                )
        );

        Assert.Contains($"'{format}'", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Symmetric_ImportPublicKeyMaterial_Throws()
    {
        Assert.Throws<NotSupportedException>(() =>
            Symmetric()
                .ImportPublicKeyMaterial(
                    new SecretKeyImportParameters
                    {
                        Format = SecretKeyImportFormats.Jwk,
                        KeyMaterial = "{}",
                    },
                    _ => "protected"
                )
        );
    }

    #endregion
}
