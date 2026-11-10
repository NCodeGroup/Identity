// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;
using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using NCode.Identity.Bff;
using NCode.Identity.OpenId.Integration.Tests.Infrastructure;
using Xunit;

namespace NCode.Identity.OpenId.Integration.Tests.Bff;

/// <summary>
/// Proves that the backend-for-frontend (BFF) refreshes a session single-flight across replicas, so the provider
/// never sees a refresh token presented twice.
/// </summary>
public class BffRefreshEndToEndTests
{
    private const string UserName = "bff-refresh-user";
    private const string Password = "bff-refresh-password";
    private const int CallsPerHost = 4;

    private static IReadOnlyDictionary<string, object> NoSettings { get; } =
        new Dictionary<string, object>();

    #region Refresh Tests

    [Fact]
    public async Task ConcurrentRefreshes_AcrossTwoReplicas_NeverReuseARefreshToken()
    {
        using var factory = new HostApplicationFactory();
        await factory.SeedLocalAccountAsync(UserName, Password, CancellationToken.None);
        await factory.SeedSharedSecretClientWithSettingsAsync(
            BffTestHost.ClientId,
            BffTestHost.ClientSecret,
            OpenIdProtocol.ClientAuthenticationMethods.ClientSecretBasic,
            NoSettings,
            [BffTestHost.CallbackUri],
            CancellationToken.None
        );
        var (keySet, valueSet, indexSet) = BffTestHost.CreateSharedState();
        var exchanges = new ConcurrentQueue<RecordedHttpExchange>();
        var configuration = new Dictionary<string, string?>
        {
            ["Bff:Tokens:RefreshBeforeExpiry"] = "01:00:00",
        };
        await using var hostA = await BffTestHost.StartAsync(
            factory,
            keySet,
            valueSet,
            indexSet,
            configuration,
            exchanges
        );
        await using var hostB = await BffTestHost.StartAsync(
            factory,
            keySet,
            valueSet,
            indexSet,
            configuration,
            exchanges
        );

        await LoginAsync(factory, hostA);
        hostB.CopyCookiesFrom(hostA);

        var csrf = (BffDefaults.AntiforgeryHeaderName, BffDefaults.AntiforgeryHeaderValue);
        var calls = new List<Task<HttpStatusCode>>();
        for (var i = 0; i < CallsPerHost; i++)
        {
            calls.Add(GetStatusAsync(hostA, csrf));
            calls.Add(GetStatusAsync(hostB, csrf));
        }

        var statuses = await Task.WhenAll(calls);
        Assert.All(statuses, status => Assert.Equal(HttpStatusCode.OK, status));

        var further = await GetStatusAsync(hostA, csrf);
        Assert.Equal(HttpStatusCode.OK, further);

        var tokenExchanges = exchanges
            .Where(exchange =>
                string.Equals(
                    exchange.Path,
                    OpenIdConstants.EndpointPaths.Token,
                    StringComparison.Ordinal
                )
            )
            .ToList();
        Assert.NotEmpty(tokenExchanges);
        Assert.DoesNotContain(
            tokenExchanges,
            exchange =>
                exchange.ResponseBody?.Contains(
                    OpenIdProtocol.ErrorCodes.InvalidGrant,
                    StringComparison.Ordinal
                ) == true
        );
    }

    #endregion

    #region Scaffolding

    private static async Task<HttpStatusCode> GetStatusAsync(
        BffTestHost host,
        (string Name, string Value) csrf
    )
    {
        using var response = await host.SendAsync(HttpMethod.Get, BffTestHost.ApiTokenPath, csrf);
        return response.StatusCode;
    }

    private static async Task LoginAsync(HostApplicationFactory factory, BffTestHost host)
    {
        var browser = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                HandleCookies = false,
            }
        );
        var providerSession = await InteractiveLogin.SignInAsync(
            browser,
            UserName,
            Password,
            CancellationToken.None
        );

        var loginPath =
            $"{BffDefaults.EndpointPrefix}/login?returnUrl=%2F&{OpenIdProtocol.Parameters.Prompt}={OpenIdProtocol.PromptTypes.None}";
        using var login = await host.SendAsync(HttpMethod.Get, loginPath);
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
        var authorizeUri = login.Headers.Location;
        Assert.NotNull(authorizeUri);

        using var authorizeRequest = new HttpRequestMessage(HttpMethod.Get, authorizeUri);
        authorizeRequest.Headers.Add(
            "Cookie",
            $"{InteractiveLogin.SessionCookieName}={providerSession}"
        );
        using var authorize = await browser.SendAsync(authorizeRequest);
        var callbackUri = authorize.Headers.Location;
        Assert.NotNull(callbackUri);

        using var callback = await host.SendAsync(HttpMethod.Get, callbackUri.PathAndQuery);
        Assert.Equal(HttpStatusCode.Redirect, callback.StatusCode);
    }

    #endregion
}
