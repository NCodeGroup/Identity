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
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using NCode.Identity.AspNetCore.Options;
using NCode.Identity.AspNetCore.Tests.TransportSecurity;
using NCode.Identity.AspNetCore.TransportSecurity;
using Xunit;

namespace NCode.Identity.AspNetCore.Tests;

public sealed class TransportSecurityRegistrationTests : BaseTests
{
    #region Helpers

    // The check reads the clock from the registered TimeProvider, so every certificate's validity window is built
    // around this fixed instant.
    private static DateTimeOffset Now { get; } = new(2030, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private static async ValueTask StartAsync(
        Action<KestrelServerOptions> listen,
        string environmentName,
        bool localDevelopment,
        CancellationToken cancellationToken
    )
    {
        var builder = WebApplication.CreateBuilder(
            new WebApplicationOptions { EnvironmentName = environmentName }
        );
        builder.Logging.ClearProviders();
        // The value is always set so the process environment cannot change the test.
        builder.Configuration.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["LocalDevelopment:Enabled"] = localDevelopment ? "true" : "false",
            }
        );
        builder.WebHost.ConfigureKestrel(listen);
        builder.Services.AddSingleton<TimeProvider>(new FakeTimeProvider(Now));
        builder.Services.AddTransportSecurity(builder.Configuration);

        await using var app = builder.Build();
        await app.StartAsync(cancellationToken);
    }

    private static async ValueTask StartConfiguredAsync(
        string urls,
        X509Certificate2 certificate,
        string environmentName,
        bool localDevelopment,
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
            var values = new Dictionary<string, string?>
            {
                ["Kestrel:Certificates:Default:Path"] = certificatePath,
                ["Kestrel:Certificates:Default:KeyPath"] = keyPath,
                ["LocalDevelopment:Enabled"] = localDevelopment ? "true" : "false",
            };
            builder.Configuration.AddInMemoryCollection(values);
            builder.WebHost.UseUrls(urls);
            builder.Services.AddSingleton<TimeProvider>(new FakeTimeProvider(Now));
            builder.Services.AddTransportSecurity(builder.Configuration);

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

    private static IConfiguration CreateConfiguration(bool localDevelopment) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["LocalDevelopment:Enabled"] = localDevelopment ? "true" : "false",
                }
            )
            .Build();

    [Fact]
    public void AddTransportSecurity_LocalDevelopmentDisabled_RegistersTheCheck()
    {
        var serviceCollection = new ServiceCollection();

        var result = serviceCollection.AddTransportSecurity(CreateConfiguration(false));

        Assert.Same(serviceCollection, result);
        var descriptor = Assert.Single(
            serviceCollection,
            candidate => candidate.ServiceType == typeof(IHostedService)
        );
        Assert.Equal(typeof(TransportSecurityStartupCheck), descriptor.ImplementationType);
    }

    [Fact]
    public void AddTransportSecurity_LocalDevelopmentAbsent_RegistersTheCheck()
    {
        var serviceCollection = new ServiceCollection();

        serviceCollection.AddTransportSecurity(new ConfigurationBuilder().Build());

        var descriptor = Assert.Single(
            serviceCollection,
            candidate => candidate.ServiceType == typeof(IHostedService)
        );
        Assert.Equal(typeof(TransportSecurityStartupCheck), descriptor.ImplementationType);
    }

    [Fact]
    public void AddTransportSecurity_LocalDevelopmentEnabled_RegistersNoCheck()
    {
        var serviceCollection = new ServiceCollection();

        var result = serviceCollection.AddTransportSecurity(CreateConfiguration(true));

        Assert.Same(serviceCollection, result);
        Assert.DoesNotContain(
            serviceCollection,
            candidate => candidate.ServiceType == typeof(IHostedService)
        );
        Assert.Contains(
            serviceCollection,
            candidate => candidate.ServiceType == typeof(IValidateOptions<LocalDevelopmentOptions>)
        );
    }

    [Fact]
    public void AddTransportSecurity_LocalDevelopmentDisabledAndCalledTwice_RegistersTheCheckOnce()
    {
        var serviceCollection = new ServiceCollection();
        var configuration = CreateConfiguration(false);

        serviceCollection.AddTransportSecurity(configuration);
        serviceCollection.AddTransportSecurity(configuration);

        var descriptor = Assert.Single(
            serviceCollection,
            candidate => candidate.ServiceType == typeof(IHostedService)
        );
        Assert.Equal(typeof(TransportSecurityStartupCheck), descriptor.ImplementationType);
    }

    #endregion

    #region Kestrel Tests

    [Fact]
    public async Task AddTransportSecurity_WithoutLocalDevelopmentAndAValidHttpsEndpoint_Starts()
    {
        using var certificate = TransportSecurityTestCertificates.CreateValid(Now);

        var exception = await Record.ExceptionAsync(async () =>
            await StartAsync(
                options =>
                    options.Listen(
                        IPAddress.Loopback,
                        0,
                        endpoint => endpoint.UseHttps(certificate)
                    ),
                Environments.Production,
                localDevelopment: false,
                CancellationToken.None
            )
        );

        Assert.Null(exception);
    }

    [Fact]
    public async Task AddTransportSecurity_WithoutLocalDevelopmentAndAPlainHttpEndpoint_Refuses()
    {
        var exception = await Record.ExceptionAsync(async () =>
            await StartAsync(
                options => options.Listen(IPAddress.Loopback, 0),
                Environments.Production,
                localDevelopment: false,
                CancellationToken.None
            )
        );

        var failure = Assert.IsType<InvalidOperationException>(exception);
        Assert.Contains("plain HTTP", failure.Message);
    }

    [Fact]
    public async Task AddTransportSecurity_WithoutLocalDevelopmentAndHttpsAndHttpEndpoints_Refuses()
    {
        using var certificate = TransportSecurityTestCertificates.CreateValid(Now);

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
                localDevelopment: false,
                CancellationToken.None
            )
        );

        var failure = Assert.IsType<InvalidOperationException>(exception);
        Assert.Contains("plain HTTP", failure.Message);
    }

    [Fact]
    public async Task AddTransportSecurity_WithoutLocalDevelopmentAndAnExpiredCertificate_Refuses()
    {
        using var certificate = TransportSecurityTestCertificates.CreateWithValidity(
            Now.AddDays(-10),
            Now.AddDays(-1)
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
                localDevelopment: false,
                CancellationToken.None
            )
        );

        var failure = Assert.IsType<InvalidOperationException>(exception);
        Assert.Contains("expired", failure.Message);
    }

    [Fact]
    public async Task AddTransportSecurity_WithoutLocalDevelopmentAndTheDevelopmentCertificate_Refuses()
    {
        using var certificate = TransportSecurityTestCertificates.CreateDevelopmentMarked(Now);

        var exception = await Record.ExceptionAsync(async () =>
            await StartAsync(
                options =>
                    options.Listen(
                        IPAddress.Loopback,
                        0,
                        endpoint => endpoint.UseHttps(certificate)
                    ),
                Environments.Production,
                localDevelopment: false,
                CancellationToken.None
            )
        );

        var failure = Assert.IsType<InvalidOperationException>(exception);
        Assert.Contains("development certificate", failure.Message);
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Staging")]
    [InlineData("Conformance")]
    public async Task AddTransportSecurity_LocalDevelopmentEnabledWithAPlainHttpEndpoint_Starts(
        string environmentName
    )
    {
        var exception = await Record.ExceptionAsync(async () =>
            await StartAsync(
                options => options.Listen(IPAddress.Loopback, 0),
                environmentName,
                localDevelopment: true,
                CancellationToken.None
            )
        );

        Assert.Null(exception);
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Staging")]
    [InlineData("Conformance")]
    public async Task AddTransportSecurity_LocalDevelopmentAbsentWithAPlainHttpEndpoint_Refuses(
        string environmentName
    )
    {
        var exception = await Record.ExceptionAsync(async () =>
            await StartAsync(
                options => options.Listen(IPAddress.Loopback, 0),
                environmentName,
                localDevelopment: false,
                CancellationToken.None
            )
        );

        var failure = Assert.IsType<InvalidOperationException>(exception);
        Assert.Contains("plain HTTP", failure.Message);
    }

    #endregion

    #region Configured Kestrel Tests

    [Fact]
    public async Task AddTransportSecurity_WithoutLocalDevelopmentAndAConfiguredCertificateAndHttpsUrl_Starts()
    {
        using var certificate = TransportSecurityTestCertificates.CreateValid(Now);

        var exception = await Record.ExceptionAsync(async () =>
            await StartConfiguredAsync(
                "https://127.0.0.1:0",
                certificate,
                Environments.Production,
                localDevelopment: false,
                CancellationToken.None
            )
        );

        Assert.Null(exception);
    }

    [Fact]
    public async Task AddTransportSecurity_WithoutLocalDevelopmentAndAConfiguredCertificateAndAWildcardHttpsUrl_Starts()
    {
        using var certificate = TransportSecurityTestCertificates.CreateValid(Now);

        var exception = await Record.ExceptionAsync(async () =>
            await StartConfiguredAsync(
                "https://+:0",
                certificate,
                Environments.Production,
                localDevelopment: false,
                CancellationToken.None
            )
        );

        Assert.Null(exception);
    }

    [Fact]
    public async Task AddTransportSecurity_WithoutLocalDevelopmentAndAConfiguredExpiredCertificate_Refuses()
    {
        using var certificate = TransportSecurityTestCertificates.CreateWithValidity(
            Now.AddDays(-10),
            Now.AddDays(-1)
        );

        var exception = await Record.ExceptionAsync(async () =>
            await StartConfiguredAsync(
                "https://127.0.0.1:0",
                certificate,
                Environments.Production,
                localDevelopment: false,
                CancellationToken.None
            )
        );

        var failure = Assert.IsType<InvalidOperationException>(exception);
        Assert.Contains("expired", failure.Message);
    }

    [Fact]
    public async Task AddTransportSecurity_WithoutLocalDevelopmentAndAConfiguredCertificateAndAnHttpUrl_Refuses()
    {
        using var certificate = TransportSecurityTestCertificates.CreateValid(Now);

        var exception = await Record.ExceptionAsync(async () =>
            await StartConfiguredAsync(
                "http://127.0.0.1:0",
                certificate,
                Environments.Production,
                localDevelopment: false,
                CancellationToken.None
            )
        );

        var failure = Assert.IsType<InvalidOperationException>(exception);
        Assert.Contains("plain HTTP", failure.Message);
    }

    #endregion
}
