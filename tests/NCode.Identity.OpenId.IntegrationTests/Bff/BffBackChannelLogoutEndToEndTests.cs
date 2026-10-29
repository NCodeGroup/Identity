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

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Web;
using Microsoft.AspNetCore.Mvc.Testing;
using NCode.Identity.Bff;
using NCode.Identity.OpenId.IntegrationTests.Infrastructure;
using NCode.Identity.OpenId.Settings;
using Xunit;

namespace NCode.Identity.OpenId.IntegrationTests.Bff;

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
        await factory.SeedLocalAccountAsync(UserName, Password);
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
            BffTestHost.CallbackUri
        );
        var (keySet, valueSet, indexSet) = BffTestHost.CreateSharedState();
        await using var host = await BffTestHost.StartAsync(factory, keySet, valueSet, indexSet);

        var browser = CreateBrowser(factory);
        var providerSession = await InteractiveLogin.SignInAsync(browser, UserName, Password);
        await LoginAsync(browser, providerSession, host);

        var csrf = (BffDefaults.AntiforgeryHeaderName, BffDefaults.AntiforgeryHeaderValue);
        var userPath = $"{BffDefaults.EndpointPrefix}/user";
        using (var before = await host.SendAsync(HttpMethod.Get, userPath, csrf))
        {
            Assert.Equal(HttpStatusCode.OK, before.StatusCode);
        }

        await EndSessionAsync(browser, providerSession);
        var logoutToken = await WaitForLogoutTokenAsync(deliveries);

        using var form = new FormUrlEncodedContent(
            new Dictionary<string, string> { [OpenIdProtocol.Parameters.LogoutToken] = logoutToken }
        );
        using var backChannel = await host.PostFormAsync(BackChannelLogoutPath, form);
        Assert.Equal(HttpStatusCode.OK, backChannel.StatusCode);

        using var after = await host.SendAsync(HttpMethod.Get, userPath, csrf);
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
            BffTestHost.CallbackUri
        );
        var (keySet, valueSet, indexSet) = BffTestHost.CreateSharedState();
        await using var host = await BffTestHost.StartAsync(
            factory,
            keySet,
            valueSet,
            indexSet,
            authenticateBeforeBff: true
        );

        var csrf = (BffDefaults.AntiforgeryHeaderName, BffDefaults.AntiforgeryHeaderValue);
        using var user = await host.SendAsync(
            HttpMethod.Get,
            $"{BffDefaults.EndpointPrefix}/user",
            csrf
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

    private static async Task LoginAsync(
        HttpClient browser,
        string providerSession,
        BffTestHost host
    )
    {
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

    private static async Task EndSessionAsync(HttpClient browser, string providerSession)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, EndSessionPath);
        request.Headers.Add("Cookie", $"{InteractiveLogin.SessionCookieName}={providerSession}");
        using var response = await browser.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // The provider delivers the logout token on a background worker, so it arrives after the end-session response.
    private static async Task<string> WaitForLogoutTokenAsync(
        ConcurrentQueue<RecordedHttpExchange> deliveries
    )
    {
        var stopwatch = Stopwatch.StartNew();
        while (deliveries.IsEmpty && stopwatch.Elapsed < TimeSpan.FromSeconds(15))
        {
            await Task.Delay(TimeSpan.FromMilliseconds(100));
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
