// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace NCode.Identity.AspNetCore.Tests;

public sealed class TransportSecurityApplicationBuilderExtensionsTests : BaseTests
{
    #region Helpers

    private static (RequestDelegate Pipeline, ServiceProvider ServiceProvider) BuildPipeline(
        string environmentName,
        bool localDevelopment,
        IConfiguration configuration,
        bool useTrustedForwardedHeaders
    )
    {
        var switchConfiguration = new ConfigurationBuilder()
            .AddConfiguration(configuration)
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["LocalDevelopment:Enabled"] = localDevelopment ? "true" : "false",
                }
            )
            .Build();

        var serviceCollection = new ServiceCollection();
        serviceCollection.AddLogging();
        serviceCollection.AddSingleton<IConfiguration>(switchConfiguration);
        serviceCollection.AddSingleton<IHostEnvironment>(
            new TestHostEnvironment { EnvironmentName = environmentName }
        );
        serviceCollection.AddLocalDevelopmentOptions(switchConfiguration);
        serviceCollection.AddTrustedForwardedHeaders(switchConfiguration);
        serviceCollection.AddHttpsRedirection(options => options.HttpsPort = 8443);

        var serviceProvider = serviceCollection.BuildServiceProvider();

        var app = new ApplicationBuilder(serviceProvider);
        if (useTrustedForwardedHeaders)
        {
            app.UseTrustedForwardedHeaders();
        }
        app.UseLocalDevelopmentHttpsRedirection();

        return (app.Build(), serviceProvider);
    }

    private static async ValueTask<HttpContext> SendPlainHttpRequestAsync(
        string environmentName,
        bool localDevelopment,
        CancellationToken cancellationToken
    ) =>
        await SendRequestAsync(
            environmentName,
            localDevelopment,
            new ConfigurationBuilder().Build(),
            useTrustedForwardedHeaders: false,
            forwardedProto: null,
            cancellationToken
        );

    private static async ValueTask<HttpContext> SendRequestAsync(
        string environmentName,
        bool localDevelopment,
        IConfiguration configuration,
        bool useTrustedForwardedHeaders,
        string? forwardedProto,
        CancellationToken cancellationToken
    )
    {
        var (pipeline, serviceProvider) = BuildPipeline(
            environmentName,
            localDevelopment,
            configuration,
            useTrustedForwardedHeaders
        );
        await using var _ = serviceProvider;

        var httpContext = new DefaultHttpContext
        {
            RequestServices = serviceProvider,
            RequestAborted = cancellationToken,
        };
        httpContext.Connection.RemoteIpAddress = IPAddress.Parse("10.0.0.4");
        httpContext.Request.Scheme = "http";
        httpContext.Request.Host = new HostString("localhost");
        httpContext.Request.Path = "/health/ready";
        if (forwardedProto is not null)
        {
            httpContext.Request.Headers["X-Forwarded-Proto"] = forwardedProto;
        }

        await pipeline(httpContext);

        return httpContext;
    }

    #endregion

    #region UseLocalDevelopmentHttpsRedirection Tests

    [Fact]
    public async Task UseLocalDevelopmentHttpsRedirection_LocalDevelopmentEnabledInDevelopment_RedirectsPlainHttp()
    {
        var httpContext = await SendPlainHttpRequestAsync(
            Environments.Development,
            localDevelopment: true,
            CancellationToken.None
        );

        Assert.Equal(StatusCodes.Status307TemporaryRedirect, httpContext.Response.StatusCode);
        Assert.Equal(
            "https://localhost:8443/health/ready",
            httpContext.Response.Headers.Location.ToString()
        );
    }

    [Fact]
    public async Task UseLocalDevelopmentHttpsRedirection_LocalDevelopmentEnabledInStaging_RedirectsPlainHttp()
    {
        var httpContext = await SendPlainHttpRequestAsync(
            Environments.Staging,
            localDevelopment: true,
            CancellationToken.None
        );

        Assert.Equal(StatusCodes.Status307TemporaryRedirect, httpContext.Response.StatusCode);
        Assert.Equal(
            "https://localhost:8443/health/ready",
            httpContext.Response.Headers.Location.ToString()
        );
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Staging")]
    [InlineData("Production")]
    public async Task UseLocalDevelopmentHttpsRedirection_LocalDevelopmentDisabled_AddsNoRedirect(
        string environmentName
    )
    {
        var httpContext = await SendPlainHttpRequestAsync(
            environmentName,
            localDevelopment: false,
            CancellationToken.None
        );

        Assert.Equal(StatusCodes.Status404NotFound, httpContext.Response.StatusCode);
        Assert.False(httpContext.Response.Headers.ContainsKey("Location"));
    }

    [Fact]
    public async Task UseLocalDevelopmentHttpsRedirection_LocalDevelopmentEnabledBehindATrustedProxyThatForwardedHttps_AddsNoRedirect()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?> { ["ForwardedHeaders:KnownProxies:0"] = "10.0.0.4" }
            )
            .Build();

        var httpContext = await SendRequestAsync(
            Environments.Staging,
            localDevelopment: true,
            configuration,
            useTrustedForwardedHeaders: true,
            forwardedProto: "https",
            CancellationToken.None
        );

        Assert.Equal(StatusCodes.Status404NotFound, httpContext.Response.StatusCode);
        Assert.False(httpContext.Response.Headers.ContainsKey("Location"));
    }

    #endregion

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;

        public string ApplicationName { get; set; } = string.Empty;

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
