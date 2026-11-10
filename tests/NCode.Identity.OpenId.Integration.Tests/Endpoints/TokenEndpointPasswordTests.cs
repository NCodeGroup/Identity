// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers.Text;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using NCode.Identity.Jose;
using NCode.Identity.OpenId.Integration.Tests.Infrastructure;
using NCode.Identity.Secrets.KeyMaterial;
using NCode.Identity.Secrets.Keys;
using Xunit;

namespace NCode.Identity.OpenId.Integration.Tests.Endpoints;

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
            [OpenIdProtocol.GrantTypes.Password],
            CancellationToken.None
        );
        await factory.SeedResourceServerWithClientGrantAsync(
            ClientId,
            "it-rs-api",
            "https://api.integration.test",
            [TestPlatformSettingsProvider.ApiScope],
            CancellationToken.None
        );
        var localAccountId = await factory.SeedLocalAccountAsync(
            UserName,
            Password,
            CancellationToken.None
        );

        var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }
        );

        var (status, body) = await RequestTokenAsync(client, CancellationToken.None);
        Assert.True(status == HttpStatusCode.OK, body);

        var jsonElement = JsonElement.Parse(body);
        var root = jsonElement;

        Assert.True(root.TryGetProperty("access_token", out var accessToken));
        Assert.False(string.IsNullOrEmpty(accessToken.GetString()));
        Assert.Equal(OpenIdProtocol.TokenTypes.Bearer, root.GetProperty("token_type").GetString());

        // The access token's subject is the stable, server-owned principal id resolved for the authenticated account —
        // never the raw local-account id or username (ADR-1035).
        var payload = DecodeJwtPayload(accessToken.GetString()!);
        var subject = payload.GetProperty("sub").GetString();
        Assert.False(string.IsNullOrEmpty(subject));
        Assert.NotEqual(localAccountId, subject);
        Assert.NotEqual(UserName, subject);

        // Re-authenticating the same account resolves to the same principal, so the subject is stable across grants.
        var (secondStatus, secondBody) = await RequestTokenAsync(client, CancellationToken.None);
        Assert.True(secondStatus == HttpStatusCode.OK, secondBody);
        var secondDocument = JsonElement.Parse(secondBody);
        var secondPayload = DecodeJwtPayload(
            secondDocument.GetProperty("access_token").GetString()!
        );
        Assert.Equal(subject, secondPayload.GetProperty("sub").GetString());
    }

    [Fact]
    public async Task PostToken_WithWrongPassword_DoesNotReturnAccessToken()
    {
        using var factory = new HostApplicationFactory();
        await factory.SeedConfidentialClientAsync(
            ClientId,
            ClientSecret,
            [OpenIdProtocol.GrantTypes.Password],
            CancellationToken.None
        );
        await factory.SeedResourceServerWithClientGrantAsync(
            ClientId,
            "it-rs-api",
            "https://api.integration.test",
            [TestPlatformSettingsProvider.ApiScope],
            CancellationToken.None
        );
        await factory.SeedLocalAccountAsync(UserName, Password, CancellationToken.None);

        var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }
        );

        using var content = new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["grant_type"] = OpenIdProtocol.GrantTypes.Password,
                ["client_id"] = ClientId,
                ["client_secret"] = ClientSecret,
                ["username"] = UserName,
                ["password"] = "wrong-password",
                ["scope"] = TestPlatformSettingsProvider.ApiScope,
            }
        );

        using var response = await client.PostAsync("/oauth2/token", content);

        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);

        var jsonElement = JsonElement.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(jsonElement.TryGetProperty("error", out _));
    }

    [Fact]
    public async Task PostToken_WhenClientRegistersEncryptedIdToken_ReturnsIdTokenEncryptedToClientKey()
    {
        using var factory = new AnonymousRegistrationApplicationFactory();
        await factory.SeedLocalAccountAsync(UserName, Password, CancellationToken.None);
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
                ["grant_types"] = new[] { OpenIdProtocol.GrantTypes.Password },
                ["token_endpoint_auth_method"] = OpenIdProtocol
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
        var registration = JsonElement.Parse(registerBody);
        var clientId = registration.GetProperty("client_id").GetString()!;
        var clientSecret = registration.GetProperty("client_secret").GetString()!;

        using var content = new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["grant_type"] = OpenIdProtocol.GrantTypes.Password,
                ["client_id"] = clientId,
                ["client_secret"] = clientSecret,
                ["username"] = UserName,
                ["password"] = Password,
                ["scope"] = OpenIdProtocol.ScopeTypes.OpenId,
            }
        );
        using var response = await client.PostAsync("/oauth2/token", content);
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, body);
        var jsonElement = JsonElement.Parse(body);
        var idToken = jsonElement.GetProperty("id_token").GetString()!;

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

        var signed = await joseSerializer.DecodeAsync(encrypted, clientKey, CancellationToken.None);
        var segments = signed.Split('.');
        Assert.Equal(3, segments.Length);
        var signedHeader = JsonElement.Parse(Base64Url.DecodeFromChars(segments[0]));
        Assert.Equal("RS256", signedHeader.GetProperty("alg").GetString());
        var payload = DecodeJwtPayload(signed);
        Assert.Equal(clientId, payload.GetProperty("aud").GetString());
    }

    private static JsonElement DecodeJwtPayload(string jwt)
    {
        var payloadSegment = jwt.Split('.')[1];
        var payloadBytes = Base64Url.DecodeFromChars(payloadSegment);
        return JsonElement.Parse(payloadBytes);
    }

    #region Scaffolding

    private static async ValueTask<(HttpStatusCode Status, string Body)> RequestTokenAsync(
        HttpClient client,
        CancellationToken cancellationToken
    )
    {
        using var content = new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["grant_type"] = OpenIdProtocol.GrantTypes.Password,
                ["client_id"] = ClientId,
                ["client_secret"] = ClientSecret,
                ["username"] = UserName,
                ["password"] = Password,
                ["scope"] = TestPlatformSettingsProvider.ApiScope,
            }
        );
        using var response = await client.PostAsync("/oauth2/token", content, cancellationToken);
        return (response.StatusCode, await response.Content.ReadAsStringAsync(cancellationToken));
    }

    #endregion
}
