// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Playwright;
using NCode.Identity.Bff.EndToEnd.Tests.Infrastructure;
using NCode.Identity.Jose.Algorithms;
using NCode.Identity.OpenId;
using Xunit;

namespace NCode.Identity.Bff.EndToEnd.Tests.Interop;

[Collection(EndToEndCollection.Name)]
public sealed class KeycloakInteropTests(EndToEndEnvironment environment)
{
    private const string UserPath = "/bff/user";

    private sealed record TokenProbe(string TokenType, string Fingerprint);

    private sealed record UserClaims(string Email);

    private sealed record UserResponse(UserClaims Claims, string LogoutUrl, string LogoutSid);

    #region Discovery Tests

    [Fact]
    public async Task Discovery_OfKeycloak_AdvertisesTheMetadataTheBffUses()
    {
        using var client = environment.Keycloak.CreateHttpClient();

        var metadata = await client.GetFromJsonAsync<Dictionary<string, JsonElement>>(
            $"{environment.Keycloak.Issuer}/.well-known/openid-configuration",
            JsonSerializerOptions.Web
        );

        Assert.NotNull(metadata);
        Assert.True(
            metadata.ContainsKey(OpenIdProtocol.MetadataNames.PushedAuthorizationRequestEndpoint)
        );
        Assert.True(metadata.ContainsKey(OpenIdProtocol.MetadataNames.RevocationEndpoint));
        Assert.True(metadata.ContainsKey(OpenIdProtocol.MetadataNames.EndSessionEndpoint));
        Assert.True(metadata.ContainsKey(OpenIdProtocol.MetadataNames.JwksUri));
        Assert.True(
            IsTrue(
                metadata,
                OpenIdProtocol.MetadataNames.AuthorizationResponseIssParameterSupported
            )
        );
        Assert.True(IsTrue(metadata, OpenIdProtocol.MetadataNames.BackchannelLogoutSupported));
        Assert.True(
            IsTrue(metadata, OpenIdProtocol.MetadataNames.BackchannelLogoutSessionSupported)
        );
        Assert.Contains(
            OpenIdProtocol.CodeChallengeMethods.S256,
            Values(metadata, OpenIdProtocol.MetadataNames.CodeChallengeMethodsSupported)
        );
        Assert.Contains(
            OpenIdProtocol.ClientAuthenticationMethods.PrivateKeyJwt,
            Values(metadata, OpenIdProtocol.MetadataNames.TokenEndpointAuthMethodsSupported)
        );
        Assert.Contains(
            AlgorithmCodes.DigitalSignature.EcdsaSha256,
            Values(metadata, OpenIdProtocol.MetadataNames.DPoPSigningAlgValuesSupported)
        );
        Assert.Contains(
            AlgorithmCodes.DigitalSignature.RsaSsaPssSha256,
            Values(metadata, OpenIdProtocol.MetadataNames.IdTokenSigningAlgValuesSupported)
        );
    }

    #endregion

    #region Login Tests

    [Fact]
    public async Task Login_AgainstKeycloak_EstablishesTheSession()
    {
        await using var context = await environment.NewContextAsync(
            BrowserType.Chromium,
            CancellationToken.None
        );
        var page = await context.NewPageAsync();

        await BrowserFlows.LoginAsync(
            page,
            environment.StrictBff,
            environment.Keycloak,
            CancellationToken.None
        );

        var outcome = await BrowserFlows.FetchAsync(page, UserPath, true, CancellationToken.None);
        Assert.Equal(StatusCodes.Status200OK, outcome.Status);
        var user = Deserialize<UserResponse>(outcome.Body);
        Assert.Equal(KeycloakProvider.UserEmail, user.Claims.Email);
    }

    #endregion

    #region AccessToken Tests

    [Fact]
    public async Task AccessToken_AgainstKeycloak_IsDPoPBoundAndRotatesOnRefresh()
    {
        await using var context = await environment.NewContextAsync(
            BrowserType.Chromium,
            CancellationToken.None
        );
        var page = await context.NewPageAsync();
        await BrowserFlows.LoginAsync(
            page,
            environment.StrictBff,
            environment.Keycloak,
            CancellationToken.None
        );

        var first = await BrowserFlows.FetchAsync(page, "/api/token", true, CancellationToken.None);
        var second = await BrowserFlows.FetchAsync(
            page,
            "/api/token",
            true,
            CancellationToken.None
        );

        Assert.Equal(StatusCodes.Status200OK, first.Status);
        Assert.Equal(StatusCodes.Status200OK, second.Status);
        var firstProbe = Deserialize<TokenProbe>(first.Body);
        var secondProbe = Deserialize<TokenProbe>(second.Body);
        Assert.Equal("DPoP", firstProbe.TokenType);
        Assert.Equal("DPoP", secondProbe.TokenType);
        Assert.NotEqual(firstProbe.Fingerprint, secondProbe.Fingerprint);
    }

    #endregion

    #region Logout Tests

    [Fact]
    public async Task Logout_AgainstKeycloak_EndsTheBffAndProviderSessions()
    {
        var bff = environment.StrictBff;
        await using var context = await environment.NewContextAsync(
            BrowserType.Chromium,
            CancellationToken.None
        );
        var page = await context.NewPageAsync();
        await BrowserFlows.LoginAsync(page, bff, environment.Keycloak, CancellationToken.None);
        var userOutcome = await BrowserFlows.FetchAsync(
            page,
            UserPath,
            true,
            CancellationToken.None
        );
        var user = Deserialize<UserResponse>(userOutcome.Body);

        await BrowserFlows.SubmitFormAndWaitForUrlAsync(
            page,
            bff.Origin + user.LogoutUrl,
            new Dictionary<string, string> { ["sid"] = user.LogoutSid },
            url => new Uri(url).GetLeftPart(UriPartial.Path) == $"{bff.Origin}/",
            CancellationToken.None
        );

        var outcome = await BrowserFlows.FetchAsync(page, UserPath, true, CancellationToken.None);
        Assert.Equal(StatusCodes.Status401Unauthorized, outcome.Status);

        await page.GotoAsync($"{bff.Origin}/bff/login?returnUrl=%2F");
        var usernameField = await page.WaitForSelectorAsync("#username");
        Assert.NotNull(usernameField);
    }

    [Fact]
    public async Task BackChannelLogout_FromKeycloak_EndsTheBffSession()
    {
        await using var context = await environment.NewContextAsync(
            BrowserType.Chromium,
            CancellationToken.None
        );
        var page = await context.NewPageAsync();
        await BrowserFlows.LoginAsync(
            page,
            environment.StrictBff,
            environment.Keycloak,
            CancellationToken.None
        );
        var before = await BrowserFlows.FetchAsync(page, UserPath, true, CancellationToken.None);
        Assert.Equal(StatusCodes.Status200OK, before.Status);

        await environment.Keycloak.LogOutUserAsync(CancellationToken.None);

        var deadline = TimeProvider.System.GetUtcNow().AddSeconds(15);
        var outcome = await BrowserFlows.FetchAsync(page, UserPath, true, CancellationToken.None);
        while (
            outcome.Status != StatusCodes.Status401Unauthorized
            && TimeProvider.System.GetUtcNow() < deadline
        )
        {
            await Task.Delay(TimeSpan.FromMilliseconds(250));
            outcome = await BrowserFlows.FetchAsync(page, UserPath, true, CancellationToken.None);
        }

        Assert.Equal(StatusCodes.Status401Unauthorized, outcome.Status);
    }

    #endregion

    private static T Deserialize<T>(string json) =>
        JsonSerializer.Deserialize<T>(json, JsonSerializerOptions.Web)
        ?? throw new InvalidOperationException("The body deserialized to null.");

    private static bool IsTrue(Dictionary<string, JsonElement> metadata, string name) =>
        metadata.TryGetValue(name, out var value) && value.ValueKind == JsonValueKind.True;

    private static string[] Values(Dictionary<string, JsonElement> metadata, string name) =>
        metadata.TryGetValue(name, out var value) && value.ValueKind == JsonValueKind.Array
            ? [.. value.EnumerateArray().Select(x => x.GetString() ?? string.Empty)]
            : [];
}
