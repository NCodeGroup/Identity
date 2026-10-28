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
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using System.Web;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using NCode.Buffers;
using NCode.Encoders;
using NCode.Identity.OpenId.IntegrationTests.Infrastructure;
using Xunit;

namespace NCode.Identity.OpenId.IntegrationTests.Endpoints;

/// <summary>
/// Exercises the <c>fapi1-advanced</c> security profile at the pushed authorization endpoint (FAPI 1.0 Advanced): the
/// client authenticates with a PS256-signed <c>private_key_jwt</c> assertion, pushes a PS256-signed request object, and
/// must ask for a JWT-secured authorization response.
/// </summary>
public sealed class Fapi1AdvancedAuthorizationTests : IDisposable
{
    private const string UserName = "fapi1-user";
    private const string Password = "fapi1-password";
    private const string RedirectUri = "https://client.example/callback";
    private const string State = "fapi1-state";
    private const string Nonce = "fapi1-nonce";

    private RSA ClientRsa { get; } = RSA.Create(2048);

    public void Dispose() => ClientRsa.Dispose();

    #region Scaffolding

    // The developer key signs only RS256, so the host provisions the tenant its own RS256, PS256 and ES256 keys, which
    // FAPI needs to sign with PS256. The developer-key mode is read while the host registers services, before
    // configuration overlays apply, so it is supplied as a host setting.
    private sealed class ProvisionedKeysApplicationFactory : HostApplicationFactory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);

            builder.UseSetting("DeveloperKeys:Mode", "None");
        }
    }

    private static HttpClient CreateClient(HostApplicationFactory factory) =>
        factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                HandleCookies = false,
            }
        );

    private static async Task<string> GetIssuerAsync(HttpClient client)
    {
        using var response = await client.GetAsync("/.well-known/openid-configuration");
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("issuer").GetString()!;
    }

    private async Task SeedFapiClientAsync(
        HostApplicationFactory factory,
        string clientId,
        string redirectUri = RedirectUri
    )
    {
        var certificateRequest = new CertificateRequest(
            $"CN={clientId}",
            ClientRsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1
        );
        using var certificate = certificateRequest.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow.AddYears(1)
        );

        await factory.SeedPrivateKeyJwtClientWithSettingsAsync(
            clientId,
            certificate,
            new Dictionary<string, object>
            {
                ["security_profiles"] = new[] { OpenIdConstants.SecurityProfiles.Fapi1Advanced },
            },
            redirectUri
        );
    }

    private string SignPs256(
        IReadOnlyDictionary<string, object> header,
        IReadOnlyDictionary<string, object> payload
    )
    {
        var encodedHeader = Base64Url.Encode(JsonSerializer.SerializeToUtf8Bytes(header));
        var encodedPayload = Base64Url.Encode(JsonSerializer.SerializeToUtf8Bytes(payload));
        var signingInput = $"{encodedHeader}.{encodedPayload}";

        var signature = ClientRsa.SignData(
            SecureEncoding.ASCII.GetBytes(signingInput),
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pss
        );

        return $"{signingInput}.{Base64Url.Encode(signature)}";
    }

    private string CreateClientAssertion(string clientId, string issuer)
    {
        var now = DateTimeOffset.UtcNow;
        var header = new Dictionary<string, object> { ["alg"] = "PS256", ["typ"] = "JWT" };
        var payload = new Dictionary<string, object>
        {
            ["iss"] = clientId,
            ["sub"] = clientId,
            ["aud"] = issuer,
            ["jti"] = Guid.NewGuid().ToString("N"),
            ["iat"] = now.ToUnixTimeSeconds(),
            ["exp"] = now.AddMinutes(4).ToUnixTimeSeconds(),
        };

        return SignPs256(header, payload);
    }

    private string CreateRequestObject(
        string clientId,
        string issuer,
        string? responseMode,
        bool includeNotBefore = true,
        string redirectUri = RedirectUri
    )
    {
        var now = DateTimeOffset.UtcNow;
        var header = new Dictionary<string, object>
        {
            ["alg"] = "PS256",
            ["typ"] = "oauth-authz-req+jwt",
        };
        var payload = new Dictionary<string, object>
        {
            ["iss"] = clientId,
            ["aud"] = issuer,
            ["iat"] = now.ToUnixTimeSeconds(),
            ["exp"] = now.AddMinutes(5).ToUnixTimeSeconds(),
            ["client_id"] = clientId,
            ["response_type"] = "code",
            ["scope"] = "openid",
            ["redirect_uri"] = redirectUri,
            ["state"] = State,
            ["nonce"] = Nonce,
            ["prompt"] = "none",
            ["code_challenge"] = TestPkce.CodeChallenge,
            ["code_challenge_method"] = "S256",
        };
        if (includeNotBefore)
            payload["nbf"] = now.ToUnixTimeSeconds();
        if (responseMode is not null)
            payload["response_mode"] = responseMode;

        return SignPs256(header, payload);
    }

    // RFC 9126 §2: the client authenticates with its assertion and pushes the request object.
    private async Task<HttpResponseMessage> PushAsync(
        HttpClient client,
        string clientId,
        string issuer,
        string requestObject
    )
    {
        var clientAssertion = CreateClientAssertion(clientId, issuer);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/oauth2/par");
        request.Content = new FormUrlEncodedContent([
            new("client_id", clientId),
            new("client_assertion_type", OpenIdProtocol.ClientAssertionTypes.JwtBearer),
            new("client_assertion", clientAssertion),
            new("request", requestObject),
        ]);
        return await client.SendAsync(request);
    }

    private static async Task<HttpResponseMessage> AuthorizeWithRequestUriAsync(
        HttpClient client,
        string sessionCookie,
        string clientId,
        string requestUri
    )
    {
        var query =
            $"?client_id={Uri.EscapeDataString(clientId)}"
            + $"&request_uri={Uri.EscapeDataString(requestUri)}";

        using var request = new HttpRequestMessage(HttpMethod.Get, "/oauth2/authorize" + query);
        request.Headers.Add("Cookie", $"{InteractiveLogin.SessionCookieName}={sessionCookie}");
        return await client.SendAsync(request);
    }

    private static JsonElement DecodeSegment(string jwt, int index)
    {
        var segments = jwt.Split('.');
        Assert.Equal(3, segments.Length);
        return JsonElement.Parse(System.Buffers.Text.Base64Url.DecodeFromChars(segments[index]));
    }

    #endregion

    #region Pushed Authorization Tests

    [Fact]
    public async Task PushedAuthorization_WhenTheCodeResponseIsJwtSecured_ReturnsARequestUri()
    {
        const string clientId = "it-fapi1-par-jwt";

        using var factory = new ProvisionedKeysApplicationFactory();
        await SeedFapiClientAsync(factory, clientId);
        var client = CreateClient(factory);
        var issuer = await GetIssuerAsync(client);
        var requestObject = CreateRequestObject(clientId, issuer, responseMode: "jwt");

        using var response = await PushAsync(client, clientId, issuer, requestObject);

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.Created, body);
        var requestUri = JsonElement.Parse(body).GetProperty("request_uri").GetString();
        Assert.False(string.IsNullOrEmpty(requestUri), body);
    }

    [Fact]
    public async Task PushedAuthorization_WhenThePlainCodeResponseIsRequested_ReturnsInvalidRequest()
    {
        const string clientId = "it-fapi1-par-plain";

        using var factory = new ProvisionedKeysApplicationFactory();
        await SeedFapiClientAsync(factory, clientId);
        var client = CreateClient(factory);
        var issuer = await GetIssuerAsync(client);
        var requestObject = CreateRequestObject(clientId, issuer, responseMode: null);

        using var response = await PushAsync(client, clientId, issuer, requestObject);

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.BadRequest, body);
        Assert.Equal(
            OpenIdProtocol.ErrorCodes.InvalidRequest,
            JsonElement.Parse(body).GetProperty("error").GetString()
        );
    }

    [Fact]
    public async Task PushedAuthorization_WhenTheRedirectUriIsHttp_ReturnsInvalidRequest()
    {
        const string clientId = "it-fapi1-par-http";
        const string httpRedirectUri = "http://client.example/callback";

        using var factory = new ProvisionedKeysApplicationFactory();
        await SeedFapiClientAsync(factory, clientId, httpRedirectUri);
        var client = CreateClient(factory);
        var issuer = await GetIssuerAsync(client);
        var requestObject = CreateRequestObject(
            clientId,
            issuer,
            responseMode: "jwt",
            redirectUri: httpRedirectUri
        );

        using var response = await PushAsync(client, clientId, issuer, requestObject);

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.BadRequest, body);
        var json = JsonElement.Parse(body);
        Assert.Equal(
            OpenIdProtocol.ErrorCodes.InvalidRequest,
            json.GetProperty("error").GetString()
        );
        Assert.Contains(
            "kind of redirect URI",
            json.GetProperty("error_description").GetString(),
            StringComparison.Ordinal
        );
    }

    [Fact]
    public async Task PushedAuthorization_WhenTheRequestObjectHasNoNotBefore_ReturnsInvalidRequestObject()
    {
        const string clientId = "it-fapi1-par-no-nbf";

        using var factory = new ProvisionedKeysApplicationFactory();
        await SeedFapiClientAsync(factory, clientId);
        var client = CreateClient(factory);
        var issuer = await GetIssuerAsync(client);
        var requestObject = CreateRequestObject(
            clientId,
            issuer,
            responseMode: "jwt",
            includeNotBefore: false
        );

        using var response = await PushAsync(client, clientId, issuer, requestObject);

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.BadRequest, body);
        Assert.Equal(
            OpenIdProtocol.ErrorCodes.InvalidRequestJwt,
            JsonElement.Parse(body).GetProperty("error").GetString()
        );
    }

    #endregion

    #region Authorize Tests

    [Fact]
    public async Task Authorize_WhenTheCodeResponseIsJwtSecured_DeliversAResponseSignedWithPs256()
    {
        const string clientId = "it-fapi1-authorize-jwt";

        using var factory = new ProvisionedKeysApplicationFactory();
        await factory.SeedLocalAccountAsync(UserName, Password);
        await SeedFapiClientAsync(factory, clientId);
        var client = CreateClient(factory);
        var sessionCookie = await InteractiveLogin.SignInAsync(client, UserName, Password);
        var issuer = await GetIssuerAsync(client);
        var requestObject = CreateRequestObject(clientId, issuer, responseMode: "jwt");

        using var parResponse = await PushAsync(client, clientId, issuer, requestObject);
        var parBody = await parResponse.Content.ReadAsStringAsync();
        Assert.True(parResponse.StatusCode == HttpStatusCode.Created, parBody);
        var requestUri = JsonElement.Parse(parBody).GetProperty("request_uri").GetString();
        Assert.NotNull(requestUri);

        using var response = await AuthorizeWithRequestUriAsync(
            client,
            sessionCookie,
            clientId,
            requestUri
        );

        Assert.Equal(HttpStatusCode.SeeOther, response.StatusCode);
        var location = response.Headers.Location;
        Assert.NotNull(location);
        var parameters = HttpUtility.ParseQueryString(location.Query);
        var jwt = parameters["response"];
        Assert.False(string.IsNullOrEmpty(jwt), location.OriginalString);
        var header = DecodeSegment(jwt, 0);
        Assert.Equal("PS256", header.GetProperty("alg").GetString());
        var payload = DecodeSegment(jwt, 1);
        Assert.False(string.IsNullOrEmpty(payload.GetProperty("code").GetString()));
        Assert.Equal(State, payload.GetProperty("state").GetString());
        Assert.Equal(clientId, payload.GetProperty("aud").GetString());
    }

    [Fact]
    public async Task Authorize_WhenAPublicClientSelectsTheProfile_IsRefusedWithoutARedirect()
    {
        const string clientId = "it-fapi1-public";

        using var factory = new ProvisionedKeysApplicationFactory();
        await factory.SeedPublicClientWithSettingsAsync(
            clientId,
            new Dictionary<string, object>
            {
                ["security_profiles"] = new[] { OpenIdConstants.SecurityProfiles.Fapi1Advanced },
            },
            RedirectUri
        );
        var client = CreateClient(factory);
        var query =
            $"?client_id={Uri.EscapeDataString(clientId)}"
            + $"&redirect_uri={Uri.EscapeDataString(RedirectUri)}"
            + "&response_type=code"
            + "&scope=openid"
            + $"&state={State}"
            + $"&nonce={Nonce}"
            + TestPkce.QueryParameters;

        using var response = await client.GetAsync("/oauth2/authorize" + query);

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.BadRequest, body);
        Assert.Null(response.Headers.Location);
    }

    #endregion
}
