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

    private FakeTimeProvider Time { get; } = new(Now);

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

    private void WriteAnchors(
        DateTime lastWriteTimeUtc,
        params X509Certificate2[] certificateAuthorities
    )
    {
        File.WriteAllText(
            AnchorPath,
            string.Concat(certificateAuthorities.Select(x => x.ExportCertificatePem() + "\n")),
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
            Time
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

    #region Verify Tests

    [Fact]
    public void Verify_WhenIssuedByTheAnchorWithClientAuthAndAllowedSubject_ReturnsNone()
    {
        using var authority = ProxyClientCertificateTestCertificates.CreateCertificateAuthority(
            "CN=ca",
            Now
        );
        WriteAnchor(authority, Now.UtcDateTime);
        using var leaf = CreateClientLeaf(authority);

        Assert.Equal(ProxyClientCertificateRefusal.None, CreateVerifier().Verify(leaf));
    }

    [Fact]
    public void Verify_WhenIssuedByAnUntrustedAuthority_ReturnsUnanchoredChain()
    {
        using var authority = ProxyClientCertificateTestCertificates.CreateCertificateAuthority(
            "CN=ca",
            Now
        );
        using var foreignAuthority =
            ProxyClientCertificateTestCertificates.CreateCertificateAuthority("CN=foreign", Now);
        WriteAnchor(authority, Now.UtcDateTime);
        using var leaf = CreateClientLeaf(foreignAuthority);

        Assert.Equal(ProxyClientCertificateRefusal.UnanchoredChain, CreateVerifier().Verify(leaf));
    }

    [Fact]
    public void Verify_WhenSelfSigned_ReturnsUnanchoredChain()
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

        Assert.Equal(ProxyClientCertificateRefusal.UnanchoredChain, CreateVerifier().Verify(leaf));
    }

    [Fact]
    public void Verify_WhenOnlyServerAuth_ReturnsMissingClientAuthUsage()
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

        Assert.Equal(
            ProxyClientCertificateRefusal.MissingClientAuthUsage,
            CreateVerifier().Verify(leaf)
        );
    }

    [Fact]
    public void Verify_WhenNoExtendedKeyUsage_ReturnsMissingClientAuthUsage()
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

        Assert.Equal(
            ProxyClientCertificateRefusal.MissingClientAuthUsage,
            CreateVerifier().Verify(leaf)
        );
    }

    [Fact]
    public void Verify_WhenSubjectIsNotAllowed_ReturnsSubjectNotAllowed()
    {
        using var authority = ProxyClientCertificateTestCertificates.CreateCertificateAuthority(
            "CN=ca",
            Now
        );
        WriteAnchor(authority, Now.UtcDateTime);
        using var leaf = CreateClientLeaf(authority, "CN=other");

        Assert.Equal(
            ProxyClientCertificateRefusal.SubjectNotAllowed,
            CreateVerifier().Verify(leaf)
        );
    }

    [Fact]
    public void Verify_WhenExpired_ReturnsNotYetValidOrExpired()
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

        Assert.Equal(
            ProxyClientCertificateRefusal.NotYetValidOrExpired,
            CreateVerifier().Verify(leaf)
        );
    }

    [Fact]
    public void Verify_WhenNotYetValid_ReturnsNotYetValidOrExpired()
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

        Assert.Equal(
            ProxyClientCertificateRefusal.NotYetValidOrExpired,
            CreateVerifier().Verify(leaf)
        );
    }

    [Fact]
    public void Verify_WhenTheIssuerIsMissingFromTheAnchors_ReturnsUnanchoredChain()
    {
        using var authority = ProxyClientCertificateTestCertificates.CreateCertificateAuthority(
            "CN=ca",
            Now
        );
        using var foreignAuthority =
            ProxyClientCertificateTestCertificates.CreateCertificateAuthority("CN=foreign", Now);
        WriteAnchor(authority, Now.UtcDateTime);
        using var leaf = ProxyClientCertificateTestCertificates.CreateLeaf(
            foreignAuthority,
            AllowedSubject,
            [ProxyClientCertificateTestCertificates.ClientAuthenticationOid],
            Now.AddDays(-1),
            Now.AddDays(30),
            caIssuersUrl: "http://127.0.0.1:1/ca.crt"
        );

        Assert.Equal(ProxyClientCertificateRefusal.UnanchoredChain, CreateVerifier().Verify(leaf));
    }

    [Fact]
    public void Verify_WhenTheAnchorsHoldTheRootAndTheIntermediate_AcceptsALeafIssuedByTheIntermediate()
    {
        using var root = ProxyClientCertificateTestCertificates.CreateCertificateAuthority(
            "CN=root",
            Now
        );
        using var intermediate = ProxyClientCertificateTestCertificates.CreateIntermediateAuthority(
            root,
            "CN=intermediate",
            Now.AddDays(-30),
            Now.AddDays(300)
        );
        WriteAnchors(Now.UtcDateTime, root, intermediate);
        using var leaf = CreateClientLeaf(intermediate);

        Assert.Equal(ProxyClientCertificateRefusal.None, CreateVerifier().Verify(leaf));
    }

    [Fact]
    public void Verify_WhenTheAnchorIsOnlyTheIntermediate_AcceptsALeafItIssued()
    {
        using var root = ProxyClientCertificateTestCertificates.CreateCertificateAuthority(
            "CN=root",
            Now
        );
        using var intermediate = ProxyClientCertificateTestCertificates.CreateIntermediateAuthority(
            root,
            "CN=intermediate",
            Now.AddDays(-30),
            Now.AddDays(300)
        );
        WriteAnchor(intermediate, Now.UtcDateTime);
        using var leaf = CreateClientLeaf(intermediate);

        Assert.Equal(ProxyClientCertificateRefusal.None, CreateVerifier().Verify(leaf));
    }

    [Fact]
    public void Verify_WhenTheAnchorIsOnlyTheRootAndTheIntermediateIsNotSent_ReturnsUnanchoredChain()
    {
        using var root = ProxyClientCertificateTestCertificates.CreateCertificateAuthority(
            "CN=root",
            Now
        );
        using var intermediate = ProxyClientCertificateTestCertificates.CreateIntermediateAuthority(
            root,
            "CN=intermediate",
            Now.AddDays(-30),
            Now.AddDays(300)
        );
        WriteAnchor(root, Now.UtcDateTime);
        using var leaf = CreateClientLeaf(intermediate);

        Assert.Equal(ProxyClientCertificateRefusal.UnanchoredChain, CreateVerifier().Verify(leaf));
    }

    [Fact]
    public void Verify_WhenTheAnchorIsAnExpiredIntermediate_ReturnsUnanchoredChain()
    {
        using var root = ProxyClientCertificateTestCertificates.CreateCertificateAuthority(
            "CN=root",
            Now
        );
        using var intermediate = ProxyClientCertificateTestCertificates.CreateIntermediateAuthority(
            root,
            "CN=intermediate",
            Now.AddDays(-60),
            Now.AddDays(-1)
        );
        WriteAnchor(intermediate, Now.UtcDateTime);
        using var leaf = CreateClientLeaf(intermediate);

        Assert.Equal(ProxyClientCertificateRefusal.UnanchoredChain, CreateVerifier().Verify(leaf));
    }

    [Fact]
    public void Verify_WhenTheAnchorFileIsRewrittenBeforeTheRefreshInterval_KeepsTheLoadedAnchors()
    {
        using var firstAuthority =
            ProxyClientCertificateTestCertificates.CreateCertificateAuthority("CN=first", Now);
        using var secondAuthority =
            ProxyClientCertificateTestCertificates.CreateCertificateAuthority("CN=second", Now);
        using var firstLeaf = CreateClientLeaf(firstAuthority);
        using var secondLeaf = CreateClientLeaf(secondAuthority);
        WriteAnchor(firstAuthority, Now.UtcDateTime);
        var verifier = CreateVerifier();

        Assert.Equal(ProxyClientCertificateRefusal.None, verifier.Verify(firstLeaf));

        WriteAnchor(secondAuthority, Now.UtcDateTime.AddMinutes(1));
        Time.Advance(
            ProxyClientCertificateVerifier.AnchorRefreshInterval - TimeSpan.FromSeconds(1)
        );

        Assert.Equal(ProxyClientCertificateRefusal.None, verifier.Verify(firstLeaf));
        Assert.Equal(ProxyClientCertificateRefusal.UnanchoredChain, verifier.Verify(secondLeaf));
    }

    [Fact]
    public void Verify_WhenTheAnchorFileIsRewrittenAndTheRefreshIntervalPassed_UsesTheNewAnchors()
    {
        using var firstAuthority =
            ProxyClientCertificateTestCertificates.CreateCertificateAuthority("CN=first", Now);
        using var secondAuthority =
            ProxyClientCertificateTestCertificates.CreateCertificateAuthority("CN=second", Now);
        using var firstLeaf = CreateClientLeaf(firstAuthority);
        using var secondLeaf = CreateClientLeaf(secondAuthority);
        WriteAnchor(firstAuthority, Now.UtcDateTime);
        var verifier = CreateVerifier();

        Assert.Equal(ProxyClientCertificateRefusal.None, verifier.Verify(firstLeaf));
        Assert.Equal(ProxyClientCertificateRefusal.UnanchoredChain, verifier.Verify(secondLeaf));

        WriteAnchor(secondAuthority, Now.UtcDateTime.AddMinutes(1));
        Time.Advance(ProxyClientCertificateVerifier.AnchorRefreshInterval);

        Assert.Equal(ProxyClientCertificateRefusal.UnanchoredChain, verifier.Verify(firstLeaf));
        Assert.Equal(ProxyClientCertificateRefusal.None, verifier.Verify(secondLeaf));
    }

    [Fact]
    public void Verify_WhenTheAnchorFileIsRemovedBeforeTheRefreshInterval_DoesNotReadTheFile()
    {
        using var authority = ProxyClientCertificateTestCertificates.CreateCertificateAuthority(
            "CN=ca",
            Now
        );
        using var firstLeaf = CreateClientLeaf(authority);
        using var secondLeaf = CreateClientLeaf(authority);
        WriteAnchor(authority, Now.UtcDateTime);
        var verifier = CreateVerifier();

        Assert.Equal(ProxyClientCertificateRefusal.None, verifier.Verify(firstLeaf));

        File.Delete(AnchorPath);
        Time.Advance(TimeSpan.FromSeconds(1));

        Assert.Equal(ProxyClientCertificateRefusal.None, verifier.Verify(secondLeaf));
    }

    [Fact]
    public void Verify_WhenTheCertificateExpiresWithinTheVerdictLifetime_RefusesAfterNotAfter()
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
            Now.AddDays(-1),
            Now.AddMinutes(2)
        );
        var verifier = CreateVerifier();

        Assert.Equal(ProxyClientCertificateRefusal.None, verifier.Verify(leaf));

        Time.Advance(TimeSpan.FromSeconds(90));

        Assert.Equal(ProxyClientCertificateRefusal.None, verifier.Verify(leaf));

        Time.Advance(TimeSpan.FromSeconds(60));

        Assert.Equal(ProxyClientCertificateRefusal.NotYetValidOrExpired, verifier.Verify(leaf));
    }

    [Fact]
    public void Verify_WhenMoreCertificatesThanTheCacheHoldsAreVerified_KeepsJudgingEachOne()
    {
        using var authority = ProxyClientCertificateTestCertificates.CreateCertificateAuthority(
            "CN=ca",
            Now
        );
        WriteAnchor(authority, Now.UtcDateTime);
        var verifier = CreateVerifier();

        for (var index = 0; index < ProxyClientCertificateVerifier.MaxCachedVerdicts + 8; index++)
        {
            using var leaf = CreateClientLeaf(authority);
            Assert.Equal(ProxyClientCertificateRefusal.None, verifier.Verify(leaf));
        }
    }

    #endregion
}
