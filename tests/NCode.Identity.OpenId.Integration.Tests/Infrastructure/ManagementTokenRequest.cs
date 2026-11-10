// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using NCode.Buffers;
using NCode.Identity.OpenId.Host;
using Xunit;

namespace NCode.Identity.OpenId.Integration.Tests.Infrastructure;

/// <summary>
/// The <c>client_credentials</c> request a client registered for <c>client_secret_basic</c> sends for a management
/// token.
/// </summary>
/// <param name="ClientId">The identifier of the client that authenticates with a shared secret.</param>
/// <param name="ClientSecret">The client's secret, as sent to the token endpoint.</param>
public sealed record ManagementTokenRequest(string ClientId, string ClientSecret)
{
    /// <summary>
    /// The scope every management token request asks for.
    /// </summary>
    public const string Scope = "read:clients";

    /// <summary>
    /// The path of the token endpoint when the tenant is not selected by path.
    /// </summary>
    public const string DefaultTokenPath = "/oauth2/token";

    /// <summary>
    /// Gets the path of the token endpoint that the request is sent to.
    /// </summary>
    public string TokenPath { get; init; } = DefaultTokenPath;

    /// <summary>
    /// Creates the token request, which authenticates with HTTP Basic authentication.
    /// </summary>
    /// <returns>A new request message that the caller disposes.</returns>
    public HttpRequestMessage CreateMessage()
    {
        var credentials = SecureEncoding.UTF8.GetBytes($"{ClientId}:{ClientSecret}");
        return new HttpRequestMessage(HttpMethod.Post, TokenPath)
        {
            Headers =
            {
                Authorization = new AuthenticationHeaderValue(
                    "Basic",
                    Convert.ToBase64String(credentials)
                ),
            },
            Content = new FormUrlEncodedContent(
                new Dictionary<string, string>
                {
                    ["grant_type"] = OpenIdProtocol.GrantTypes.ClientCredentials,
                    ["scope"] = Scope,
                }
            ),
        };
    }

    /// <summary>
    /// Sends the token request and returns the access token that the token endpoint issues.
    /// </summary>
    /// <param name="client">The HTTP client that sends the request.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The access token.</returns>
    public async ValueTask<string> GetAccessTokenAsync(
        HttpClient client,
        CancellationToken cancellationToken
    )
    {
        using var request = CreateMessage();
        using var response = await client.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        Assert.True(response.StatusCode == HttpStatusCode.OK, body);

        var jsonElement = JsonElement.Parse(body);
        var accessToken = jsonElement.GetProperty("access_token").GetString();
        Assert.False(string.IsNullOrEmpty(accessToken), body);
        return accessToken;
    }

    /// <summary>
    /// Sends the token request and sets the issued access token as the bearer token of the client.
    /// </summary>
    /// <param name="client">The HTTP client that sends the request and carries the bearer token.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    public async ValueTask AuthorizeAsync(HttpClient client, CancellationToken cancellationToken)
    {
        var accessToken = await GetAccessTokenAsync(client, cancellationToken);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            accessToken
        );
    }

    /// <summary>
    /// Creates an HTTP client for the given host that carries the bearer token that the token request issues.
    /// </summary>
    /// <param name="factory">The host that the client calls.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The HTTP client, which does not follow redirects and carries the bearer token.</returns>
    public async ValueTask<HttpClient> CreateClientAsync(
        WebApplicationFactory<HostApiMarker> factory,
        CancellationToken cancellationToken
    )
    {
        var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }
        );
        try
        {
            await AuthorizeAsync(client, cancellationToken);
            return client;
        }
        catch
        {
            client.Dispose();
            throw;
        }
    }
}
