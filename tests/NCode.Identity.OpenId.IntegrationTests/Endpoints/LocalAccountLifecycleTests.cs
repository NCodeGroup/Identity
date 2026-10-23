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

using System.Buffers.Text;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Web;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NCode.Identity.OpenId.Accounts.Stores;
using NCode.Identity.OpenId.IntegrationTests.Infrastructure;
using NCode.Persistence.Stores;
using Xunit;

namespace NCode.Identity.OpenId.IntegrationTests.Endpoints;

// Deleting or disabling a local account ends its sessions, and a token request checks the account (ADR-0224).
public class LocalAccountLifecycleTests
{
    private const string UserName = "lifecycle-user";
    private const string Password = "lifecycle-password";
    private const string RedirectUri = "https://client.example/callback";
    private const string BootstrapClientId = "it-bootstrap-admin";
    private const string BootstrapClientSecret = "bootstrap-secret-value";
    private const string ManagementScope = "read:clients";
    private const string TenantApiPath = $"/api/tenants/{HostApplicationFactory.TenantId}";

    #region Scaffolding

    private static HttpClient CreateHttpClient(HostApplicationFactory factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    private static HttpClient CreateClient(HostApplicationFactory factory) =>
        factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                HandleCookies = false,
            }
        );

    private static async Task<HttpClient> CreateAdministratorClientAsync(
        HostApplicationFactory factory
    )
    {
        var client = CreateHttpClient(factory);
        var credentials = Encoding.UTF8.GetBytes($"{BootstrapClientId}:{BootstrapClientSecret}");
        using var tokenRequest = new HttpRequestMessage(HttpMethod.Post, "/oauth2/token")
        {
            Headers =
            {
                Authorization = new AuthenticationHeaderValue(
                    "Basic",
                    Convert.ToBase64String(credentials)
                ),
            },
            Content = new FormUrlEncodedContent(
                new Dictionary<string, string>
                {
                    ["grant_type"] = OpenIdConstants.GrantTypes.ClientCredentials,
                    ["scope"] = ManagementScope,
                }
            ),
        };

        using var response = await client.SendAsync(tokenRequest);
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, body);
        using var document = JsonDocument.Parse(body);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            document.RootElement.GetProperty("access_token").GetString()
        );
        return client;
    }

    private static async Task<string> GetCodeAsync(
        HttpClient client,
        string sessionCookie,
        string clientId
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
        using var response = await client.SendAsync(request);

        var location = response.Headers.Location;
        Assert.NotNull(location);
        var parameters = HttpUtility.ParseQueryString(location.Query);

        var code = parameters["code"];
        Assert.True(!string.IsNullOrEmpty(code), $"error='{parameters["error"]}'");
        return code;
    }

    private static async Task<(HttpStatusCode StatusCode, JsonElement Body)> PostTokenAsync(
        HttpClient client,
        Dictionary<string, string> form
    )
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/oauth2/token")
        {
            Content = new FormUrlEncodedContent(form),
        };

        using var response = await client.SendAsync(request);
        var body = JsonElement.Parse(await response.Content.ReadAsStringAsync());
        return (response.StatusCode, body);
    }

    private static Task<(HttpStatusCode StatusCode, JsonElement Body)> RefreshAsync(
        HttpClient client,
        string clientId,
        string refreshToken
    ) =>
        PostTokenAsync(
            client,
            new Dictionary<string, string>
            {
                ["grant_type"] = "refresh_token",
                ["client_id"] = clientId,
                ["refresh_token"] = refreshToken,
            }
        );

    // Signs in, authorizes and redeems the code for the refresh token of the new session.
    private static async Task<string> IssueRefreshTokenAsync(HttpClient client, string clientId)
    {
        var sessionCookie = await InteractiveLogin.SignInAsync(client, UserName, Password);

        var code = await GetCodeAsync(client, sessionCookie, clientId);
        var (statusCode, body) = await PostTokenAsync(
            client,
            new Dictionary<string, string>
            {
                ["grant_type"] = "authorization_code",
                ["client_id"] = clientId,
                ["code"] = code,
                ["redirect_uri"] = RedirectUri,
                ["code_verifier"] = TestPkce.CodeVerifier,
            }
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

    private sealed class BootstrapAdminHostApplicationFactory : HostApplicationFactory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);

            builder.ConfigureAppConfiguration(configurationBuilder =>
                configurationBuilder.AddInMemoryCollection(
                    new Dictionary<string, string?>
                    {
                        ["BootstrapAdmin:ClientId"] = BootstrapClientId,
                        ["BootstrapAdmin:ClientSecret"] = Base64Url.EncodeToString(
                            Encoding.UTF8.GetBytes(BootstrapClientSecret)
                        ),
                        ["BootstrapAdmin:TenantId"] = HostApplicationFactory.TenantId,
                    }
                )
            );
        }
    }

    #endregion

    #region Lifecycle Tests

    [Fact]
    public async Task Disable_EndsTheAccountsSessionsAndRevokesItsRefreshToken()
    {
        const string clientId = "it-lifecycle-disable";
        using var factory = new BootstrapAdminHostApplicationFactory();
        var localAccountId = await factory.SeedLocalAccountAsync(UserName, Password);
        await factory.SeedPublicClientAsync(clientId, RedirectUri);
        var client = CreateClient(factory);
        var refreshToken = await IssueRefreshTokenAsync(client, clientId);
        using var administrator = await CreateAdministratorClientAsync(factory);

        using var response = await administrator.PostAsync(
            $"{TenantApiPath}/local-accounts/{localAccountId}/disable",
            content: null
        );
        var responseBody = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.NoContent, responseBody);

        var (statusCode, body) = await RefreshAsync(client, clientId, refreshToken);
        AssertRefused(statusCode, body, "invalid_grant");
    }

    [Fact]
    public async Task Delete_EndsTheAccountsSessionsAndErasesItsRefreshToken()
    {
        const string clientId = "it-lifecycle-delete";
        using var factory = new BootstrapAdminHostApplicationFactory();
        var localAccountId = await factory.SeedLocalAccountAsync(UserName, Password);
        await factory.SeedPublicClientAsync(clientId, RedirectUri);
        var client = CreateClient(factory);
        var refreshToken = await IssueRefreshTokenAsync(client, clientId);
        using var administrator = await CreateAdministratorClientAsync(factory);

        using var response = await administrator.DeleteAsync(
            $"{TenantApiPath}/local-accounts/{localAccountId}"
        );
        var responseBody = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.NoContent, responseBody);

        var (statusCode, body) = await RefreshAsync(client, clientId, refreshToken);
        AssertRefused(statusCode, body, "invalid_grant");
    }

    [Fact]
    public async Task Refresh_WhenTheAccountWasDisabledInTheStore_IsRefused()
    {
        const string clientId = "it-lifecycle-store";
        using var factory = new HostApplicationFactory();
        var localAccountId = await factory.SeedLocalAccountAsync(UserName, Password);
        await factory.SeedPublicClientAsync(clientId, RedirectUri);
        var client = CreateClient(factory);
        var refreshToken = await IssueRefreshTokenAsync(client, clientId);

        // The account is disabled behind the server's back, so its sessions and grants are still live.
        using (var scope = factory.Services.CreateScope())
        {
            var storeManagerFactory =
                scope.ServiceProvider.GetRequiredService<IStoreManagerFactory>();
            await using var storeManager = await storeManagerFactory.CreateAsync(
                CancellationToken.None
            );
            var store = storeManager.GetStore<ILocalAccountStore>();
            var account = await store.GetByIdOrDefaultAsync(localAccountId, CancellationToken.None);
            Assert.NotNull(account);
            account.IsEnabled = false;
            await store.UpdateAsync(account, CancellationToken.None);
            await storeManager.SaveChangesAsync(CancellationToken.None);
        }

        var (statusCode, body) = await RefreshAsync(client, clientId, refreshToken);
        AssertRefused(statusCode, body, "access_denied");
    }

    #endregion
}
