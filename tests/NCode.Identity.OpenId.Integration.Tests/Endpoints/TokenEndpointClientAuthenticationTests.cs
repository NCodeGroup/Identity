// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using NCode.Buffers;
using NCode.Identity.OpenId.Integration.Tests.Infrastructure;
using Xunit;

namespace NCode.Identity.OpenId.Integration.Tests.Endpoints;

/// <summary>
/// Verifies client authentication at the token endpoint: a client authenticates only with the method it is registered to
/// use (RFC 7591 §2), within the platform's <c>token_endpoint_auth_methods_supported</c>; and, per RFC 6749 §5.2, a client
/// that authenticates via the HTTP <c>Authorization</c> request header (<c>client_secret_basic</c>) and fails gets
/// <c>401 Unauthorized</c> with a matching <c>WWW-Authenticate</c> challenge, while failures via the request body
/// (<c>client_secret_post</c>) stay <c>400</c>.
/// </summary>
public class TokenEndpointClientAuthenticationTests
{
    private const string ClientId = "it-auth-client";
    private const string ClientSecret = "confidential-secret-value";
    private const string EncodedClientId = "it auth:client+%";
    private const string EncodedClientSecret = "a secret: with+plus %percent&amp=equals";
    private const string TokenPath = "/oauth2/token";

    private static HttpClient CreateClient(HostApplicationFactory factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    private static AuthenticationHeaderValue BasicHeader(string clientId, string clientSecret)
    {
        // RFC 6749 section 2.3.1 form-urlencodes the client id and secret before the Basic encoding.
        var raw = $"{WebUtility.UrlEncode(clientId)}:{WebUtility.UrlEncode(clientSecret)}";
        var encoded = Convert.ToBase64String(SecureEncoding.UTF8.GetBytes(raw));
        return new AuthenticationHeaderValue("Basic", encoded);
    }

    private static FormUrlEncodedContent ClientCredentialsBody() =>
        new(
            new Dictionary<string, string>
            {
                ["grant_type"] = OpenIdProtocol.GrantTypes.ClientCredentials,
                ["scope"] = TestPlatformSettingsProvider.ApiScope,
            }
        );

    private static FormUrlEncodedContent ClientSecretPostBody(string clientSecret) =>
        new(
            new Dictionary<string, string>
            {
                ["grant_type"] = OpenIdProtocol.GrantTypes.ClientCredentials,
                ["client_id"] = ClientId,
                ["client_secret"] = clientSecret,
                ["scope"] = TestPlatformSettingsProvider.ApiScope,
            }
        );

    private static async ValueTask SeedApiGrantAsync(
        HostApplicationFactory factory,
        CancellationToken cancellationToken
    ) =>
        await factory.SeedResourceServerWithClientGrantAsync(
            ClientId,
            "it-auth-rs-api",
            "https://api.auth.integration.test",
            [TestPlatformSettingsProvider.ApiScope],
            cancellationToken
        );

    private static async ValueTask<HttpResponseMessage> SendBasicAsync(
        HttpClient client,
        string clientSecret,
        CancellationToken cancellationToken
    )
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, TokenPath)
        {
            Headers = { Authorization = BasicHeader(ClientId, clientSecret) },
            Content = ClientCredentialsBody(),
        };

        return await client.SendAsync(request, cancellationToken);
    }

    private static async ValueTask AssertInvalidClientAsync(
        HttpResponseMessage response,
        HttpStatusCode expectedStatusCode,
        CancellationToken cancellationToken
    )
    {
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        Assert.True(response.StatusCode == expectedStatusCode, body);

        var jsonElement = JsonElement.Parse(body);
        Assert.Equal(
            OpenIdProtocol.ErrorCodes.InvalidClient,
            jsonElement.GetProperty("error").GetString()
        );
    }

    #region Registered Method Tests

    [Fact]
    public async Task PostToken_WhenAConfidentialClientPresentsOnlyItsClientId_ReturnsInvalidClient()
    {
        // A client registered as confidential never authenticates as public, whatever grant it requests.
        using var factory = new HostApplicationFactory();
        await factory.SeedClientSecretBasicClientAsync(
            ClientId,
            ClientSecret,
            [],
            CancellationToken.None
        );
        var client = CreateClient(factory);

        using var content = new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["grant_type"] = OpenIdProtocol.GrantTypes.RefreshToken,
                ["client_id"] = ClientId,
                ["refresh_token"] = "any-refresh-token",
            }
        );
        using var response = await client.PostAsync(TokenPath, content);

        await AssertInvalidClientAsync(response, HttpStatusCode.BadRequest, CancellationToken.None);
    }

    [Fact]
    public async Task PostToken_WhenAClientSecretBasicClientPostsItsSecret_ReturnsInvalidClient()
    {
        using var factory = new HostApplicationFactory();
        await factory.SeedClientSecretBasicClientAsync(
            ClientId,
            ClientSecret,
            [],
            CancellationToken.None
        );
        await SeedApiGrantAsync(factory, CancellationToken.None);
        var client = CreateClient(factory);

        using var content = ClientSecretPostBody(ClientSecret);
        using var response = await client.PostAsync(TokenPath, content);

        await AssertInvalidClientAsync(response, HttpStatusCode.BadRequest, CancellationToken.None);
    }

    [Fact]
    public async Task PostToken_WhenAClientSecretPostClientUsesBasicAuth_Returns401WithChallenge()
    {
        using var factory = new HostApplicationFactory();
        await factory.SeedConfidentialClientAsync(
            ClientId,
            ClientSecret,
            [],
            CancellationToken.None
        );
        await SeedApiGrantAsync(factory, CancellationToken.None);
        var client = CreateClient(factory);

        using var response = await SendBasicAsync(client, ClientSecret, CancellationToken.None);

        await AssertInvalidClientAsync(
            response,
            HttpStatusCode.Unauthorized,
            CancellationToken.None
        );
        Assert.Contains(response.Headers.WwwAuthenticate, challenge => challenge.Scheme == "Basic");
    }

    [Fact]
    public async Task PostToken_WhenAPublicClientPresentsASecret_ReturnsInvalidClient()
    {
        // A public client has no secret (RFC 7591 §2 'none'), so one it presents authenticates nothing.
        using var factory = new HostApplicationFactory();
        await factory.SeedPublicClientAsync(
            ClientId,
            ["https://client.example/callback"],
            CancellationToken.None
        );
        var client = CreateClient(factory);

        using var content = ClientSecretPostBody(ClientSecret);
        using var response = await client.PostAsync(TokenPath, content);

        await AssertInvalidClientAsync(response, HttpStatusCode.BadRequest, CancellationToken.None);
    }

    [Fact]
    public async Task PostToken_WhenTheClientRegistersNoMethod_AuthenticatesOnlyWithClientSecretBasic()
    {
        // RFC 7591 §2: a client that registers no token_endpoint_auth_method uses client_secret_basic.
        using var factory = new HostApplicationFactory();
        await factory.SeedClientWithoutAuthenticationMethodAsync(
            ClientId,
            ClientSecret,
            [],
            CancellationToken.None
        );
        await SeedApiGrantAsync(factory, CancellationToken.None);
        var client = CreateClient(factory);

        using var basicResponse = await SendBasicAsync(
            client,
            ClientSecret,
            CancellationToken.None
        );
        using var postContent = ClientSecretPostBody(ClientSecret);
        using var postResponse = await client.PostAsync(TokenPath, postContent);

        var basicBody = await basicResponse.Content.ReadAsStringAsync();
        Assert.True(basicResponse.StatusCode == HttpStatusCode.OK, basicBody);
        await AssertInvalidClientAsync(
            postResponse,
            HttpStatusCode.BadRequest,
            CancellationToken.None
        );
    }

    [Fact]
    public async Task PostToken_WhenThePlatformDoesNotPermitTheRegisteredMethod_ReturnsInvalidClient()
    {
        // token_endpoint_auth_methods_supported is a ceiling: a client registered outside it cannot authenticate.
        using var factory = new ConfiguredHostApplicationFactory(
            new Dictionary<string, string?>
            {
                ["OpenId:Platform:Settings:token_endpoint_auth_methods_supported:0"] =
                    OpenIdProtocol.ClientAuthenticationMethods.ClientSecretPost,
            }
        );
        await factory.SeedClientSecretBasicClientAsync(
            ClientId,
            ClientSecret,
            [],
            CancellationToken.None
        );
        await SeedApiGrantAsync(factory, CancellationToken.None);
        var client = CreateClient(factory);

        using var response = await SendBasicAsync(client, ClientSecret, CancellationToken.None);

        await AssertInvalidClientAsync(
            response,
            HttpStatusCode.Unauthorized,
            CancellationToken.None
        );
    }

    #endregion

    #region Credential Encoding Tests
    [Fact]
    public async Task PostToken_WhenTheBasicSecretNeedsFormEncoding_ReturnsAccessToken()
    {
        // RFC 6749 section 2.3.1: a secret with a space, colon, plus and percent is form-urlencoded in the header.
        using var factory = new HostApplicationFactory();
        await factory.SeedClientSecretBasicClientAsync(
            ClientId,
            EncodedClientSecret,
            [],
            CancellationToken.None
        );
        await factory.SeedResourceServerWithClientGrantAsync(
            ClientId,
            "it-auth-rs-api",
            "https://api.auth.integration.test",
            [TestPlatformSettingsProvider.ApiScope],
            CancellationToken.None
        );

        var client = CreateClient(factory);

        using var request = new HttpRequestMessage(HttpMethod.Post, TokenPath)
        {
            Headers = { Authorization = BasicHeader(ClientId, EncodedClientSecret) },
            Content = ClientCredentialsBody(),
        };

        using var response = await client.SendAsync(request);

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, body);

        var jsonElement = JsonElement.Parse(body);
        Assert.False(string.IsNullOrEmpty(jsonElement.GetProperty("access_token").GetString()));
    }

    [Fact]
    public async Task PostToken_WhenTheBasicClientIdNeedsFormEncoding_ReturnsAccessToken()
    {
        // RFC 6749 section 2.3.1: a client id with a space, colon, plus and percent is form-urlencoded in the header.
        using var factory = new HostApplicationFactory();
        await factory.SeedClientSecretBasicClientAsync(
            EncodedClientId,
            ClientSecret,
            [],
            CancellationToken.None
        );
        await factory.SeedResourceServerWithClientGrantAsync(
            EncodedClientId,
            "it-auth-rs-api",
            "https://api.auth.integration.test",
            [TestPlatformSettingsProvider.ApiScope],
            CancellationToken.None
        );

        var client = CreateClient(factory);

        using var request = new HttpRequestMessage(HttpMethod.Post, TokenPath)
        {
            Headers = { Authorization = BasicHeader(EncodedClientId, ClientSecret) },
            Content = ClientCredentialsBody(),
        };

        using var response = await client.SendAsync(request);

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, body);

        var jsonElement = JsonElement.Parse(body);
        Assert.False(string.IsNullOrEmpty(jsonElement.GetProperty("access_token").GetString()));
    }

    #endregion

    #region Challenge Tests
    [Fact]
    public async Task PostToken_BasicAuthWithWrongSecret_Returns401WithChallenge()
    {
        using var factory = new HostApplicationFactory();
        await factory.SeedClientSecretBasicClientAsync(
            ClientId,
            ClientSecret,
            [],
            CancellationToken.None
        );

        var client = CreateClient(factory);

        using var request = new HttpRequestMessage(HttpMethod.Post, TokenPath)
        {
            Headers = { Authorization = BasicHeader(ClientId, "wrong-secret") },
            Content = ClientCredentialsBody(),
        };

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        var challenge = Assert.Single(response.Headers.WwwAuthenticate);
        Assert.Equal("Basic", challenge.Scheme);
        Assert.Contains("realm=", challenge.Parameter);

        var jsonElement = JsonElement.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(
            OpenIdProtocol.ErrorCodes.InvalidClient,
            jsonElement.GetProperty("error").GetString()
        );
    }

    [Fact]
    public async Task PostToken_BasicAuthWithUnknownClient_Returns401WithChallenge()
    {
        using var factory = new HostApplicationFactory();
        // Warm up the tenant so the request reaches client authentication.
        using var warmup = CreateClient(factory);
        using var warmupResponse = await warmup.GetAsync("/oauth2/jwks");

        var client = CreateClient(factory);

        using var request = new HttpRequestMessage(HttpMethod.Post, TokenPath)
        {
            Headers = { Authorization = BasicHeader("no-such-client", "whatever") },
            Content = ClientCredentialsBody(),
        };

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Contains(response.Headers.WwwAuthenticate, challenge => challenge.Scheme == "Basic");
    }

    [Fact]
    public async Task PostToken_BasicAuthWithCorrectSecret_ReturnsAccessToken()
    {
        using var factory = new HostApplicationFactory();
        await factory.SeedClientSecretBasicClientAsync(
            ClientId,
            ClientSecret,
            [],
            CancellationToken.None
        );
        await factory.SeedResourceServerWithClientGrantAsync(
            ClientId,
            "it-auth-rs-api",
            "https://api.auth.integration.test",
            [TestPlatformSettingsProvider.ApiScope],
            CancellationToken.None
        );

        var client = CreateClient(factory);

        using var request = new HttpRequestMessage(HttpMethod.Post, TokenPath)
        {
            Headers = { Authorization = BasicHeader(ClientId, ClientSecret) },
            Content = ClientCredentialsBody(),
        };

        using var response = await client.SendAsync(request);

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, body);

        var jsonElement = JsonElement.Parse(body);
        Assert.False(string.IsNullOrEmpty(jsonElement.GetProperty("access_token").GetString()));
    }

    [Fact]
    public async Task PostToken_PostAuthWithWrongSecret_Returns400WithoutChallenge()
    {
        using var factory = new HostApplicationFactory();
        await factory.SeedConfidentialClientAsync(
            ClientId,
            ClientSecret,
            [],
            CancellationToken.None
        );

        var client = CreateClient(factory);

        using var content = new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["grant_type"] = OpenIdProtocol.GrantTypes.ClientCredentials,
                ["client_id"] = ClientId,
                ["client_secret"] = "wrong-secret",
                ["scope"] = TestPlatformSettingsProvider.ApiScope,
            }
        );
        using var response = await client.PostAsync(TokenPath, content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(response.Headers.WwwAuthenticate);

        var jsonElement = JsonElement.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(
            OpenIdProtocol.ErrorCodes.InvalidClient,
            jsonElement.GetProperty("error").GetString()
        );
    }

    #endregion
}
