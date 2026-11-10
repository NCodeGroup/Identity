// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using NCode.Identity.OpenId.Integration.Tests.Infrastructure;
using NCode.Identity.OpenId.Settings;
using Xunit;

namespace NCode.Identity.OpenId.Integration.Tests.Endpoints;

// The failed password attempts of an account are limited (ADR-1237).
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
    private static ValueTask SeedLimitAsync(
        HostApplicationFactory factory,
        CancellationToken cancellationToken
    ) =>
        factory.SeedTenantSettingsAsync(
            new Dictionary<string, object>
            {
                [OpenIdSettingNames.PasswordFailedAttemptsLimit] = FailedAttemptsLimit,
            },
            cancellationToken
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
                ["grant_type"] = OpenIdProtocol.GrantTypes.Password,
                ["client_id"] = ClientId,
                ["client_secret"] = ClientSecret,
                ["username"] = UserName,
                ["password"] = password,
                ["scope"] = OpenIdProtocol.ScopeTypes.OpenId,
            }
        );

        using var response = await client.PostAsync("/oauth2/token", content);
        var body = await response.Content.ReadAsStringAsync();
        var jsonElement = JsonElement.Parse(body);
        var error = jsonElement.TryGetProperty("error", out var errorElement)
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
            "/foo",
            CancellationToken.None
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
        await SeedLimitAsync(factory, CancellationToken.None);
        await factory.SeedConfidentialClientAsync(
            ClientId,
            ClientSecret,
            [OpenIdProtocol.GrantTypes.Password],
            CancellationToken.None
        );
        await factory.SeedLocalAccountAsync(UserName, Password, CancellationToken.None);
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
        await SeedLimitAsync(factory, CancellationToken.None);
        await factory.SeedLocalAccountAsync(UserName, Password, CancellationToken.None);
        var client = CreateClient(factory);

        var firstSessionCookie = await SubmitAsync(client, Password);
        Assert.NotNull(firstSessionCookie);

        for (var attempt = 0; attempt < FailedAttemptsLimit; attempt++)
        {
            var failedSessionCookie = await SubmitAsync(client, WrongPassword);
            Assert.Null(failedSessionCookie);
        }

        using var limitedResponse = await InteractiveLogin.SubmitLoginAsync(
            client,
            UserName,
            Password,
            "/foo",
            CancellationToken.None
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
        await SeedLimitAsync(factory, CancellationToken.None);
        await factory.SeedLocalAccountAsync(UserName, Password, CancellationToken.None);
        var client = CreateClient(factory);

        var wrongSessionCookie = await SubmitAsync(client, WrongPassword);
        Assert.Null(wrongSessionCookie);
        var rightSessionCookie = await SubmitAsync(client, Password);
        Assert.NotNull(rightSessionCookie);
        var wrongSessionCookieAfterRight = await SubmitAsync(client, WrongPassword);
        Assert.Null(wrongSessionCookieAfterRight);
        var rightSessionCookieAfterWrong = await SubmitAsync(client, Password);
        Assert.NotNull(rightSessionCookieAfterWrong);
    }

    #endregion
}
