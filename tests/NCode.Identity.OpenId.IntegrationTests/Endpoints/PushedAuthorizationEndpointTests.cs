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

public class PushedAuthorizationEndpointTests(HostApplicationFactory factory)
    : IClassFixture<HostApplicationFactory>
{
    private const string ClientId = "it-par-client";
    private const string RedirectUri = "https://client.example/callback";
    private const string RequestUriPrefix = "urn:ietf:params:oauth:request_uri:";

    private HostApplicationFactory Factory { get; } = factory;

    private static FormUrlEncodedContent BuildAuthorizationForm(
        IEnumerable<KeyValuePair<string, string>> extra
    )
    {
        var values = new List<KeyValuePair<string, string>>
        {
            new("client_id", ClientId),
            new("redirect_uri", RedirectUri),
            new("response_type", "code"),
            new("scope", "openid"),
            new("state", "abc123"),
        };
        values.AddRange(TestPkce.FormFields);
        values.AddRange(extra);
        return new FormUrlEncodedContent(values);
    }

    [Fact]
    public async Task PostPar_WithSeededPublicClient_ReturnsRequestUriAndExpiresIn()
    {
        await Factory.SeedPublicClientAsync(ClientId, RedirectUri);

        var client = Factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }
        );

        using var response = await client.PostAsync("/oauth2/par", BuildAuthorizationForm([]));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;

        Assert.True(root.TryGetProperty("request_uri", out var requestUri));
        Assert.StartsWith(RequestUriPrefix, requestUri.GetString());

        Assert.True(root.TryGetProperty("expires_in", out var expiresIn));
        Assert.True(expiresIn.GetInt32() > 0);
    }

    [Fact]
    public async Task PostPar_WithoutCodeChallenge_ReturnsInvalidRequest()
    {
        // The pushed request is validated as the authorization endpoint would validate it, so a missing
        // code_challenge is refused at the PAR endpoint with invalid_request (RFC 7636 §4.4.1).
        await Factory.SeedPublicClientAsync(ClientId, RedirectUri);

        var client = Factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }
        );

        using var form = new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["client_id"] = ClientId,
                ["redirect_uri"] = RedirectUri,
                ["response_type"] = "code",
                ["scope"] = "openid",
                ["state"] = "abc123",
            }
        );
        using var response = await client.PostAsync("/oauth2/par", form);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("invalid_request", document.RootElement.GetProperty("error").GetString());
    }

    [Fact]
    public async Task PostPar_WhenAConfidentialClientPresentsOnlyItsClientId_ReturnsInvalidClient()
    {
        // The PAR endpoint authenticates the client, and a client registered as confidential never authenticates as
        // public by omitting its credentials.
        const string confidentialClientId = "it-par-confidential";
        using var factory = new HostApplicationFactory();
        await factory.SeedClientSecretBasicClientAsync(confidentialClientId, "par-client-secret");

        var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }
        );

        var values = new List<KeyValuePair<string, string>>
        {
            new("client_id", confidentialClientId),
            new("redirect_uri", RedirectUri),
            new("response_type", "code"),
            new("scope", "openid"),
            new("state", "abc123"),
        };
        values.AddRange(TestPkce.FormFields);
        using var form = new FormUrlEncodedContent(values);
        using var response = await client.PostAsync("/oauth2/par", form);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("invalid_client", document.RootElement.GetProperty("error").GetString());
    }

    [Fact]
    public async Task PostPar_WithRequestUriParameter_ReturnsBadRequest()
    {
        await Factory.SeedPublicClientAsync(ClientId, RedirectUri);

        var client = Factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }
        );

        using var response = await client.PostAsync(
            "/oauth2/par",
            BuildAuthorizationForm([
                new KeyValuePair<string, string>("request_uri", "https://client.example/req.jwt"),
            ])
        );

        // RFC 9126 §2.1: the request_uri parameter must not be used at the PAR endpoint.
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostParThenAuthorize_WithRequestUri_ProcessesRequest()
    {
        await Factory.SeedPublicClientAsync(ClientId, RedirectUri);

        var client = Factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }
        );

        using var parResponse = await client.PostAsync("/oauth2/par", BuildAuthorizationForm([]));
        Assert.Equal(HttpStatusCode.Created, parResponse.StatusCode);

        using var parDocument = JsonDocument.Parse(await parResponse.Content.ReadAsStringAsync());
        var requestUri = parDocument.RootElement.GetProperty("request_uri").GetString();
        Assert.NotNull(requestUri);

        var query = $"?client_id={ClientId}&request_uri={Uri.EscapeDataString(requestUri)}";

        using var authorizeResponse = await client.GetAsync("/oauth2/authorize" + query);

        // The pushed request is replayed and processed: with no authenticated end-user the
        // authorization handler issues an interactive challenge (a redirect).
        Assert.Equal(HttpStatusCode.Redirect, authorizeResponse.StatusCode);
    }

    [Fact]
    public async Task Authorize_WithRequestUriConsumedTwice_SecondUseFails()
    {
        await Factory.SeedPublicClientAsync(ClientId, RedirectUri);

        var client = Factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }
        );

        using var parResponse = await client.PostAsync("/oauth2/par", BuildAuthorizationForm([]));
        using var parDocument = JsonDocument.Parse(await parResponse.Content.ReadAsStringAsync());
        var requestUri = parDocument.RootElement.GetProperty("request_uri").GetString();
        Assert.NotNull(requestUri);

        var query = $"?client_id={ClientId}&request_uri={Uri.EscapeDataString(requestUri)}";

        using var firstResponse = await client.GetAsync("/oauth2/authorize" + query);
        Assert.Equal(HttpStatusCode.Redirect, firstResponse.StatusCode);

        // A pushed request_uri is single-use (RFC 9126 §4); the second use is rejected.
        using var secondResponse = await client.GetAsync("/oauth2/authorize" + query);
        Assert.Equal(HttpStatusCode.BadRequest, secondResponse.StatusCode);
    }

    [Fact]
    public async Task Authorize_WithUnknownPushedRequestUri_ReturnsBadRequest()
    {
        await Factory.SeedPublicClientAsync(ClientId, RedirectUri);

        var client = Factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }
        );

        var requestUri = RequestUriPrefix + "unknown-reference";
        var query = $"?client_id={ClientId}&request_uri={Uri.EscapeDataString(requestUri)}";

        using var response = await client.GetAsync("/oauth2/authorize" + query);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetDiscovery_AdvertisesPushedAuthorizationRequestEndpoint()
    {
        var client = Factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }
        );

        using var response = await client.GetAsync("/.well-known/openid-configuration");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(
            document.RootElement.TryGetProperty(
                "pushed_authorization_request_endpoint",
                out var endpoint
            )
        );
        Assert.False(string.IsNullOrEmpty(endpoint.GetString()));
    }
}
