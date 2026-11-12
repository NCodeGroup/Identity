// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NCode.Buffers;
using NCode.Identity.AspNetCore.Options;
using NCode.Identity.AspNetCore.ProxyClientCertificate;
using Xunit;

namespace NCode.Identity.AspNetCore.Tests.ProxyClientCertificate;

public sealed class ProxyClientCertificateMiddlewareTests : IDisposable
{
    private const string AllowedSubject = "CN=ingress";

    private static DateTimeOffset Now { get; } = new(2030, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private DirectoryInfo Root { get; } = Directory.CreateTempSubdirectory("proxy-middleware-");

    public void Dispose() => Root.Delete(recursive: true);

    #region Helpers

    private sealed class Harness
    {
        internal required ProxyClientCertificateMiddleware Middleware { get; init; }
        internal bool NextCalled { get; set; }
    }

    private Harness CreateHarness(X509Certificate2 certificateAuthority)
    {
        var anchorPath = Path.Combine(Root.FullName, "ca.crt");
        File.WriteAllText(
            anchorPath,
            certificateAuthority.ExportCertificatePem(),
            SecureEncoding.UTF8
        );

        var verifier = new ProxyClientCertificateVerifier(
            Microsoft.Extensions.Options.Options.Create(
                new ProxyClientCertificateOptions
                {
                    Required = true,
                    CertificateAuthorityPath = anchorPath,
                    AllowedSubjects = [AllowedSubject],
                }
            ),
            new FakeTimeProvider(Now)
        );

        Harness? harness = null;
        var middleware = new ProxyClientCertificateMiddleware(
            _ =>
            {
                harness!.NextCalled = true;
                return Task.CompletedTask;
            },
            verifier,
            NullLogger<ProxyClientCertificateMiddleware>.Instance
        );
        harness = new Harness { Middleware = middleware };
        return harness;
    }

    private static DefaultHttpContext CreateContext(string path, X509Certificate2? certificate)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        context.Connection.ClientCertificate = certificate;
        return context;
    }

    private static X509Certificate2 CreateClientLeaf(X509Certificate2 certificateAuthority) =>
        ProxyClientCertificateTestCertificates.CreateLeaf(
            certificateAuthority,
            AllowedSubject,
            [ProxyClientCertificateTestCertificates.ClientAuthenticationOid],
            Now.AddDays(-1),
            Now.AddDays(30)
        );

    #endregion

    #region InvokeAsync Tests

    [Theory]
    [InlineData(HealthProbeDefaults.LivenessPath)]
    [InlineData(HealthProbeDefaults.ReadinessPath)]
    [InlineData("/Health/Live")]
    public async Task InvokeAsync_WhenAProbePathHasNoCertificate_CallsNextAndSetsNoFeature(
        string path
    )
    {
        using var authority = ProxyClientCertificateTestCertificates.CreateCertificateAuthority(
            "CN=ca",
            Now
        );
        var harness = CreateHarness(authority);
        var context = CreateContext(path, certificate: null);

        await harness.Middleware.InvokeAsync(context);

        Assert.True(harness.NextCalled);
        Assert.Null(context.Features.Get<IProxyClientCertificateFeature>());
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_WhenAnotherPathHasNoCertificate_RefusesWithForbidden()
    {
        using var authority = ProxyClientCertificateTestCertificates.CreateCertificateAuthority(
            "CN=ca",
            Now
        );
        var harness = CreateHarness(authority);
        var context = CreateContext("/", certificate: null);

        await harness.Middleware.InvokeAsync(context);

        Assert.False(harness.NextCalled);
        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
        Assert.Null(context.Features.Get<IProxyClientCertificateFeature>());
    }

    [Theory]
    [InlineData("/health/live/")]
    [InlineData("/health/live/x")]
    [InlineData("/health/ready/")]
    [InlineData("/health/livez")]
    [InlineData("/health")]
    public async Task InvokeAsync_WhenAPathOnlyResemblesAProbePath_RefusesWithForbidden(string path)
    {
        using var authority = ProxyClientCertificateTestCertificates.CreateCertificateAuthority(
            "CN=ca",
            Now
        );
        var harness = CreateHarness(authority);
        var context = CreateContext(path, certificate: null);

        await harness.Middleware.InvokeAsync(context);

        Assert.False(harness.NextCalled);
        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
        Assert.Null(context.Features.Get<IProxyClientCertificateFeature>());
    }

    [Fact]
    public async Task InvokeAsync_WhenAnotherPathHasAnUntrustedCertificate_RefusesWithForbidden()
    {
        using var authority = ProxyClientCertificateTestCertificates.CreateCertificateAuthority(
            "CN=ca",
            Now
        );
        using var foreignAuthority =
            ProxyClientCertificateTestCertificates.CreateCertificateAuthority("CN=foreign", Now);
        using var leaf = CreateClientLeaf(foreignAuthority);
        var harness = CreateHarness(authority);
        var context = CreateContext("/connect/token", leaf);

        await harness.Middleware.InvokeAsync(context);

        Assert.False(harness.NextCalled);
        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
        Assert.Null(context.Features.Get<IProxyClientCertificateFeature>());
    }

    [Fact]
    public async Task InvokeAsync_WhenTheCertificateIsTrusted_CallsNextAndSetsTheFeature()
    {
        using var authority = ProxyClientCertificateTestCertificates.CreateCertificateAuthority(
            "CN=ca",
            Now
        );
        using var leaf = CreateClientLeaf(authority);
        var harness = CreateHarness(authority);
        var context = CreateContext("/connect/token", leaf);

        await harness.Middleware.InvokeAsync(context);

        Assert.True(harness.NextCalled);
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        var feature = context.Features.Get<IProxyClientCertificateFeature>();
        Assert.NotNull(feature);
        Assert.Same(leaf, feature.Certificate);
    }

    #endregion
}
