// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using NCode.Buffers;
using Xunit;

namespace NCode.Identity.AspNetCore.Tests.ProxyClientCertificate;

/// <summary>
/// Proves the proxy client certificate requirement over a real TLS handshake with Kestrel on a loopback ephemeral
/// port. The loopback connection is the deliberate exception to the hermetic rule: the handshake is the behavior
/// under test.
/// </summary>
public sealed class ProxyClientCertificateLoopbackTests : IAsyncLifetime
{
    private const string AllowedSubject = "CN=ingress";

    private static DateTimeOffset Now { get; } = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private DirectoryInfo Root { get; } = Directory.CreateTempSubdirectory("proxy-loopback-");
    private List<IDisposable> Disposables { get; } = [];
    private WebApplication? Application { get; set; }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        if (Application is not null)
        {
            await Application.StopAsync();
            await Application.DisposeAsync();
        }

        foreach (var disposable in Disposables)
        {
            disposable.Dispose();
        }

        Root.Delete(recursive: true);
    }

    #region Helpers

    private T Own<T>(T disposable)
        where T : IDisposable
    {
        Disposables.Add(disposable);
        return disposable;
    }

    private static X509Certificate2 CreateCertificate(
        X509Certificate2? issuer,
        string commonName,
        string usage
    )
    {
        // Windows' TLS stack refuses an ephemeral key, so the certificate is reloaded from a PKCS#12 image: its key is
        // then one that the platform accepts and that .NET removes when the certificate is disposed.
        using var ephemeral = CreateEphemeralCertificate(issuer, commonName, usage);
        return X509CertificateLoader.LoadPkcs12(
            ephemeral.Export(X509ContentType.Pfx),
            password: null
        );
    }

    private static X509Certificate2 CreateEphemeralCertificate(
        X509Certificate2? issuer,
        string commonName,
        string usage
    )
    {
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var subject = new X500DistinguishedNameBuilder();
        subject.AddCommonName(commonName);
        var request = new CertificateRequest(subject.Build(), ecdsa, HashAlgorithmName.SHA256);
        request.CertificateExtensions.Add(
            new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, critical: true)
        );
        request.CertificateExtensions.Add(
            new X509EnhancedKeyUsageExtension([new Oid(usage)], critical: false)
        );

        if (issuer is null)
        {
            return request.CreateSelfSigned(Now.AddYears(-1), Now.AddYears(10));
        }

        request.CertificateExtensions.Add(
            X509AuthorityKeyIdentifierExtension.CreateFromCertificate(
                issuer,
                includeKeyIdentifier: true,
                includeIssuerAndSerial: false
            )
        );

        var serialNumber = new byte[8];
        RandomNumberGenerator.Fill(serialNumber);
        serialNumber[0] &= 0x7F;
        serialNumber[0] |= 0x01;

        using var created = request.Create(issuer, Now.AddDays(-1), Now.AddDays(30), serialNumber);
        return created.CopyWithPrivateKey(ecdsa);
    }

    private X509Certificate2 CreateAuthority(string commonName) =>
        Own(
            ProxyClientCertificateTestCertificates.CreateCertificateAuthority(
                $"CN={commonName}",
                Now
            )
        );

    private X509Certificate2 CreateClientCertificate(X509Certificate2 authority) =>
        Own(
            CreateCertificate(
                authority,
                "ingress",
                ProxyClientCertificateTestCertificates.ClientAuthenticationOid
            )
        );

    private async ValueTask<(HttpClient Client, Uri BaseAddress)> StartAsync(
        X509Certificate2 authority,
        X509Certificate2? clientCertificate,
        CancellationToken cancellationToken
    )
    {
        var anchorPath = Path.Combine(Root.FullName, "ca.crt");
        await File.WriteAllTextAsync(
            anchorPath,
            authority.ExportCertificatePem(),
            SecureEncoding.UTF8,
            cancellationToken
        );

        var serverCertificate = Own(
            CreateCertificate(
                issuer: null,
                "localhost",
                ProxyClientCertificateTestCertificates.ServerAuthenticationOid
            )
        );

        var builder = WebApplication.CreateSlimBuilder();
        builder.Logging.ClearProviders();
        builder.Configuration.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["ProxyClientCertificate:Required"] = "true",
                ["ProxyClientCertificate:CertificateAuthorityPath"] = anchorPath,
                ["ProxyClientCertificate:AllowedSubjects:0"] = AllowedSubject,
            }
        );
        builder.Services.AddSingleton<TimeProvider>(new FakeTimeProvider(Now));
        builder.Services.AddProxyClientCertificate(builder.Configuration);

        var proxyOptions = builder.Configuration.GetProxyClientCertificateOptions();
        builder.WebHost.UseKestrel(kestrel =>
        {
            kestrel.ConfigureHttpsDefaults(https =>
            {
                https.ServerCertificate = serverCertificate;
                https.RequestProxyClientCertificate(proxyOptions);
            });
            kestrel.Listen(IPAddress.Loopback, 0, listen => listen.UseHttps());
        });

        var application = builder.Build();
        application.UseProxyClientCertificate();
        application.MapGet("/", () => "ok");
        application.MapGet(HealthProbeDefaults.LivenessPath, () => "live");
        Application = application;

        await application.StartAsync(cancellationToken);

        var address = application
            .Services.GetRequiredService<IServer>()
            .Features.Get<IServerAddressesFeature>()
            ?.Addresses.Single();
        Assert.NotNull(address);

        var serverHash = serverCertificate.GetCertHash(HashAlgorithmName.SHA256);
        var handler = new SocketsHttpHandler();
        handler.SslOptions.RemoteCertificateValidationCallback = (_, certificate, _, _) =>
            certificate is not null
            && certificate.GetCertHash(HashAlgorithmName.SHA256).AsSpan().SequenceEqual(serverHash);
        if (clientCertificate is not null)
        {
            handler.SslOptions.ClientCertificates = [clientCertificate];
        }

        var client = Own(new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(30) });
        return (client, new Uri(address));
    }

    #endregion

    #region Handshake Tests

    [Fact]
    public async Task Request_WhenTheClientPresentsNoCertificate_IsRefusedWithForbidden()
    {
        var authority = CreateAuthority("ca");
        var (client, baseAddress) = await StartAsync(
            authority,
            clientCertificate: null,
            CancellationToken.None
        );

        using var response = await client.GetAsync(new Uri(baseAddress, "/"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task LivenessProbe_WhenTheClientPresentsNoCertificate_IsServed()
    {
        var authority = CreateAuthority("ca");
        var (client, baseAddress) = await StartAsync(
            authority,
            clientCertificate: null,
            CancellationToken.None
        );

        using var response = await client.GetAsync(
            new Uri(baseAddress, HealthProbeDefaults.LivenessPath)
        );

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("live", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Request_WhenTheClientPresentsACertificateFromTheAnchors_IsServed()
    {
        var authority = CreateAuthority("ca");
        var certificate = CreateClientCertificate(authority);
        var (client, baseAddress) = await StartAsync(
            authority,
            certificate,
            CancellationToken.None
        );

        using var response = await client.GetAsync(new Uri(baseAddress, "/"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("ok", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Request_WhenTheClientPresentsACertificateFromAnotherAuthority_IsRefusedWithForbidden()
    {
        var authority = CreateAuthority("ca");
        var otherAuthority = CreateAuthority("other-ca");
        var certificate = CreateClientCertificate(otherAuthority);
        var (client, baseAddress) = await StartAsync(
            authority,
            certificate,
            CancellationToken.None
        );

        using var response = await client.GetAsync(new Uri(baseAddress, "/"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    #endregion
}
