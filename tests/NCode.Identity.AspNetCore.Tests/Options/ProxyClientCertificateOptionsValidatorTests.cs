// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using NCode.Buffers;
using NCode.Identity.AspNetCore.Options;
using Xunit;

namespace NCode.Identity.AspNetCore.Tests.Options;

public sealed class ProxyClientCertificateOptionsValidatorTests : IDisposable
{
    private static ProxyClientCertificateOptionsValidator Validator { get; } = new();

    private static DateTimeOffset Now { get; } = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private DirectoryInfo Root { get; } = Directory.CreateTempSubdirectory("proxy-ca-");

    public void Dispose() => Root.Delete(recursive: true);

    #region Helpers

    private string WriteCertificateAuthority()
    {
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var request = new CertificateRequest("CN=ca", ecdsa, HashAlgorithmName.SHA256);
        using var certificate = request.CreateSelfSigned(Now.AddDays(-1), Now.AddDays(30));

        var path = Path.Combine(Root.FullName, "ca.crt");
        File.WriteAllText(path, certificate.ExportCertificatePem(), SecureEncoding.UTF8);
        return path;
    }

    private string WriteFile(string contents)
    {
        var path = Path.Combine(Root.FullName, "other.crt");
        File.WriteAllText(path, contents, SecureEncoding.UTF8);
        return path;
    }

    #endregion

    #region Validate Tests

    [Fact]
    public void Validate_WhenNotRequiredAndEmpty_Succeeds()
    {
        var result = Validator.Validate(name: null, new ProxyClientCertificateOptions());

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_WhenNotRequiredWithAPath_Fails()
    {
        var result = Validator.Validate(
            name: null,
            new ProxyClientCertificateOptions
            {
                CertificateAuthorityPath = WriteCertificateAuthority(),
            }
        );

        Assert.True(result.Failed);
        Assert.Contains(
            "ProxyClientCertificate:CertificateAuthorityPath is set while ProxyClientCertificate:Required is false",
            result.FailureMessage,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void Validate_WhenNotRequiredWithASubject_Fails()
    {
        var result = Validator.Validate(
            name: null,
            new ProxyClientCertificateOptions { AllowedSubjects = ["CN=ingress"] }
        );

        Assert.True(result.Failed);
        Assert.Contains(
            "ProxyClientCertificate:AllowedSubjects is set while ProxyClientCertificate:Required is false",
            result.FailureMessage,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void Validate_WhenRequiredWithAValidFileAndSubject_Succeeds()
    {
        var result = Validator.Validate(
            name: null,
            new ProxyClientCertificateOptions
            {
                Required = true,
                CertificateAuthorityPath = WriteCertificateAuthority(),
                AllowedSubjects = ["CN=ingress"],
            }
        );

        Assert.True(result.Succeeded);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Validate_WhenRequiredWithoutAPath_Fails(string? path)
    {
        var result = Validator.Validate(
            name: null,
            new ProxyClientCertificateOptions
            {
                Required = true,
                CertificateAuthorityPath = path,
                AllowedSubjects = ["CN=ingress"],
            }
        );

        Assert.True(result.Failed);
        Assert.Contains(
            "ProxyClientCertificate:CertificateAuthorityPath is required",
            result.FailureMessage,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void Validate_WhenRequiredWithAMissingFile_Fails()
    {
        var result = Validator.Validate(
            name: null,
            new ProxyClientCertificateOptions
            {
                Required = true,
                CertificateAuthorityPath = Path.Combine(Root.FullName, "missing.crt"),
                AllowedSubjects = ["CN=ingress"],
            }
        );

        Assert.True(result.Failed);
        Assert.Contains("is not an existing file", result.FailureMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_WhenRequiredWithAFileWithoutACertificate_Fails()
    {
        var result = Validator.Validate(
            name: null,
            new ProxyClientCertificateOptions
            {
                Required = true,
                CertificateAuthorityPath = WriteFile("not a certificate"),
                AllowedSubjects = ["CN=ingress"],
            }
        );

        Assert.True(result.Failed);
        Assert.Contains("holds no certificate.", result.FailureMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_WhenRequiredWithAnEmptyFile_Fails()
    {
        var result = Validator.Validate(
            name: null,
            new ProxyClientCertificateOptions
            {
                Required = true,
                CertificateAuthorityPath = WriteFile(string.Empty),
                AllowedSubjects = ["CN=ingress"],
            }
        );

        Assert.True(result.Failed);
        Assert.Contains("holds no certificate.", result.FailureMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_WhenRequiredWithoutASubject_Fails()
    {
        var result = Validator.Validate(
            name: null,
            new ProxyClientCertificateOptions
            {
                Required = true,
                CertificateAuthorityPath = WriteCertificateAuthority(),
            }
        );

        Assert.True(result.Failed);
        Assert.Contains(
            "ProxyClientCertificate:AllowedSubjects must list at least one subject",
            result.FailureMessage,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void Validate_WhenRequiredWithAMalformedSubject_FailsNamingItsIndex()
    {
        var result = Validator.Validate(
            name: null,
            new ProxyClientCertificateOptions
            {
                Required = true,
                CertificateAuthorityPath = WriteCertificateAuthority(),
                AllowedSubjects = ["CN=ingress", "not a distinguished name"],
            }
        );

        Assert.True(result.Failed);
        Assert.Contains(
            "ProxyClientCertificate:AllowedSubjects:1 'not a distinguished name'",
            result.FailureMessage,
            StringComparison.Ordinal
        );
    }

    #endregion
}
