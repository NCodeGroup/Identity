// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using NCode.Identity.OpenId.Integration.Tests.Infrastructure;
using Xunit;

namespace NCode.Identity.OpenId.Integration.Tests.Endpoints;

// A password is judged, hashed and verified in its NFC form (ADR-1238).
public class PasswordNormalizationTests
{
    private const string ClientId = "it-normalization-client";
    private const string ClientSecret = "normalization-client-secret";
    private const string UserName = "normalization-user";

    // The same characters in two compositions: U+00E9, and "e" followed by the combining acute accent U+0301.
    private const string ComposedPassword = "caf\u00E9-normalization-password";
    private const string DecomposedPassword = "cafe\u0301-normalization-password";

    #region Scaffolding

    private static HttpClient CreateClient(HostApplicationFactory factory) =>
        factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                HandleCookies = false,
            }
        );

    private static async ValueTask<(HttpStatusCode StatusCode, string Body)> RequestTokenAsync(
        HttpClient client,
        string password,
        CancellationToken cancellationToken
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

        using var response = await client.PostAsync("/oauth2/token", content, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        return (response.StatusCode, body);
    }

    #endregion

    #region Verification Tests

    [Fact]
    public async Task SignIn_WhenTheComposedFormIsTyped_VerifiesAPasswordSetDecomposed()
    {
        using var factory = new HostApplicationFactory();
        await factory.SeedLocalAccountAsync(UserName, DecomposedPassword, CancellationToken.None);
        var client = CreateClient(factory);

        var sessionCookie = await InteractiveLogin.SignInAsync(
            client,
            UserName,
            ComposedPassword,
            CancellationToken.None
        );

        Assert.False(string.IsNullOrEmpty(sessionCookie));
    }

    [Fact]
    public async Task PasswordGrant_WhenTheDecomposedFormIsTyped_VerifiesAPasswordSetComposed()
    {
        using var factory = new HostApplicationFactory();
        await factory.SeedConfidentialClientAsync(
            ClientId,
            ClientSecret,
            [OpenIdProtocol.GrantTypes.Password],
            CancellationToken.None
        );
        await factory.SeedLocalAccountAsync(UserName, ComposedPassword, CancellationToken.None);
        var client = CreateClient(factory);

        var (statusCode, body) = await RequestTokenAsync(
            client,
            DecomposedPassword,
            CancellationToken.None
        );

        Assert.True(statusCode == HttpStatusCode.OK, body);
    }

    #endregion
}
