// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using NCode.Identity.OpenId.Integration.Tests.Infrastructure;
using NCode.Identity.OpenId.Settings;
using Xunit;

namespace NCode.Identity.OpenId.Integration.Tests.Endpoints;

/// <summary>
/// Exercises how the authorization, pushed authorization, and dynamic client registration endpoints refuse a redirect
/// URI whose kind the effective <c>redirect_uri_kinds_supported</c> setting does not permit, before the response is
/// delivered or the client is registered.
/// </summary>
public class RedirectUriKindEnforcementTests(HostApplicationFactory factory)
    : IClassFixture<HostApplicationFactory>
{
    private HostApplicationFactory Factory { get; } = factory;

    #region Scaffolding

    private async ValueTask<HttpResponseMessage> AuthorizeAsync(
        string clientId,
        string redirectUri,
        CancellationToken cancellationToken
    )
    {
        var client = Factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }
        );

        var query =
            $"?client_id={Uri.EscapeDataString(clientId)}"
            + $"&redirect_uri={Uri.EscapeDataString(redirectUri)}"
            + $"&response_type={Uri.EscapeDataString(OpenIdProtocol.ResponseTypes.Code)}"
            + "&scope=openid"
            + "&state=abc123"
            + TestPkce.QueryParameters
            + "&prompt=none";

        return await client.GetAsync("/oauth2/authorize" + query, cancellationToken);
    }

    private async ValueTask<HttpResponseMessage> PushAsync(
        string clientId,
        string redirectUri,
        CancellationToken cancellationToken
    )
    {
        var client = Factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }
        );

        var values = new List<KeyValuePair<string, string>>
        {
            new("client_id", clientId),
            new("redirect_uri", redirectUri),
            new("response_type", OpenIdProtocol.ResponseTypes.Code),
            new("scope", "openid"),
            new("state", "abc123"),
        };
        values.AddRange(TestPkce.FormFields);

        using var form = new FormUrlEncodedContent(values);
        return await client.PostAsync("/oauth2/par", form, cancellationToken);
    }

    private ValueTask SeedClientAsync(
        string clientId,
        string[] redirectUriKinds,
        string registeredRedirectUri,
        CancellationToken cancellationToken
    ) =>
        Factory.SeedPublicClientWithSettingsAsync(
            clientId,
            new Dictionary<string, object>
            {
                [OpenIdSettingNames.RedirectUriKindsSupported] = redirectUriKinds,
            },
            [registeredRedirectUri],
            cancellationToken
        );

    private static async ValueTask<HttpClient> CreateHttpsOnlyRegistrationClientAsync(
        AnonymousRegistrationApplicationFactory registrationFactory,
        CancellationToken cancellationToken
    )
    {
        await registrationFactory.SeedTenantSettingsAsync(
            new Dictionary<string, object>
            {
                [OpenIdSettingNames.RedirectUriKindsSupported] = new[]
                {
                    OpenIdConstants.RedirectUriKinds.Https,
                },
            },
            cancellationToken
        );

        return registrationFactory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }
        );
    }

    private static async ValueTask<HttpResponseMessage> RegisterAsync(
        HttpClient client,
        string redirectUri,
        CancellationToken cancellationToken
    ) =>
        await client.PostAsJsonAsync(
            "/oauth2/register",
            new Dictionary<string, object>
            {
                ["redirect_uris"] = new[] { redirectUri },
                ["token_endpoint_auth_method"] = OpenIdProtocol
                    .ClientAuthenticationMethods
                    .ClientSecretBasic,
            },
            cancellationToken
        );

    #endregion

    #region Authorize Tests

    [Fact]
    public async Task Authorize_WhenOnlyHttpsIsPermitted_RefusesAnHttpRedirectUriWithoutRedirecting()
    {
        const string clientId = "it-kind-https-only-refuses-http";
        const string redirectUri = "http://client.example/callback";
        await SeedClientAsync(
            clientId,
            [OpenIdConstants.RedirectUriKinds.Https],
            redirectUri,
            CancellationToken.None
        );

        using var response = await AuthorizeAsync(clientId, redirectUri, CancellationToken.None);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null(response.Headers.Location);

        var body = await response.Content.ReadAsStringAsync();
        var jsonElement = JsonElement.Parse(body);
        Assert.Equal(
            OpenIdProtocol.ErrorCodes.InvalidRequest,
            jsonElement.GetProperty("error").GetString()
        );
    }

    [Fact]
    public async Task Authorize_WhenOnlyHttpsIsPermitted_DeliversToAnHttpsRedirectUri()
    {
        const string clientId = "it-kind-https-only-delivers-https";
        const string redirectUri = "https://client.example/callback";
        await SeedClientAsync(
            clientId,
            [OpenIdConstants.RedirectUriKinds.Https],
            redirectUri,
            CancellationToken.None
        );

        using var response = await AuthorizeAsync(clientId, redirectUri, CancellationToken.None);

        Assert.Equal(HttpStatusCode.SeeOther, response.StatusCode);
    }

    [Fact]
    public async Task Authorize_WhenLoopbackIsPermitted_DeliversToALoopbackRedirectUriOnAnyPort()
    {
        const string clientId = "it-kind-loopback-delivers-any-port";
        const string registeredRedirectUri = "http://127.0.0.1/callback";
        const string requestedRedirectUri = "http://127.0.0.1:51234/callback";
        await SeedClientAsync(
            clientId,
            [OpenIdConstants.RedirectUriKinds.Https, OpenIdConstants.RedirectUriKinds.Loopback],
            registeredRedirectUri,
            CancellationToken.None
        );

        using var response = await AuthorizeAsync(
            clientId,
            requestedRedirectUri,
            CancellationToken.None
        );

        Assert.Equal(HttpStatusCode.SeeOther, response.StatusCode);
        var location = response.Headers.Location;
        Assert.NotNull(location);
        Assert.StartsWith(requestedRedirectUri, location.OriginalString);
    }

    [Fact]
    public async Task Authorize_WhenLoopbackIsPermitted_RefusesALocalhostRedirectUri()
    {
        const string clientId = "it-kind-loopback-refuses-localhost";
        const string redirectUri = "http://localhost/callback";
        await SeedClientAsync(
            clientId,
            [OpenIdConstants.RedirectUriKinds.Https, OpenIdConstants.RedirectUriKinds.Loopback],
            redirectUri,
            CancellationToken.None
        );

        using var response = await AuthorizeAsync(clientId, redirectUri, CancellationToken.None);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null(response.Headers.Location);
    }

    [Fact]
    public async Task Authorize_WhenThePrivateUseSchemeIsNotAReverseDomainName_RefusesItWithoutRedirecting()
    {
        const string clientId = "no-kind-client";
        const string redirectUri = "myapp://callback";
        await Factory.SeedPublicClientAsync(clientId, [redirectUri], CancellationToken.None);

        using var response = await AuthorizeAsync(clientId, redirectUri, CancellationToken.None);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null(response.Headers.Location);

        var body = await response.Content.ReadAsStringAsync();
        var jsonElement = JsonElement.Parse(body);
        Assert.Equal(
            OpenIdProtocol.ErrorCodes.InvalidRequest,
            jsonElement.GetProperty("error").GetString()
        );
    }

    [Fact]
    public async Task Authorize_UnderTheFapi2Kinds_DeliversToAReverseDomainNameRedirectUri()
    {
        const string clientId = "reverse-domain-client";
        const string redirectUri = "com.example.app:/callback";
        await SeedClientAsync(
            clientId,
            [
                OpenIdConstants.RedirectUriKinds.Https,
                OpenIdConstants.RedirectUriKinds.Loopback,
                OpenIdConstants.RedirectUriKinds.PrivateUse,
            ],
            redirectUri,
            CancellationToken.None
        );

        using var response = await AuthorizeAsync(clientId, redirectUri, CancellationToken.None);

        Assert.Equal(HttpStatusCode.SeeOther, response.StatusCode);
        var location = response.Headers.Location;
        Assert.NotNull(location);
        Assert.StartsWith(redirectUri, location.OriginalString);
    }

    #endregion

    #region PushedAuthorization Tests

    [Fact]
    public async Task PushedAuthorization_WhenOnlyHttpsIsPermitted_RefusesAnHttpRedirectUri()
    {
        const string clientId = "it-kind-par-https-only-refuses-http";
        const string redirectUri = "http://client.example/callback";
        await SeedClientAsync(
            clientId,
            [OpenIdConstants.RedirectUriKinds.Https],
            redirectUri,
            CancellationToken.None
        );

        using var response = await PushAsync(clientId, redirectUri, CancellationToken.None);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        var jsonElement = JsonElement.Parse(body);
        Assert.Equal(
            OpenIdProtocol.ErrorCodes.InvalidRequest,
            jsonElement.GetProperty("error").GetString()
        );
    }

    #endregion

    #region Register Tests

    [Fact]
    public async Task Register_WhenTheTenantPermitsOnlyHttps_RefusesAnHttpRedirectUri()
    {
        using var registrationFactory = new AnonymousRegistrationApplicationFactory();
        var client = await CreateHttpsOnlyRegistrationClientAsync(
            registrationFactory,
            CancellationToken.None
        );

        using var response = await RegisterAsync(
            client,
            "http://client.example/callback",
            CancellationToken.None
        );

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.BadRequest, body);

        var jsonElement = JsonElement.Parse(body);
        Assert.Equal(
            OpenIdProtocol.ErrorCodes.InvalidRedirectUri,
            jsonElement.GetProperty("error").GetString()
        );
    }

    [Fact]
    public async Task Register_WhenTheTenantPermitsOnlyHttps_RegistersAnHttpsRedirectUri()
    {
        using var registrationFactory = new AnonymousRegistrationApplicationFactory();
        var client = await CreateHttpsOnlyRegistrationClientAsync(
            registrationFactory,
            CancellationToken.None
        );

        using var response = await RegisterAsync(
            client,
            "https://client.example/callback",
            CancellationToken.None
        );

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.Created, body);
    }

    [Fact]
    public async Task Register_WhenThePrivateUseSchemeIsNotAReverseDomainName_ReturnsInvalidRedirectUri()
    {
        using var registrationFactory = new AnonymousRegistrationApplicationFactory();
        var client = registrationFactory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }
        );

        using var response = await RegisterAsync(
            client,
            "myapp://callback",
            CancellationToken.None
        );

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.BadRequest, body);

        var jsonElement = JsonElement.Parse(body);
        Assert.Equal(
            OpenIdProtocol.ErrorCodes.InvalidRedirectUri,
            jsonElement.GetProperty("error").GetString()
        );
    }

    #endregion
}
