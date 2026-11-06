// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using NCode.Identity.Bff;
using NCode.Identity.OpenId.Integration.Tests.Infrastructure;
using Xunit;

namespace NCode.Identity.OpenId.Integration.Tests.Bff;

/// <summary>
/// Proves that two frontends of one backend-for-frontend (BFF) deployment keep separate sessions against the NCode
/// OpenID provider hosted in-process.
/// </summary>
public class BffFrontendsEndToEndTests
{
    private const string UserName = "bff-frontends-user";
    private const string Password = "bff-frontends-password";
    private const string AdminClientId = "it-bff-e2e-admin";
    private const string AdminCallbackUri = "https://bff.example/admin/bff/callback";
    private const string AdminPrefix = "Bff:Frontends:admin";

    private static IReadOnlyDictionary<string, object> NoSettings { get; } =
        new Dictionary<string, object>();

    #region Login Tests

    [Fact]
    public async Task Login_TwoPathFrontends_KeepSeparateSessions()
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
        await factory.SeedSharedSecretClientWithSettingsAsync(
            AdminClientId,
            BffTestHost.ClientSecret,
            OpenIdProtocol.ClientAuthenticationMethods.ClientSecretBasic,
            NoSettings,
            AdminCallbackUri
        );
        var (keySet, valueSet, indexSet) = BffTestHost.CreateSharedState();
        await using var host = await BffTestHost.StartAsync(
            factory,
            keySet,
            valueSet,
            indexSet,
            extraConfiguration: new Dictionary<string, string?>
            {
                [$"{AdminPrefix}:Selection:Rule"] = "path",
                [$"{AdminPrefix}:Selection:Values:0"] = "/admin",
                [$"{AdminPrefix}:Authority"] = HostApplicationFactory.TestServerIssuer,
                [$"{AdminPrefix}:PublicOrigin"] = "https://bff.example",
                [$"{AdminPrefix}:ClientId"] = AdminClientId,
                [$"{AdminPrefix}:ClientAuthentication:Method"] = OpenIdProtocol
                    .ClientAuthenticationMethods
                    .ClientSecretBasic,
                [$"{AdminPrefix}:ClientAuthentication:KeyRing"] = BffTestHost.KeyRing,
                [$"{AdminPrefix}:ReturnUrls:0"] = "/admin",
                [$"{AdminPrefix}:PathPrefix"] = "/admin/bff",
            }
        );

        var browser = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                HandleCookies = false,
            }
        );
        var providerSession = await InteractiveLogin.SignInAsync(browser, UserName, Password);
        var csrf = (BffDefaults.AntiforgeryHeaderName, BffDefaults.AntiforgeryHeaderValue);
        const string webUserPath = "/bff/user";
        const string adminUserPath = "/admin/bff/user";

        await LoginAsync(browser, providerSession, host, "/bff/login?returnUrl=%2F");
        Assert.Equal(HttpStatusCode.OK, await GetStatusAsync(host, webUserPath, csrf));
        Assert.Equal(HttpStatusCode.Unauthorized, await GetStatusAsync(host, adminUserPath, csrf));

        await LoginAsync(browser, providerSession, host, "/admin/bff/login?returnUrl=%2Fadmin");
        Assert.Equal(HttpStatusCode.OK, await GetStatusAsync(host, webUserPath, csrf));
        Assert.Equal(HttpStatusCode.OK, await GetStatusAsync(host, adminUserPath, csrf));

        string logoutSid;
        using (var user = await host.SendAsync(HttpMethod.Get, webUserPath, csrf))
        {
            var body = JsonElement.Parse(await user.Content.ReadAsStringAsync());
            logoutSid = body.GetProperty("logoutSid").GetString() ?? string.Empty;
        }

        Assert.NotEmpty(logoutSid);

        using var form = new FormUrlEncodedContent(
            new Dictionary<string, string> { ["sid"] = logoutSid, ["returnUrl"] = "/" }
        );
        using (var logout = await host.SendAsync(HttpMethod.Post, "/bff/logout", form))
        {
            Assert.Equal(HttpStatusCode.Redirect, logout.StatusCode);
        }

        Assert.Equal(HttpStatusCode.Unauthorized, await GetStatusAsync(host, webUserPath, csrf));
        Assert.Equal(HttpStatusCode.OK, await GetStatusAsync(host, adminUserPath, csrf));
    }

    #endregion

    #region Scaffolding

    private static async Task LoginAsync(
        HttpClient browser,
        string providerSession,
        BffTestHost host,
        string loginPath
    )
    {
        var pathAndQuery =
            $"{loginPath}&{OpenIdProtocol.Parameters.Prompt}={OpenIdProtocol.PromptTypes.None}";
        using var login = await host.SendAsync(HttpMethod.Get, pathAndQuery);
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

    private static async Task<HttpStatusCode> GetStatusAsync(
        BffTestHost host,
        string path,
        (string Name, string Value) csrf
    )
    {
        using var response = await host.SendAsync(HttpMethod.Get, path, csrf);
        return response.StatusCode;
    }

    #endregion
}
