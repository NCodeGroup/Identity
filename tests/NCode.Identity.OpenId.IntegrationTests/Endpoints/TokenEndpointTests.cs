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
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using NCode.Identity.OpenId.IntegrationTests.Infrastructure;
using Xunit;

namespace NCode.Identity.OpenId.IntegrationTests.Endpoints;

public class TokenEndpointTests(PlaygroundApplicationFactory factory)
    : IClassFixture<PlaygroundApplicationFactory>
{
    private const string TokenPath = "/oauth2/token";

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
    public async Task PostToken_WithNonFormContentType_ReturnsInvalidRequest()
    {
        var client = CreateClient();

        using var content = new StringContent("{}", Encoding.UTF8, "application/json");
        using var response = await client.PostAsync(TokenPath, content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(OpenIdConstants.ErrorCodes.InvalidRequest, await GetErrorCodeAsync(response));
    }

    [Fact]
    public async Task PostToken_WithoutClientCredentials_ReturnsClientError()
    {
        var client = CreateClient();

        using var content = new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                [OpenIdConstants.Parameters.GrantType] = OpenIdConstants
                    .GrantTypes
                    .ClientCredentials,
            }
        );
        using var response = await client.PostAsync(TokenPath, content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var errorCode = await GetErrorCodeAsync(response);
        Assert.Contains(
            errorCode,
            new[]
            {
                OpenIdConstants.ErrorCodes.InvalidClient,
                OpenIdConstants.ErrorCodes.InvalidRequest,
            }
        );
    }

    [Fact]
    public async Task GetToken_ReturnsMethodNotAllowed()
    {
        var client = CreateClient();

        using var response = await client.GetAsync(TokenPath);

        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
    }
}
