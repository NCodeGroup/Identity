// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NCode.Identity.AspNetCore.Tests.TransportSecurity;
using NCode.Identity.AspNetCore.TransportSecurity;
using Xunit;

namespace NCode.Identity.AspNetCore.Tests;

public sealed class TransportSecurityRegistrationTests : BaseTests
{
    #region Helpers

    private IHostEnvironment CreateEnvironment(string environmentName)
    {
        var mock = CreateStrictMock<IHostEnvironment>();
        mock.SetupGet(x => x.EnvironmentName).Returns(environmentName).Verifiable();
        return mock.Object;
    }

    private static async ValueTask StartAsync(
        Action<KestrelServerOptions> listen,
        string environmentName,
        CancellationToken cancellationToken
    )
    {
        var builder = WebApplication.CreateBuilder(
            new WebApplicationOptions { EnvironmentName = environmentName }
        );
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(listen);
        builder.Services.AddTransportSecurity(builder.Environment);

        await using var app = builder.Build();
        await app.StartAsync(cancellationToken);
    }

    private static async ValueTask StartConfiguredAsync(
        string urls,
        X509Certificate2 certificate,
        string environmentName,
        CancellationToken cancellationToken
    )
    {
        var directory = Directory.CreateTempSubdirectory();
        try
        {
            var (certificatePath, keyPath) = TransportSecurityTestCertificates.WritePemFiles(
                certificate,
                directory.FullName
            );

            var builder = WebApplication.CreateBuilder(
                new WebApplicationOptions { EnvironmentName = environmentName }
            );
            builder.Logging.ClearProviders();
            builder.Configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["Kestrel:Certificates:Default:Path"] = certificatePath,
                    ["Kestrel:Certificates:Default:KeyPath"] = keyPath,
                }
            );
            builder.WebHost.UseUrls(urls);
            builder.Services.AddTransportSecurity(builder.Environment);

            await using var app = builder.Build();
            await app.StartAsync(cancellationToken);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    #endregion

    #region AddTransportSecurity Tests

    [Theory]
    [InlineData("Development")]
    [InlineData("Staging")]
    [InlineData("Conformance")]
    public void AddTransportSecurity_WhenNotProduction_RegistersNothing(string environmentName)
    {
        var serviceCollection = new ServiceCollection();

        var result = serviceCollection.AddTransportSecurity(CreateEnvironment(environmentName));

        Assert.Same(serviceCollection, result);
        Assert.Empty(serviceCollection);
    }

    [Fact]
    public void AddTransportSecurity_WhenProductionAndCalledTwice_RegistersTheCheckOnce()
    {
        var serviceCollection = new ServiceCollection();
        var environment = CreateEnvironment(Environments.Production);

        serviceCollection.AddTransportSecurity(environment);
        serviceCollection.AddTransportSecurity(environment);

        var descriptor = Assert.Single(
            serviceCollection,
            candidate => candidate.ServiceType == typeof(IHostedService)
        );
        Assert.Equal(typeof(TransportSecurityStartupCheck), descriptor.ImplementationType);
    }

    #endregion

    #region Kestrel Tests

    [Fact]
    public async Task AddTransportSecurity_InProductionWithAValidHttpsEndpoint_Starts()
    {
        using var certificate = TransportSecurityTestCertificates.CreateValid(
            DateTimeOffset.UtcNow
        );

        var exception = await Record.ExceptionAsync(async () =>
            await StartAsync(
                options =>
                    options.Listen(
                        IPAddress.Loopback,
                        0,
                        endpoint => endpoint.UseHttps(certificate)
                    ),
                Environments.Production,
                CancellationToken.None
            )
        );

        Assert.Null(exception);
    }

    [Fact]
    public async Task AddTransportSecurity_InProductionWithAPlainHttpEndpoint_Refuses()
    {
        var exception = await Record.ExceptionAsync(async () =>
            await StartAsync(
                options => options.Listen(IPAddress.Loopback, 0),
                Environments.Production,
                CancellationToken.None
            )
        );

        var failure = Assert.IsType<InvalidOperationException>(exception);
        Assert.Contains("plain HTTP", failure.Message);
    }

    [Fact]
    public async Task AddTransportSecurity_InProductionWithHttpsAndHttpEndpoints_Refuses()
    {
        using var certificate = TransportSecurityTestCertificates.CreateValid(
            DateTimeOffset.UtcNow
        );

        var exception = await Record.ExceptionAsync(async () =>
            await StartAsync(
                options =>
                {
                    options.Listen(
                        IPAddress.Loopback,
                        0,
                        endpoint => endpoint.UseHttps(certificate)
                    );
                    options.Listen(IPAddress.Loopback, 0);
                },
                Environments.Production,
                CancellationToken.None
            )
        );

        var failure = Assert.IsType<InvalidOperationException>(exception);
        Assert.Contains("plain HTTP", failure.Message);
    }

    [Fact]
    public async Task AddTransportSecurity_InProductionWithAnExpiredCertificate_Refuses()
    {
        var now = DateTimeOffset.UtcNow;
        using var certificate = TransportSecurityTestCertificates.CreateWithValidity(
            now.AddDays(-10),
            now.AddDays(-1)
        );

        var exception = await Record.ExceptionAsync(async () =>
            await StartAsync(
                options =>
                    options.Listen(
                        IPAddress.Loopback,
                        0,
                        endpoint => endpoint.UseHttps(certificate)
                    ),
                Environments.Production,
                CancellationToken.None
            )
        );

        var failure = Assert.IsType<InvalidOperationException>(exception);
        Assert.Contains("expired", failure.Message);
    }

    [Fact]
    public async Task AddTransportSecurity_InProductionWithTheDevelopmentCertificate_Refuses()
    {
        using var certificate = TransportSecurityTestCertificates.CreateDevelopmentMarked(
            DateTimeOffset.UtcNow
        );

        var exception = await Record.ExceptionAsync(async () =>
            await StartAsync(
                options =>
                    options.Listen(
                        IPAddress.Loopback,
                        0,
                        endpoint => endpoint.UseHttps(certificate)
                    ),
                Environments.Production,
                CancellationToken.None
            )
        );

        var failure = Assert.IsType<InvalidOperationException>(exception);
        Assert.Contains("development certificate", failure.Message);
    }

    [Fact]
    public async Task AddTransportSecurity_InDevelopmentWithAPlainHttpEndpoint_Starts()
    {
        var exception = await Record.ExceptionAsync(async () =>
            await StartAsync(
                options => options.Listen(IPAddress.Loopback, 0),
                Environments.Development,
                CancellationToken.None
            )
        );

        Assert.Null(exception);
    }

    #endregion

    #region Configured Kestrel Tests

    [Fact]
    public async Task AddTransportSecurity_InProductionWithAConfiguredCertificateAndHttpsUrl_Starts()
    {
        using var certificate = TransportSecurityTestCertificates.CreateValid(
            DateTimeOffset.UtcNow
        );

        var exception = await Record.ExceptionAsync(async () =>
            await StartConfiguredAsync(
                "https://127.0.0.1:0",
                certificate,
                Environments.Production,
                CancellationToken.None
            )
        );

        Assert.Null(exception);
    }

    [Fact]
    public async Task AddTransportSecurity_InProductionWithAConfiguredCertificateAndAWildcardHttpsUrl_Starts()
    {
        using var certificate = TransportSecurityTestCertificates.CreateValid(
            DateTimeOffset.UtcNow
        );

        var exception = await Record.ExceptionAsync(async () =>
            await StartConfiguredAsync(
                "https://+:0",
                certificate,
                Environments.Production,
                CancellationToken.None
            )
        );

        Assert.Null(exception);
    }

    [Fact]
    public async Task AddTransportSecurity_InProductionWithAConfiguredExpiredCertificate_Refuses()
    {
        var now = DateTimeOffset.UtcNow;
        using var certificate = TransportSecurityTestCertificates.CreateWithValidity(
            now.AddDays(-10),
            now.AddDays(-1)
        );

        var exception = await Record.ExceptionAsync(async () =>
            await StartConfiguredAsync(
                "https://127.0.0.1:0",
                certificate,
                Environments.Production,
                CancellationToken.None
            )
        );

        var failure = Assert.IsType<InvalidOperationException>(exception);
        Assert.Contains("expired", failure.Message);
    }

    [Fact]
    public async Task AddTransportSecurity_InProductionWithAConfiguredCertificateAndAnHttpUrl_Refuses()
    {
        using var certificate = TransportSecurityTestCertificates.CreateValid(
            DateTimeOffset.UtcNow
        );

        var exception = await Record.ExceptionAsync(async () =>
            await StartConfiguredAsync(
                "http://127.0.0.1:0",
                certificate,
                Environments.Production,
                CancellationToken.None
            )
        );

        var failure = Assert.IsType<InvalidOperationException>(exception);
        Assert.Contains("plain HTTP", failure.Message);
    }

    #endregion
}
