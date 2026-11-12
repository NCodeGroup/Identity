// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

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
        string environmentName
    )
    {
        var environmentMock = CreateStrictMock<IHostEnvironment>();
        environmentMock
            .SetupGet(environment => environment.EnvironmentName)
            .Returns(environmentName)
            .Verifiable();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddSingleton(environmentMock.Object);
        services.AddHttpsRedirection(options => options.HttpsPort = 8443);

        var serviceProvider = services.BuildServiceProvider();

        var app = new ApplicationBuilder(serviceProvider);
        app.UseHttpsRedirectionOutsideProduction();

        return (app.Build(), serviceProvider);
    }

    private async ValueTask<HttpContext> SendPlainHttpRequestAsync(
        string environmentName,
        CancellationToken cancellationToken
    )
    {
        var (pipeline, serviceProvider) = BuildPipeline(environmentName);
        await using var _ = serviceProvider;

        var httpContext = new DefaultHttpContext
        {
            RequestServices = serviceProvider,
            RequestAborted = cancellationToken,
        };
        httpContext.Request.Scheme = "http";
        httpContext.Request.Host = new HostString("localhost");
        httpContext.Request.Path = "/health/ready";

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

    #endregion
}
