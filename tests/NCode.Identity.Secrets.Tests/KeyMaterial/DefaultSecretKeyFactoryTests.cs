// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.DataProtection;
using NCode.Buffers;
using NCode.Extensions.DataProtection;
using NCode.Identity.Secrets.Keys;
using NCode.Identity.Secrets.KeyTypes;
using NCode.Identity.Secrets.RemoteKeys;

namespace NCode.Identity.Secrets.KeyMaterial;

public class DefaultSecretKeyFactoryTests : BaseTests
{
    private static EccCurveSpecification Brainpool256 { get; } =
        new()
        {
            CurveName = "brainpoolP256r1",
            CurveSizeBits = 256,
            Curve = ECCurve.NamedCurves.brainpoolP256r1,
            Domain = EccCurveDomain.FromCurve(ECCurve.NamedCurves.brainpoolP256r1),
        };

    private DefaultSecretKeyFactory CreateSecretKeyFactory(
        params IEnumerable<EccCurveSpecification> additionalCurves
    )
    {
        var provider = new EphemeralDataProtectionProvider();
        var protector = provider.CreateProtector("test");

        var mockFactory = CreateStrictMock<IDataProtectorFactory<SecretKey>>();
        mockFactory.Setup(x => x.CreateDataProtector()).Returns(protector).Verifiable();

        return new DefaultSecretKeyFactory(
            mockFactory.Object,
            new DefaultEccCurveCatalog(additionalCurves)
        );
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
        byte[] expected = SecureEncoding.UTF8.GetBytes(password);

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

    [Theory]
    [InlineData("nistP256", "P-256")]
    [InlineData("nistP384", "P-384")]
    [InlineData("nistP521", "P-521")]
    public void CreateEcc_FromKey_NamesTheCurveAsTheCatalogDoes(
        string friendlyName,
        string expected
    )
    {
        var factory = CreateSecretKeyFactory();
        using var ecdsa = ECDsa.Create(ECCurve.CreateFromFriendlyName(friendlyName));

        var key = factory.CreateEcc(new KeyMetadata { KeyId = "ecc" }, ecdsa);

        Assert.Equal(expected, key.CurveName);
    }

    // brainpoolP256r1 has P-256's size, so the curve is named by its OID, and only when the catalog holds it.
    [Fact]
    public void CreateEcc_WhenTheCatalogHoldsTheCurve_NamesItByItsOid()
    {
        var factory = CreateSecretKeyFactory(Brainpool256);
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.brainpoolP256r1);

        var key = factory.CreateEcc(new KeyMetadata { KeyId = "ecc" }, ecdsa);

        Assert.Equal("brainpoolP256r1", key.CurveName);
    }

    [Fact]
    public void CreateEcc_WhenTheCatalogDoesNotHoldTheCurve_HasNoCurveName()
    {
        var factory = CreateSecretKeyFactory();
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.brainpoolP256r1);

        var key = factory.CreateEcc(new KeyMetadata { KeyId = "ecc" }, ecdsa);

        Assert.Null(key.CurveName);
    }

    [Fact]
    public void CreateEccSpki_NamesTheCurveOfThePublicKey()
    {
        var factory = CreateSecretKeyFactory();
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP384);

        var key = factory.CreateEccSpki(new KeyMetadata(), ecdsa.ExportSubjectPublicKeyInfo());

        Assert.Equal("P-384", key.CurveName);
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
    public void Create_WhenCertificateHasNoPrivateKey_ReturnsVerifyOnlyPublicKey()
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

        var key = Assert.IsAssignableFrom<RsaSecretKey>(
            factory.Create(new KeyMetadata(), publicOnly)
        );

        Assert.True(key.HasCertificate);
        Assert.Equal(publicOnly.Thumbprint, key.KeyId);

        byte[] data = [9, 8, 7, 6];
        var signature = rsa.SignData(data, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

        using var exported = key.ExportRSA();
        Assert.True(
            exported.VerifyData(
                data,
                signature,
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1
            )
        );
    }

    // A CA, a certificate it issued with its private key, and the CA's public certificate.
    private static (X509Certificate2 Leaf, X509Certificate2 Issuer) CreateIssuedCertificate(
        ECDsa leafKey
    )
    {
        using var issuerKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var issuerRequest = new CertificateRequest(
            "CN=test-ca",
            issuerKey,
            HashAlgorithmName.SHA256
        );
        issuerRequest.CertificateExtensions.Add(
            new X509BasicConstraintsExtension(true, false, 0, true)
        );
        using var issuer = issuerRequest.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddDays(-2),
            DateTimeOffset.UtcNow.AddDays(2)
        );

        var leafRequest = new CertificateRequest("CN=test", leafKey, HashAlgorithmName.SHA256);
        using var leafPublic = leafRequest.Create(
            issuer,
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow.AddDays(1),
            [1, 2, 3, 4]
        );

        return (
            leafPublic.CopyWithPrivateKey(leafKey),
            X509CertificateLoader.LoadCertificate(issuer.Export(X509ContentType.Cert))
        );
    }

    [Fact]
    public void Create_FromCertificateCollection_KeepsTheIssuersOfTheCertificateWithAPrivateKey()
    {
        var factory = CreateSecretKeyFactory();
        using var leafKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var (leaf, issuer) = CreateIssuedCertificate(leafKey);
        using var _ = leaf;
        using var __ = issuer;
        using var leafPublic = X509CertificateLoader.LoadCertificate(leaf.RawData);

        // The leaf's public certificate, as a PKCS#12 file may also carry, is not an issuer.
        var key = Assert.IsAssignableFrom<EccSecretKey>(
            factory.Create(new KeyMetadata(), [issuer, leaf, leafPublic])
        );

        Assert.True(key.HasCertificate);
        Assert.Equal(leaf.RawData, key.CertificateRawData.Value.ToArray());
        var issuerRawData = Assert.Single(key.IssuerCertificatesRawData);
        Assert.Equal(issuer.RawData, issuerRawData.ToArray());

        var exported = key.ExportIssuerCertificates();
        using var exportedIssuer = Assert.Single(exported);
        Assert.Equal(issuer.Thumbprint, exportedIssuer.Thumbprint);
    }

    [Fact]
    public void Create_FromCertificateCollectionWithoutAPrivateKey_Throws()
    {
        var factory = CreateSecretKeyFactory();
        using var leafKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var (leaf, issuer) = CreateIssuedCertificate(leafKey);
        using var _ = leaf;
        using var __ = issuer;
        using var leafPublic = X509CertificateLoader.LoadCertificate(leaf.RawData);

        var exception = Assert.Throws<ArgumentException>(() =>
            factory.Create(new KeyMetadata(), [leafPublic, issuer])
        );

        Assert.Equal("certificates", exception.ParamName);
    }

    [Fact]
    public void Create_FromCertificateCollectionWithTwoPrivateKeys_Throws()
    {
        var factory = CreateSecretKeyFactory();
        using var firstKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var secondKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var (first, firstIssuer) = CreateIssuedCertificate(firstKey);
        var (second, secondIssuer) = CreateIssuedCertificate(secondKey);
        using var _ = first;
        using var __ = firstIssuer;
        using var ___ = second;
        using var ____ = secondIssuer;

        var exception = Assert.Throws<ArgumentException>(() =>
            factory.Create(new KeyMetadata(), [first, second])
        );

        Assert.Equal("certificates", exception.ParamName);
    }

    [Fact]
    public void Create_FromASingleCertificate_HasNoIssuers()
    {
        var factory = CreateSecretKeyFactory();
        using var leafKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var (leaf, issuer) = CreateIssuedCertificate(leafKey);
        using var _ = leaf;
        using var __ = issuer;

        var key = Assert.IsAssignableFrom<AsymmetricSecretKey>(
            factory.Create(new KeyMetadata(), leaf)
        );

        Assert.Empty(key.IssuerCertificatesRawData);
        Assert.Empty(key.ExportIssuerCertificates());
    }
    #endregion

    #region Public Key Tests

    [Fact]
    public void CreateRsaPublicKey_VerifiesSignatureButCannotSign()
    {
        var factory = CreateSecretKeyFactory();
        using var signingKey = RSA.Create(2048);
        using var publicKey = RSA.Create();
        publicKey.ImportParameters(signingKey.ExportParameters(includePrivateParameters: false));

        var key = factory.CreateRsaPublicKey(new KeyMetadata { KeyId = "rsa-pub" }, publicKey);

        Assert.Equal(2048, key.KeySizeBits);
        Assert.False(key.HasCertificate);
        Assert.Null(key.ExportCertificate());

        byte[] data = [1, 2, 3, 4];
        var signature = signingKey.SignData(
            data,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1
        );

        using var exported = key.ExportRSA();
        Assert.True(
            exported.VerifyData(
                data,
                signature,
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1
            )
        );
        // ThrowsAny, not Throws: the concrete exception type is a platform implementation detail. Windows throws
        // CryptographicException itself, while OpenSSL-backed platforms throw a derived
        // Interop+Crypto+OpenSslCryptographicException, and Assert.Throws demands an exact type match.
        Assert.ThrowsAny<CryptographicException>(() =>
            exported.SignData(data, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1)
        );
    }

    [Theory]
    [InlineData(256)]
    [InlineData(384)]
    [InlineData(521)]
    public void CreateEccPublicKey_VerifiesSignatureAndReportsCurve(int expectedSizeBits)
    {
        var factory = CreateSecretKeyFactory();
        var curve = expectedSizeBits switch
        {
            256 => ECCurve.NamedCurves.nistP256,
            384 => ECCurve.NamedCurves.nistP384,
            _ => ECCurve.NamedCurves.nistP521,
        };
        using var signingKey = ECDsa.Create(curve);
        using var publicKey = ECDsa.Create();
        publicKey.ImportParameters(
            new ECParameters
            {
                Curve = curve,
                Q = signingKey.ExportParameters(includePrivateParameters: false).Q,
            }
        );

        var key = factory.CreateEccPublicKey(new KeyMetadata { KeyId = "ecc-pub" }, publicKey);

        Assert.Equal(expectedSizeBits, key.KeySizeBits);
        Assert.Equal(curve.Oid.Value, key.GetECCurve().Oid.Value);
        Assert.False(key.HasCertificate);

        byte[] data = [5, 6, 7, 8];
        var signature = signingKey.SignData(data, HashAlgorithmName.SHA256);

        using var exported = key.ExportECDsa();
        Assert.True(exported.VerifyData(data, signature, HashAlgorithmName.SHA256));
    }

    #endregion

    #region Remote Tests

    private static RemoteSigningAlgorithm CreateRsaRow(int keySizeBits = 2048) =>
        new()
        {
            Provider = RemoteKeyProviderNames.AwsKms,
            Algorithm = "RS256",
            SecretType = SecretKeyTypes.Rsa,
            KeySizeBits = keySizeBits,
            KeySpec = "RSA_2048",
            SigningAlgorithm = "RSASSA_PKCS1_V1_5_SHA_256",
            DigestAlgorithm = HashAlgorithmName.SHA256,
        };

    private static RemoteSigningAlgorithm CreateEccRow(
        string secretType = SecretKeyTypes.Ecc,
        int keySizeBits = 256,
        string? curve = "P-256"
    ) =>
        new()
        {
            Provider = RemoteKeyProviderNames.AwsKms,
            Algorithm = "ES256",
            SecretType = secretType,
            KeySizeBits = keySizeBits,
            Curve = curve,
            KeySpec = "ECC_NIST_P256",
            SigningAlgorithm = "ECDSA_SHA_256",
            DigestAlgorithm = HashAlgorithmName.SHA256,
            IsDerEncodedSignature = true,
        };

    private RemoteKeyBinding CreateBinding(RemoteKeyAlgorithm row) =>
        new()
        {
            Provider = CreateStrictMock<IRemoteKeyProvider>().Object,
            Reference = new RemoteKeyReference(RemoteKeyProviderNames.AwsKms, "key-1"),
            Algorithm = row,
        };

    private static RemoteDecryptionAlgorithm CreateDecryptionRow(
        string secretType = SecretKeyTypes.Rsa,
        int keySizeBits = 2048
    ) =>
        new()
        {
            Provider = RemoteKeyProviderNames.AwsKms,
            Algorithm = "RSA-OAEP-256",
            SecretType = secretType,
            KeySizeBits = keySizeBits,
            KeySpec = "RSA_2048",
            EncryptionAlgorithm = "RSAES_OAEP_SHA_256",
        };

    private static RemoteKeyAgreementAlgorithm CreateKeyAgreementRow(
        string secretType = SecretKeyTypes.Ecc
    ) =>
        new()
        {
            Provider = RemoteKeyProviderNames.AwsKms,
            Algorithm = "ECDH-ES",
            SecretType = secretType,
            KeySizeBits = 256,
            Curve = "P-256",
            KeySpec = "ECC_NIST_P256",
            KeyAgreementAlgorithm = "ECDH",
        };

    [Fact]
    public void CreateRsaRemote_DecryptionRow_ReturnsRemoteKey()
    {
        var factory = CreateSecretKeyFactory();
        using var rsa = RSA.Create(2048);
        var binding = CreateBinding(CreateDecryptionRow());

        var key = factory.CreateRsaRemote(
            new KeyMetadata { KeyId = "rsa-remote" },
            rsa.ExportSubjectPublicKeyInfo(),
            binding
        );

        var remoteKey = Assert.IsAssignableFrom<IRemoteSecretKey>(key);
        Assert.Same(binding, remoteKey.Binding);
    }

    [Fact]
    public void CreateRsaRemote_KeyAgreementRow_Throws()
    {
        var factory = CreateSecretKeyFactory();
        using var rsa = RSA.Create(2048);
        var binding = CreateBinding(CreateKeyAgreementRow(secretType: SecretKeyTypes.Rsa));

        Assert.Throws<ArgumentException>(() =>
            factory.CreateRsaRemote(new KeyMetadata(), rsa.ExportSubjectPublicKeyInfo(), binding)
        );
    }

    [Fact]
    public void CreateEccRemote_KeyAgreementRow_ReturnsRemoteKey()
    {
        var factory = CreateSecretKeyFactory();
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var binding = CreateBinding(CreateKeyAgreementRow());

        var key = factory.CreateEccRemote(
            new KeyMetadata { KeyId = "ecc-remote" },
            ecdsa.ExportSubjectPublicKeyInfo(),
            binding
        );

        var remoteKey = Assert.IsAssignableFrom<IRemoteSecretKey>(key);
        Assert.Same(binding, remoteKey.Binding);
    }

    [Fact]
    public void CreateEccRemote_DecryptionRow_Throws()
    {
        var factory = CreateSecretKeyFactory();
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var binding = CreateBinding(CreateDecryptionRow(secretType: SecretKeyTypes.Ecc));

        Assert.Throws<ArgumentException>(() =>
            factory.CreateEccRemote(new KeyMetadata(), ecdsa.ExportSubjectPublicKeyInfo(), binding)
        );
    }

    [Fact]
    public void CreateRsaRemote_WhenTheRowMatches_CreatesARemoteKeyThatVerifies()
    {
        var factory = CreateSecretKeyFactory();
        using var rsa = RSA.Create(2048);
        var binding = CreateBinding(CreateRsaRow());

        var key = factory.CreateRsaRemote(
            new KeyMetadata { KeyId = "rsa-remote" },
            rsa.ExportSubjectPublicKeyInfo(),
            binding
        );

        var remoteKey = Assert.IsAssignableFrom<IRemoteSecretKey>(key);
        Assert.Same(binding, remoteKey.Binding);
        Assert.Equal(2048, key.KeySizeBits);

        byte[] data = [1, 2, 3, 4];
        var signature = rsa.SignData(data, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var exported = key.ExportRSA();
        Assert.True(
            exported.VerifyData(
                data,
                signature,
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1
            )
        );
    }

    [Fact]
    public void CreateRsaRemote_WhenTheRowIsNotForRsa_Throws()
    {
        var factory = CreateSecretKeyFactory();
        using var rsa = RSA.Create(2048);
        var binding = CreateBinding(CreateEccRow());

        Assert.Throws<ArgumentException>(() =>
            factory.CreateRsaRemote(new KeyMetadata(), rsa.ExportSubjectPublicKeyInfo(), binding)
        );
    }

    [Fact]
    public void CreateRsaRemote_WhenTheSizeDiffersFromTheRow_Throws()
    {
        var factory = CreateSecretKeyFactory();
        using var rsa = RSA.Create(2048);
        var binding = CreateBinding(CreateRsaRow(keySizeBits: 3072));

        Assert.Throws<ArgumentException>(() =>
            factory.CreateRsaRemote(new KeyMetadata(), rsa.ExportSubjectPublicKeyInfo(), binding)
        );
    }

    [Fact]
    public void CreateEccRemote_WhenTheRowMatches_CreatesARemoteKeyThatVerifies()
    {
        var factory = CreateSecretKeyFactory();
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var binding = CreateBinding(CreateEccRow());

        var key = factory.CreateEccRemote(
            new KeyMetadata { KeyId = "ecc-remote" },
            ecdsa.ExportSubjectPublicKeyInfo(),
            binding
        );

        var remoteKey = Assert.IsAssignableFrom<IRemoteSecretKey>(key);
        Assert.Same(binding, remoteKey.Binding);
        Assert.Equal(256, key.KeySizeBits);
        Assert.Equal("P-256", key.CurveName);

        byte[] data = [5, 6, 7, 8];
        var signature = ecdsa.SignData(data, HashAlgorithmName.SHA256);
        using var exported = key.ExportECDsa();
        Assert.True(exported.VerifyData(data, signature, HashAlgorithmName.SHA256));
    }

    [Fact]
    public void CreateEccRemote_WhenTheRowIsNotForEcc_Throws()
    {
        var factory = CreateSecretKeyFactory();
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var binding = CreateBinding(CreateEccRow(secretType: SecretKeyTypes.Rsa));

        Assert.Throws<ArgumentException>(() =>
            factory.CreateEccRemote(new KeyMetadata(), ecdsa.ExportSubjectPublicKeyInfo(), binding)
        );
    }

    [Fact]
    public void CreateEccRemote_WhenTheSizeDiffersFromTheRow_Throws()
    {
        var factory = CreateSecretKeyFactory();
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var binding = CreateBinding(CreateEccRow(keySizeBits: 384));

        Assert.Throws<ArgumentException>(() =>
            factory.CreateEccRemote(new KeyMetadata(), ecdsa.ExportSubjectPublicKeyInfo(), binding)
        );
    }

    [Fact]
    public void CreateEccRemote_WhenTheCurveDiffersFromTheRow_Throws()
    {
        var factory = CreateSecretKeyFactory(Brainpool256);
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var binding = CreateBinding(CreateEccRow(curve: Brainpool256.CurveName));

        Assert.Throws<ArgumentException>(() =>
            factory.CreateEccRemote(new KeyMetadata(), ecdsa.ExportSubjectPublicKeyInfo(), binding)
        );
    }

    #endregion
}
