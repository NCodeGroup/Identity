// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using NCode.Identity.OpenId.Host;
using NCode.Identity.OpenId.Integration.Tests.Infrastructure;
using NCode.Identity.OpenId.Settings;
using Xunit;

namespace NCode.Identity.OpenId.Integration.Tests.Endpoints;

/// <summary>
/// Drives failed client authentications at the token endpoint through the real host and confirms that a remote address
/// is refused for a client once it reaches the limit, with the answer a rate limit gives, while the same client from
/// another address still authenticates.
/// </summary>
// See ADR-1399.
public class ClientAuthenticationAttemptLimitTests
{
    private const string ClientId = "it-attempt-client";
    private const string ClientSecret = "confidential-secret-value";
    private const string TokenPath = "/oauth2/token";
    private const string AttackerAddress = "203.0.113.5";
    private const string ClientAddress = "198.51.100.7";

    private static ConfiguredHostApplicationFactory CreateFactory(int limit) =>
        new(
            new Dictionary<string, string?>
            {
                [
                    $"OpenId:Platform:Settings:{OpenIdSettingNames.ClientAuthenticationFailedAttemptsLimit}"
                ] = limit.ToString(CultureInfo.InvariantCulture),
            }
        );

    private static HttpClient CreateClient(WebApplicationFactory<HostApiMarker> factory) =>
        factory
            .WithWebHostBuilder(builder =>
                builder.ConfigureServices(serviceCollection =>
                    serviceCollection.AddSingleton<IStartupFilter, TestRemoteAddressStartupFilter>()
                )
            )
            .CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    private static async ValueTask<HttpResponseMessage> PostTokenAsync(
        HttpClient client,
        string remoteAddress,
        string clientSecret,
        CancellationToken cancellationToken
    )
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, TokenPath)
        {
            Content = new FormUrlEncodedContent(
                new Dictionary<string, string>
                {
                    ["grant_type"] = OpenIdProtocol.GrantTypes.ClientCredentials,
                    ["client_id"] = ClientId,
                    ["client_secret"] = clientSecret,
                    ["scope"] = TestPlatformSettingsProvider.ApiScope,
                }
            ),
        };
        request.Headers.Add(TestRemoteAddressStartupFilter.HeaderName, remoteAddress);
        return await client.SendAsync(request, cancellationToken);
    }

    private static async ValueTask SeedAsync(
        HostApplicationFactory factory,
        CancellationToken cancellationToken
    )
    {
        await factory.SeedConfidentialClientAsync(ClientId, ClientSecret, [], cancellationToken);
        await factory.SeedResourceServerWithClientGrantAsync(
            ClientId,
            "it-attempt-rs-api",
            "https://api.attempt.integration.test",
            [TestPlatformSettingsProvider.ApiScope],
            cancellationToken
        );
    }

    #region Attempt Limit Tests

    [Fact]
    public async Task PostToken_WhenTheAddressReachedTheLimitForTheClient_RefusesTheRightSecretWithTooManyRequests()
    {
        using var factory = CreateFactory(limit: 2);
        await SeedAsync(factory, CancellationToken.None);
        var client = CreateClient(factory);

        for (var i = 0; i < 2; ++i)
        {
            using var failed = await PostTokenAsync(
                client,
                AttackerAddress,
                "guessed-secret",
                CancellationToken.None
            );
            Assert.Equal(HttpStatusCode.BadRequest, failed.StatusCode);
        }

        // The right secret from the same address is refused before it is verified, until the window ends.
        using var response = await PostTokenAsync(
            client,
            AttackerAddress,
            ClientSecret,
            CancellationToken.None
        );

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.TooManyRequests, body);
        var retryAfter = response.Headers.RetryAfter?.Delta;
        Assert.NotNull(retryAfter);
        Assert.InRange(retryAfter.Value, TimeSpan.FromSeconds(1), TimeSpan.FromMinutes(15));
        Assert.True(response.Headers.CacheControl?.NoStore);
        Assert.Equal(
            OpenIdProtocol.ErrorCodes.TemporarilyUnavailable,
            JsonElement.Parse(body).GetProperty("error").GetString()
        );
    }

    [Fact]
    public async Task PostToken_WhenAnotherAddressReachedTheLimitForTheClient_AuthenticatesTheClient()
    {
        // Failures from one address never lock the client out from another: the limit is no lockout lever.
        using var factory = CreateFactory(limit: 2);
        await SeedAsync(factory, CancellationToken.None);
        var client = CreateClient(factory);

        for (var i = 0; i < 3; ++i)
        {
            using var failed = await PostTokenAsync(
                client,
                AttackerAddress,
                "guessed-secret",
                CancellationToken.None
            );
            Assert.NotEqual(HttpStatusCode.OK, failed.StatusCode);
        }

        using var response = await PostTokenAsync(
            client,
            ClientAddress,
            ClientSecret,
            CancellationToken.None
        );

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, body);
    }

    [Fact]
    public async Task PostToken_WhenBelowTheLimit_AuthenticatesTheRightSecret()
    {
        using var factory = CreateFactory(limit: 2);
        await SeedAsync(factory, CancellationToken.None);
        var client = CreateClient(factory);

        using (
            var failed = await PostTokenAsync(
                client,
                ClientAddress,
                "mistyped-secret",
                CancellationToken.None
            )
        )
            Assert.Equal(HttpStatusCode.BadRequest, failed.StatusCode);

        using var response = await PostTokenAsync(
            client,
            ClientAddress,
            ClientSecret,
            CancellationToken.None
        );

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, body);
    }

    #endregion
}
