// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers.Text;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.DataProtection;
using NCode.Identity.Secrets.KeyMaterial;
using NCode.Identity.Secrets.Keys;
using NCode.Identity.Secrets.KeyTypes;

namespace NCode.Identity.Secrets.Persistence;

public class DefaultSecretImporterTests
{
    private readonly DefaultSecretImporter _importer;

    public DefaultSecretImporterTests()
    {
        var provider = new EphemeralDataProtectionProvider();
        var secretKeyFactory = new DefaultSecretKeyFactory(
            new TestDataProtectorFactory<SecretKey>(provider.CreateProtector("factory")),
            new DefaultEccCurveCatalog([])
        );

        var catalog = new DefaultSecretKeyTypeCatalog([
            new SymmetricSecretKeyTypeDescriptor(secretKeyFactory),
            new RsaSecretKeyTypeDescriptor(secretKeyFactory),
        ]);

        _importer = new DefaultSecretImporter(catalog);
    }

    private static SecretKeyImportRequest CreateRequest(
        string secretType,
        string keyMaterial,
        string? keyId,
        string format = SecretKeyImportFormats.Pem
    ) =>
        new()
        {
            SecretId = "secret-1",
            KeyId = keyId,
            SecretType = secretType,
            Format = format,
            KeyMaterial = keyMaterial,
            Use = "sig",
            Algorithm = "RS256",
            CreatedWhen = DateTimeOffset.UnixEpoch,
            ExpiresWhen = DateTimeOffset.MaxValue,
        };

    #region ImportSecret Tests

    [Fact]
    public void ImportSecret_WhenKeyIdIsSet_PersistsKeyId()
    {
        using var rsa = RSA.Create(2048);
        var request = CreateRequest(
            SecretTypes.Rsa,
            rsa.ExportSubjectPublicKeyInfoPem(),
            "client-kid"
        );

        var persisted = _importer.ImportSecret(request);

        Assert.Equal("secret-1", persisted.SecretId);
        Assert.Equal("client-kid", persisted.KeyId);
        Assert.Equal(SecretEncodings.Spki, persisted.SecretEncoding);
    }

    [Fact]
    public void ImportSecret_SpkiPem_StoresPlainBase64Url()
    {
        using var rsa = RSA.Create(2048);
        var request = CreateRequest(SecretTypes.Rsa, rsa.ExportSubjectPublicKeyInfoPem(), null);

        var persisted = _importer.ImportSecret(request);

        Assert.Equal(SecretEncodings.Spki, persisted.SecretEncoding);
        Assert.Equal(
            rsa.ExportSubjectPublicKeyInfo(),
            Base64Url.DecodeFromChars(persisted.EncodedValue)
        );
    }

    [Fact]
    public void ImportSecret_Certificate_StoresPlainX509()
    {
        using var rsa = RSA.Create(2048);
        var certificateRequest = new CertificateRequest(
            "CN=client",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1
        );
        using var certificate = certificateRequest.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow.AddDays(1)
        );
        var request = CreateRequest(
            SecretTypes.Rsa,
            certificate.ExportCertificatePem(),
            null,
            format: SecretKeyImportFormats.Certificate
        );

        var persisted = _importer.ImportSecret(request);

        Assert.Equal(SecretEncodings.X509, persisted.SecretEncoding);
        Assert.Equal(certificate.RawData, Base64Url.DecodeFromChars(persisted.EncodedValue));
    }

    [Fact]
    public void ImportSecret_WhenKeyIdIsNull_LeavesKeyIdUnset()
    {
        using var rsa = RSA.Create(2048);
        var request = CreateRequest(SecretTypes.Rsa, rsa.ExportSubjectPublicKeyInfoPem(), null);

        var persisted = _importer.ImportSecret(request);

        Assert.Null(persisted.KeyId);
    }

    [Fact]
    public void ImportSecret_WhenTheRequestNamesNoLifecycle_ActivatesAtCreationAndRetiresAtExpiry()
    {
        using var rsa = RSA.Create(2048);
        var request = CreateRequest(SecretKeyTypes.Rsa, rsa.ExportSubjectPublicKeyInfoPem(), null);

        var persisted = _importer.ImportSecret(request);

        Assert.Equal(request.CreatedWhen, persisted.ActivatesWhen);
        Assert.Equal(request.ExpiresWhen, persisted.RetiresWhen);
        Assert.Null(persisted.ManagedBy);
    }

    [Fact]
    public void ImportSecret_WhenTheRequestNamesALifecycle_StoresIt()
    {
        using var rsa = RSA.Create(2048);
        var request = new SecretKeyImportRequest
        {
            SecretId = "secret-1",
            KeyId = null,
            SecretType = SecretKeyTypes.Rsa,
            Format = SecretKeyImportFormats.Pem,
            KeyMaterial = rsa.ExportSubjectPublicKeyInfoPem(),
            Use = "sig",
            Algorithm = "RS256",
            CreatedWhen = DateTimeOffset.UnixEpoch,
            ActivatesWhen = DateTimeOffset.UnixEpoch.AddDays(2),
            RetiresWhen = DateTimeOffset.UnixEpoch.AddDays(92),
            ExpiresWhen = DateTimeOffset.UnixEpoch.AddDays(99),
        };

        var persisted = _importer.ImportSecret(request);

        Assert.Equal(DateTimeOffset.UnixEpoch.AddDays(2), persisted.ActivatesWhen);
        Assert.Equal(DateTimeOffset.UnixEpoch.AddDays(92), persisted.RetiresWhen);
        Assert.Equal(DateTimeOffset.UnixEpoch.AddDays(99), persisted.ExpiresWhen);
    }

    [Fact]
    public void ImportSecret_WhenTheLifecycleIsOutOfOrder_ThrowsArgumentException()
    {
        using var rsa = RSA.Create(2048);
        var request = new SecretKeyImportRequest
        {
            SecretId = "secret-1",
            KeyId = null,
            SecretType = SecretKeyTypes.Rsa,
            Format = SecretKeyImportFormats.Pem,
            KeyMaterial = rsa.ExportSubjectPublicKeyInfoPem(),
            Use = "sig",
            Algorithm = "RS256",
            CreatedWhen = DateTimeOffset.UnixEpoch,
            RetiresWhen = DateTimeOffset.UnixEpoch.AddDays(100),
            ExpiresWhen = DateTimeOffset.UnixEpoch.AddDays(99),
        };

        var exception = Assert.Throws<ArgumentException>(() => _importer.ImportSecret(request));

        Assert.Contains("cannot expire", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ImportSecret_WhenTypeHasNoPublicKey_Throws()
    {
        var request = CreateRequest(SecretTypes.Symmetric, "material", null);

        Assert.Throws<ArgumentException>(() => _importer.ImportSecret(request));
    }

    [Fact]
    public void ImportSecret_WhenTheTypeDoesNotImportTheFormat_Throws()
    {
        using var rsa = RSA.Create(2048);
        var request = CreateRequest(
            SecretTypes.Rsa,
            rsa.ExportSubjectPublicKeyInfoPem(),
            null,
            format: "openssh"
        );

        var exception = Assert.Throws<ArgumentException>(() => _importer.ImportSecret(request));

        Assert.Contains("'openssh'", exception.Message, StringComparison.Ordinal);
    }

    #endregion
}
