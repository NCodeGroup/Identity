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
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using NCode.Buffers;
using NCode.Identity.Jose;
using NCode.Identity.OpenId.Host;
using NCode.Identity.OpenId.IntegrationTests.Infrastructure;
using NCode.Identity.Secrets.KeyMaterial;
using NCode.Identity.Secrets.Keys;
using Xunit;

namespace NCode.Identity.OpenId.IntegrationTests.Endpoints;

public class UserInfoEndpointTests
{
    private const string UserInfoPath = "/oauth2/userinfo";
    private const string UserName = "alice";
    private const string Password = "correct-horse-battery-staple";
    private const string Email = "alice@localhost";
    private const string Scope = "openid email";

    #region Scaffolding

    private static HttpClient CreateHttpClient(WebApplicationFactory<HostApiMarker> factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    // Registers a confidential password-grant client through dynamic client registration, so the response-protection
    // metadata travels the same path a relying party's registration does.
    private static async Task<(string ClientId, string ClientSecret)> RegisterClientAsync(
        HttpClient client,
        IReadOnlyDictionary<string, object> metadata
    )
    {
        var registration = new Dictionary<string, object>(metadata)
        {
            ["grant_types"] = new[] { OpenIdProtocol.GrantTypes.Password },
            ["token_endpoint_auth_method"] = OpenIdConstants
                .ClientAuthenticationMethods
                .ClientSecretPost,
        };

        using var response = await client.PostAsJsonAsync("/oauth2/register", registration);
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.Created, body);

        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;
        return (
            root.GetProperty("client_id").GetString() ?? string.Empty,
            root.GetProperty("client_secret").GetString() ?? string.Empty
        );
    }

    private static async Task<string> RequestAccessTokenAsync(
        HttpClient client,
        string clientId,
        string clientSecret
    )
    {
        using var content = new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["grant_type"] = OpenIdProtocol.GrantTypes.Password,
                ["client_id"] = clientId,
                ["client_secret"] = clientSecret,
                ["username"] = UserName,
                ["password"] = Password,
                ["scope"] = Scope,
            }
        );

        using var response = await client.PostAsync("/oauth2/token", content);
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, body);

        using var document = JsonDocument.Parse(body);
        return document.RootElement.GetProperty("access_token").GetString() ?? string.Empty;
    }

    private static async Task<HttpResponseMessage> GetUserInfoAsync(
        HttpClient client,
        string accessToken
    )
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, UserInfoPath);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return await client.SendAsync(request);
    }

    private static JsonElement DecodeSegment(string segment) =>
        JsonElement.Parse(SecureEncoding.UTF8.GetString(Base64Url.DecodeFromChars(segment)));

    // Verifies an RS256 JWS against the tenant's published JWKS and returns its payload.
    private static async Task<JsonElement> VerifyWithTenantJwksAsync(HttpClient client, string jws)
    {
        var segments = jws.Split('.');
        Assert.Equal(3, segments.Length);
        var header = DecodeSegment(segments[0]);
        Assert.Equal("RS256", header.GetProperty("alg").GetString());
        var keyId = header.GetProperty("kid").GetString();

        using var jwksResponse = await client.GetAsync("/oauth2/jwks");
        var jwks = JsonElement.Parse(await jwksResponse.Content.ReadAsStringAsync());
        var jwk = jwks.GetProperty("keys")
            .EnumerateArray()
            .Single(key => key.GetProperty("kid").GetString() == keyId);

        using var rsa = RSA.Create(
            new RSAParameters
            {
                Modulus = Base64Url.DecodeFromChars(jwk.GetProperty("n").GetString()),
                Exponent = Base64Url.DecodeFromChars(jwk.GetProperty("e").GetString()),
            }
        );
        var signingInput = SecureEncoding.ASCII.GetBytes($"{segments[0]}.{segments[1]}");
        var signature = Base64Url.DecodeFromChars(segments[2]);
        Assert.True(
            rsa.VerifyData(
                signingInput,
                signature,
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1
            ),
            "The UserInfo JWT signature does not verify against the tenant's JWKS."
        );

        return DecodeSegment(segments[1]);
    }

    private static Dictionary<string, object> CreateEncryptionJwks(RSA rsa)
    {
        var parameters = rsa.ExportParameters(includePrivateParameters: false);
        return new Dictionary<string, object>
        {
            ["keys"] = new[]
            {
                new Dictionary<string, string>
                {
                    ["kty"] = "RSA",
                    ["kid"] = "client-enc",
                    ["use"] = "enc",
                    ["n"] = Base64Url.EncodeToString(parameters.Modulus),
                    ["e"] = Base64Url.EncodeToString(parameters.Exponent),
                },
            },
        };
    }

    #endregion

    #region HandleRouteAsync Tests

    [Fact]
    public async Task GetUserInfo_WithoutAccessToken_ReturnsUnauthorized()
    {
        using var factory = new HostApplicationFactory();
        var client = CreateHttpClient(factory);

        using var response = await client.GetAsync(UserInfoPath);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetUserInfo_WithSessionButNoAccessToken_ReturnsUnauthorized()
    {
        // UserInfo is reached only with an access token (OpenID Connect Core 1.0 §5.3.1); an authenticated session is
        // never a credential for it.
        using var factory = new HostApplicationFactory();
        using var sessionFactory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(serviceCollection =>
                serviceCollection
                    .AddAuthentication(TestSubjectAuthenticationHandler.SchemeName)
                    .AddScheme<AuthenticationSchemeOptions, TestSubjectAuthenticationHandler>(
                        TestSubjectAuthenticationHandler.SchemeName,
                        configureOptions: null
                    )
            )
        );
        var client = CreateHttpClient(sessionFactory);

        using var request = new HttpRequestMessage(HttpMethod.Get, UserInfoPath);
        request.Headers.Add(TestSubjectAuthenticationHandler.SubjectHeaderName, "alice");
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetUserInfo_WithAccessToken_ReturnsJsonClaims()
    {
        using var factory = new AnonymousRegistrationApplicationFactory();
        var client = CreateHttpClient(factory);
        await factory.SeedLocalAccountAsync(UserName, Password, Email, emailVerified: true);
        var (clientId, clientSecret) = await RegisterClientAsync(
            client,
            new Dictionary<string, object>()
        );
        var accessToken = await RequestAccessTokenAsync(client, clientId, clientSecret);

        using var response = await GetUserInfoAsync(client, accessToken);

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, body);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        var claims = JsonElement.Parse(body);
        Assert.False(string.IsNullOrEmpty(claims.GetProperty("sub").GetString()));
        Assert.Equal(Email, claims.GetProperty("email").GetString());
    }

    [Fact]
    public async Task PostUserInfo_WithAccessTokenInFormBody_ReturnsJsonClaims()
    {
        using var factory = new AnonymousRegistrationApplicationFactory();
        var client = CreateHttpClient(factory);
        await factory.SeedLocalAccountAsync(UserName, Password, Email, emailVerified: true);
        var (clientId, clientSecret) = await RegisterClientAsync(
            client,
            new Dictionary<string, object>()
        );
        var accessToken = await RequestAccessTokenAsync(client, clientId, clientSecret);

        using var content = new FormUrlEncodedContent(
            new Dictionary<string, string> { ["access_token"] = accessToken }
        );
        using var response = await client.PostAsync(UserInfoPath, content);

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, body);
        Assert.Equal(Email, JsonElement.Parse(body).GetProperty("email").GetString());
    }

    [Fact]
    public async Task GetUserInfo_WhenClientRegistersSignedResponse_ReturnsJwtSignedByTenant()
    {
        using var factory = new AnonymousRegistrationApplicationFactory();
        var client = CreateHttpClient(factory);
        await factory.SeedLocalAccountAsync(UserName, Password, Email, emailVerified: true);
        var (clientId, clientSecret) = await RegisterClientAsync(
            client,
            new Dictionary<string, object> { ["userinfo_signed_response_alg"] = "RS256" }
        );
        var accessToken = await RequestAccessTokenAsync(client, clientId, clientSecret);

        using var response = await GetUserInfoAsync(client, accessToken);

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, body);
        Assert.Equal("application/jwt", response.Content.Headers.ContentType?.MediaType);

        // OpenID Connect Core 1.0 §5.3.2: a signed UserInfo response carries the issuer and the client as audience.
        var payload = await VerifyWithTenantJwksAsync(client, body);
        Assert.Equal(clientId, payload.GetProperty("aud").GetString());
        Assert.False(string.IsNullOrEmpty(payload.GetProperty("iss").GetString()));
        Assert.False(string.IsNullOrEmpty(payload.GetProperty("sub").GetString()));
        Assert.Equal(Email, payload.GetProperty("email").GetString());
    }

    [Fact]
    public async Task GetUserInfo_WhenClientRegistersSignedAndEncryptedResponse_ReturnsNestedJwtEncryptedToClientKey()
    {
        using var factory = new AnonymousRegistrationApplicationFactory();
        var client = CreateHttpClient(factory);
        await factory.SeedLocalAccountAsync(UserName, Password, Email, emailVerified: true);
        using var clientRsa = RSA.Create(2048);
        var (clientId, clientSecret) = await RegisterClientAsync(
            client,
            new Dictionary<string, object>
            {
                ["jwks"] = CreateEncryptionJwks(clientRsa),
                ["userinfo_signed_response_alg"] = "RS256",
                ["userinfo_encrypted_response_alg"] = "RSA-OAEP-256",
                ["userinfo_encrypted_response_enc"] = "A256GCM",
            }
        );
        var accessToken = await RequestAccessTokenAsync(client, clientId, clientSecret);

        using var response = await GetUserInfoAsync(client, accessToken);

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, body);
        Assert.Equal("application/jwt", response.Content.Headers.ContentType?.MediaType);

        // Signed then encrypted (a Nested JWT): only the client's private key opens it.
        var joseSerializer = factory.Services.GetRequiredService<IJoseSerializer>();
        var secretKeyFactory = factory.Services.GetRequiredService<ISecretKeyFactory>();
        var clientKey = secretKeyFactory.CreateRsa(
            new KeyMetadata { KeyId = "client-enc" },
            clientRsa
        );
        var encrypted = joseSerializer.Parse(body);
        Assert.Equal(JoseProtectionTypes.Jwe, encrypted.ProtectionType);
        Assert.Equal("RSA-OAEP-256", encrypted.Header.GetProperty("alg").GetString());
        Assert.Equal("A256GCM", encrypted.Header.GetProperty("enc").GetString());
        Assert.Equal("JWT", encrypted.Header.GetProperty("cty").GetString());
        Assert.False(encrypted.Header.TryGetProperty("zip", out _));

        var signed = joseSerializer.Decode(encrypted, clientKey);
        var payload = await VerifyWithTenantJwksAsync(client, signed);
        Assert.Equal(clientId, payload.GetProperty("aud").GetString());
        Assert.Equal(Email, payload.GetProperty("email").GetString());
    }

    [Fact]
    public async Task PostRegister_WhenEncryptedResponseWithoutClientKeys_ReturnsInvalidClientMetadata()
    {
        // A response is encrypted to a public key the client publishes, so registering one requires its keys.
        using var factory = new AnonymousRegistrationApplicationFactory();
        var client = CreateHttpClient(factory);

        using var response = await client.PostAsJsonAsync(
            "/oauth2/register",
            new Dictionary<string, object>
            {
                ["grant_types"] = new[] { OpenIdProtocol.GrantTypes.Password },
                ["userinfo_encrypted_response_alg"] = "RSA-OAEP-256",
            }
        );

        var body = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(
            OpenIdProtocol.ErrorCodes.InvalidClientMetadata,
            JsonElement.Parse(body).GetProperty("error").GetString()
        );
    }

    [Fact]
    public async Task Discovery_AdvertisesUserInfoEndpoint()
    {
        using var factory = new HostApplicationFactory();
        var client = CreateHttpClient(factory);

        using var response = await client.GetAsync("/.well-known/openid-configuration");
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, body);

        using var document = JsonDocument.Parse(body);
        Assert.True(
            document.RootElement.TryGetProperty("userinfo_endpoint", out var endpoint),
            body
        );
        Assert.EndsWith(UserInfoPath, endpoint.GetString());
    }

    #endregion
}
