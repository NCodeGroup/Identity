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
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using NCode.Encoders;
using NCode.Identity.OpenId.IntegrationTests.Infrastructure;
using Xunit;

namespace NCode.Identity.OpenId.IntegrationTests.Endpoints;

/// <summary>
/// Verifies JWT-bearer client authentication at the token endpoint (OpenID Connect Core §9, RFC 7523). The
/// <c>client_secret_jwt</c> branch is exercised end-to-end here because its symmetric key is the seeded client secret;
/// the <c>private_key_jwt</c> branch shares the same validation pipeline, differing only in the signing-key family.
/// </summary>
public class TokenEndpointClientAssertionTests
{
    private const string ClientId = "it-assert-client";
    private const string ClientSecret = "confidential-secret-value-0123456789";
    private const string TokenPath = "/oauth2/token";

    private static HttpClient CreateClient(PlaygroundApplicationFactory factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    private static async Task<string> GetIssuerAsync(HttpClient client)
    {
        using var response = await client.GetAsync("/.well-known/openid-configuration");
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("issuer").GetString()!;
    }

    private static string BuildClientSecretJwt(
        string clientId,
        string clientSecret,
        string audience,
        string? jti = null,
        DateTimeOffset? expires = null
    )
    {
        var now = DateTimeOffset.UtcNow;
        var header = new Dictionary<string, object> { ["alg"] = "HS256", ["typ"] = "JWT" };
        var payload = new Dictionary<string, object>
        {
            ["iss"] = clientId,
            ["sub"] = clientId,
            ["aud"] = audience,
            ["jti"] = jti ?? Guid.NewGuid().ToString("N"),
            ["iat"] = now.ToUnixTimeSeconds(),
            ["exp"] = (expires ?? now.AddMinutes(5)).ToUnixTimeSeconds(),
        };

        var encodedHeader = Base64Url.Encode(JsonSerializer.SerializeToUtf8Bytes(header));
        var encodedPayload = Base64Url.Encode(JsonSerializer.SerializeToUtf8Bytes(payload));
        var signingInput = $"{encodedHeader}.{encodedPayload}";

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(clientSecret));
        var signature = hmac.ComputeHash(Encoding.ASCII.GetBytes(signingInput));

        return $"{signingInput}.{Base64Url.Encode(signature)}";
    }

    private static string BuildPrivateKeyJwt(
        string clientId,
        RSA privateKey,
        string audience,
        string? jti = null
    )
    {
        var now = DateTimeOffset.UtcNow;
        var header = new Dictionary<string, object> { ["alg"] = "RS256", ["typ"] = "JWT" };
        var payload = new Dictionary<string, object>
        {
            ["iss"] = clientId,
            ["sub"] = clientId,
            ["aud"] = audience,
            ["jti"] = jti ?? Guid.NewGuid().ToString("N"),
            ["iat"] = now.ToUnixTimeSeconds(),
            ["exp"] = now.AddMinutes(5).ToUnixTimeSeconds(),
        };

        var encodedHeader = Base64Url.Encode(JsonSerializer.SerializeToUtf8Bytes(header));
        var encodedPayload = Base64Url.Encode(JsonSerializer.SerializeToUtf8Bytes(payload));
        var signingInput = $"{encodedHeader}.{encodedPayload}";

        var signature = privateKey.SignData(
            Encoding.ASCII.GetBytes(signingInput),
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1
        );

        return $"{signingInput}.{Base64Url.Encode(signature)}";
    }

    private static FormUrlEncodedContent ClientAssertionBody(string assertion) =>
        new(
            new Dictionary<string, string>
            {
                ["grant_type"] = OpenIdConstants.GrantTypes.ClientCredentials,
                ["scope"] = TestServerSettingsProvider.ApiScope,
                ["client_assertion_type"] = OpenIdConstants.ClientAssertionTypes.JwtBearer,
                ["client_assertion"] = assertion,
            }
        );

    private static async Task<PlaygroundApplicationFactory> CreateSeededFactoryAsync()
    {
        var factory = new PlaygroundApplicationFactory();
        await factory.SeedConfidentialClientAsync(
            ClientId,
            ClientSecret,
            OpenIdConstants.GrantTypes.ClientCredentials
        );
        await factory.SeedResourceServerWithClientGrantAsync(
            ClientId,
            "it-assert-rs-api",
            "https://api.assert.integration.test",
            TestServerSettingsProvider.ApiScope
        );
        return factory;
    }

    [Fact]
    public async Task PostToken_ClientSecretJwt_ReturnsAccessToken()
    {
        using var factory = await CreateSeededFactoryAsync();
        var client = CreateClient(factory);

        var issuer = await GetIssuerAsync(client);
        var assertion = BuildClientSecretJwt(ClientId, ClientSecret, issuer);

        using var response = await client.PostAsync(TokenPath, ClientAssertionBody(assertion));

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, body);

        using var document = JsonDocument.Parse(body);
        Assert.False(
            string.IsNullOrEmpty(document.RootElement.GetProperty("access_token").GetString())
        );
    }

    [Fact]
    public async Task PostToken_ClientSecretJwt_WhenExpAtMaxNumericDate_ReturnsAccessToken()
    {
        // The replay record's retention (exp plus a skew margin) saturates instead of overflowing.
        using var factory = await CreateSeededFactoryAsync();
        var client = CreateClient(factory);

        var issuer = await GetIssuerAsync(client);
        var assertion = BuildClientSecretJwt(
            ClientId,
            ClientSecret,
            issuer,
            expires: DateTimeOffset.MaxValue
        );

        using var response = await client.PostAsync(TokenPath, ClientAssertionBody(assertion));

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, body);
    }

    [Fact]
    public async Task PostToken_ClientSecretJwt_AdvertisedInDiscovery()
    {
        using var factory = await CreateSeededFactoryAsync();
        var client = CreateClient(factory);

        using var response = await client.GetAsync("/.well-known/openid-configuration");
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        var methods = document
            .RootElement.GetProperty("token_endpoint_auth_methods_supported")
            .EnumerateArray()
            .Select(element => element.GetString())
            .ToList();

        Assert.Contains(OpenIdConstants.ClientAuthenticationMethods.ClientSecretJwt, methods);
        Assert.Contains(OpenIdConstants.ClientAuthenticationMethods.PrivateKeyJwt, methods);
    }

    [Fact]
    public async Task PostToken_ClientSecretJwt_WhenReplayed_Returns400()
    {
        using var factory = await CreateSeededFactoryAsync();
        var client = CreateClient(factory);

        var issuer = await GetIssuerAsync(client);
        var assertion = BuildClientSecretJwt(ClientId, ClientSecret, issuer);

        using var first = await client.PostAsync(TokenPath, ClientAssertionBody(assertion));
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        using var second = await client.PostAsync(TokenPath, ClientAssertionBody(assertion));
        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);

        using var document = JsonDocument.Parse(await second.Content.ReadAsStringAsync());
        Assert.Equal(
            OpenIdConstants.ErrorCodes.InvalidClient,
            document.RootElement.GetProperty("error").GetString()
        );
    }

    [Fact]
    public async Task PostToken_ClientSecretJwt_WhenWrongAudience_Returns400()
    {
        using var factory = await CreateSeededFactoryAsync();
        var client = CreateClient(factory);

        var assertion = BuildClientSecretJwt(ClientId, ClientSecret, "https://wrong.audience.test");

        using var response = await client.PostAsync(TokenPath, ClientAssertionBody(assertion));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(
            OpenIdConstants.ErrorCodes.InvalidClient,
            document.RootElement.GetProperty("error").GetString()
        );
    }

    [Fact]
    public async Task PostToken_ClientSecretJwt_WhenTampered_Returns400()
    {
        using var factory = await CreateSeededFactoryAsync();
        var client = CreateClient(factory);

        var issuer = await GetIssuerAsync(client);
        var assertion = BuildClientSecretJwt(ClientId, "the-wrong-signing-secret", issuer);

        using var response = await client.PostAsync(TokenPath, ClientAssertionBody(assertion));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(
            OpenIdConstants.ErrorCodes.InvalidClient,
            document.RootElement.GetProperty("error").GetString()
        );
    }

    [Fact]
    public async Task PostToken_PrivateKeyJwt_WithRegisteredCertificate_ReturnsAccessToken()
    {
        const string privateKeyClientId = "it-assert-pk-client";

        using var factory = new PlaygroundApplicationFactory();
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            $"CN={privateKeyClientId}",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1
        );
        using var certificate = request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow.AddYears(1)
        );

        await factory.SeedConfidentialClientWithCertificateAsync(
            privateKeyClientId,
            certificate,
            OpenIdConstants.GrantTypes.ClientCredentials
        );
        await factory.SeedResourceServerWithClientGrantAsync(
            privateKeyClientId,
            "it-assert-pk-rs-api",
            "https://api.assert-pk.integration.test",
            TestServerSettingsProvider.ApiScope
        );

        var client = CreateClient(factory);
        var issuer = await GetIssuerAsync(client);
        var assertion = BuildPrivateKeyJwt(privateKeyClientId, rsa, issuer);

        using var response = await client.PostAsync(TokenPath, ClientAssertionBody(assertion));

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, body);

        using var document = JsonDocument.Parse(body);
        Assert.False(
            string.IsNullOrEmpty(document.RootElement.GetProperty("access_token").GetString())
        );
    }
}
