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
using NCode.Identity.Jose.Algorithms;
using NCode.Identity.Jose.Credentials;
using NCode.Identity.Jwt.Encoding;
using NCode.Identity.OpenId.IntegrationTests.Infrastructure;
using NCode.Identity.Secrets.KeyMaterial;
using NCode.Identity.Secrets.Keys;
using Xunit;

namespace NCode.Identity.OpenId.IntegrationTests.Endpoints;

/// <summary>
/// Exercises request objects (RFC 9101) at the authorization endpoint: the algorithms a request object may use are those
/// the requesting client's effective settings permit, which a client narrows by registering a
/// <c>request_object_signing_alg</c> (ADR-0155).
/// </summary>
public sealed class AuthorizationRequestObjectTests : IDisposable
{
    private const string UserName = "jar-user";
    private const string Password = "jar-password";
    private const string RedirectUri = "https://client.example/callback";
    private const string State = "jar-state";

    private RSA ClientRsa { get; } = RSA.Create(2048);

    public void Dispose() => ClientRsa.Dispose();

    #region Scaffolding

    private static HttpClient CreateClient(HostApplicationFactory factory) =>
        factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                HandleCookies = false,
            }
        );

    private async Task<string> RegisterClientAsync(HttpClient client)
    {
        var parameters = ClientRsa.ExportParameters(includePrivateParameters: false);
        using var response = await client.PostAsJsonAsync(
            "/oauth2/register",
            new Dictionary<string, object>
            {
                ["redirect_uris"] = new[] { RedirectUri },
                ["token_endpoint_auth_method"] = OpenIdConstants
                    .ClientAuthenticationMethods
                    .ClientSecretBasic,
                ["request_object_signing_alg"] = "RS256",
                ["jwks"] = new Dictionary<string, object>
                {
                    ["keys"] = new[]
                    {
                        new Dictionary<string, string>
                        {
                            ["kty"] = "RSA",
                            ["kid"] = "client-sig",
                            ["use"] = "sig",
                            ["n"] = Base64Url.EncodeToString(parameters.Modulus),
                            ["e"] = Base64Url.EncodeToString(parameters.Exponent),
                        },
                    },
                },
            }
        );

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.Created, body);

        var registration = JsonElement.Parse(body);
        Assert.Equal("RS256", registration.GetProperty("request_object_signing_alg").GetString());
        return registration.GetProperty("client_id").GetString() ?? string.Empty;
    }

    private static async Task<string> GetIssuerAsync(HttpClient client)
    {
        using var response = await client.GetAsync("/.well-known/openid-configuration");
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("issuer").GetString()!;
    }

    private string CreateRequestObject(
        HostApplicationFactory factory,
        string clientId,
        string algorithmCode,
        string? audience,
        TimeSpan? lifetime = null
    )
    {
        var now = DateTimeOffset.UtcNow;
        var algorithms = factory
            .Services.GetRequiredService<IAlgorithmCollectionProvider>()
            .Collection;
        Assert.True(algorithms.TryGetSignatureAlgorithm(algorithmCode, out var algorithm));

        var clientKey = factory
            .Services.GetRequiredService<ISecretKeyFactory>()
            .CreateRsa(new KeyMetadata { KeyId = "client-sig" }, ClientRsa);

        var encoder = factory.Services.GetRequiredService<IJwtEncoder>();
        return encoder.Encode(
            new JwtEncodingParameters
            {
                SigningCredentials = new JoseSigningCredentials(clientKey, algorithm),
                Issuer = clientId,
                Audience = audience,
                IssuedAt = now,
                NotBefore = now,
                Expires = now + (lifetime ?? TimeSpan.FromMinutes(5)),
                ExtraPayloadClaims = new Dictionary<string, object>
                {
                    ["client_id"] = clientId,
                    ["response_type"] = "code",
                    ["redirect_uri"] = RedirectUri,
                    ["scope"] = "openid",
                    ["state"] = State,
                    ["prompt"] = "none",
                    ["code_challenge"] = TestPkce.CodeChallenge,
                    ["code_challenge_method"] = "S256",
                },
            }
        );
    }

    private static async Task<HttpResponseMessage> AuthorizeAsync(
        HttpClient client,
        string sessionCookie,
        string clientId,
        string requestObject
    )
    {
        var query =
            $"?client_id={Uri.EscapeDataString(clientId)}"
            + $"&redirect_uri={Uri.EscapeDataString(RedirectUri)}"
            + "&response_type=code"
            + "&scope=openid"
            + $"&request={Uri.EscapeDataString(requestObject)}";

        using var request = new HttpRequestMessage(HttpMethod.Get, "/oauth2/authorize" + query);
        request.Headers.Add("Cookie", $"{InteractiveLogin.SessionCookieName}={sessionCookie}");
        return await client.SendAsync(request);
    }

    // The authorization endpoint redirects an error to a validated redirect URI, or renders it when it cannot.
    private static async Task<(string? Code, string? Error)> ReadResultAsync(
        HttpResponseMessage response
    )
    {
        if (response.Headers.Location is { } location)
        {
            var parameters = HttpUtility.ParseQueryString(location.Query);
            return (parameters["code"], parameters["error"]);
        }

        var body = await response.Content.ReadAsStringAsync();
        return (null, body);
    }

    #endregion

    #region Authorize Tests

    [Fact]
    public async Task Authorize_WhenRequestObjectSignedWithTheRegisteredAlgorithm_IssuesCode()
    {
        using var factory = new AnonymousRegistrationApplicationFactory();
        await factory.SeedLocalAccountAsync(UserName, Password);
        var client = CreateClient(factory);
        var clientId = await RegisterClientAsync(client);
        var sessionCookie = await InteractiveLogin.SignInAsync(client, UserName, Password);
        var issuer = await GetIssuerAsync(client);
        var requestObject = CreateRequestObject(factory, clientId, "RS256", issuer);

        using var response = await AuthorizeAsync(client, sessionCookie, clientId, requestObject);

        var (code, error) = await ReadResultAsync(response);
        Assert.True(!string.IsNullOrEmpty(code), $"error='{error}'");
    }

    [Fact]
    public async Task Authorize_WhenRequestObjectSignedWithAnotherAlgorithm_ReturnsInvalidRequestObject()
    {
        // The client registered RS256, so a request object signed with PS256 — even by its own key — is refused
        // before its signature is verified (OpenID Connect Dynamic Client Registration 1.0 §2).
        using var factory = new AnonymousRegistrationApplicationFactory();
        await factory.SeedLocalAccountAsync(UserName, Password);
        var client = CreateClient(factory);
        var clientId = await RegisterClientAsync(client);
        var sessionCookie = await InteractiveLogin.SignInAsync(client, UserName, Password);
        var issuer = await GetIssuerAsync(client);
        var requestObject = CreateRequestObject(factory, clientId, "PS256", issuer);

        using var response = await AuthorizeAsync(client, sessionCookie, clientId, requestObject);

        var (code, error) = await ReadResultAsync(response);
        Assert.True(string.IsNullOrEmpty(code));
        Assert.NotNull(error);
        Assert.Contains(
            OpenIdConstants.ErrorCodes.InvalidRequestJwt,
            error,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public async Task Authorize_WhenRequestObjectIsNotAddressedToTheIssuer_ReturnsInvalidRequestObject()
    {
        // A request object is addressed to the authorization server, so one without the issuer in its 'aud' is
        // refused (RFC 9101 §4, ADR-0187).
        using var factory = new AnonymousRegistrationApplicationFactory();
        await factory.SeedLocalAccountAsync(UserName, Password);
        var client = CreateClient(factory);
        var clientId = await RegisterClientAsync(client);
        var sessionCookie = await InteractiveLogin.SignInAsync(client, UserName, Password);
        var requestObject = CreateRequestObject(factory, clientId, "RS256", audience: null);

        using var response = await AuthorizeAsync(client, sessionCookie, clientId, requestObject);

        var (code, error) = await ReadResultAsync(response);
        Assert.True(string.IsNullOrEmpty(code));
        Assert.NotNull(error);
        Assert.Contains(
            OpenIdConstants.ErrorCodes.InvalidRequestJwt,
            error,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public async Task Authorize_WhenRequestObjectOutlivesTheMaximumLifetime_ReturnsInvalidRequestObject()
    {
        // The default request_object_max_lifetime is 60 minutes, so a request object valid for longer is refused
        // (ADR-0187).
        using var factory = new AnonymousRegistrationApplicationFactory();
        await factory.SeedLocalAccountAsync(UserName, Password);
        var client = CreateClient(factory);
        var clientId = await RegisterClientAsync(client);
        var sessionCookie = await InteractiveLogin.SignInAsync(client, UserName, Password);
        var issuer = await GetIssuerAsync(client);
        var requestObject = CreateRequestObject(
            factory,
            clientId,
            "RS256",
            issuer,
            lifetime: TimeSpan.FromMinutes(61)
        );

        using var response = await AuthorizeAsync(client, sessionCookie, clientId, requestObject);

        var (code, error) = await ReadResultAsync(response);
        Assert.True(string.IsNullOrEmpty(code));
        Assert.NotNull(error);
        Assert.Contains(
            OpenIdConstants.ErrorCodes.InvalidRequestJwt,
            error,
            StringComparison.Ordinal
        );
    }

    #endregion
}
