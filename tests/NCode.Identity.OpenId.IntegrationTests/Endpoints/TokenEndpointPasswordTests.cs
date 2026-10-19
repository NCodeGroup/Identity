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
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using NCode.Identity.Jose;
using NCode.Identity.OpenId.IntegrationTests.Infrastructure;
using NCode.Identity.Secrets.KeyMaterial;
using NCode.Identity.Secrets.Keys;
using Xunit;

namespace NCode.Identity.OpenId.IntegrationTests.Endpoints;

public class TokenEndpointPasswordTests
{
    private const string ClientId = "it-pwd-client";
    private const string ClientSecret = "confidential-secret-value";
    private const string UserName = "alice";
    private const string Password = "correct-horse-battery-staple";

    [Fact]
    public async Task PostToken_WithResourceOwnerPassword_ReturnsAccessToken()
    {
        using var factory = new HostApplicationFactory();
        await factory.SeedConfidentialClientAsync(
            ClientId,
            ClientSecret,
            OpenIdConstants.GrantTypes.Password
        );
        await factory.SeedResourceServerWithClientGrantAsync(
            ClientId,
            "it-rs-api",
            "https://api.integration.test",
            TestServerSettingsProvider.ApiScope
        );
        var localAccountId = await factory.SeedLocalAccountAsync(UserName, Password);

        var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }
        );

        async Task<(HttpStatusCode Status, string Body)> RequestTokenAsync()
        {
            using var content = new FormUrlEncodedContent(
                new Dictionary<string, string>
                {
                    ["grant_type"] = OpenIdConstants.GrantTypes.Password,
                    ["client_id"] = ClientId,
                    ["client_secret"] = ClientSecret,
                    ["username"] = UserName,
                    ["password"] = Password,
                    ["scope"] = TestServerSettingsProvider.ApiScope,
                }
            );
            using var response = await client.PostAsync("/oauth2/token", content);
            return (response.StatusCode, await response.Content.ReadAsStringAsync());
        }

        var (status, body) = await RequestTokenAsync();
        Assert.True(status == HttpStatusCode.OK, body);

        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;

        Assert.True(root.TryGetProperty("access_token", out var accessToken));
        Assert.False(string.IsNullOrEmpty(accessToken.GetString()));
        Assert.Equal(OpenIdConstants.TokenTypes.Bearer, root.GetProperty("token_type").GetString());

        // The access token's subject is the stable, server-owned principal id resolved for the authenticated account —
        // never the raw local-account id or username (ADR-0035).
        using var payload = DecodeJwtPayload(accessToken.GetString()!);
        var subject = payload.RootElement.GetProperty("sub").GetString();
        Assert.False(string.IsNullOrEmpty(subject));
        Assert.NotEqual(localAccountId, subject);
        Assert.NotEqual(UserName, subject);

        // Re-authenticating the same account resolves to the same principal, so the subject is stable across grants.
        var (secondStatus, secondBody) = await RequestTokenAsync();
        Assert.True(secondStatus == HttpStatusCode.OK, secondBody);
        using var secondDocument = JsonDocument.Parse(secondBody);
        using var secondPayload = DecodeJwtPayload(
            secondDocument.RootElement.GetProperty("access_token").GetString()!
        );
        Assert.Equal(subject, secondPayload.RootElement.GetProperty("sub").GetString());
    }

    [Fact]
    public async Task PostToken_WithWrongPassword_DoesNotReturnAccessToken()
    {
        using var factory = new HostApplicationFactory();
        await factory.SeedConfidentialClientAsync(
            ClientId,
            ClientSecret,
            OpenIdConstants.GrantTypes.Password
        );
        await factory.SeedResourceServerWithClientGrantAsync(
            ClientId,
            "it-rs-api",
            "https://api.integration.test",
            TestServerSettingsProvider.ApiScope
        );
        await factory.SeedLocalAccountAsync(UserName, Password);

        var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }
        );

        using var content = new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["grant_type"] = OpenIdConstants.GrantTypes.Password,
                ["client_id"] = ClientId,
                ["client_secret"] = ClientSecret,
                ["username"] = UserName,
                ["password"] = "wrong-password",
                ["scope"] = TestServerSettingsProvider.ApiScope,
            }
        );

        using var response = await client.PostAsync("/oauth2/token", content);

        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(document.RootElement.TryGetProperty("error", out _));
    }

    [Fact]
    public async Task PostToken_WhenClientRegistersEncryptedIdToken_ReturnsIdTokenEncryptedToClientKey()
    {
        using var factory = new AnonymousRegistrationApplicationFactory();
        await factory.SeedLocalAccountAsync(UserName, Password);
        var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }
        );
        using var clientRsa = RSA.Create(2048);
        var publicParameters = clientRsa.ExportParameters(includePrivateParameters: false);

        // Registering an encryption algorithm without a content-encryption algorithm takes A128CBC-HS256 (OpenID Connect
        // Dynamic Client Registration 1.0 §2).
        using var registerResponse = await client.PostAsJsonAsync(
            "/oauth2/register",
            new Dictionary<string, object>
            {
                ["grant_types"] = new[] { OpenIdConstants.GrantTypes.Password },
                ["token_endpoint_auth_method"] = OpenIdConstants
                    .ClientAuthenticationMethods
                    .ClientSecretPost,
                ["id_token_encrypted_response_alg"] = "RSA-OAEP-256",
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
        var clientSecret = registration.RootElement.GetProperty("client_secret").GetString()!;

        using var content = new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["grant_type"] = OpenIdConstants.GrantTypes.Password,
                ["client_id"] = clientId,
                ["client_secret"] = clientSecret,
                ["username"] = UserName,
                ["password"] = Password,
                ["scope"] = OpenIdConstants.ScopeTypes.OpenId,
            }
        );
        using var response = await client.PostAsync("/oauth2/token", content);
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, body);
        using var document = JsonDocument.Parse(body);
        var idToken = document.RootElement.GetProperty("id_token").GetString()!;

        // Signed by the tenant, then encrypted (a Nested JWT) to the client's key: only the client's private key opens it.
        var joseSerializer = factory.Services.GetRequiredService<IJoseSerializer>();
        var clientKey = factory
            .Services.GetRequiredService<ISecretKeyFactory>()
            .CreateRsa(new KeyMetadata { KeyId = "client-enc" }, clientRsa);
        var encrypted = joseSerializer.Parse(idToken);
        Assert.Equal(JoseProtectionTypes.Jwe, encrypted.ProtectionType);
        Assert.Equal("client-enc", encrypted.Header.GetProperty("kid").GetString());
        Assert.Equal("RSA-OAEP-256", encrypted.Header.GetProperty("alg").GetString());
        Assert.Equal("A128CBC-HS256", encrypted.Header.GetProperty("enc").GetString());
        Assert.Equal("JWT", encrypted.Header.GetProperty("cty").GetString());
        Assert.False(encrypted.Header.TryGetProperty("zip", out _));

        var signed = joseSerializer.Decode(encrypted, clientKey);
        var segments = signed.Split('.');
        Assert.Equal(3, segments.Length);
        using var signedHeader = JsonDocument.Parse(Base64Url.DecodeFromChars(segments[0]));
        Assert.Equal("RS256", signedHeader.RootElement.GetProperty("alg").GetString());
        using var payload = DecodeJwtPayload(signed);
        Assert.Equal(clientId, payload.RootElement.GetProperty("aud").GetString());
    }

    private static JsonDocument DecodeJwtPayload(string jwt)
    {
        var payloadSegment = jwt.Split('.')[1];
        var payloadBytes = Base64Url.DecodeFromChars(payloadSegment);
        return JsonDocument.Parse(payloadBytes);
    }
}
