// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NCode.Buffers;
using NCode.Identity.AspNetCore.Options;
using Xunit;

namespace NCode.Identity.AspNetCore.Tests.ProxyClientCertificate;

public sealed class ProxyClientCertificateRegistrationTests : IDisposable
{
    private DirectoryInfo Root { get; } = Directory.CreateTempSubdirectory("proxy-registration-");

    public void Dispose() => Root.Delete(recursive: true);

    private static DateTimeOffset Now { get; } = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    #region Helpers

    private static IConfiguration CreateConfiguration(
        Dictionary<string, string?> configurationData
    ) => new ConfigurationBuilder().AddInMemoryCollection(configurationData).Build();

    private string WriteAnchorFile()
    {
        using var authority = ProxyClientCertificateTestCertificates.CreateCertificateAuthority(
            "CN=ca",
            Now
        );
        var path = Path.Combine(Root.FullName, "ca.crt");
        File.WriteAllText(path, authority.ExportCertificatePem(), SecureEncoding.UTF8);
        return path;
    }

    private static ServiceProvider BuildProvider(IConfiguration configuration)
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddLogging();
        serviceCollection.AddProxyClientCertificate(configuration);
        return serviceCollection.BuildServiceProvider();
    }

    private static async ValueTask<(bool NextCalled, int StatusCode)> SendAsync(
        IConfiguration configuration,
        CancellationToken cancellationToken
    )
    {
        await using var serviceProvider = BuildProvider(configuration);

        var nextCalled = false;
        var app = new ApplicationBuilder(serviceProvider);
        app.UseProxyClientCertificate();
        app.Run(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        var context = new DefaultHttpContext { RequestServices = serviceProvider };
        context.Request.Path = "/";
        context.RequestAborted = cancellationToken;

        await app.Build()(context);

        return (nextCalled, context.Response.StatusCode);
    }

    #endregion

    #region AddProxyClientCertificate Tests

    [Fact]
    public void AddProxyClientCertificate_WhenSectionHasUnknownKey_Throws()
    {
        var configuration = CreateConfiguration(
            new Dictionary<string, string?> { ["ProxyClientCertificate:Require"] = "true" }
        );
        using var serviceProvider = BuildProvider(configuration);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            serviceProvider.GetRequiredService<IOptions<ProxyClientCertificateOptions>>().Value
        );

        Assert.Contains("'ProxyClientCertificate'", exception.Message, StringComparison.Ordinal);
        Assert.Contains("'Require'", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AddProxyClientCertificate_WhenRequiredWithoutAPath_FailsValidation()
    {
        var configuration = CreateConfiguration(
            new Dictionary<string, string?>
            {
                ["ProxyClientCertificate:Required"] = "true",
                ["ProxyClientCertificate:AllowedSubjects:0"] = "CN=ingress",
            }
        );
        using var serviceProvider = BuildProvider(configuration);

        var exception = Assert.Throws<OptionsValidationException>(() =>
            serviceProvider.GetRequiredService<IOptions<ProxyClientCertificateOptions>>().Value
        );

        Assert.Contains(
            "ProxyClientCertificate:CertificateAuthorityPath",
            exception.Message,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void AddProxyClientCertificate_WhenRequiredAndValid_BindsTheOptions()
    {
        var path = WriteAnchorFile();
        var configuration = CreateConfiguration(
            new Dictionary<string, string?>
            {
                ["ProxyClientCertificate:Required"] = "true",
                ["ProxyClientCertificate:CertificateAuthorityPath"] = path,
                ["ProxyClientCertificate:AllowedSubjects:0"] = "CN=ingress",
            }
        );
        using var serviceProvider = BuildProvider(configuration);

        var options = serviceProvider
            .GetRequiredService<IOptions<ProxyClientCertificateOptions>>()
            .Value;

        Assert.True(options.Required);
        Assert.Equal(path, options.CertificateAuthorityPath);
        Assert.Equal(["CN=ingress"], options.AllowedSubjects);
    }

    #endregion

    #region GetProxyClientCertificateOptions Tests

    [Fact]
    public void GetProxyClientCertificateOptions_WhenSectionHasUnknownKey_Throws()
    {
        var configuration = CreateConfiguration(
            new Dictionary<string, string?> { ["ProxyClientCertificate:Require"] = "true" }
        );

        Assert.Throws<InvalidOperationException>(() =>
            configuration.GetProxyClientCertificateOptions()
        );
    }

    [Fact]
    public void GetProxyClientCertificateOptions_WhenSectionIsSet_BindsTheOptions()
    {
        var configuration = CreateConfiguration(
            new Dictionary<string, string?>
            {
                ["ProxyClientCertificate:Required"] = "true",
                ["ProxyClientCertificate:AllowedSubjects:0"] = "CN=ingress",
            }
        );

        var options = configuration.GetProxyClientCertificateOptions();

        Assert.True(options.Required);
        Assert.Equal(["CN=ingress"], options.AllowedSubjects);
    }

    #endregion

    #region UseProxyClientCertificate Tests

    [Fact]
    public async Task UseProxyClientCertificate_WhenNotRequired_AddsNoMiddleware()
    {
        var (nextCalled, statusCode) = await SendAsync(
            CreateConfiguration(new Dictionary<string, string?>()),
            CancellationToken.None
        );

        Assert.True(nextCalled);
        Assert.Equal(StatusCodes.Status200OK, statusCode);
    }

    [Fact]
    public async Task UseProxyClientCertificate_WhenRequiredAndTheConnectionHasNoCertificate_RefusesWithForbidden()
    {
        var path = WriteAnchorFile();

        var (nextCalled, statusCode) = await SendAsync(
            CreateConfiguration(
                new Dictionary<string, string?>
                {
                    ["ProxyClientCertificate:Required"] = "true",
                    ["ProxyClientCertificate:CertificateAuthorityPath"] = path,
                    ["ProxyClientCertificate:AllowedSubjects:0"] = "CN=ingress",
                }
            ),
            CancellationToken.None
        );

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status403Forbidden, statusCode);
    }

    #endregion

    #region RequestProxyClientCertificate Tests

    [Fact]
    public void RequestProxyClientCertificate_WhenNotRequired_LeavesTheAdapterOptionsUnchanged()
    {
        var https = new HttpsConnectionAdapterOptions();
        var mode = https.ClientCertificateMode;
        var validation = https.ClientCertificateValidation;

        https.RequestProxyClientCertificate(new ProxyClientCertificateOptions());

        Assert.Equal(mode, https.ClientCertificateMode);
        Assert.Same(validation, https.ClientCertificateValidation);
    }

    [Fact]
    public void RequestProxyClientCertificate_WhenRequired_AllowsACertificateAndAcceptsAnyAtTheHandshake()
    {
        var https = new HttpsConnectionAdapterOptions();

        https.RequestProxyClientCertificate(new ProxyClientCertificateOptions { Required = true });

        Assert.Equal(ClientCertificateMode.AllowCertificate, https.ClientCertificateMode);
        var validation = https.ClientCertificateValidation;
        Assert.NotNull(validation);

        using var certificate = ProxyClientCertificateTestCertificates.CreateSelfSignedLeaf(
            "CN=anyone",
            Now
        );
        Assert.True(
            validation(
                certificate,
                null,
                System.Net.Security.SslPolicyErrors.RemoteCertificateChainErrors
            )
        );
    }

    #endregion
}
