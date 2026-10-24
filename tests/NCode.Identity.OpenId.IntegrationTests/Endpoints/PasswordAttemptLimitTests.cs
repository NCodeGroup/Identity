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
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using NCode.Identity.OpenId.IntegrationTests.Infrastructure;
using NCode.Identity.OpenId.Settings;
using Xunit;

namespace NCode.Identity.OpenId.IntegrationTests.Endpoints;

// The failed password attempts of an account are limited (ADR-0237).
public class PasswordAttemptLimitTests
{
    private const string ClientId = "it-attempt-limit-client";
    private const string ClientSecret = "attempt-limit-client-secret";
    private const string UserName = "attempt-limit-user";
    private const string Password = "attempt-limit-password";
    private const string WrongPassword = "attempt-limit-wrong-password";
    private const int FailedAttemptsLimit = 2;

    #region Scaffolding

    // The tenant reads its settings at its first request, so this is always the first call on a factory.
    private static Task SeedLimitAsync(HostApplicationFactory factory) =>
        factory.SeedTenantSettingsAsync(
            new Dictionary<string, object>
            {
                [OpenIdSettingNames.PasswordFailedAttemptsLimit] = FailedAttemptsLimit,
            }
        );

    private static HttpClient CreateClient(HostApplicationFactory factory) =>
        factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                HandleCookies = false,
            }
        );

    private static async Task<(HttpStatusCode StatusCode, string? Error)> RequestTokenAsync(
        HttpClient client,
        string password
    )
    {
        using var content = new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["grant_type"] = OpenIdConstants.GrantTypes.Password,
                ["client_id"] = ClientId,
                ["client_secret"] = ClientSecret,
                ["username"] = UserName,
                ["password"] = password,
                ["scope"] = OpenIdConstants.ScopeTypes.OpenId,
            }
        );

        using var response = await client.PostAsync("/oauth2/token", content);
        var body = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(body);
        var error = document.RootElement.TryGetProperty("error", out var errorElement)
            ? errorElement.GetString()
            : null;
        return (response.StatusCode, error);
    }

    private static async Task<string?> SubmitAsync(HttpClient client, string password)
    {
        using var response = await InteractiveLogin.SubmitLoginAsync(
            client,
            UserName,
            password,
            "/foo"
        );
        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        return InteractiveLogin.ReadSessionCookie(response);
    }

    #endregion

    #region Limit Tests

    [Fact]
    public async Task PasswordGrant_WhenTheLimitIsReached_RefusesTheRightPasswordAsAWrongOne()
    {
        using var factory = new HostApplicationFactory();
        await SeedLimitAsync(factory);
        await factory.SeedConfidentialClientAsync(
            ClientId,
            ClientSecret,
            OpenIdConstants.GrantTypes.Password
        );
        await factory.SeedLocalAccountAsync(UserName, Password);
        var client = CreateClient(factory);

        var (acceptedStatusCode, acceptedError) = await RequestTokenAsync(client, Password);
        Assert.True(acceptedStatusCode == HttpStatusCode.OK, $"error='{acceptedError}'");

        string? wrongError = null;
        for (var attempt = 0; attempt < FailedAttemptsLimit; attempt++)
        {
            var (wrongStatusCode, error) = await RequestTokenAsync(client, WrongPassword);
            Assert.Equal(HttpStatusCode.BadRequest, wrongStatusCode);
            wrongError = error;
        }

        Assert.NotNull(wrongError);

        var (limitedStatusCode, limitedError) = await RequestTokenAsync(client, Password);
        Assert.Equal(HttpStatusCode.BadRequest, limitedStatusCode);
        Assert.Equal(wrongError, limitedError);
    }

    [Fact]
    public async Task SignIn_WhenTheLimitIsReached_RefusesTheRightPassword()
    {
        using var factory = new HostApplicationFactory();
        await SeedLimitAsync(factory);
        await factory.SeedLocalAccountAsync(UserName, Password);
        var client = CreateClient(factory);

        Assert.NotNull(await SubmitAsync(client, Password));

        for (var attempt = 0; attempt < FailedAttemptsLimit; attempt++)
        {
            Assert.Null(await SubmitAsync(client, WrongPassword));
        }

        using var limitedResponse = await InteractiveLogin.SubmitLoginAsync(
            client,
            UserName,
            Password,
            "/foo"
        );
        Assert.Equal(HttpStatusCode.Found, limitedResponse.StatusCode);
        Assert.Null(InteractiveLogin.ReadSessionCookie(limitedResponse));
        var location = limitedResponse.Headers.Location;
        Assert.NotNull(location);
        Assert.Contains("error=invalid_credentials", location.OriginalString);
    }

    [Fact]
    public async Task SignIn_AfterTheRightPassword_CountsTheFailuresFromZero()
    {
        using var factory = new HostApplicationFactory();
        await SeedLimitAsync(factory);
        await factory.SeedLocalAccountAsync(UserName, Password);
        var client = CreateClient(factory);

        Assert.Null(await SubmitAsync(client, WrongPassword));
        Assert.NotNull(await SubmitAsync(client, Password));
        Assert.Null(await SubmitAsync(client, WrongPassword));
        Assert.NotNull(await SubmitAsync(client, Password));
    }

    #endregion
}
