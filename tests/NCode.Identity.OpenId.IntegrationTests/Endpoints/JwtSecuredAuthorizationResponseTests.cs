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
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Web;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using NCode.Buffers;
using NCode.Identity.Jose;
using NCode.Identity.OpenId.IntegrationTests.Infrastructure;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.Secrets.KeyMaterial;
using NCode.Identity.Secrets.Keys;
using Xunit;

namespace NCode.Identity.OpenId.IntegrationTests.Endpoints;

/// <summary>
/// Exercises JWT-secured authorization responses (JARM) end to end: the response parameters travel as one signed JWT
/// issued by the tenant and addressed to the client, in the <c>response</c> parameter of the requested response mode
/// (ADR-0201).
/// </summary>
public class JwtSecuredAuthorizationResponseTests
{
    private const string UserName = "jarm-user";
    private const string Password = "jarm-password";
    private const string ClientId = "it-jarm-public";
    private const string RedirectUri = "https://client.example/callback";

    #region Scaffolding

    private static async Task<(HttpClient Client, string SessionCookie)> SignInAsync(
        HostApplicationFactory factory
    )
    {
        await factory.SeedLocalAccountAsync(UserName, Password);
        await factory.SeedPublicClientAsync(ClientId, RedirectUri);
        var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                HandleCookies = false,
            }
        );
        var sessionCookie = await InteractiveLogin.SignInAsync(client, UserName, Password);
        return (client, sessionCookie);
    }

    private static async Task<HttpResponseMessage> AuthorizeAsync(
        HttpClient client,
        string? sessionCookie,
        string responseMode
    )
    {
        var query =
            $"?client_id={Uri.EscapeDataString(ClientId)}"
            + $"&redirect_uri={Uri.EscapeDataString(RedirectUri)}"
            + "&response_type=code"
            + "&scope=openid"
            + "&state=abc123"
            + "&prompt=none"
            + $"&response_mode={Uri.EscapeDataString(responseMode)}"
            + TestPkce.QueryParameters;

        using var request = new HttpRequestMessage(HttpMethod.Get, "/oauth2/authorize" + query);
        if (sessionCookie is not null)
            request.Headers.Add("Cookie", $"{InteractiveLogin.SessionCookieName}={sessionCookie}");
        return await client.SendAsync(request);
    }

    private static async Task<HttpResponseMessage> AuthorizeWithoutSessionAsync(
        HttpClient client,
        string clientId,
        string extraQuery
    )
    {
        var query =
            $"?client_id={Uri.EscapeDataString(clientId)}"
            + $"&redirect_uri={Uri.EscapeDataString(RedirectUri)}"
            + "&response_type=code"
            + "&scope=openid"
            + "&state=abc123"
            + TestPkce.QueryParameters
            + extraQuery;

        return await client.GetAsync("/oauth2/authorize" + query);
    }

    private static (JsonElement Header, JsonElement Payload) DecodeJwt(string? jwt)
    {
        Assert.False(string.IsNullOrEmpty(jwt));
        var segments = jwt.Split('.');
        Assert.Equal(3, segments.Length);
        var header = JsonElement.Parse(
            SecureEncoding.UTF8.GetString(Base64Url.DecodeFromChars(segments[0]))
        );
        var payload = JsonElement.Parse(
            SecureEncoding.UTF8.GetString(Base64Url.DecodeFromChars(segments[1]))
        );
        return (header, payload);
    }

    private static void AssertAddressedToTheClient(JsonElement header, JsonElement payload)
    {
        Assert.Equal("RS256", header.GetProperty("alg").GetString());
        Assert.Equal(ClientId, payload.GetProperty("aud").GetString());
        Assert.False(string.IsNullOrEmpty(payload.GetProperty("iss").GetString()));
        var lifetime =
            payload.GetProperty("exp").GetInt64() - payload.GetProperty("iat").GetInt64();
        Assert.InRange(lifetime, 1, 600);
    }

    #endregion

    #region Response Mode Tests

    [Fact]
    public async Task Authorize_WithQueryJwt_ReturnsTheResponseAsASignedJwtInTheQuery()
    {
        using var factory = new HostApplicationFactory();
        var (client, sessionCookie) = await SignInAsync(factory);

        using var response = await AuthorizeAsync(
            client,
            sessionCookie,
            OpenIdProtocol.ResponseModes.QueryJwt
        );

        Assert.Equal(HttpStatusCode.SeeOther, response.StatusCode);
        var location = response.Headers.Location;
        Assert.NotNull(location);
        Assert.StartsWith(RedirectUri, location.AbsoluteUri, StringComparison.Ordinal);
        var parameters = HttpUtility.ParseQueryString(location.Query);
        Assert.Null(parameters["code"]);
        Assert.Null(parameters["state"]);

        var (header, payload) = DecodeJwt(parameters["response"]);
        AssertAddressedToTheClient(header, payload);
        Assert.False(string.IsNullOrEmpty(payload.GetProperty("code").GetString()));
        Assert.Equal("abc123", payload.GetProperty("state").GetString());
    }

    [Fact]
    public async Task Authorize_WithTheJwtShorthandForACode_ResolvesToQueryJwt()
    {
        using var factory = new HostApplicationFactory();
        var (client, sessionCookie) = await SignInAsync(factory);

        using var response = await AuthorizeAsync(
            client,
            sessionCookie,
            OpenIdProtocol.ResponseModes.Jwt
        );

        var location = response.Headers.Location;
        Assert.NotNull(location);
        var parameters = HttpUtility.ParseQueryString(location.Query);
        var (header, payload) = DecodeJwt(parameters["response"]);
        AssertAddressedToTheClient(header, payload);
        Assert.False(string.IsNullOrEmpty(payload.GetProperty("code").GetString()));
    }

    [Fact]
    public async Task Authorize_WithFragmentJwt_ReturnsTheResponseJwtInTheFragment()
    {
        using var factory = new HostApplicationFactory();
        var (client, sessionCookie) = await SignInAsync(factory);

        using var response = await AuthorizeAsync(
            client,
            sessionCookie,
            OpenIdProtocol.ResponseModes.FragmentJwt
        );

        var location = response.Headers.Location;
        Assert.NotNull(location);
        var parameters = HttpUtility.ParseQueryString(location.Fragment.TrimStart('#'));
        var (header, payload) = DecodeJwt(parameters["response"]);
        AssertAddressedToTheClient(header, payload);
        Assert.Equal("abc123", payload.GetProperty("state").GetString());
    }

    [Fact]
    public async Task Authorize_WithFormPostJwt_PostsTheResponseJwtAsTheOnlyField()
    {
        using var factory = new HostApplicationFactory();
        var (client, sessionCookie) = await SignInAsync(factory);

        using var response = await AuthorizeAsync(
            client,
            sessionCookie,
            OpenIdProtocol.ResponseModes.FormPostJwt
        );

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("name='response'", html, StringComparison.Ordinal);
        Assert.DoesNotContain("name='code'", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Authorize_WhenTheRequestFailsWithQueryJwt_ReturnsTheErrorAsASignedJwt()
    {
        using var factory = new HostApplicationFactory();
        var (client, _) = await SignInAsync(factory);

        using var response = await AuthorizeAsync(
            client,
            sessionCookie: null,
            OpenIdProtocol.ResponseModes.QueryJwt
        );

        var location = response.Headers.Location;
        Assert.NotNull(location);
        var parameters = HttpUtility.ParseQueryString(location.Query);
        Assert.Null(parameters["error"]);
        var (header, payload) = DecodeJwt(parameters["response"]);
        AssertAddressedToTheClient(header, payload);
        Assert.Equal(
            OpenIdProtocol.ErrorCodes.LoginRequired,
            payload.GetProperty("error").GetString()
        );
        Assert.Equal("abc123", payload.GetProperty("state").GetString());
    }

    [Fact]
    public async Task Authorize_WithAResponseModeNoHandlerServes_IsRefusedWithoutARedirect()
    {
        using var factory = new HostApplicationFactory();
        var (client, sessionCookie) = await SignInAsync(factory);

        using var response = await AuthorizeAsync(client, sessionCookie, "web_message");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null(response.Headers.Location);
    }

    #endregion

    #region Failure and Encryption Tests

    [Fact]
    public async Task Authorize_WhenTheTenantHasNoKeyForTheResponseAlgorithm_RefusesBeforeTheUserSignsIn()
    {
        const string clientId = "it-jarm-no-key";
        using var factory = new HostApplicationFactory();

        // The test host's key signs only RS256, so the tenant holds no key for PS256.
        await factory.SeedPublicClientWithSettingsAsync(
            clientId,
            new Dictionary<string, object>
            {
                [OpenIdSettingNames.AuthorizationSignedResponseAlg] = "PS256",
            },
            RedirectUri
        );
        var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }
        );

        using var response = await AuthorizeWithoutSessionAsync(
            client,
            clientId,
            "&response_mode=query.jwt"
        );

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Null(response.Headers.Location);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(
            OpenIdProtocol.ErrorCodes.ServerError,
            document.RootElement.GetProperty("error").GetString()
        );
        Assert.Contains(
            "no key",
            document.RootElement.GetProperty("error_description").GetString(),
            StringComparison.Ordinal
        );
    }

    [Fact]
    public async Task Authorize_WhenTheClientRegistersEncryptionWithoutAKey_RefusesBeforeTheUserSignsIn()
    {
        const string clientId = "it-jarm-no-enc-key";
        using var factory = new HostApplicationFactory();
        await factory.SeedPublicClientWithSettingsAsync(
            clientId,
            new Dictionary<string, object>
            {
                [OpenIdSettingNames.AuthorizationEncryptedResponseAlg] = "RSA-OAEP-256",
            },
            RedirectUri
        );
        var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }
        );

        using var response = await AuthorizeWithoutSessionAsync(
            client,
            clientId,
            "&response_mode=query.jwt"
        );

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Null(response.Headers.Location);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(
            OpenIdProtocol.ErrorCodes.ServerError,
            document.RootElement.GetProperty("error").GetString()
        );
        Assert.Contains(
            "no public key",
            document.RootElement.GetProperty("error_description").GetString(),
            StringComparison.Ordinal
        );
    }

    [Fact]
    public async Task Authorize_WhenTheClientRegistersEncryption_ReturnsTheResponseAsAnEncryptedJwt()
    {
        using var factory = new AnonymousRegistrationApplicationFactory();
        var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }
        );
        using var clientRsa = RSA.Create(2048);
        var publicParameters = clientRsa.ExportParameters(includePrivateParameters: false);

        using var registerResponse = await client.PostAsJsonAsync(
            "/oauth2/register",
            new Dictionary<string, object>
            {
                ["redirect_uris"] = new[] { RedirectUri },
                ["token_endpoint_auth_method"] = OpenIdConstants
                    .ClientAuthenticationMethods
                    .ClientSecretBasic,
                ["authorization_encrypted_response_alg"] = "RSA-OAEP-256",
                ["jwks"] = new Dictionary<string, object>
                {
                    ["keys"] = new[]
                    {
                        new Dictionary<string, string>
                        {
                            ["kty"] = "RSA",
                            ["kid"] = "client-enc",
                            ["use"] = "enc",
                            ["n"] = Base64Url.EncodeToString(publicParameters.Modulus),
                            ["e"] = Base64Url.EncodeToString(publicParameters.Exponent),
                        },
                    },
                },
            }
        );
        var registerBody = await registerResponse.Content.ReadAsStringAsync();
        Assert.True(registerResponse.StatusCode == HttpStatusCode.Created, registerBody);
        using var registration = JsonDocument.Parse(registerBody);
        var clientId = registration.RootElement.GetProperty("client_id").GetString()!;

        using var response = await AuthorizeWithoutSessionAsync(
            client,
            clientId,
            "&prompt=none&response_mode=query.jwt"
        );

        Assert.Equal(HttpStatusCode.SeeOther, response.StatusCode);
        var location = response.Headers.Location;
        Assert.NotNull(location);
        var parameters = HttpUtility.ParseQueryString(location.Query);
        var responseJwt = parameters["response"];
        Assert.False(string.IsNullOrEmpty(responseJwt));

        // Signed by the tenant, then encrypted (a Nested JWT) to the client's key: only the client's private key opens it.
        var joseSerializer = factory.Services.GetRequiredService<IJoseSerializer>();
        var clientKey = factory
            .Services.GetRequiredService<ISecretKeyFactory>()
            .CreateRsa(new KeyMetadata { KeyId = "client-enc" }, clientRsa);
        var encrypted = joseSerializer.Parse(responseJwt);
        Assert.Equal(JoseProtectionTypes.Jwe, encrypted.ProtectionType);
        Assert.Equal("RSA-OAEP-256", encrypted.Header.GetProperty("alg").GetString());
        Assert.Equal("A128CBC-HS256", encrypted.Header.GetProperty("enc").GetString());

        var signed = joseSerializer.Decode(encrypted, clientKey);
        var (_, payload) = DecodeJwt(signed);
        Assert.Equal(
            OpenIdProtocol.ErrorCodes.LoginRequired,
            payload.GetProperty("error").GetString()
        );
        Assert.Equal("abc123", payload.GetProperty("state").GetString());
        Assert.Equal(clientId, payload.GetProperty("aud").GetString());
    }

    #endregion
}
