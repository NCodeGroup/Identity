// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using NCode.Identity.OpenId.Host;
using NCode.Identity.OpenId.Integration.Tests.Infrastructure;
using Xunit;

namespace NCode.Identity.OpenId.Integration.Tests.Endpoints;

public sealed class ApiReferenceEndpointTests
{
    private const string BaseAddress = "https://op.integration.test/";

    // Qualified because the NCode.Identity.OpenId.Environments namespace shadows the framework's Environments class.
    private static string StagingEnvironment { get; } =
        Microsoft.Extensions.Hosting.Environments.Staging;

    #region Map Tests

    [Fact]
    public async Task GetOpenApiDocument_OutsideDevelopmentByDefault_ReturnsNotFound()
    {
        using var factory = new HostApplicationFactory();
        using var staging = factory.WithWebHostBuilder(builder =>
            builder.UseEnvironment(StagingEnvironment)
        );
        using var client = CreateClient(staging);

        using var response = await client.GetAsync("/openapi/v1.json");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetApiReferencePage_OutsideDevelopmentByDefault_ReturnsNotFound()
    {
        using var factory = new HostApplicationFactory();
        using var staging = factory.WithWebHostBuilder(builder =>
            builder.UseEnvironment(StagingEnvironment)
        );
        using var client = CreateClient(staging);

        using var response = await client.GetAsync("/scalar/v1");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetOpenApiDocument_WhenEnabledOutsideDevelopment_ReturnsTheDocument()
    {
        using var factory = new ConfiguredHostApplicationFactory(
            new Dictionary<string, string?> { ["ApiReference:DocumentEnabled"] = "true" }
        );
        using var staging = factory.WithWebHostBuilder(builder =>
            builder.UseEnvironment(StagingEnvironment)
        );
        using var client = CreateClient(staging);

        using var response = await client.GetAsync("/openapi/v1.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task GetApiReferencePage_WhenOnlyTheDocumentIsEnabled_ReturnsNotFound()
    {
        using var factory = new ConfiguredHostApplicationFactory(
            new Dictionary<string, string?> { ["ApiReference:DocumentEnabled"] = "true" }
        );
        using var staging = factory.WithWebHostBuilder(builder =>
            builder.UseEnvironment(StagingEnvironment)
        );
        using var client = CreateClient(staging);

        using var response = await client.GetAsync("/scalar/v1");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    #endregion

    private static HttpClient CreateClient(WebApplicationFactory<HostApiMarker> factory) =>
        factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri(BaseAddress),
                AllowAutoRedirect = false,
            }
        );
}
