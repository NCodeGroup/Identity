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
        var options = new BffTestHostOptions
        {
            ExtraConfiguration = configuration,
            Exchanges = exchanges,
        };
        await using var hostA = await BffTestHost.StartAsync(
            factory,
            keySet,
            valueSet,
            indexSet,
            options,
            CancellationToken.None
        );
        await using var hostB = await BffTestHost.StartAsync(
            factory,
            keySet,
            valueSet,
            indexSet,
            options,
            CancellationToken.None
        );

        await LoginAsync(factory, hostA, CancellationToken.None);
        hostB.CopyCookiesFrom(hostA);

        var csrf = (BffDefaults.AntiforgeryHeaderName, BffDefaults.AntiforgeryHeaderValue);
        var calls = new List<Task<HttpStatusCode>>();
        for (var i = 0; i < CallsPerHost; i++)
        {
            var callA = GetStatusAsync(hostA, csrf, CancellationToken.None).AsTask();
            var callB = GetStatusAsync(hostB, csrf, CancellationToken.None).AsTask();
            calls.Add(callA);
            calls.Add(callB);
        }

        var statuses = await Task.WhenAll(calls);
        Assert.All(statuses, status => Assert.Equal(HttpStatusCode.OK, status));

        var further = await GetStatusAsync(hostA, csrf, CancellationToken.None);
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

    private static async ValueTask<HttpStatusCode> GetStatusAsync(
        BffTestHost host,
        (string Name, string Value) csrf,
        CancellationToken cancellationToken
    )
    {
        using var response = await host.SendAsync(
            HttpMethod.Get,
            BffTestHost.ApiTokenPath,
            [csrf],
            cancellationToken
        );
        return response.StatusCode;
    }

    private static async ValueTask LoginAsync(
        HostApplicationFactory factory,
        BffTestHost host,
        CancellationToken cancellationToken
    )
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
            cancellationToken
        );

        var loginPath =
            $"{BffDefaults.EndpointPrefix}/login?returnUrl=%2F&{OpenIdProtocol.Parameters.Prompt}={OpenIdProtocol.PromptTypes.None}";
        using var login = await host.SendAsync(HttpMethod.Get, loginPath, [], cancellationToken);
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
        var authorizeUri = login.Headers.Location;
        Assert.NotNull(authorizeUri);

        using var authorizeRequest = new HttpRequestMessage(HttpMethod.Get, authorizeUri);
        authorizeRequest.Headers.Add(
            "Cookie",
            $"{InteractiveLogin.SessionCookieName}={providerSession}"
        );
        using var authorize = await browser.SendAsync(authorizeRequest, cancellationToken);
        var callbackUri = authorize.Headers.Location;
        Assert.NotNull(callbackUri);

        using var callback = await host.SendAsync(
            HttpMethod.Get,
            callbackUri.PathAndQuery,
            [],
            cancellationToken
        );
        Assert.Equal(HttpStatusCode.Redirect, callback.StatusCode);
    }

    #endregion
}
