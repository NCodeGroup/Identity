// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using NCode.Identity.AspNetCore;
using NCode.Identity.OpenId.Integration.Tests.Infrastructure;
using Xunit;

namespace NCode.Identity.OpenId.Integration.Tests.Endpoints;

/// <summary>
/// Verifies that no CORS policy applies to the front-channel endpoints (FAPI 2.0 Security Profile §5.3.2.1), even in
/// Development where the developer policy applies to every other endpoint, and that they carry no CORS metadata, which
/// would fail every request a host without CORS middleware serves.
/// </summary>
public class FrontChannelCorsTests
{
    // One of the origins the host's Development overlay allows.
    private const string AllowedOrigin = "https://localhost:5174";

    private static HttpClient CreateClient(HostApplicationFactory factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    [Theory]
    [InlineData("/oauth2/authorize")]
    [InlineData("/oauth2/continue")]
    [InlineData("/oauth2/end_session")]
    [InlineData("/account/login")]
    [InlineData("/account/consent")]
    public async Task Get_FrontChannelEndpointFromAnAllowedOrigin_ReturnsNoCorsHeaders(string path)
    {
        using var factory = new HostApplicationFactory();
        var client = CreateClient(factory);
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("Origin", AllowedOrigin);

        using var response = await client.SendAsync(request);

        Assert.False(
            response.Headers.Contains("Access-Control-Allow-Origin"),
            $"{path} answered a cross-origin request with CORS headers."
        );
    }

    [Theory]
    [InlineData("/oauth2/authorize")]
    [InlineData("/oauth2/continue")]
    [InlineData("/oauth2/end_session")]
    [InlineData("/account/login")]
    [InlineData("/account/consent")]
    public async Task Preflight_FrontChannelEndpointFromAnAllowedOrigin_RefusesWithoutCorsHeaders(
        string path
    )
    {
        using var factory = new HostApplicationFactory();
        var client = CreateClient(factory);
        using var request = new HttpRequestMessage(HttpMethod.Options, path);
        request.Headers.Add("Origin", AllowedOrigin);
        request.Headers.Add("Access-Control-Request-Method", "GET");

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
        Assert.False(
            response.Headers.Contains("Access-Control-Allow-Origin"),
            $"{path} answered a CORS preflight request with CORS headers."
        );
    }

    [Theory]
    [InlineData("/oauth2/authorize")]
    [InlineData("/oauth2/end_session")]
    [InlineData("/account/login")]
    public async Task Preflight_FrontChannelEndpointForAnUnmappedMethod_ReturnsNoCorsHeaders(
        string path
    )
    {
        using var factory = new HostApplicationFactory();
        var client = CreateClient(factory);
        using var request = new HttpRequestMessage(HttpMethod.Options, path);
        request.Headers.Add("Origin", AllowedOrigin);
        request.Headers.Add("Access-Control-Request-Method", "PUT");

        using var response = await client.SendAsync(request);

        Assert.False(
            response.Headers.Contains("Access-Control-Allow-Origin"),
            $"{path} answered a CORS preflight request with CORS headers."
        );
    }

    [Fact]
    public async Task Preflight_TokenEndpointFromAnAllowedOrigin_ReturnsCorsHeaders()
    {
        using var factory = new HostApplicationFactory();
        var client = CreateClient(factory);
        using var request = new HttpRequestMessage(HttpMethod.Options, "/oauth2/token");
        request.Headers.Add("Origin", AllowedOrigin);
        request.Headers.Add("Access-Control-Request-Method", "POST");
        request.Headers.Add("Access-Control-Request-Headers", "dpop");

        using var response = await client.SendAsync(request);

        // A browser client still sends a DPoP proof to the token endpoint, which needs an answered preflight.
        Assert.True(response.Headers.TryGetValues("Access-Control-Allow-Origin", out var origins));
        Assert.Equal([AllowedOrigin], origins);
    }

    [Fact]
    public async Task Get_DiscoveryFromAnAllowedOrigin_ReturnsCorsHeaders()
    {
        using var factory = new HostApplicationFactory();
        var client = CreateClient(factory);
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            "/.well-known/openid-configuration"
        );
        request.Headers.Add("Origin", AllowedOrigin);

        using var response = await client.SendAsync(request);

        // The developer policy still applies to the endpoints a browser client calls from script.
        Assert.True(response.Headers.TryGetValues("Access-Control-Allow-Origin", out var origins));
        Assert.Equal([AllowedOrigin], origins);
    }

    [Fact]
    public void FrontChannelEndpoints_CarryNoCorsMetadata()
    {
        using var factory = new HostApplicationFactory();
        var endpoints = factory
            .Services.GetRequiredService<EndpointDataSource>()
            .Endpoints.Where(endpoint => endpoint.IsFrontChannel)
            .ToList();

        Assert.Contains(
            endpoints,
            endpoint =>
                endpoint.Metadata.GetMetadata<IEndpointNameMetadata>()?.EndpointName
                == OpenIdConstants.EndpointNames.Authorization
        );
        Assert.All(
            endpoints,
            endpoint => Assert.Null(endpoint.Metadata.GetMetadata<ICorsMetadata>())
        );
    }
}
