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
using System.Text.Json;
using System.Web;
using Microsoft.AspNetCore.Mvc.Testing;
using NCode.Buffers;
using NCode.Identity.OpenId.IntegrationTests.Infrastructure;
using NCode.Identity.OpenId.Settings;
using Xunit;

namespace NCode.Identity.OpenId.IntegrationTests.Endpoints;

/// <summary>
/// Exercises how the authorization endpoint derives the delivery of its response, including the response modes a
/// client lists in <c>response_modes_supported</c> (ADR-0201).
/// </summary>
public class AuthorizationResponseDeliveryTests(HostApplicationFactory factory)
    : IClassFixture<HostApplicationFactory>
{
    private const string RedirectUri = "https://client.example/callback";

    private HostApplicationFactory Factory { get; } = factory;

    #region Scaffolding

    private async Task<HttpResponseMessage> AuthorizeAsync(
        string clientId,
        string redirectUri,
        string extraParameters,
        string responseType = OpenIdConstants.ResponseTypes.Code
    )
    {
        var client = Factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }
        );

        var query =
            $"?client_id={Uri.EscapeDataString(clientId)}"
            + $"&redirect_uri={Uri.EscapeDataString(redirectUri)}"
            + $"&response_type={Uri.EscapeDataString(responseType)}"
            + "&scope=openid"
            + "&state=abc123"
            + TestPkce.QueryParameters
            + extraParameters;

        return await client.GetAsync("/oauth2/authorize" + query);
    }

    private static JsonElement DecodePayload(string? jwt)
    {
        Assert.False(string.IsNullOrEmpty(jwt));
        var segments = jwt.Split('.');
        Assert.Equal(3, segments.Length);

        return JsonElement.Parse(
            SecureEncoding.UTF8.GetString(Base64Url.DecodeFromChars(segments[1]))
        );
    }

    #endregion

    #region Response Mode Tests

    [Fact]
    public async Task Authorize_WhenTheJwtShorthandIsNotListed_RefusesWithoutRedirecting()
    {
        const string clientId = "it-delivery-jwt-not-listed";
        await Factory.SeedPublicClientWithSettingsAsync(
            clientId,
            new Dictionary<string, object>
            {
                [OpenIdSettingNames.ResponseModesSupported] = new[]
                {
                    OpenIdConstants.ResponseModes.Query,
                    OpenIdConstants.ResponseModes.QueryJwt,
                },
            },
            RedirectUri
        );

        using var response = await AuthorizeAsync(
            clientId,
            RedirectUri,
            "&prompt=none&response_mode=jwt"
        );

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null(response.Headers.Location);
    }

    [Fact]
    public async Task Authorize_WhenTheDefaultResponseModeIsNotListed_RefusesWithoutRedirecting()
    {
        const string clientId = "it-delivery-default-not-listed";
        await Factory.SeedPublicClientWithSettingsAsync(
            clientId,
            new Dictionary<string, object>
            {
                [OpenIdSettingNames.ResponseModesSupported] = new[]
                {
                    OpenIdConstants.ResponseModes.FormPost,
                },
            },
            RedirectUri
        );

        using var response = await AuthorizeAsync(clientId, RedirectUri, "&prompt=none");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null(response.Headers.Location);
    }

    [Fact]
    public async Task Authorize_WhenTheJwtShorthandAndItsResolvedModeAreListed_DeliversASignedResponse()
    {
        const string clientId = "it-delivery-jwt-listed";
        await Factory.SeedPublicClientWithSettingsAsync(
            clientId,
            new Dictionary<string, object>
            {
                [OpenIdSettingNames.ResponseModesSupported] = new[]
                {
                    OpenIdConstants.ResponseModes.Jwt,
                    OpenIdConstants.ResponseModes.QueryJwt,
                },
            },
            RedirectUri
        );

        using var response = await AuthorizeAsync(
            clientId,
            RedirectUri,
            "&prompt=none&response_mode=jwt"
        );

        Assert.Equal(HttpStatusCode.SeeOther, response.StatusCode);
        var location = response.Headers.Location;
        Assert.NotNull(location);
        var parameters = HttpUtility.ParseQueryString(location.Query);
        Assert.False(string.IsNullOrEmpty(parameters["response"]));
    }

    [Fact]
    public async Task Authorize_WhenTheResponseCannotBeSigned_RefusesBeforeTheUserSignsIn()
    {
        const string clientId = "it-delivery-cannot-be-signed";
        await Factory.SeedPublicClientWithSettingsAsync(
            clientId,
            new Dictionary<string, object>
            {
                [OpenIdSettingNames.AuthorizationSigningAlgValuesSupported] = new[] { "PS256" },
                [OpenIdSettingNames.AuthorizationSignedResponseAlg] = "RS256",
            },
            RedirectUri
        );

        using var response = await AuthorizeAsync(
            clientId,
            RedirectUri,
            "&response_mode=query.jwt"
        );

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Null(response.Headers.Location);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(
            OpenIdConstants.ErrorCodes.ServerError,
            document.RootElement.GetProperty("error").GetString()
        );
    }

    [Fact]
    public async Task Authorize_WhenTheClientSetsAResponseLifetime_TheResponseJwtExpiresAfterIt()
    {
        const string clientId = "it-delivery-response-lifetime";
        await Factory.SeedPublicClientWithSettingsAsync(
            clientId,
            new Dictionary<string, object>
            {
                [OpenIdSettingNames.AuthorizationResponseLifetime] = "00:01:00",
            },
            RedirectUri
        );

        using var response = await AuthorizeAsync(
            clientId,
            RedirectUri,
            "&prompt=none&response_mode=query.jwt"
        );

        Assert.Equal(HttpStatusCode.SeeOther, response.StatusCode);
        var location = response.Headers.Location;
        Assert.NotNull(location);
        var parameters = HttpUtility.ParseQueryString(location.Query);
        var payload = DecodePayload(parameters["response"]);
        Assert.Equal(
            60,
            payload.GetProperty("exp").GetInt64() - payload.GetProperty("iat").GetInt64()
        );
    }

    #endregion

    #region Redirect URI Tests

    [Fact]
    public async Task Authorize_WhenTheRedirectUriAddsAQueryParameter_RefusesWithoutRedirecting()
    {
        const string clientId = "it-delivery-redirect-adds-query";
        await Factory.SeedPublicClientWithSettingsAsync(
            clientId,
            new Dictionary<string, object>(),
            RedirectUri
        );

        using var response = await AuthorizeAsync(
            clientId,
            RedirectUri + "?injected=1",
            "&prompt=none"
        );

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null(response.Headers.Location);
    }

    [Fact]
    public async Task Authorize_WhenTheRegisteredRedirectUriHasAQuery_KeepsItInTheResponse()
    {
        const string clientId = "it-delivery-redirect-registered-query";
        const string redirectUri = RedirectUri + "?tenant=a";
        await Factory.SeedPublicClientWithSettingsAsync(
            clientId,
            new Dictionary<string, object>(),
            redirectUri
        );

        using var response = await AuthorizeAsync(clientId, redirectUri, "&prompt=none");

        Assert.Equal(HttpStatusCode.SeeOther, response.StatusCode);
        var location = response.Headers.Location;
        Assert.NotNull(location);
        var parameters = HttpUtility.ParseQueryString(location.Query);
        Assert.Equal("a", parameters["tenant"]);
        Assert.Equal(OpenIdConstants.ErrorCodes.LoginRequired, parameters["error"]);
    }

    [Fact]
    public async Task Authorize_WhenAnUnregisteredLoopbackRedirectUriIsRequested_RefusesWithoutRedirecting()
    {
        const string clientId = "it-delivery-redirect-unregistered-loopback";
        await Factory.SeedPublicClientWithSettingsAsync(
            clientId,
            new Dictionary<string, object>(),
            RedirectUri
        );

        using var response = await AuthorizeAsync(
            clientId,
            "http://127.0.0.1:5555/callback",
            "&prompt=none"
        );

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null(response.Headers.Location);
    }

    [Fact]
    public async Task Authorize_WhenARegisteredLoopbackRedirectUriUsesAnotherPort_DeliversTheResponse()
    {
        const string clientId = "it-delivery-redirect-loopback-port";
        const string requestedUri = "http://127.0.0.1:51234/callback";
        await Factory.SeedPublicClientWithSettingsAsync(
            clientId,
            new Dictionary<string, object>(),
            "http://127.0.0.1/callback"
        );

        using var response = await AuthorizeAsync(clientId, requestedUri, "&prompt=none");

        Assert.Equal(HttpStatusCode.SeeOther, response.StatusCode);
        var location = response.Headers.Location;
        Assert.NotNull(location);
        Assert.StartsWith(requestedUri, location.AbsoluteUri, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Authorize_WhenLoopbackPortChangesAreNotAllowed_RefusesAnotherPort()
    {
        const string clientId = "it-delivery-redirect-loopback-port-off";
        await Factory.SeedPublicClientWithSettingsAsync(
            clientId,
            new Dictionary<string, object> { [OpenIdSettingNames.AllowLoopbackRedirect] = false },
            "http://127.0.0.1/callback"
        );

        using var response = await AuthorizeAsync(
            clientId,
            "http://127.0.0.1:51234/callback",
            "&prompt=none"
        );

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null(response.Headers.Location);
    }

    [Fact]
    public async Task Authorize_WhenALocalhostRedirectUriUsesAnotherPort_RefusesWithoutRedirecting()
    {
        const string clientId = "it-delivery-redirect-localhost-port";
        await Factory.SeedPublicClientWithSettingsAsync(
            clientId,
            new Dictionary<string, object>(),
            "http://localhost/callback"
        );

        using var response = await AuthorizeAsync(
            clientId,
            "http://localhost:51234/callback",
            "&prompt=none"
        );

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null(response.Headers.Location);
    }

    [Fact]
    public async Task Authorize_WhenTheRedirectUriIsRepeated_RefusesAsAnInvalidRequest()
    {
        const string clientId = "it-delivery-redirect-repeated";
        await Factory.SeedPublicClientWithSettingsAsync(
            clientId,
            new Dictionary<string, object>(),
            RedirectUri
        );

        using var response = await AuthorizeAsync(
            clientId,
            RedirectUri,
            "&redirect_uri=" + Uri.EscapeDataString("https://evil.example/cb") + "&prompt=none"
        );

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null(response.Headers.Location);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(
            OpenIdConstants.ErrorCodes.InvalidRequest,
            document.RootElement.GetProperty("error").GetString()
        );
    }

    [Fact]
    public async Task Authorize_WhenTheResponseModeIsRepeated_RefusesAsAnInvalidRequest()
    {
        const string clientId = "it-delivery-response-mode-repeated";
        await Factory.SeedPublicClientWithSettingsAsync(
            clientId,
            new Dictionary<string, object>(),
            RedirectUri
        );

        using var response = await AuthorizeAsync(
            clientId,
            RedirectUri,
            "&response_mode=query&response_mode=fragment&prompt=none"
        );

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null(response.Headers.Location);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(
            OpenIdConstants.ErrorCodes.InvalidRequest,
            document.RootElement.GetProperty("error").GetString()
        );
    }

    #endregion

    #region Token Response Type Tests

    [Fact]
    public async Task Authorize_WhenATokenResponseTypeUsesTheQueryResponseMode_RefusesIt()
    {
        const string clientId = "it-delivery-token-in-query";
        await Factory.SeedPublicClientWithSettingsAsync(
            clientId,
            new Dictionary<string, object>(),
            RedirectUri
        );

        using var response = await AuthorizeAsync(
            clientId,
            RedirectUri,
            "&nonce=n1&prompt=none&response_mode=query",
            "code id_token"
        );

        Assert.Equal(HttpStatusCode.SeeOther, response.StatusCode);
        var location = response.Headers.Location;
        Assert.NotNull(location);
        var parameters = HttpUtility.ParseQueryString(location.Query);
        Assert.Equal(OpenIdConstants.ErrorCodes.InvalidRequest, parameters["error"]);
        Assert.Contains("response mode", parameters["error_description"], StringComparison.Ordinal);
    }

    [Fact]
    public async Task Authorize_WhenATokenResponseTypeUsesTheQueryJwtResponseMode_RefusesIt()
    {
        const string clientId = "it-delivery-token-in-query-jwt";
        await Factory.SeedPublicClientWithSettingsAsync(
            clientId,
            new Dictionary<string, object>(),
            RedirectUri
        );

        using var response = await AuthorizeAsync(
            clientId,
            RedirectUri,
            "&nonce=n1&prompt=none&response_mode=query.jwt",
            "code id_token"
        );

        Assert.Equal(HttpStatusCode.SeeOther, response.StatusCode);
        var location = response.Headers.Location;
        Assert.NotNull(location);
        var parameters = HttpUtility.ParseQueryString(location.Query);
        var payload = DecodePayload(parameters["response"]);
        Assert.Equal(
            OpenIdConstants.ErrorCodes.InvalidRequest,
            payload.GetProperty("error").GetString()
        );
    }

    [Fact]
    public async Task Authorize_WhenATokenResponseTypeUsesTheFragmentResponseMode_IsNotRefusedForItsMode()
    {
        const string clientId = "it-delivery-token-in-fragment";
        await Factory.SeedPublicClientWithSettingsAsync(
            clientId,
            new Dictionary<string, object>(),
            RedirectUri
        );

        using var response = await AuthorizeAsync(
            clientId,
            RedirectUri,
            "&nonce=n1&prompt=none&response_mode=fragment",
            "code id_token"
        );

        Assert.Equal(HttpStatusCode.SeeOther, response.StatusCode);
        var location = response.Headers.Location;
        Assert.NotNull(location);
        Assert.Empty(location.Query);
        Assert.NotEmpty(location.Fragment);
        Assert.DoesNotContain("invalid_request", location.Fragment, StringComparison.Ordinal);
    }

    #endregion
}
