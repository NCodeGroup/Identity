// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace NCode.Identity.AspNetCore.Tests;

public sealed class TransportSecurityApplicationBuilderExtensionsTests : BaseTests
{
    #region Helpers

    private (RequestDelegate Pipeline, ServiceProvider ServiceProvider) BuildPipeline(
        string environmentName,
        IConfiguration configuration,
        bool useTrustedForwardedHeaders
    )
    {
        var environmentMock = CreateStrictMock<IHostEnvironment>();
        environmentMock.SetupGet(x => x.EnvironmentName).Returns(environmentName).Verifiable();

        var serviceCollection = new ServiceCollection();
        serviceCollection.AddLogging();
        serviceCollection.AddSingleton(configuration);
        serviceCollection.AddSingleton(environmentMock.Object);
        serviceCollection.AddTrustedForwardedHeaders(configuration);
        serviceCollection.AddHttpsRedirection(options => options.HttpsPort = 8443);

        var serviceProvider = serviceCollection.BuildServiceProvider();

        var app = new ApplicationBuilder(serviceProvider);
        if (useTrustedForwardedHeaders)
        {
            app.UseTrustedForwardedHeaders();
        }
        app.UseHttpsRedirectionOutsideProduction();

        return (app.Build(), serviceProvider);
    }

    private async ValueTask<HttpContext> SendPlainHttpRequestAsync(
        string environmentName,
        CancellationToken cancellationToken
    ) =>
        await SendRequestAsync(
            environmentName,
            new ConfigurationBuilder().Build(),
            useTrustedForwardedHeaders: false,
            forwardedProto: null,
            cancellationToken
        );

    private async ValueTask<HttpContext> SendRequestAsync(
        string environmentName,
        IConfiguration configuration,
        bool useTrustedForwardedHeaders,
        string? forwardedProto,
        CancellationToken cancellationToken
    )
    {
        var (pipeline, serviceProvider) = BuildPipeline(
            environmentName,
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

    #region UseHttpsRedirectionOutsideProduction Tests

    [Fact]
    public async Task UseHttpsRedirectionOutsideProduction_InDevelopment_RedirectsPlainHttp()
    {
        var httpContext = await SendPlainHttpRequestAsync(
            Environments.Development,
            CancellationToken.None
        );

        Assert.Equal(StatusCodes.Status307TemporaryRedirect, httpContext.Response.StatusCode);
        Assert.Equal(
            "https://localhost:8443/health/ready",
            httpContext.Response.Headers.Location.ToString()
        );
    }

    [Fact]
    public async Task UseHttpsRedirectionOutsideProduction_InStaging_RedirectsPlainHttp()
    {
        var httpContext = await SendPlainHttpRequestAsync(
            Environments.Staging,
            CancellationToken.None
        );

        Assert.Equal(StatusCodes.Status307TemporaryRedirect, httpContext.Response.StatusCode);
        Assert.Equal(
            "https://localhost:8443/health/ready",
            httpContext.Response.Headers.Location.ToString()
        );
    }

    [Fact]
    public async Task UseHttpsRedirectionOutsideProduction_InProduction_AddsNoRedirect()
    {
        var httpContext = await SendPlainHttpRequestAsync(
            Environments.Production,
            CancellationToken.None
        );

        Assert.Equal(StatusCodes.Status404NotFound, httpContext.Response.StatusCode);
        Assert.False(httpContext.Response.Headers.ContainsKey("Location"));
    }

    [Fact]
    public async Task UseHttpsRedirectionOutsideProduction_OutsideProductionBehindATrustedProxyThatForwardedHttps_AddsNoRedirect()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?> { ["ForwardedHeaders:KnownProxies:0"] = "10.0.0.4" }
            )
            .Build();

        var httpContext = await SendRequestAsync(
            Environments.Staging,
            configuration,
            useTrustedForwardedHeaders: true,
            forwardedProto: "https",
            CancellationToken.None
        );

        Assert.Equal(StatusCodes.Status404NotFound, httpContext.Response.StatusCode);
        Assert.False(httpContext.Response.Headers.ContainsKey("Location"));
    }

    #endregion
}
