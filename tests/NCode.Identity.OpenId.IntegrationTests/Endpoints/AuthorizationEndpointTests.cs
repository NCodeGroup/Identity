#region Copyright Preamble

// Copyright @ 2026 NCode Group
//
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
//
//        http://www.apache.org/licenses/LICENSE-2.0
//
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.

#endregion

using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using NCode.Identity.OpenId.IntegrationTests.Infrastructure;
using Xunit;

namespace NCode.Identity.OpenId.IntegrationTests.Endpoints;

public class AuthorizationEndpointTests(HostApplicationFactory factory)
    : IClassFixture<HostApplicationFactory>
{
    private const string AuthorizePath = "/oauth2/authorize";

    private HostApplicationFactory Factory { get; } = factory;

    private HttpClient CreateClient() =>
        Factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    private static async Task<string> GetErrorCodeAsync(HttpResponseMessage response)
    {
        var json = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(json);
        return document.RootElement.GetProperty("error").GetString() ?? string.Empty;
    }

    [Fact]
    public async Task GetAuthorize_WithoutClientId_ReturnsInvalidClient()
    {
        var client = CreateClient();

        using var response = await client.GetAsync(AuthorizePath);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(OpenIdConstants.ErrorCodes.InvalidClient, await GetErrorCodeAsync(response));
    }

    [Fact]
    public async Task GetAuthorize_WithUnknownClientId_ReturnsInvalidClient()
    {
        var client = CreateClient();

        using var response = await client.GetAsync(
            $"{AuthorizePath}?{OpenIdConstants.Parameters.ClientId}=unknown-client"
        );

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(OpenIdConstants.ErrorCodes.InvalidClient, await GetErrorCodeAsync(response));
    }

    [Fact]
    public async Task PostAuthorize_WithoutClientId_ReturnsInvalidClient()
    {
        var client = CreateClient();

        using var content = new FormUrlEncodedContent(new Dictionary<string, string>());
        using var response = await client.PostAsync(AuthorizePath, content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(OpenIdConstants.ErrorCodes.InvalidClient, await GetErrorCodeAsync(response));
    }

    [Fact]
    public async Task DeleteAuthorize_ReturnsMethodNotAllowed()
    {
        var client = CreateClient();

        using var response = await client.DeleteAsync(AuthorizePath);

        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
    }

    [Fact]
    public async Task GetAuthorize_WhenBrowserAcceptsHtml_RendersHtmlErrorPage()
    {
        var client = CreateClient();

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"{AuthorizePath}?{OpenIdConstants.Parameters.ClientId}=unknown-client"
        );
        request.Headers.Accept.ParseAdd("text/html,application/xhtml+xml,*/*;q=0.8");

        using var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("id=\"authorization-error\"", body, StringComparison.Ordinal);
        Assert.Contains(OpenIdConstants.ErrorCodes.InvalidClient, body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetAuthorize_WhenClientAcceptsJson_ReturnsJsonError()
    {
        var client = CreateClient();

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"{AuthorizePath}?{OpenIdConstants.Parameters.ClientId}=unknown-client"
        );
        request.Headers.Accept.ParseAdd("application/json");

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(OpenIdConstants.ErrorCodes.InvalidClient, await GetErrorCodeAsync(response));
    }

    [Fact]
    public async Task GetAuthorize_WhenWildcardAccept_ReturnsJsonError()
    {
        var client = CreateClient();

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"{AuthorizePath}?{OpenIdConstants.Parameters.ClientId}=unknown-client"
        );
        request.Headers.Accept.ParseAdd("*/*");

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(OpenIdConstants.ErrorCodes.InvalidClient, await GetErrorCodeAsync(response));
    }
}
