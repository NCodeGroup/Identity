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

using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Xml.Linq;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Microsoft.AspNetCore.DataProtection.XmlEncryption;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NCode.Identity.Server.KeyRingProtection;
using Xunit;

namespace NCode.Identity.Server.Tests;

public sealed class KeyRingProtectionRegistrationTests : BaseTests
{
    private const string Password = "key-ring-password";
    private const string Purpose = "key-ring-test";
    private const string Section = KeyRingProtectionOptions.DefaultSectionName;

    private static readonly XName EncryptedDataName = XName.Get(
        "EncryptedData",
        "http://www.w3.org/2001/04/xmlenc#"
    );

    private string TempDirectory { get; } =
        Directory.CreateTempSubdirectory("key-ring-protection-").FullName;

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            Directory.Delete(TempDirectory, recursive: true);

        base.Dispose(disposing);
    }

    #region Helpers

    private sealed class InMemoryXmlRepository : IXmlRepository
    {
        private List<XElement> Elements { get; } = [];

        public IReadOnlyCollection<XElement> GetAllElements() =>
            Elements.Select(element => new XElement(element)).ToList();

        public void StoreElement(XElement element, string friendlyName) =>
            Elements.Add(new XElement(element));
    }

    private (string CertificatePath, string PrivateKeyPath) WriteRsaPem(
        string name,
        bool encryptPrivateKey = false
    )
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            $"CN={name}",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1
        );
        using var certificate = request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow.AddDays(1)
        );

        var certificatePath = Path.Combine(TempDirectory, $"{name}.crt");
        var privateKeyPath = Path.Combine(TempDirectory, $"{name}.key");
        File.WriteAllText(certificatePath, certificate.ExportCertificatePem());
        File.WriteAllText(
            privateKeyPath,
            encryptPrivateKey
                ? rsa.ExportEncryptedPkcs8PrivateKeyPem(
                    Password,
                    new PbeParameters(
                        PbeEncryptionAlgorithm.Aes256Cbc,
                        HashAlgorithmName.SHA256,
                        iterationCount: 1000
                    )
                )
                : rsa.ExportPkcs8PrivateKeyPem()
        );
        return (certificatePath, privateKeyPath);
    }

    private string WriteRsaPkcs12(string name)
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            $"CN={name}",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1
        );
        using var certificate = request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow.AddDays(1)
        );

        var path = Path.Combine(TempDirectory, $"{name}.pfx");
        File.WriteAllBytes(
            path,
            certificate.ExportPkcs12(Pkcs12ExportPbeParameters.Pbes2Aes256Sha256, Password)
        );
        return path;
    }

    private (string CertificatePath, string PrivateKeyPath) WriteEcdsaPem(string name)
    {
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var request = new CertificateRequest($"CN={name}", ecdsa, HashAlgorithmName.SHA256);
        using var certificate = request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow.AddDays(1)
        );

        var certificatePath = Path.Combine(TempDirectory, $"{name}.crt");
        var privateKeyPath = Path.Combine(TempDirectory, $"{name}.key");
        File.WriteAllText(certificatePath, certificate.ExportCertificatePem());
        File.WriteAllText(privateKeyPath, ecdsa.ExportPkcs8PrivateKeyPem());
        return (certificatePath, privateKeyPath);
    }

    private ServiceProvider BuildServiceProvider(
        Dictionary<string, string?> configurationData,
        IXmlRepository? repository = null,
        bool appliesProtection = true
    )
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configurationData)
            .Build();

        var serviceCollection = new ServiceCollection();
        var dataProtectionBuilder = serviceCollection
            .AddDataProtection()
            .SetApplicationName("key-ring-protection-tests");
        if (repository is not null)
            serviceCollection.Configure<KeyManagementOptions>(options =>
                options.XmlRepository = repository
            );

        // The builder is only touched when a certificate is applied; otherwise the strict mock rejects any access.
        var builder = CreateStrictMock<IIdentityServerBuilder>();
        if (appliesProtection)
            builder.Setup(x => x.DataProtectionBuilder).Returns(dataProtectionBuilder).Verifiable();

        KeyRingProtectionRegistration.AddKeyRingProtection(builder.Object, configuration);

        return serviceCollection.BuildServiceProvider();
    }

    #endregion

    #region AddKeyRingProtection

    [Fact]
    public void AddKeyRingProtection_WhenSectionAbsent_LeavesTheKeyRingUnprotected()
    {
        using var serviceProvider = BuildServiceProvider([], appliesProtection: false);

        var options = serviceProvider.GetRequiredService<IOptions<KeyManagementOptions>>().Value;

        Assert.Null(options.XmlEncryptor);
    }

    [Fact]
    public void AddKeyRingProtection_WhenPemCertificateConfigured_EncryptsNewKeys()
    {
        var (certificatePath, privateKeyPath) = WriteRsaPem("current");
        var repository = new InMemoryXmlRepository();
        using var serviceProvider = BuildServiceProvider(
            new Dictionary<string, string?>
            {
                [$"{Section}:Certificate:CertificatePath"] = certificatePath,
                [$"{Section}:Certificate:PrivateKeyPath"] = privateKeyPath,
            },
            repository
        );

        var protector = serviceProvider
            .GetRequiredService<IDataProtectionProvider>()
            .CreateProtector(Purpose);
        var protectedPayload = protector.Protect("payload");

        Assert.IsType<CertificateXmlEncryptor>(
            serviceProvider.GetRequiredService<IOptions<KeyManagementOptions>>().Value.XmlEncryptor
        );
        Assert.Equal("payload", protector.Unprotect(protectedPayload));
        var key = Assert.Single(repository.GetAllElements());
        Assert.Single(key.Descendants(EncryptedDataName));
    }

    [Fact]
    public void AddKeyRingProtection_WhenEncryptedPemPrivateKeyConfigured_EncryptsNewKeys()
    {
        var (certificatePath, privateKeyPath) = WriteRsaPem("current", encryptPrivateKey: true);
        using var serviceProvider = BuildServiceProvider(
            new Dictionary<string, string?>
            {
                [$"{Section}:Certificate:CertificatePath"] = certificatePath,
                [$"{Section}:Certificate:PrivateKeyPath"] = privateKeyPath,
                [$"{Section}:Certificate:Password"] = Password,
            }
        );

        var options = serviceProvider.GetRequiredService<IOptions<KeyManagementOptions>>().Value;

        Assert.IsType<CertificateXmlEncryptor>(options.XmlEncryptor);
    }

    [Fact]
    public void AddKeyRingProtection_WhenPkcs12CertificateConfigured_EncryptsNewKeys()
    {
        var path = WriteRsaPkcs12("current");
        using var serviceProvider = BuildServiceProvider(
            new Dictionary<string, string?>
            {
                [$"{Section}:Certificate:CertificatePath"] = path,
                [$"{Section}:Certificate:Password"] = Password,
            }
        );

        var options = serviceProvider.GetRequiredService<IOptions<KeyManagementOptions>>().Value;

        Assert.IsType<CertificateXmlEncryptor>(options.XmlEncryptor);
    }

    [Fact]
    public void AddKeyRingProtection_WhenCertificateRotated_DecryptsKeysWithThePreviousCertificate()
    {
        var (oldCertificatePath, oldPrivateKeyPath) = WriteRsaPem("old");
        var (newCertificatePath, newPrivateKeyPath) = WriteRsaPem("new");
        var repository = new InMemoryXmlRepository();
        string protectedPayload;
        using (
            var oldProvider = BuildServiceProvider(
                new Dictionary<string, string?>
                {
                    [$"{Section}:Certificate:CertificatePath"] = oldCertificatePath,
                    [$"{Section}:Certificate:PrivateKeyPath"] = oldPrivateKeyPath,
                },
                repository
            )
        )
        {
            protectedPayload = oldProvider
                .GetRequiredService<IDataProtectionProvider>()
                .CreateProtector(Purpose)
                .Protect("payload");
        }

        using var rotatedProvider = BuildServiceProvider(
            new Dictionary<string, string?>
            {
                [$"{Section}:Certificate:CertificatePath"] = newCertificatePath,
                [$"{Section}:Certificate:PrivateKeyPath"] = newPrivateKeyPath,
                [$"{Section}:PreviousCertificates:0:CertificatePath"] = oldCertificatePath,
                [$"{Section}:PreviousCertificates:0:PrivateKeyPath"] = oldPrivateKeyPath,
            },
            repository
        );

        var payload = rotatedProvider
            .GetRequiredService<IDataProtectionProvider>()
            .CreateProtector(Purpose)
            .Unprotect(protectedPayload);

        Assert.Equal("payload", payload);
    }

    [Fact]
    public void AddKeyRingProtection_WhenPreviousCertificatesWithoutCurrent_Throws()
    {
        var (certificatePath, privateKeyPath) = WriteRsaPem("old");

        var exception = Assert.Throws<InvalidOperationException>(() =>
            BuildServiceProvider(
                new Dictionary<string, string?>
                {
                    [$"{Section}:PreviousCertificates:0:CertificatePath"] = certificatePath,
                    [$"{Section}:PreviousCertificates:0:PrivateKeyPath"] = privateKeyPath,
                },
                appliesProtection: false
            )
        );

        Assert.Contains("no current 'Certificate'", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AddKeyRingProtection_WhenSectionHasUnknownKey_Throws()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            BuildServiceProvider(
                new Dictionary<string, string?> { [$"{Section}:Thumbprint"] = "ABCDEF" },
                appliesProtection: false
            )
        );

        Assert.Contains($"'{Section}'", exception.Message, StringComparison.Ordinal);
    }

    #endregion

    #region LoadCertificate

    [Fact]
    public void LoadCertificate_WhenCertificatePathUnset_Throws()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            KeyRingProtectionRegistration.LoadCertificate(
                new KeyRingCertificateOptions(),
                "Section:Certificate"
            )
        );

        Assert.Equal("'Section:Certificate:CertificatePath' is not set.", exception.Message);
    }

    [Fact]
    public void LoadCertificate_WhenFileMissing_Throws()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            KeyRingProtectionRegistration.LoadCertificate(
                new KeyRingCertificateOptions
                {
                    CertificatePath = Path.Combine(TempDirectory, "missing.crt"),
                    PrivateKeyPath = Path.Combine(TempDirectory, "missing.key"),
                },
                "Section:Certificate"
            )
        );

        Assert.StartsWith(
            "The key ring protection certificate 'Section:Certificate' cannot be loaded",
            exception.Message,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void LoadCertificate_WhenPkcs12PasswordWrong_Throws()
    {
        var path = WriteRsaPkcs12("current");

        var exception = Assert.Throws<InvalidOperationException>(() =>
            KeyRingProtectionRegistration.LoadCertificate(
                new KeyRingCertificateOptions { CertificatePath = path, Password = "wrong" },
                "Section:Certificate"
            )
        );

        Assert.IsAssignableFrom<CryptographicException>(exception.InnerException);
    }

    [Fact]
    public void LoadCertificate_WhenCertificateHasNoRsaKey_Throws()
    {
        var (certificatePath, privateKeyPath) = WriteEcdsaPem("ecdsa");

        var exception = Assert.Throws<InvalidOperationException>(() =>
            KeyRingProtectionRegistration.LoadCertificate(
                new KeyRingCertificateOptions
                {
                    CertificatePath = certificatePath,
                    PrivateKeyPath = privateKeyPath,
                },
                "Section:Certificate"
            )
        );

        Assert.Contains("has no RSA private key", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void LoadCertificate_WhenPemConfigured_ReturnsCertificateWithPrivateKey()
    {
        var (certificatePath, privateKeyPath) = WriteRsaPem("current");

        using var certificate = KeyRingProtectionRegistration.LoadCertificate(
            new KeyRingCertificateOptions
            {
                CertificatePath = certificatePath,
                PrivateKeyPath = privateKeyPath,
            },
            "Section:Certificate"
        );

        Assert.True(certificate.HasPrivateKey);
        Assert.Equal("CN=current", certificate.Subject);
    }

    #endregion
}
