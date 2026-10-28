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
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using NCode.Identity.OpenId.IntegrationTests.Infrastructure;
using NCode.Identity.OpenId.Settings;
using Xunit;

namespace NCode.Identity.OpenId.IntegrationTests.Endpoints;

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

    private async Task<HttpResponseMessage> AuthorizeAsync(string clientId, string redirectUri)
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

        return await client.GetAsync("/oauth2/authorize" + query);
    }

    private async Task<HttpResponseMessage> PushAsync(string clientId, string redirectUri)
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
        return await client.PostAsync("/oauth2/par", form);
    }

    private Task SeedClientAsync(
        string clientId,
        string[] redirectUriKinds,
        string registeredRedirectUri
    ) =>
        Factory.SeedPublicClientWithSettingsAsync(
            clientId,
            new Dictionary<string, object>
            {
                [OpenIdSettingNames.RedirectUriKindsSupported] = redirectUriKinds,
            },
            registeredRedirectUri
        );

    private static async Task<HttpClient> CreateHttpsOnlyRegistrationClientAsync(
        AnonymousRegistrationApplicationFactory registrationFactory
    )
    {
        await registrationFactory.SeedTenantSettingsAsync(
            new Dictionary<string, object>
            {
                [OpenIdSettingNames.RedirectUriKindsSupported] = new[]
                {
                    OpenIdConstants.RedirectUriKinds.Https,
                },
            }
        );

        return registrationFactory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }
        );
    }

    private static Task<HttpResponseMessage> RegisterAsync(HttpClient client, string redirectUri) =>
        client.PostAsJsonAsync(
            "/oauth2/register",
            new Dictionary<string, object>
            {
                ["redirect_uris"] = new[] { redirectUri },
                ["token_endpoint_auth_method"] = OpenIdProtocol
                    .ClientAuthenticationMethods
                    .ClientSecretBasic,
            }
        );

    #endregion

    #region Authorize Tests

    [Fact]
    public async Task Authorize_WhenOnlyHttpsIsPermitted_RefusesAnHttpRedirectUriWithoutRedirecting()
    {
        const string clientId = "it-kind-https-only-refuses-http";
        const string redirectUri = "http://client.example/callback";
        await SeedClientAsync(clientId, [OpenIdConstants.RedirectUriKinds.Https], redirectUri);

        using var response = await AuthorizeAsync(clientId, redirectUri);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null(response.Headers.Location);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(
            OpenIdProtocol.ErrorCodes.InvalidRequest,
            document.RootElement.GetProperty("error").GetString()
        );
    }

    [Fact]
    public async Task Authorize_WhenOnlyHttpsIsPermitted_DeliversToAnHttpsRedirectUri()
    {
        const string clientId = "it-kind-https-only-delivers-https";
        const string redirectUri = "https://client.example/callback";
        await SeedClientAsync(clientId, [OpenIdConstants.RedirectUriKinds.Https], redirectUri);

        using var response = await AuthorizeAsync(clientId, redirectUri);

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
            registeredRedirectUri
        );

        using var response = await AuthorizeAsync(clientId, requestedRedirectUri);

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
            redirectUri
        );

        using var response = await AuthorizeAsync(clientId, redirectUri);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null(response.Headers.Location);
    }

    [Fact]
    public async Task Authorize_WhenThePrivateUseSchemeIsNotAReverseDomainName_RefusesItWithoutRedirecting()
    {
        const string clientId = "no-kind-client";
        const string redirectUri = "myapp://callback";
        await Factory.SeedPublicClientAsync(clientId, redirectUri);

        using var response = await AuthorizeAsync(clientId, redirectUri);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null(response.Headers.Location);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(
            OpenIdProtocol.ErrorCodes.InvalidRequest,
            document.RootElement.GetProperty("error").GetString()
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
            redirectUri
        );

        using var response = await AuthorizeAsync(clientId, redirectUri);

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
        await SeedClientAsync(clientId, [OpenIdConstants.RedirectUriKinds.Https], redirectUri);

        using var response = await PushAsync(clientId, redirectUri);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(
            OpenIdProtocol.ErrorCodes.InvalidRequest,
            document.RootElement.GetProperty("error").GetString()
        );
    }

    #endregion

    #region Register Tests

    [Fact]
    public async Task Register_WhenTheTenantPermitsOnlyHttps_RefusesAnHttpRedirectUri()
    {
        using var registrationFactory = new AnonymousRegistrationApplicationFactory();
        var client = await CreateHttpsOnlyRegistrationClientAsync(registrationFactory);

        using var response = await RegisterAsync(client, "http://client.example/callback");

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.BadRequest, body);

        using var document = JsonDocument.Parse(body);
        Assert.Equal(
            OpenIdProtocol.ErrorCodes.InvalidRedirectUri,
            document.RootElement.GetProperty("error").GetString()
        );
    }

    [Fact]
    public async Task Register_WhenTheTenantPermitsOnlyHttps_RegistersAnHttpsRedirectUri()
    {
        using var registrationFactory = new AnonymousRegistrationApplicationFactory();
        var client = await CreateHttpsOnlyRegistrationClientAsync(registrationFactory);

        using var response = await RegisterAsync(client, "https://client.example/callback");

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

        using var response = await RegisterAsync(client, "myapp://callback");

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.BadRequest, body);

        using var document = JsonDocument.Parse(body);
        Assert.Equal(
            OpenIdProtocol.ErrorCodes.InvalidRedirectUri,
            document.RootElement.GetProperty("error").GetString()
        );
    }

    #endregion
}
