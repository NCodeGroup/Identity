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
            new TestDataProtectorFactory<SecretKey>(provider.CreateProtector("factory"))
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
