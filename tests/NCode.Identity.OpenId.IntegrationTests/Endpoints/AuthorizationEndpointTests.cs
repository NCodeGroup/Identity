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

public class AuthorizationEndpointTests(PlaygroundApplicationFactory factory)
    : IClassFixture<PlaygroundApplicationFactory>
{
    private const string AuthorizePath = "/oauth2/authorize";

    private PlaygroundApplicationFactory Factory { get; } = factory;

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
}
