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
using Microsoft.AspNetCore.Mvc.Testing;
using NCode.Identity.Bff;
using NCode.Identity.OpenId.IntegrationTests.Infrastructure;
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

    [Fact]
    public async Task Login_ThenUserAndApi_Succeed()
    {
        using var factory = new HostApplicationFactory();
        await factory.SeedLocalAccountAsync(UserName, Password);
        await factory.SeedSharedSecretClientWithSettingsAsync(
            BffTestHost.ClientId,
            BffTestHost.ClientSecret,
            OpenIdProtocol.ClientAuthenticationMethods.ClientSecretBasic,
            NoSettings,
            BffTestHost.CallbackUri
        );
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
