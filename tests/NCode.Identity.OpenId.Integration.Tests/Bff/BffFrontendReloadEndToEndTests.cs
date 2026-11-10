// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using NCode.Identity.Bff;
using NCode.Identity.OpenId.Integration.Tests.Infrastructure;
using Xunit;

namespace NCode.Identity.OpenId.Integration.Tests.Bff;

/// <summary>
/// Proves that a frontend added to, changed in and removed from the configuration of a backend-for-frontend (BFF)
/// deployment at run time takes effect without a restart against the NCode OpenID provider hosted in-process.
/// </summary>
public class BffFrontendReloadEndToEndTests
{
    private const string UserName = "bff-reload-user";
    private const string Password = "bff-reload-password";
    private const string AdminClientId = "it-bff-e2e-admin";
    private const string AdminOtherClientId = "it-bff-e2e-admin-other";
    private const string AdminCallbackUri = "https://bff.example/admin/bff/callback";
    private const string AdminPrefix = "Bff:Frontends:admin";

    private static IReadOnlyDictionary<string, object> NoSettings { get; } =
        new Dictionary<string, object>();

    #region Login Tests

    [Fact]
    public async Task Login_FrontendAddedAndRemovedAtRunTime_FollowsTheConfiguration()
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
        var reloadable = new BffReloadableConfiguration();
        await using var host = await BffTestHost.StartAsync(
            factory,
            keySet,
            valueSet,
            indexSet,
            reloadableConfiguration: reloadable
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
        const string adminLoginPath = "/admin/bff/login?returnUrl=%2Fadmin";
        const string adminUserPath = "/admin/bff/user";

        using (var absent = await host.SendAsync(HttpMethod.Get, adminLoginPath))
        {
            Assert.Equal(HttpStatusCode.NotFound, absent.StatusCode);
        }

        reloadable.Replace(CreateAdminConfiguration(AdminClientId));
        await LoginAsync(browser, providerSession, host, adminLoginPath);
        var adminUserStatus = await GetStatusAsync(host, adminUserPath, csrf);
        Assert.Equal(HttpStatusCode.OK, adminUserStatus);

        reloadable.Replace(new Dictionary<string, string?>());
        var removedAdminUserStatus = await GetStatusAsync(host, adminUserPath, csrf);
        Assert.Equal(HttpStatusCode.NotFound, removedAdminUserStatus);

        reloadable.Replace(CreateAdminConfiguration(AdminOtherClientId));
        var otherClientAdminUserStatus = await GetStatusAsync(host, adminUserPath, csrf);
        Assert.Equal(HttpStatusCode.Unauthorized, otherClientAdminUserStatus);
    }

    #endregion

    #region Scaffolding

    private static Dictionary<string, string?> CreateAdminConfiguration(string clientId) =>
        new()
        {
            [$"{AdminPrefix}:Selection:Rule"] = "path",
            [$"{AdminPrefix}:Selection:Values:0"] = "/admin",
            [$"{AdminPrefix}:Authority"] = HostApplicationFactory.TestServerIssuer,
            [$"{AdminPrefix}:PublicOrigin"] = "https://bff.example",
            [$"{AdminPrefix}:ClientId"] = clientId,
            [$"{AdminPrefix}:ClientAuthentication:Method"] = OpenIdProtocol
                .ClientAuthenticationMethods
                .ClientSecretBasic,
            [$"{AdminPrefix}:ClientAuthentication:KeyRing"] = BffTestHost.KeyRing,
            [$"{AdminPrefix}:ReturnUrls:0"] = "/admin",
            [$"{AdminPrefix}:PathPrefix"] = "/admin/bff",
        };

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
