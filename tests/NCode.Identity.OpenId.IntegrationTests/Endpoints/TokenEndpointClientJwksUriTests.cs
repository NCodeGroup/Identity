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
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using NCode.Buffers;
using NCode.Encoders;
using NCode.Identity.OpenId.IntegrationTests.Infrastructure;
using Xunit;

namespace NCode.Identity.OpenId.IntegrationTests.Endpoints;

/// <summary>
/// Verifies <c>private_key_jwt</c> client authentication (OpenID Connect Core §9, RFC 7523) where the client's public
/// verification key is resolved at runtime from its <c>jwks_uri</c> rather than a stored secret (issue #54 Phase 0).
/// </summary>
public class TokenEndpointClientJwksUriTests
{
    private const string ClientId = "it-jwks-client";
    private const string JwksUri = "https://client.jwks.integration.test/jwks.json";
    private const string KeyId = "it-jwks-key-1";
    private const string TokenPath = "/oauth2/token";

    private static string BuildJwks(RSA rsa)
    {
        var parameters = rsa.ExportParameters(includePrivateParameters: false);
        var jwk = new Dictionary<string, object>
        {
            ["kty"] = "RSA",
            ["use"] = "sig",
            ["alg"] = "RS256",
            ["kid"] = KeyId,
            ["n"] = Base64Url.Encode(parameters.Modulus!),
            ["e"] = Base64Url.Encode(parameters.Exponent!),
        };
        var jwks = new Dictionary<string, object> { ["keys"] = new[] { jwk } };
        return JsonSerializer.Serialize(jwks);
    }

    private static string BuildPrivateKeyJwt(string clientId, RSA privateKey, string audience)
    {
        var now = DateTimeOffset.UtcNow;
        var header = new Dictionary<string, object>
        {
            ["alg"] = "RS256",
            ["typ"] = "JWT",
            ["kid"] = KeyId,
        };
        var payload = new Dictionary<string, object>
        {
            ["iss"] = clientId,
            ["sub"] = clientId,
            ["aud"] = audience,
            ["jti"] = Guid.NewGuid().ToString("N"),
            ["iat"] = now.ToUnixTimeSeconds(),
            ["exp"] = now.AddMinutes(5).ToUnixTimeSeconds(),
        };

        var encodedHeader = Base64Url.Encode(JsonSerializer.SerializeToUtf8Bytes(header));
        var encodedPayload = Base64Url.Encode(JsonSerializer.SerializeToUtf8Bytes(payload));
        var signingInput = $"{encodedHeader}.{encodedPayload}";

        var signature = privateKey.SignData(
            SecureEncoding.ASCII.GetBytes(signingInput),
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1
        );

        return $"{signingInput}.{Base64Url.Encode(signature)}";
    }

    private static FormUrlEncodedContent ClientAssertionBody(string assertion) =>
        new(
            new Dictionary<string, string>
            {
                ["grant_type"] = OpenIdProtocol.GrantTypes.ClientCredentials,
                ["scope"] = TestServerSettingsProvider.ApiScope,
                ["client_assertion_type"] = OpenIdProtocol.ClientAssertionTypes.JwtBearer,
                ["client_assertion"] = assertion,
            }
        );

    private static async Task<string> GetIssuerAsync(HttpClient client)
    {
        using var response = await client.GetAsync("/.well-known/openid-configuration");
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("issuer").GetString()!;
    }

    [Fact]
    public async Task PostToken_PrivateKeyJwt_WithJwksUri_ReturnsAccessToken()
    {
        using var rsa = RSA.Create(2048);
        var jwks = BuildJwks(rsa);

        using var factory = new HostApplicationFactory { ClientJwksResponder = _ => jwks };

        await factory.SeedConfidentialClientWithJwksUriAsync(
            ClientId,
            JwksUri,
            OpenIdProtocol.GrantTypes.ClientCredentials
        );
        await factory.SeedResourceServerWithClientGrantAsync(
            ClientId,
            "it-jwks-rs-api",
            "https://api.jwks.integration.test",
            TestServerSettingsProvider.ApiScope
        );

        var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }
        );
        var issuer = await GetIssuerAsync(client);
        var assertion = BuildPrivateKeyJwt(ClientId, rsa, issuer);

        using var response = await client.PostAsync(TokenPath, ClientAssertionBody(assertion));

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, body);

        using var document = JsonDocument.Parse(body);
        Assert.False(
            string.IsNullOrEmpty(document.RootElement.GetProperty("access_token").GetString())
        );
    }

    [Fact]
    public async Task PostToken_PrivateKeyJwt_WithJwksUri_WhenTampered_Returns400()
    {
        using var rsa = RSA.Create(2048);
        using var wrongKey = RSA.Create(2048);
        var jwks = BuildJwks(rsa);

        using var factory = new HostApplicationFactory { ClientJwksResponder = _ => jwks };

        await factory.SeedConfidentialClientWithJwksUriAsync(
            ClientId,
            JwksUri,
            OpenIdProtocol.GrantTypes.ClientCredentials
        );
        await factory.SeedResourceServerWithClientGrantAsync(
            ClientId,
            "it-jwks-rs-api",
            "https://api.jwks.integration.test",
            TestServerSettingsProvider.ApiScope
        );

        var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }
        );
        var issuer = await GetIssuerAsync(client);

        // Signed by a key whose public half is not published at the jwks_uri.
        var assertion = BuildPrivateKeyJwt(ClientId, wrongKey, issuer);

        using var response = await client.PostAsync(TokenPath, ClientAssertionBody(assertion));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(
            OpenIdProtocol.ErrorCodes.InvalidClient,
            document.RootElement.GetProperty("error").GetString()
        );
    }
}
