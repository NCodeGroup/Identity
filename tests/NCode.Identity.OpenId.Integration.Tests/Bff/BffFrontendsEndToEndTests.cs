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
        await factory.SeedLocalAccountAsync(UserName, Password, CancellationToken.None);
        await factory.SeedSharedSecretClientWithSettingsAsync(
            BffTestHost.ClientId,
            BffTestHost.ClientSecret,
            OpenIdProtocol.ClientAuthenticationMethods.ClientSecretBasic,
            NoSettings,
            [BffTestHost.CallbackUri],
            CancellationToken.None
        );
        await factory.SeedSharedSecretClientWithSettingsAsync(
            AdminClientId,
            BffTestHost.ClientSecret,
            OpenIdProtocol.ClientAuthenticationMethods.ClientSecretBasic,
            NoSettings,
            [AdminCallbackUri],
            CancellationToken.None
        );
        var (keySet, valueSet, indexSet) = BffTestHost.CreateSharedState();
        await using var host = await BffTestHost.StartAsync(
            factory,
            keySet,
            valueSet,
            indexSet,
            new BffTestHostOptions
            {
                ExtraConfiguration = new Dictionary<string, string?>
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
                },
            },
            CancellationToken.None
        );

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
        var csrf = (BffDefaults.AntiforgeryHeaderName, BffDefaults.AntiforgeryHeaderValue);
        const string webUserPath = "/bff/user";
        const string adminUserPath = "/admin/bff/user";

        await LoginAsync(
            browser,
            providerSession,
            host,
            "/bff/login?returnUrl=%2F",
            CancellationToken.None
        );
        var webUserStatus = await GetStatusAsync(host, webUserPath, csrf, CancellationToken.None);
        Assert.Equal(HttpStatusCode.OK, webUserStatus);
        var unauthenticatedAdminUserStatus = await GetStatusAsync(
            host,
            adminUserPath,
            csrf,
            CancellationToken.None
        );
        Assert.Equal(HttpStatusCode.Unauthorized, unauthenticatedAdminUserStatus);

        await LoginAsync(
            browser,
            providerSession,
            host,
            "/admin/bff/login?returnUrl=%2Fadmin",
            CancellationToken.None
        );
        var adminSessionWebUserStatus = await GetStatusAsync(
            host,
            webUserPath,
            csrf,
            CancellationToken.None
        );
        Assert.Equal(HttpStatusCode.OK, adminSessionWebUserStatus);
        var adminSessionAdminUserStatus = await GetStatusAsync(
            host,
            adminUserPath,
            csrf,
            CancellationToken.None
        );
        Assert.Equal(HttpStatusCode.OK, adminSessionAdminUserStatus);

        using var user = await host.SendAsync(
            HttpMethod.Get,
            webUserPath,
            [csrf],
            CancellationToken.None
        );
        var body = JsonElement.Parse(await user.Content.ReadAsStringAsync());
        var logoutSid = body.GetProperty("logoutSid").GetString() ?? string.Empty;

        Assert.NotEmpty(logoutSid);

        using var form = new FormUrlEncodedContent(
            new Dictionary<string, string> { ["sid"] = logoutSid, ["returnUrl"] = "/" }
        );
        using var logout = await host.SendAsync(
            HttpMethod.Post,
            "/bff/logout",
            form,
            [],
            CancellationToken.None
        );
        Assert.Equal(HttpStatusCode.Redirect, logout.StatusCode);

        var loggedOutWebUserStatus = await GetStatusAsync(
            host,
            webUserPath,
            csrf,
            CancellationToken.None
        );
        Assert.Equal(HttpStatusCode.Unauthorized, loggedOutWebUserStatus);
        var retainedAdminUserStatus = await GetStatusAsync(
            host,
            adminUserPath,
            csrf,
            CancellationToken.None
        );
        Assert.Equal(HttpStatusCode.OK, retainedAdminUserStatus);
    }

    #endregion

    #region Scaffolding

    private static async ValueTask LoginAsync(
        HttpClient browser,
        string providerSession,
        BffTestHost host,
        string loginPath,
        CancellationToken cancellationToken
    )
    {
        var pathAndQuery =
            $"{loginPath}&{OpenIdProtocol.Parameters.Prompt}={OpenIdProtocol.PromptTypes.None}";
        using var login = await host.SendAsync(HttpMethod.Get, pathAndQuery, [], cancellationToken);
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

    private static async ValueTask<HttpStatusCode> GetStatusAsync(
        BffTestHost host,
        string path,
        (string Name, string Value) csrf,
        CancellationToken cancellationToken
    )
    {
        using var response = await host.SendAsync(HttpMethod.Get, path, [csrf], cancellationToken);
        return response.StatusCode;
    }

    #endregion
}
