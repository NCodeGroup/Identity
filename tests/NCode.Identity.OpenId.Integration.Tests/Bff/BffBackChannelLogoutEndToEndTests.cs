// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Web;
using Microsoft.AspNetCore.Mvc.Testing;
using NCode.Identity.Bff;
using NCode.Identity.OpenId.Integration.Tests.Infrastructure;
using NCode.Identity.OpenId.Settings;
using Xunit;

namespace NCode.Identity.OpenId.Integration.Tests.Bff;

/// <summary>
/// Proves that a back-channel logout from the NCode OpenID provider ends the backend-for-frontend (BFF) session, and
/// that a pipeline which authenticates before <c>UseBff()</c> fails closed.
/// </summary>
public class BffBackChannelLogoutEndToEndTests
{
    private const string UserName = "bff-bcl-user";
    private const string Password = "bff-bcl-password";
    private const string EndSessionPath = "/oauth2/end_session";
    private const string BackChannelLogoutPath = "/bff/backchannel-logout";

    private static IReadOnlyDictionary<string, object> NoSettings { get; } =
        new Dictionary<string, object>();

    #region BackChannelLogout Tests

    [Fact]
    public async Task BackChannelLogout_EndsTheSession()
    {
        var deliveries = new ConcurrentQueue<RecordedHttpExchange>();
        using var factory = new HostApplicationFactory { BackChannelLogoutExchanges = deliveries };
        await factory.SeedLocalAccountAsync(UserName, Password, CancellationToken.None);
        await factory.SeedSharedSecretClientWithSettingsAsync(
            BffTestHost.ClientId,
            BffTestHost.ClientSecret,
            OpenIdProtocol.ClientAuthenticationMethods.ClientSecretBasic,
            new Dictionary<string, object>
            {
                [OpenIdSettingNames.BackChannelLogoutUri] =
                    $"https://bff.example{BackChannelLogoutPath}",
                [OpenIdSettingNames.BackChannelLogoutSessionRequired] = true,
            },
            [BffTestHost.CallbackUri],
            CancellationToken.None
        );
        var (keySet, valueSet, indexSet) = BffTestHost.CreateSharedState();
        await using var host = await BffTestHost.StartAsync(
            factory,
            keySet,
            valueSet,
            indexSet,
            new BffTestHostOptions(),
            CancellationToken.None
        );

        var browser = CreateBrowser(factory);
        var providerSession = await InteractiveLogin.SignInAsync(
            browser,
            UserName,
            Password,
            CancellationToken.None
        );
        await LoginAsync(browser, providerSession, host, CancellationToken.None);

        var csrf = (BffDefaults.AntiforgeryHeaderName, BffDefaults.AntiforgeryHeaderValue);
        var userPath = $"{BffDefaults.EndpointPrefix}/user";
        using var before = await host.SendAsync(
            HttpMethod.Get,
            userPath,
            [csrf],
            CancellationToken.None
        );
        Assert.Equal(HttpStatusCode.OK, before.StatusCode);

        await EndSessionAsync(browser, providerSession, CancellationToken.None);
        var logoutToken = await WaitForLogoutTokenAsync(deliveries, CancellationToken.None);

        using var form = new FormUrlEncodedContent(
            new Dictionary<string, string> { [OpenIdProtocol.Parameters.LogoutToken] = logoutToken }
        );
        using var backChannel = await host.PostFormAsync(
            BackChannelLogoutPath,
            form,
            CancellationToken.None
        );
        Assert.Equal(HttpStatusCode.OK, backChannel.StatusCode);

        using var after = await host.SendAsync(
            HttpMethod.Get,
            userPath,
            [csrf],
            CancellationToken.None
        );
        Assert.Equal(HttpStatusCode.Unauthorized, after.StatusCode);
    }

    #endregion

    #region UseBff Tests

    [Fact]
    public async Task UseBffAfterAuthentication_Fails()
    {
        using var factory = new HostApplicationFactory();
        await factory.SeedSharedSecretClientWithSettingsAsync(
            BffTestHost.ClientId,
            BffTestHost.ClientSecret,
            OpenIdProtocol.ClientAuthenticationMethods.ClientSecretBasic,
            NoSettings,
            [BffTestHost.CallbackUri],
            CancellationToken.None
        );
        var (keySet, valueSet, indexSet) = BffTestHost.CreateSharedState();
        await using var host = await BffTestHost.StartAsync(
            factory,
            keySet,
            valueSet,
            indexSet,
            new BffTestHostOptions { AuthenticateBeforeBff = true },
            CancellationToken.None
        );

        var csrf = (BffDefaults.AntiforgeryHeaderName, BffDefaults.AntiforgeryHeaderValue);
        using var user = await host.SendAsync(
            HttpMethod.Get,
            $"{BffDefaults.EndpointPrefix}/user",
            [csrf],
            CancellationToken.None
        );

        Assert.Equal(HttpStatusCode.InternalServerError, user.StatusCode);
    }

    #endregion

    #region Scaffolding

    private static HttpClient CreateBrowser(HostApplicationFactory factory) =>
        factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                HandleCookies = false,
            }
        );

    private static async ValueTask LoginAsync(
        HttpClient browser,
        string providerSession,
        BffTestHost host,
        CancellationToken cancellationToken
    )
    {
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

    private static async ValueTask EndSessionAsync(
        HttpClient browser,
        string providerSession,
        CancellationToken cancellationToken
    )
    {
        using var response = await InteractiveLogin.ConfirmEndSessionAsync(
            browser,
            EndSessionPath,
            providerSession,
            cancellationToken
        );

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // The provider delivers the logout token on a background worker, so it arrives after the end-session response.
    private static async ValueTask<string> WaitForLogoutTokenAsync(
        ConcurrentQueue<RecordedHttpExchange> deliveries,
        CancellationToken cancellationToken
    )
    {
        var stopwatch = Stopwatch.StartNew();
        while (deliveries.IsEmpty && stopwatch.Elapsed < TimeSpan.FromSeconds(15))
        {
            await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);
        }

        var delivery = Assert.Single(deliveries);
        Assert.Equal(BackChannelLogoutPath, delivery.Path);

        var logoutToken = HttpUtility.ParseQueryString(delivery.Body ?? string.Empty)[
            OpenIdProtocol.Parameters.LogoutToken
        ];
        Assert.False(string.IsNullOrEmpty(logoutToken));
        return logoutToken;
    }

    #endregion
}
