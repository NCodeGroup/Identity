// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Web;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using NCode.Identity.OpenId.Accounts;
using NCode.Identity.OpenId.Accounts.Stores;
using NCode.Identity.OpenId.Integration.Tests.Infrastructure;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.Persistence.Stores;
using Xunit;

namespace NCode.Identity.OpenId.Integration.Tests.Endpoints;

// An administrator's actions on an end user end its sessions, and every grant checks the subject (ADR-1224, ADR-1225, ADR-1226).
public class LocalAccountLifecycleTests
{
    private const string UserName = "lifecycle-user";
    private const string Password = "lifecycle-password";
    private const string NewPassword = "lifecycle-password-changed";
    private const string RedirectUri = "https://client.example/callback";
    private const string TenantApiPath = $"/api/tenants/{HostApplicationFactory.TenantId}";

    #region Scaffolding

    private static HttpClient CreateClient(HostApplicationFactory factory) =>
        factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                HandleCookies = false,
            }
        );

    private static async ValueTask<string> GetCodeAsync(
        HttpClient client,
        string sessionCookie,
        string clientId,
        CancellationToken cancellationToken
    )
    {
        var query =
            $"?client_id={Uri.EscapeDataString(clientId)}"
            + $"&redirect_uri={Uri.EscapeDataString(RedirectUri)}"
            + "&response_type=code"
            + "&scope="
            + Uri.EscapeDataString("openid offline_access")
            + "&state=abc123"
            + "&prompt=none"
            + TestPkce.QueryParameters;

        using var request = new HttpRequestMessage(HttpMethod.Get, "/oauth2/authorize" + query);
        request.Headers.Add("Cookie", $"{InteractiveLogin.SessionCookieName}={sessionCookie}");
        using var response = await client.SendAsync(request, cancellationToken);

        var location = response.Headers.Location;
        Assert.NotNull(location);
        var parameters = HttpUtility.ParseQueryString(location.Query);

        var code = parameters["code"];
        Assert.True(!string.IsNullOrEmpty(code), $"error='{parameters["error"]}'");
        return code;
    }

    private static async ValueTask BlockInStoreAsync(
        HostApplicationFactory factory,
        string principalId,
        CancellationToken cancellationToken
    )
    {
        using var scope = factory.Services.CreateScope();
        var storeManagerFactory = scope.ServiceProvider.GetRequiredService<IStoreManagerFactory>();
        await using var storeManager = await storeManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<IFederatedPrincipalStore>();
        var principal = await store.GetOrDefaultAsync(principalId, cancellationToken);
        Assert.NotNull(principal);
        principal.IsBlocked = true;
        await store.UpdateAsync(principal, cancellationToken);
        await storeManager.SaveChangesAsync(cancellationToken);
    }

    // Sends an interactive authorization request, with the session cookie when one is given.
    private static async ValueTask<HttpResponseMessage> SendAuthorizeAsync(
        HttpClient client,
        string? sessionCookie,
        string clientId,
        CancellationToken cancellationToken
    )
    {
        var query =
            $"?client_id={Uri.EscapeDataString(clientId)}"
            + $"&redirect_uri={Uri.EscapeDataString(RedirectUri)}"
            + "&response_type=code"
            + "&scope="
            + Uri.EscapeDataString("openid offline_access")
            + "&state=abc123"
            + TestPkce.QueryParameters;

        using var request = new HttpRequestMessage(HttpMethod.Get, "/oauth2/authorize" + query);
        if (sessionCookie is not null)
        {
            request.Headers.Add("Cookie", $"{InteractiveLogin.SessionCookieName}={sessionCookie}");
        }

        return await client.SendAsync(request, cancellationToken);
    }

    private static async ValueTask<(HttpStatusCode StatusCode, JsonElement Body)> PostTokenAsync(
        HttpClient client,
        Dictionary<string, string> form,
        CancellationToken cancellationToken
    )
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/oauth2/token")
        {
            Content = new FormUrlEncodedContent(form),
        };

        using var response = await client.SendAsync(request, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        var body = JsonElement.Parse(responseBody);
        return (response.StatusCode, body);
    }

    private static ValueTask<(HttpStatusCode StatusCode, JsonElement Body)> RefreshAsync(
        HttpClient client,
        string clientId,
        string refreshToken,
        CancellationToken cancellationToken
    ) =>
        PostTokenAsync(
            client,
            new Dictionary<string, string>
            {
                ["grant_type"] = "refresh_token",
                ["client_id"] = clientId,
                ["refresh_token"] = refreshToken,
            },
            cancellationToken
        );

    // Signs in, authorizes and redeems the code for the refresh token of the new session.
    private static async ValueTask<string> IssueRefreshTokenAsync(
        HttpClient client,
        string clientId,
        CancellationToken cancellationToken
    )
    {
        var sessionCookie = await InteractiveLogin.SignInAsync(
            client,
            UserName,
            Password,
            cancellationToken
        );

        var code = await GetCodeAsync(client, sessionCookie, clientId, cancellationToken);
        var (statusCode, body) = await PostTokenAsync(
            client,
            new Dictionary<string, string>
            {
                ["grant_type"] = "authorization_code",
                ["client_id"] = clientId,
                ["code"] = code,
                ["redirect_uri"] = RedirectUri,
                ["code_verifier"] = TestPkce.CodeVerifier,
            },
            cancellationToken
        );
        Assert.True(statusCode == HttpStatusCode.OK, body.ToString());

        var refreshToken = body.GetProperty("refresh_token").GetString();
        Assert.False(string.IsNullOrEmpty(refreshToken), body.ToString());
        return refreshToken;
    }

    private static void AssertRefused(HttpStatusCode statusCode, JsonElement body, string error)
    {
        Assert.Equal(HttpStatusCode.BadRequest, statusCode);
        Assert.Equal(error, body.GetProperty("error").GetString());
    }

    private static async ValueTask DisableInStoreAsync(
        HostApplicationFactory factory,
        string localAccountId,
        CancellationToken cancellationToken
    )
    {
        using var scope = factory.Services.CreateScope();
        var storeManagerFactory = scope.ServiceProvider.GetRequiredService<IStoreManagerFactory>();
        await using var storeManager = await storeManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<ILocalAccountStore>();
        var account = await store.GetByIdOrDefaultAsync(localAccountId, cancellationToken);
        Assert.NotNull(account);
        account.IsEnabled = false;
        await store.UpdateAsync(account, cancellationToken);
        await storeManager.SaveChangesAsync(cancellationToken);
    }

    private static async ValueTask<string> GetPrincipalIdAsync(
        HostApplicationFactory factory,
        string localAccountId,
        CancellationToken cancellationToken
    )
    {
        using var scope = factory.Services.CreateScope();
        var storeManagerFactory = scope.ServiceProvider.GetRequiredService<IStoreManagerFactory>();
        await using var storeManager = await storeManagerFactory.CreateAsync(cancellationToken);
        var identity = await storeManager
            .GetStore<IFederatedIdentityStore>()
            .GetByIssuerSubjectAsync(
                AccountConstants.SelfIssuer,
                localAccountId,
                cancellationToken
            );
        Assert.NotNull(identity);
        return identity.PrincipalId;
    }

    private static async ValueTask<(
        string DeviceCode,
        string UserCode
    )> StartDeviceAuthorizationAsync(
        HttpClient client,
        string clientId,
        CancellationToken cancellationToken
    )
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/oauth2/device_authorization")
        {
            Content = new FormUrlEncodedContent(
                new Dictionary<string, string>
                {
                    ["client_id"] = clientId,
                    ["scope"] = "openid offline_access",
                }
            ),
        };

        using var response = await client.SendAsync(request, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        Assert.True(response.StatusCode == HttpStatusCode.OK, responseBody);

        var jsonElement = JsonElement.Parse(responseBody);
        var deviceCode = jsonElement.GetProperty("device_code").GetString();
        var userCode = jsonElement.GetProperty("user_code").GetString();
        Assert.False(string.IsNullOrEmpty(deviceCode), responseBody);
        Assert.False(string.IsNullOrEmpty(userCode), responseBody);
        return (deviceCode, userCode);
    }

    private static async ValueTask<HttpStatusCode> PostDeviceVerificationAsync(
        HttpClient client,
        string sessionCookie,
        string userCode,
        CancellationToken cancellationToken
    )
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/oauth2/device")
        {
            Content = new FormUrlEncodedContent(
                new Dictionary<string, string> { ["user_code"] = userCode, ["action"] = "approve" }
            ),
        };
        request.Headers.Add("Cookie", $"{InteractiveLogin.SessionCookieName}={sessionCookie}");

        using var response = await client.SendAsync(request, cancellationToken);
        return response.StatusCode;
    }

    #endregion

    #region Lifecycle Tests

    [Fact]
    public async Task Disable_EndsTheAccountsSessionsAndRevokesItsRefreshToken()
    {
        const string clientId = "it-lifecycle-disable";
        using var factory = new BootstrapAdminApplicationFactory();
        var localAccountId = await factory.SeedLocalAccountAsync(
            UserName,
            Password,
            CancellationToken.None
        );
        await factory.SeedPublicClientAsync(clientId, [RedirectUri], CancellationToken.None);
        var client = CreateClient(factory);
        var refreshToken = await IssueRefreshTokenAsync(client, clientId, CancellationToken.None);
        using var administrator = await factory.CreateAdministratorClientAsync(
            CancellationToken.None
        );

        using var response = await administrator.PostAsync(
            $"{TenantApiPath}/local-accounts/{localAccountId}/disable",
            content: null
        );
        var responseBody = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.NoContent, responseBody);

        var (statusCode, body) = await RefreshAsync(
            client,
            clientId,
            refreshToken,
            CancellationToken.None
        );
        AssertRefused(statusCode, body, "invalid_grant");
    }

    [Fact]
    public async Task ResetPassword_EndsTheAccountsSessionsAndRevokesItsRefreshToken()
    {
        const string clientId = "it-lifecycle-reset";
        using var factory = new BootstrapAdminApplicationFactory();
        var localAccountId = await factory.SeedLocalAccountAsync(
            UserName,
            Password,
            CancellationToken.None
        );
        await factory.SeedPublicClientAsync(clientId, [RedirectUri], CancellationToken.None);
        var client = CreateClient(factory);
        var refreshToken = await IssueRefreshTokenAsync(client, clientId, CancellationToken.None);
        using var administrator = await factory.CreateAdministratorClientAsync(
            CancellationToken.None
        );

        using var response = await administrator.PostAsync(
            $"{TenantApiPath}/local-accounts/{localAccountId}/password",
            JsonContent.Create(new { password = "lifecycle-password-reset" })
        );
        var responseBody = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.NoContent, responseBody);

        var (statusCode, body) = await RefreshAsync(
            client,
            clientId,
            refreshToken,
            CancellationToken.None
        );
        AssertRefused(statusCode, body, "invalid_grant");
    }

    [Fact]
    public async Task Delete_EndsTheAccountsSessionsAndErasesItsRefreshToken()
    {
        const string clientId = "it-lifecycle-delete";
        using var factory = new BootstrapAdminApplicationFactory();
        var localAccountId = await factory.SeedLocalAccountAsync(
            UserName,
            Password,
            CancellationToken.None
        );
        await factory.SeedPublicClientAsync(clientId, [RedirectUri], CancellationToken.None);
        var client = CreateClient(factory);
        var refreshToken = await IssueRefreshTokenAsync(client, clientId, CancellationToken.None);
        using var administrator = await factory.CreateAdministratorClientAsync(
            CancellationToken.None
        );

        using var response = await administrator.DeleteAsync(
            $"{TenantApiPath}/local-accounts/{localAccountId}"
        );
        var responseBody = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.NoContent, responseBody);

        var (statusCode, body) = await RefreshAsync(
            client,
            clientId,
            refreshToken,
            CancellationToken.None
        );
        AssertRefused(statusCode, body, "invalid_grant");
    }

    [Fact]
    public async Task Refresh_WhenTheAccountWasDisabledInTheStore_IsRefused()
    {
        const string clientId = "it-lifecycle-store";
        using var factory = new HostApplicationFactory();
        var localAccountId = await factory.SeedLocalAccountAsync(
            UserName,
            Password,
            CancellationToken.None
        );
        await factory.SeedPublicClientAsync(clientId, [RedirectUri], CancellationToken.None);
        var client = CreateClient(factory);
        var refreshToken = await IssueRefreshTokenAsync(client, clientId, CancellationToken.None);

        // The account is disabled behind the server's back, so its sessions and grants are still live.
        await DisableInStoreAsync(factory, localAccountId, CancellationToken.None);

        var (statusCode, body) = await RefreshAsync(
            client,
            clientId,
            refreshToken,
            CancellationToken.None
        );
        AssertRefused(statusCode, body, "access_denied");
    }

    [Fact]
    public async Task DeviceCode_WhenTheApprovingAccountIsEnabled_IssuesTokens()
    {
        const string clientId = "it-lifecycle-device-enabled";
        using var factory = new HostApplicationFactory();
        await factory.SeedLocalAccountAsync(UserName, Password, CancellationToken.None);
        await factory.SeedPublicClientAsync(clientId, [RedirectUri], CancellationToken.None);
        var client = CreateClient(factory);
        var (deviceCode, userCode) = await StartDeviceAuthorizationAsync(
            client,
            clientId,
            CancellationToken.None
        );
        var sessionCookie = await InteractiveLogin.SignInAsync(
            client,
            UserName,
            Password,
            CancellationToken.None
        );
        var approvalStatusCode = await PostDeviceVerificationAsync(
            client,
            sessionCookie,
            userCode,
            CancellationToken.None
        );
        Assert.Equal(HttpStatusCode.OK, approvalStatusCode);

        var (statusCode, body) = await PostTokenAsync(
            client,
            new Dictionary<string, string>
            {
                ["grant_type"] = OpenIdProtocol.GrantTypes.DeviceCode,
                ["device_code"] = deviceCode,
                ["client_id"] = clientId,
            },
            CancellationToken.None
        );
        Assert.True(statusCode == HttpStatusCode.OK, body.ToString());
        Assert.False(
            string.IsNullOrEmpty(body.GetProperty("access_token").GetString()),
            body.ToString()
        );
    }

    [Fact]
    public async Task DeviceCode_WhenAnotherClientPresentsTheCode_IsRefusedAsInvalidGrant()
    {
        const string ownerClientId = "it-lifecycle-device-owner";
        const string otherClientId = "it-lifecycle-device-other";
        using var factory = new HostApplicationFactory();
        await factory.SeedLocalAccountAsync(UserName, Password, CancellationToken.None);
        await factory.SeedPublicClientAsync(ownerClientId, [RedirectUri], CancellationToken.None);
        await factory.SeedPublicClientAsync(otherClientId, [RedirectUri], CancellationToken.None);
        var client = CreateClient(factory);
        var (deviceCode, userCode) = await StartDeviceAuthorizationAsync(
            client,
            ownerClientId,
            CancellationToken.None
        );
        var sessionCookie = await InteractiveLogin.SignInAsync(
            client,
            UserName,
            Password,
            CancellationToken.None
        );
        var approvalStatusCode = await PostDeviceVerificationAsync(
            client,
            sessionCookie,
            userCode,
            CancellationToken.None
        );
        Assert.Equal(HttpStatusCode.OK, approvalStatusCode);

        var (statusCode, body) = await PostTokenAsync(
            client,
            new Dictionary<string, string>
            {
                ["grant_type"] = OpenIdProtocol.GrantTypes.DeviceCode,
                ["device_code"] = deviceCode,
                ["client_id"] = otherClientId,
            },
            CancellationToken.None
        );
        AssertRefused(statusCode, body, "invalid_grant");
    }

    [Fact]
    public async Task DeviceCode_WhenTheApprovingAccountWasDisabledInTheStore_IsRefused()
    {
        const string clientId = "it-lifecycle-device";
        using var factory = new HostApplicationFactory();
        var localAccountId = await factory.SeedLocalAccountAsync(
            UserName,
            Password,
            CancellationToken.None
        );
        await factory.SeedPublicClientAsync(clientId, [RedirectUri], CancellationToken.None);
        var client = CreateClient(factory);
        var (deviceCode, userCode) = await StartDeviceAuthorizationAsync(
            client,
            clientId,
            CancellationToken.None
        );
        var sessionCookie = await InteractiveLogin.SignInAsync(
            client,
            UserName,
            Password,
            CancellationToken.None
        );
        var approvalStatusCode = await PostDeviceVerificationAsync(
            client,
            sessionCookie,
            userCode,
            CancellationToken.None
        );
        Assert.Equal(HttpStatusCode.OK, approvalStatusCode);

        // The account is disabled behind the server's back, so its sessions and grants are still live.
        await DisableInStoreAsync(factory, localAccountId, CancellationToken.None);

        var (statusCode, body) = await PostTokenAsync(
            client,
            new Dictionary<string, string>
            {
                ["grant_type"] = OpenIdProtocol.GrantTypes.DeviceCode,
                ["device_code"] = deviceCode,
                ["client_id"] = clientId,
            },
            CancellationToken.None
        );
        AssertRefused(statusCode, body, "access_denied");
    }

    [Fact]
    public async Task DeviceVerification_WhenTheSessionHasEnded_ChallengesForANewLogin()
    {
        const string clientId = "it-lifecycle-device-session";
        using var factory = new BootstrapAdminApplicationFactory();
        var localAccountId = await factory.SeedLocalAccountAsync(
            UserName,
            Password,
            CancellationToken.None
        );
        await factory.SeedPublicClientAsync(clientId, [RedirectUri], CancellationToken.None);
        var client = CreateClient(factory);
        var (_, userCode) = await StartDeviceAuthorizationAsync(
            client,
            clientId,
            CancellationToken.None
        );
        var sessionCookie = await InteractiveLogin.SignInAsync(
            client,
            UserName,
            Password,
            CancellationToken.None
        );
        using var administrator = await factory.CreateAdministratorClientAsync(
            CancellationToken.None
        );

        using var response = await administrator.PostAsync(
            $"{TenantApiPath}/local-accounts/{localAccountId}/disable",
            content: null
        );
        var responseBody = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.NoContent, responseBody);

        var statusCode = await PostDeviceVerificationAsync(
            client,
            sessionCookie,
            userCode,
            CancellationToken.None
        );
        Assert.Equal(HttpStatusCode.Redirect, statusCode);
    }

    [Fact]
    public async Task Block_EndsThePrincipalsSessionsAndRevokesItsRefreshToken()
    {
        const string clientId = "it-lifecycle-block";
        using var factory = new BootstrapAdminApplicationFactory();
        var localAccountId = await factory.SeedLocalAccountAsync(
            UserName,
            Password,
            CancellationToken.None
        );
        await factory.SeedPublicClientAsync(clientId, [RedirectUri], CancellationToken.None);
        var client = CreateClient(factory);
        var refreshToken = await IssueRefreshTokenAsync(client, clientId, CancellationToken.None);
        var principalId = await GetPrincipalIdAsync(
            factory,
            localAccountId,
            CancellationToken.None
        );
        using var administrator = await factory.CreateAdministratorClientAsync(
            CancellationToken.None
        );

        using var response = await administrator.PostAsync(
            $"{TenantApiPath}/principals/{principalId}/block",
            content: null
        );
        var responseBody = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.NoContent, responseBody);

        using var getResponse = await administrator.GetAsync(
            $"{TenantApiPath}/principals/{principalId}"
        );
        var getBody = await getResponse.Content.ReadAsStringAsync();
        Assert.True(getResponse.StatusCode == HttpStatusCode.OK, getBody);
        var jsonElement = JsonElement.Parse(getBody);
        Assert.True(jsonElement.GetProperty("isBlocked").GetBoolean(), getBody);

        var (statusCode, body) = await RefreshAsync(
            client,
            clientId,
            refreshToken,
            CancellationToken.None
        );
        AssertRefused(statusCode, body, "invalid_grant");
    }

    [Fact]
    public async Task Refresh_WhenThePrincipalWasBlockedInTheStore_IsRefused()
    {
        const string clientId = "it-lifecycle-principal-store";
        using var factory = new HostApplicationFactory();
        var localAccountId = await factory.SeedLocalAccountAsync(
            UserName,
            Password,
            CancellationToken.None
        );
        await factory.SeedPublicClientAsync(clientId, [RedirectUri], CancellationToken.None);
        var client = CreateClient(factory);
        var refreshToken = await IssueRefreshTokenAsync(client, clientId, CancellationToken.None);
        var principalId = await GetPrincipalIdAsync(
            factory,
            localAccountId,
            CancellationToken.None
        );

        // The principal is blocked behind the server's back, so its sessions and grants are still live.
        await BlockInStoreAsync(factory, principalId, CancellationToken.None);

        var (statusCode, body) = await RefreshAsync(
            client,
            clientId,
            refreshToken,
            CancellationToken.None
        );
        AssertRefused(statusCode, body, "access_denied");
    }

    [Fact]
    public async Task Create_WhenThePasswordMeetsThePolicy_CreatesTheAccount()
    {
        const string userName = "policy-user";
        const string password = "policy-user-password";
        using var factory = new BootstrapAdminApplicationFactory();
        using var administrator = await factory.CreateAdministratorClientAsync(
            CancellationToken.None
        );

        using var response = await administrator.PostAsync(
            $"{TenantApiPath}/local-accounts",
            JsonContent.Create(new { userName, password })
        );

        var responseBody = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.Created, responseBody);
        var client = CreateClient(factory);
        var signInCookie = await InteractiveLogin.SignInAsync(
            client,
            userName,
            password,
            CancellationToken.None
        );
        Assert.NotNull(signInCookie);
    }

    [Fact]
    public async Task Create_WhenThePolicyRefusesThePassword_AnswersBadRequest()
    {
        using var factory = new BootstrapAdminApplicationFactory();
        using var administrator = await factory.CreateAdministratorClientAsync(
            CancellationToken.None
        );

        using var response = await administrator.PostAsync(
            $"{TenantApiPath}/local-accounts",
            JsonContent.Create(new { userName = "policy-user", password = "passwordpassword" })
        );

        var responseBody = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.BadRequest, responseBody);
        Assert.Contains("commonly used", responseBody);
    }

    [Fact]
    public async Task ResetPassword_WhenThePolicyRefusesThePassword_KeepsThePassword()
    {
        using var factory = new BootstrapAdminApplicationFactory();
        var localAccountId = await factory.SeedLocalAccountAsync(
            UserName,
            Password,
            CancellationToken.None
        );
        using var administrator = await factory.CreateAdministratorClientAsync(
            CancellationToken.None
        );

        using var response = await administrator.PostAsync(
            $"{TenantApiPath}/local-accounts/{localAccountId}/password",
            JsonContent.Create(new { password = "too-short-pw" })
        );

        var responseBody = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.BadRequest, responseBody);
        Assert.Contains("at least 15", responseBody);
        var client = CreateClient(factory);
        var signInCookie = await InteractiveLogin.SignInAsync(
            client,
            UserName,
            Password,
            CancellationToken.None
        );
        Assert.NotNull(signInCookie);
    }

    [Fact]
    public async Task ChangePassword_WhenThePolicyRefusesTheNewPassword_ChangesNothing()
    {
        const string refusedPassword = "short-pass";
        using var factory = new HostApplicationFactory();
        await factory.SeedLocalAccountAsync(UserName, Password, CancellationToken.None);
        var client = CreateClient(factory);
        var sessionCookie = await InteractiveLogin.SignInAsync(
            client,
            UserName,
            Password,
            CancellationToken.None
        );

        using var changeResponse = await InteractiveLogin.PostFormAsync(
            client,
            "/account/password",
            "/account/password",
            sessionCookie,
            new Dictionary<string, string>
            {
                ["current_password"] = Password,
                ["new_password"] = refusedPassword,
                ["confirm_password"] = refusedPassword,
            },
            CancellationToken.None
        );

        Assert.Equal(HttpStatusCode.Found, changeResponse.StatusCode);
        var location = changeResponse.Headers.Location;
        Assert.NotNull(location);
        Assert.Contains("error=password_too_short", location.OriginalString);

        var signInCookie = await InteractiveLogin.SignInAsync(
            client,
            UserName,
            Password,
            CancellationToken.None
        );
        Assert.NotNull(signInCookie);
    }

    [Fact]
    public async Task ResetPassword_ClearsTheFailedPasswordAttempts()
    {
        const string wrongPassword = "lifecycle-wrong-password";
        const string resetPassword = "lifecycle-password-reset";
        using var factory = new BootstrapAdminApplicationFactory();

        // The tenant reads its settings at its first request, so they are seeded before anything is sent.
        await factory.SeedTenantSettingsAsync(
            new Dictionary<string, object> { [OpenIdSettingNames.PasswordFailedAttemptsLimit] = 2 },
            CancellationToken.None
        );
        var localAccountId = await factory.SeedLocalAccountAsync(
            UserName,
            Password,
            CancellationToken.None
        );
        var client = CreateClient(factory);

        using var firstWrongResponse = await InteractiveLogin.SubmitLoginAsync(
            client,
            UserName,
            wrongPassword,
            "/foo",
            CancellationToken.None
        );
        Assert.Null(InteractiveLogin.ReadSessionCookie(firstWrongResponse));

        using var secondWrongResponse = await InteractiveLogin.SubmitLoginAsync(
            client,
            UserName,
            wrongPassword,
            "/foo",
            CancellationToken.None
        );
        Assert.Null(InteractiveLogin.ReadSessionCookie(secondWrongResponse));

        // The limit holds, so even the right password is refused.
        using var limitedResponse = await InteractiveLogin.SubmitLoginAsync(
            client,
            UserName,
            Password,
            "/foo",
            CancellationToken.None
        );
        Assert.Null(InteractiveLogin.ReadSessionCookie(limitedResponse));

        using var administrator = await factory.CreateAdministratorClientAsync(
            CancellationToken.None
        );
        using var resetResponse = await administrator.PostAsync(
            $"{TenantApiPath}/local-accounts/{localAccountId}/password",
            JsonContent.Create(new { password = resetPassword })
        );
        var resetBody = await resetResponse.Content.ReadAsStringAsync();
        Assert.True(resetResponse.StatusCode == HttpStatusCode.NoContent, resetBody);

        var signInCookie = await InteractiveLogin.SignInAsync(
            client,
            UserName,
            resetPassword,
            CancellationToken.None
        );
        Assert.NotNull(signInCookie);
    }

    #endregion

    #region Interactive Surface Tests

    [Fact]
    public async Task AccountMe_WhenThePrincipalWasBlocked_ReturnsUnauthorized()
    {
        using var factory = new BootstrapAdminApplicationFactory();
        var localAccountId = await factory.SeedLocalAccountAsync(
            UserName,
            Password,
            CancellationToken.None
        );
        var client = CreateClient(factory);
        var sessionCookie = await InteractiveLogin.SignInAsync(
            client,
            UserName,
            Password,
            CancellationToken.None
        );
        var principalId = await GetPrincipalIdAsync(
            factory,
            localAccountId,
            CancellationToken.None
        );
        using var administrator = await factory.CreateAdministratorClientAsync(
            CancellationToken.None
        );

        using var blockResponse = await administrator.PostAsync(
            $"{TenantApiPath}/principals/{principalId}/block",
            content: null
        );
        var blockBody = await blockResponse.Content.ReadAsStringAsync();
        Assert.True(blockResponse.StatusCode == HttpStatusCode.NoContent, blockBody);

        using var request = new HttpRequestMessage(HttpMethod.Get, "/account/me");
        request.Headers.Add("Cookie", $"{InteractiveLogin.SessionCookieName}={sessionCookie}");
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task SignIn_WhenThePrincipalIsBlocked_ShowsTheRefusalOnTheLoginPage()
    {
        using var factory = new HostApplicationFactory();
        var localAccountId = await factory.SeedLocalAccountAsync(
            UserName,
            Password,
            CancellationToken.None
        );
        var client = CreateClient(factory);
        await InteractiveLogin.SignInAsync(client, UserName, Password, CancellationToken.None);
        var principalId = await GetPrincipalIdAsync(
            factory,
            localAccountId,
            CancellationToken.None
        );
        await BlockInStoreAsync(factory, principalId, CancellationToken.None);

        using var response = await InteractiveLogin.SubmitLoginAsync(
            client,
            UserName,
            Password,
            "/foo",
            CancellationToken.None
        );

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        var location = response.Headers.Location;
        Assert.NotNull(location);
        Assert.Contains("error=access_denied", location.OriginalString);
        Assert.Null(InteractiveLogin.ReadSessionCookie(response));
    }

    [Fact]
    public async Task SignIn_WhenThePrincipalIsBlockedDuringAnAuthorizationRequest_AnswersTheClientWithAccessDenied()
    {
        const string clientId = "it-lifecycle-signin-blocked";
        using var factory = new HostApplicationFactory();
        var localAccountId = await factory.SeedLocalAccountAsync(
            UserName,
            Password,
            CancellationToken.None
        );
        await factory.SeedPublicClientAsync(clientId, [RedirectUri], CancellationToken.None);
        var client = CreateClient(factory);
        await InteractiveLogin.SignInAsync(client, UserName, Password, CancellationToken.None);
        var principalId = await GetPrincipalIdAsync(
            factory,
            localAccountId,
            CancellationToken.None
        );
        await BlockInStoreAsync(factory, principalId, CancellationToken.None);

        using var authorizeResponse = await SendAuthorizeAsync(
            client,
            null,
            clientId,
            CancellationToken.None
        );
        Assert.Equal(HttpStatusCode.Found, authorizeResponse.StatusCode);
        Assert.NotNull(client.BaseAddress);
        Assert.NotNull(authorizeResponse.Headers.Location);
        var loginLocation = new Uri(client.BaseAddress, authorizeResponse.Headers.Location);
        var returnUrl = HttpUtility.ParseQueryString(loginLocation.Query)["ReturnUrl"];
        Assert.False(string.IsNullOrEmpty(returnUrl), loginLocation.OriginalString);

        using var response = await InteractiveLogin.SubmitLoginAsync(
            client,
            UserName,
            Password,
            returnUrl,
            CancellationToken.None
        );

        Assert.Equal(HttpStatusCode.SeeOther, response.StatusCode);
        var location = response.Headers.Location;
        Assert.NotNull(location);
        Assert.StartsWith(RedirectUri, location.OriginalString);
        var parameters = HttpUtility.ParseQueryString(location.Query);
        Assert.Equal("access_denied", parameters["error"]);
        Assert.Equal("abc123", parameters["state"]);
    }

    [Fact]
    public async Task SignIn_WhenTheAccountIsDisabled_ShowsTheRefusalOnTheLoginPage()
    {
        using var factory = new HostApplicationFactory();
        var localAccountId = await factory.SeedLocalAccountAsync(
            UserName,
            Password,
            CancellationToken.None
        );
        var client = CreateClient(factory);
        await InteractiveLogin.SignInAsync(client, UserName, Password, CancellationToken.None);
        await DisableInStoreAsync(factory, localAccountId, CancellationToken.None);

        using var response = await InteractiveLogin.SubmitLoginAsync(
            client,
            UserName,
            Password,
            "/foo",
            CancellationToken.None
        );

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        var location = response.Headers.Location;
        Assert.NotNull(location);
        Assert.Contains("error=access_denied", location.OriginalString);
        Assert.Null(InteractiveLogin.ReadSessionCookie(response));
    }

    [Fact]
    public async Task Authorize_WhenTheSignedInPrincipalIsBlocked_AnswersTheClientWithAccessDenied()
    {
        const string clientId = "it-lifecycle-authorize-blocked";
        using var factory = new HostApplicationFactory();
        var localAccountId = await factory.SeedLocalAccountAsync(
            UserName,
            Password,
            CancellationToken.None
        );
        await factory.SeedPublicClientAsync(clientId, [RedirectUri], CancellationToken.None);
        var client = CreateClient(factory);
        var sessionCookie = await InteractiveLogin.SignInAsync(
            client,
            UserName,
            Password,
            CancellationToken.None
        );
        var principalId = await GetPrincipalIdAsync(
            factory,
            localAccountId,
            CancellationToken.None
        );

        // The principal is blocked behind the server's back, so its session is still active.
        await BlockInStoreAsync(factory, principalId, CancellationToken.None);

        using var response = await SendAuthorizeAsync(
            client,
            sessionCookie,
            clientId,
            CancellationToken.None
        );

        Assert.Equal(HttpStatusCode.SeeOther, response.StatusCode);
        var location = response.Headers.Location;
        Assert.NotNull(location);
        Assert.StartsWith(RedirectUri, location.OriginalString);
        var parameters = HttpUtility.ParseQueryString(location.Query);
        Assert.Equal("access_denied", parameters["error"]);
        Assert.Equal("abc123", parameters["state"]);
    }

    [Fact]
    public async Task ChangePassword_KeepsTheCurrentSessionAndEndsTheOthers()
    {
        const string clientId = "it-lifecycle-change-password";
        using var factory = new HostApplicationFactory();
        await factory.SeedLocalAccountAsync(UserName, Password, CancellationToken.None);
        await factory.SeedPublicClientAsync(clientId, [RedirectUri], CancellationToken.None);
        var client = CreateClient(factory);
        var otherRefreshToken = await IssueRefreshTokenAsync(
            client,
            clientId,
            CancellationToken.None
        );
        var sessionCookie = await InteractiveLogin.SignInAsync(
            client,
            UserName,
            Password,
            CancellationToken.None
        );

        using var changeResponse = await InteractiveLogin.PostFormAsync(
            client,
            "/account/password",
            "/account/password",
            sessionCookie,
            new Dictionary<string, string>
            {
                ["current_password"] = Password,
                ["new_password"] = NewPassword,
                ["confirm_password"] = NewPassword,
            },
            CancellationToken.None
        );

        Assert.Equal(HttpStatusCode.Found, changeResponse.StatusCode);
        var location = changeResponse.Headers.Location;
        Assert.NotNull(location);
        Assert.EndsWith("status=changed", location.OriginalString);

        var (statusCode, body) = await RefreshAsync(
            client,
            clientId,
            otherRefreshToken,
            CancellationToken.None
        );
        AssertRefused(statusCode, body, "invalid_grant");

        using var meRequest = new HttpRequestMessage(HttpMethod.Get, "/account/me");
        meRequest.Headers.Add("Cookie", $"{InteractiveLogin.SessionCookieName}={sessionCookie}");
        using var meResponse = await client.SendAsync(meRequest);
        Assert.Equal(HttpStatusCode.OK, meResponse.StatusCode);

        var signInCookie = await InteractiveLogin.SignInAsync(
            client,
            UserName,
            NewPassword,
            CancellationToken.None
        );
        Assert.NotNull(signInCookie);
    }

    [Fact]
    public async Task ChangePassword_WhenTheCurrentPasswordIsWrong_ChangesNothing()
    {
        using var factory = new HostApplicationFactory();
        await factory.SeedLocalAccountAsync(UserName, Password, CancellationToken.None);
        var client = CreateClient(factory);
        var sessionCookie = await InteractiveLogin.SignInAsync(
            client,
            UserName,
            Password,
            CancellationToken.None
        );

        using var changeResponse = await InteractiveLogin.PostFormAsync(
            client,
            "/account/password",
            "/account/password",
            sessionCookie,
            new Dictionary<string, string>
            {
                ["current_password"] = "wrong",
                ["new_password"] = NewPassword,
                ["confirm_password"] = NewPassword,
            },
            CancellationToken.None
        );

        Assert.Equal(HttpStatusCode.Found, changeResponse.StatusCode);
        var location = changeResponse.Headers.Location;
        Assert.NotNull(location);
        Assert.Contains("error=invalid_current_password", location.OriginalString);

        var signInCookie = await InteractiveLogin.SignInAsync(
            client,
            UserName,
            Password,
            CancellationToken.None
        );
        Assert.NotNull(signInCookie);
    }

    [Fact]
    public async Task ChangePassword_RevokesTheGrantsIssuedUnderNoSession()
    {
        const string passwordClientId = "it-lifecycle-change-password-ropc";
        const string passwordClientSecret = "lifecycle-password-client-secret";
        using var factory = new HostApplicationFactory();
        await factory.SeedLocalAccountAsync(UserName, Password, CancellationToken.None);
        await factory.SeedConfidentialClientAsync(
            passwordClientId,
            passwordClientSecret,
            [OpenIdProtocol.GrantTypes.Password, OpenIdProtocol.GrantTypes.RefreshToken],
            CancellationToken.None
        );
        var client = CreateClient(factory);
        var (issueStatusCode, issueBody) = await PostTokenAsync(
            client,
            new Dictionary<string, string>
            {
                ["grant_type"] = OpenIdProtocol.GrantTypes.Password,
                ["client_id"] = passwordClientId,
                ["client_secret"] = passwordClientSecret,
                ["username"] = UserName,
                ["password"] = Password,
                ["scope"] = "openid offline_access",
            },
            CancellationToken.None
        );
        Assert.True(issueStatusCode == HttpStatusCode.OK, issueBody.ToString());
        var refreshToken = issueBody.GetProperty("refresh_token").GetString();
        Assert.False(string.IsNullOrEmpty(refreshToken), issueBody.ToString());
        var sessionCookie = await InteractiveLogin.SignInAsync(
            client,
            UserName,
            Password,
            CancellationToken.None
        );

        using var changeResponse = await InteractiveLogin.PostFormAsync(
            client,
            "/account/password",
            "/account/password",
            sessionCookie,
            new Dictionary<string, string>
            {
                ["current_password"] = Password,
                ["new_password"] = NewPassword,
                ["confirm_password"] = NewPassword,
            },
            CancellationToken.None
        );

        Assert.Equal(HttpStatusCode.Found, changeResponse.StatusCode);
        var location = changeResponse.Headers.Location;
        Assert.NotNull(location);
        Assert.EndsWith("status=changed", location.OriginalString);

        var (statusCode, body) = await PostTokenAsync(
            client,
            new Dictionary<string, string>
            {
                ["grant_type"] = OpenIdProtocol.GrantTypes.RefreshToken,
                ["client_id"] = passwordClientId,
                ["client_secret"] = passwordClientSecret,
                ["refresh_token"] = refreshToken,
            },
            CancellationToken.None
        );
        AssertRefused(statusCode, body, "invalid_grant");
    }

    [Fact]
    public async Task ChangePassword_WithoutTheAntiforgeryToken_ChangesNothing()
    {
        using var factory = new HostApplicationFactory();
        await factory.SeedLocalAccountAsync(UserName, Password, CancellationToken.None);
        var client = CreateClient(factory);
        var sessionCookie = await InteractiveLogin.SignInAsync(
            client,
            UserName,
            Password,
            CancellationToken.None
        );

        using var request = new HttpRequestMessage(HttpMethod.Post, "/account/password");
        request.Headers.Add("Cookie", $"{InteractiveLogin.SessionCookieName}={sessionCookie}");
        request.Content = new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["current_password"] = Password,
                ["new_password"] = NewPassword,
                ["confirm_password"] = NewPassword,
            }
        );
        using var response = await client.SendAsync(request);

        Assert.InRange((int)response.StatusCode, 400, 499);
        var signInCookie = await InteractiveLogin.SignInAsync(
            client,
            UserName,
            Password,
            CancellationToken.None
        );
        Assert.NotNull(signInCookie);
    }

    #endregion
}
