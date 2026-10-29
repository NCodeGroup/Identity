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
using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using NCode.Identity.Bff;
using NCode.Identity.OpenId.IntegrationTests.Infrastructure;
using NCode.Identity.OpenId.Settings;
using Xunit;

namespace NCode.Identity.OpenId.IntegrationTests.Bff;

/// <summary>
/// Proves the backend-for-frontend (BFF) against the NCode OpenID provider hosted in-process: login, the callback,
/// <c>/bff/user</c> and a local API behind the session cookie.
/// </summary>
public class BffEndToEndTests
{
    private const string UserName = "bff-e2e-user";
    private const string Password = "bff-e2e-password";

    private static IReadOnlyDictionary<string, object> NoSettings { get; } =
        new Dictionary<string, object>();

    #region Login Tests

    private static async Task SeedAsync(
        HostApplicationFactory factory,
        IReadOnlyDictionary<string, object>? settings = null
    )
    {
        await factory.SeedLocalAccountAsync(UserName, Password);
        await factory.SeedSharedSecretClientWithSettingsAsync(
            BffTestHost.ClientId,
            BffTestHost.ClientSecret,
            OpenIdProtocol.ClientAuthenticationMethods.ClientSecretBasic,
            settings ?? NoSettings,
            BffTestHost.CallbackUri
        );
    }

    [Fact]
    public async Task Login_WithUserInfoLoad_CreatesTheSessionWithTheUserInfoClaims()
    {
        using var factory = new HostApplicationFactory();
        await SeedAsync(factory);
        var (keySet, valueSet, indexSet) = BffTestHost.CreateSharedState();
        var exchanges = new ConcurrentQueue<RecordedHttpExchange>();
        await using var host = await BffTestHost.StartAsync(
            factory,
            keySet,
            valueSet,
            indexSet,
            extraConfiguration: new Dictionary<string, string?> { ["Bff:UserInfo:Load"] = "true" },
            exchanges: exchanges
        );

        // The callback requests UserInfo with the new access token; a failure would redirect with an error instead.
        using var callback = await CompleteLoginAsync(factory, host);
        Assert.Equal(HttpStatusCode.Redirect, callback.StatusCode);
        Assert.Equal("/", callback.Headers.Location?.OriginalString);

        // The test host configures no DPoP key, so UserInfo is requested with the bearer token the OP issued.
        var userInfo = exchanges.Last(exchange =>
            exchange.Path.EndsWith("/userinfo", StringComparison.Ordinal)
        );
        Assert.Equal(HttpStatusCode.OK, userInfo.StatusCode);
        Assert.Equal(OpenIdProtocol.TokenTypes.Bearer, userInfo.AuthorizationScheme);
        Assert.Null(userInfo.DPoPProof);

        var csrf = (BffDefaults.AntiforgeryHeaderName, BffDefaults.AntiforgeryHeaderValue);
        using var user = await host.SendAsync(
            HttpMethod.Get,
            $"{BffDefaults.EndpointPrefix}/user",
            csrf
        );
        Assert.Equal(HttpStatusCode.OK, user.StatusCode);
    }

    [Fact]
    public async Task Login_WithSignedUserInfo_ValidatesTheJwtAndCreatesTheSession()
    {
        using var factory = new HostApplicationFactory();
        await SeedAsync(
            factory,
            new Dictionary<string, object>
            {
                [OpenIdSettingNames.UserInfoSignedResponseAlg] = "RS256",
            }
        );
        var (keySet, valueSet, indexSet) = BffTestHost.CreateSharedState();
        var exchanges = new ConcurrentQueue<RecordedHttpExchange>();
        await using var host = await BffTestHost.StartAsync(
            factory,
            keySet,
            valueSet,
            indexSet,
            extraConfiguration: new Dictionary<string, string?>
            {
                ["Bff:UserInfo:Load"] = "true",
                ["Bff:UserInfo:SigningAlgorithms:0"] = "RS256",
            },
            exchanges: exchanges
        );

        // A UserInfo response that failed its validation would redirect with userinfo_failed instead.
        using var callback = await CompleteLoginAsync(factory, host);
        Assert.Equal(HttpStatusCode.Redirect, callback.StatusCode);
        Assert.Equal("/", callback.Headers.Location?.OriginalString);

        var userInfo = exchanges.Last(exchange =>
            exchange.Path.EndsWith("/userinfo", StringComparison.Ordinal)
        );
        Assert.Equal(HttpStatusCode.OK, userInfo.StatusCode);
        Assert.Equal(3, userInfo.ResponseBody?.Split('.').Length);
    }

    private static async Task<HttpResponseMessage> CompleteLoginAsync(
        HostApplicationFactory factory,
        BffTestHost host
    )
    {
        var browser = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                HandleCookies = false,
            }
        );
        var providerSession = await InteractiveLogin.SignInAsync(browser, UserName, Password);

        var loginPath =
            $"{BffDefaults.EndpointPrefix}/login?returnUrl=%2F&{OpenIdProtocol.Parameters.Prompt}={OpenIdProtocol.PromptTypes.None}";
        using var login = await host.SendAsync(HttpMethod.Get, loginPath);
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

        return await host.SendAsync(HttpMethod.Get, callbackUri.PathAndQuery);
    }

    [Fact]
    public async Task Login_ThenUserAndApi_Succeed()
    {
        using var factory = new HostApplicationFactory();
        await SeedAsync(factory);
        var (keySet, valueSet, indexSet) = BffTestHost.CreateSharedState();
        await using var host = await BffTestHost.StartAsync(factory, keySet, valueSet, indexSet);

        var browser = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                HandleCookies = false,
            }
        );
        var providerSession = await InteractiveLogin.SignInAsync(browser, UserName, Password);

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
        Assert.StartsWith(
            BffTestHost.CallbackUri,
            callbackUri.OriginalString,
            StringComparison.Ordinal
        );

        using var callback = await host.SendAsync(HttpMethod.Get, callbackUri.PathAndQuery);
        Assert.Equal(HttpStatusCode.Redirect, callback.StatusCode);
        Assert.Equal("/", callback.Headers.Location?.OriginalString);

        var csrf = (BffDefaults.AntiforgeryHeaderName, BffDefaults.AntiforgeryHeaderValue);
        var userPath = $"{BffDefaults.EndpointPrefix}/user";
        using var user = await host.SendAsync(HttpMethod.Get, userPath, csrf);
        Assert.Equal(HttpStatusCode.OK, user.StatusCode);
        var userBody = await user.Content.ReadAsStringAsync();
        Assert.Contains("\"sub\"", userBody, StringComparison.Ordinal);

        using var userWithoutHeader = await host.SendAsync(HttpMethod.Get, userPath);
        Assert.Equal(HttpStatusCode.Forbidden, userWithoutHeader.StatusCode);

        using var api = await host.SendAsync(HttpMethod.Get, BffTestHost.ApiTokenPath, csrf);
        Assert.Equal(HttpStatusCode.OK, api.StatusCode);

        host.ClearCookies();
        using var anonymousApi = await host.SendAsync(
            HttpMethod.Get,
            BffTestHost.ApiTokenPath,
            csrf
        );
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousApi.StatusCode);
    }

    #endregion
}
