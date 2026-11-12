// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Time.Testing;
using NCode.Buffers;
using NCode.Identity.AspNetCore.Options;
using NCode.Identity.AspNetCore.ProxyClientCertificate;
using Xunit;

namespace NCode.Identity.AspNetCore.Tests.ProxyClientCertificate;

public sealed class ProxyClientCertificateVerifierTests : IDisposable
{
    private const string AllowedSubject = "CN=ingress";

    private static DateTimeOffset Now { get; } = new(2030, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private DirectoryInfo Root { get; } = Directory.CreateTempSubdirectory("proxy-verifier-");

    public void Dispose() => Root.Delete(recursive: true);

    #region Helpers

    private string AnchorPath => Path.Combine(Root.FullName, "ca.crt");

    private void WriteAnchor(X509Certificate2 certificateAuthority, DateTime lastWriteTimeUtc)
    {
        File.WriteAllText(
            AnchorPath,
            certificateAuthority.ExportCertificatePem(),
            SecureEncoding.UTF8
        );
        File.SetLastWriteTimeUtc(AnchorPath, lastWriteTimeUtc);
    }

    private ProxyClientCertificateVerifier CreateVerifier() =>
        new(
            Microsoft.Extensions.Options.Options.Create(
                new ProxyClientCertificateOptions
                {
                    Required = true,
                    CertificateAuthorityPath = AnchorPath,
                    AllowedSubjects = [AllowedSubject],
                }
            ),
            new FakeTimeProvider(Now)
        );

    private static X509Certificate2 CreateClientLeaf(
        X509Certificate2 certificateAuthority,
        string subject = AllowedSubject
    ) =>
        ProxyClientCertificateTestCertificates.CreateLeaf(
            certificateAuthority,
            subject,
            [ProxyClientCertificateTestCertificates.ClientAuthenticationOid],
            Now.AddDays(-1),
            Now.AddDays(30)
        );

    #endregion

    #region IsTrusted Tests

    [Fact]
    public void IsTrusted_WhenIssuedByTheAnchorWithClientAuthAndAllowedSubject_ReturnsTrue()
    {
        using var authority = ProxyClientCertificateTestCertificates.CreateCertificateAuthority(
            "CN=ca",
            Now
        );
        WriteAnchor(authority, Now.UtcDateTime);
        using var leaf = CreateClientLeaf(authority);

        Assert.True(CreateVerifier().IsTrusted(leaf));
    }

    [Fact]
    public void IsTrusted_WhenIssuedByAnUntrustedAuthority_ReturnsFalse()
    {
        using var authority = ProxyClientCertificateTestCertificates.CreateCertificateAuthority(
            "CN=ca",
            Now
        );
        using var foreignAuthority =
            ProxyClientCertificateTestCertificates.CreateCertificateAuthority("CN=foreign", Now);
        WriteAnchor(authority, Now.UtcDateTime);
        using var leaf = CreateClientLeaf(foreignAuthority);

        Assert.False(CreateVerifier().IsTrusted(leaf));
    }

    [Fact]
    public void IsTrusted_WhenSelfSigned_ReturnsFalse()
    {
        using var authority = ProxyClientCertificateTestCertificates.CreateCertificateAuthority(
            "CN=ca",
            Now
        );
        WriteAnchor(authority, Now.UtcDateTime);
        using var leaf = ProxyClientCertificateTestCertificates.CreateSelfSignedLeaf(
            AllowedSubject,
            Now
        );

        Assert.False(CreateVerifier().IsTrusted(leaf));
    }

    [Fact]
    public void IsTrusted_WhenOnlyServerAuth_ReturnsFalse()
    {
        using var authority = ProxyClientCertificateTestCertificates.CreateCertificateAuthority(
            "CN=ca",
            Now
        );
        WriteAnchor(authority, Now.UtcDateTime);
        using var leaf = ProxyClientCertificateTestCertificates.CreateLeaf(
            authority,
            AllowedSubject,
            [ProxyClientCertificateTestCertificates.ServerAuthenticationOid],
            Now.AddDays(-1),
            Now.AddDays(30)
        );

        Assert.False(CreateVerifier().IsTrusted(leaf));
    }

    [Fact]
    public void IsTrusted_WhenNoExtendedKeyUsage_ReturnsFalse()
    {
        using var authority = ProxyClientCertificateTestCertificates.CreateCertificateAuthority(
            "CN=ca",
            Now
        );
        WriteAnchor(authority, Now.UtcDateTime);
        using var leaf = ProxyClientCertificateTestCertificates.CreateLeaf(
            authority,
            AllowedSubject,
            usages: null,
            Now.AddDays(-1),
            Now.AddDays(30)
        );

        Assert.False(CreateVerifier().IsTrusted(leaf));
    }

    [Fact]
    public void IsTrusted_WhenSubjectIsNotAllowed_ReturnsFalse()
    {
        using var authority = ProxyClientCertificateTestCertificates.CreateCertificateAuthority(
            "CN=ca",
            Now
        );
        WriteAnchor(authority, Now.UtcDateTime);
        using var leaf = CreateClientLeaf(authority, "CN=other");

        Assert.False(CreateVerifier().IsTrusted(leaf));
    }

    [Fact]
    public void IsTrusted_WhenExpired_ReturnsFalse()
    {
        using var authority = ProxyClientCertificateTestCertificates.CreateCertificateAuthority(
            "CN=ca",
            Now
        );
        WriteAnchor(authority, Now.UtcDateTime);
        using var leaf = ProxyClientCertificateTestCertificates.CreateLeaf(
            authority,
            AllowedSubject,
            [ProxyClientCertificateTestCertificates.ClientAuthenticationOid],
            Now.AddDays(-30),
            Now.AddDays(-1)
        );

        Assert.False(CreateVerifier().IsTrusted(leaf));
    }

    [Fact]
    public void IsTrusted_WhenNotYetValid_ReturnsFalse()
    {
        using var authority = ProxyClientCertificateTestCertificates.CreateCertificateAuthority(
            "CN=ca",
            Now
        );
        WriteAnchor(authority, Now.UtcDateTime);
        using var leaf = ProxyClientCertificateTestCertificates.CreateLeaf(
            authority,
            AllowedSubject,
            [ProxyClientCertificateTestCertificates.ClientAuthenticationOid],
            Now.AddDays(1),
            Now.AddDays(30)
        );

        Assert.False(CreateVerifier().IsTrusted(leaf));
    }

    [Fact]
    public void IsTrusted_WhenTheAnchorFileIsRewritten_UsesTheNewAnchors()
    {
        using var firstAuthority =
            ProxyClientCertificateTestCertificates.CreateCertificateAuthority("CN=first", Now);
        using var secondAuthority =
            ProxyClientCertificateTestCertificates.CreateCertificateAuthority("CN=second", Now);
        using var firstLeaf = CreateClientLeaf(firstAuthority);
        using var secondLeaf = CreateClientLeaf(secondAuthority);
        WriteAnchor(firstAuthority, Now.UtcDateTime);
        var verifier = CreateVerifier();

        Assert.True(verifier.IsTrusted(firstLeaf));
        Assert.False(verifier.IsTrusted(secondLeaf));

        WriteAnchor(secondAuthority, Now.UtcDateTime.AddMinutes(1));

        Assert.False(verifier.IsTrusted(firstLeaf));
        Assert.True(verifier.IsTrusted(secondLeaf));
    }

    #endregion
}
