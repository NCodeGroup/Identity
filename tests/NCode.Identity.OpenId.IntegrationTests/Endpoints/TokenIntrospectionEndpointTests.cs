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
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using NCode.Identity.OpenId.IntegrationTests.Infrastructure;
using Xunit;

namespace NCode.Identity.OpenId.IntegrationTests.Endpoints;

/// <summary>
/// Drives token introspection (RFC 7662, RFC 9701) the way a deployment uses it: an operator creates a client that acts
/// for a resource server through the management API, and that client learns about the access tokens issued for its
/// resource server — and only those, narrowed to it — including tokens the server encrypted.
/// </summary>
public class TokenIntrospectionEndpointTests
{
    private const string IssuingClientId = "it-introspect-client";
    private const string ClientSecret = "confidential-secret-value";
    private const string OrdersResourceServerId = "it-orders-rs";
    private const string OrdersAudience = "https://orders.introspect.test";
    private const string OrdersScope = "orders:read";
    private const string BillingResourceServerId = "it-billing-rs";
    private const string BillingAudience = "https://billing.introspect.test";
    private const string BillingScope = "billing:read";
    private const string BootstrapClientId = "it-bootstrap-admin";
    private const string BootstrapClientSecret = "bootstrap-secret-value";
    private const string ManagementScope = "read:clients";
    private const string JwtResponseMediaType = "application/token-introspection+jwt";
    private const string IntrospectPath = "/oauth2/introspect";
    private const string TokenPath = "/oauth2/token";
    private const string TenantApiPath = $"/api/tenants/{HostApplicationFactory.TenantId}";

    private static IReadOnlyDictionary<string, object> NoSettings { get; } =
        new Dictionary<string, object>();

    private static IReadOnlyDictionary<string, object> EncryptionRequired { get; } =
        new Dictionary<string, object> { ["access_token_encryption_required"] = true };

    #region Scaffolding

    // The tenant's provisioned keys sign and encrypt its tokens, and the bootstrap administrator creates the clients that
    // act for resource servers through the management API.
    private static async Task<HostApplicationFactory> CreateFactoryAsync(
        IReadOnlyDictionary<string, object> tenantSettings
    )
    {
        var factory = new ProvisionedKeysHostApplicationFactory();
        await factory.SeedTenantSettingsAsync(tenantSettings);
        await factory.SeedConfidentialClientAsync(IssuingClientId, ClientSecret);
        return factory;
    }

    private static HttpClient CreateHttpClient(HostApplicationFactory factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    private static async Task<HttpClient> CreateAdministratorClientAsync(
        HostApplicationFactory factory
    )
    {
        var client = CreateHttpClient(factory);
        var credentials = Encoding.UTF8.GetBytes($"{BootstrapClientId}:{BootstrapClientSecret}");
        using var tokenRequest = new HttpRequestMessage(HttpMethod.Post, TokenPath)
        {
            Headers =
            {
                Authorization = new AuthenticationHeaderValue(
                    "Basic",
                    Convert.ToBase64String(credentials)
                ),
            },
            Content = new FormUrlEncodedContent(
                new Dictionary<string, string>
                {
                    ["grant_type"] = OpenIdConstants.GrantTypes.ClientCredentials,
                    ["scope"] = ManagementScope,
                }
            ),
        };

        var accessToken = await RequestAccessTokenAsync(client, tokenRequest);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            accessToken
        );
        return client;
    }

    private static async Task<string> RequestAccessTokenAsync(
        HttpClient client,
        HttpRequestMessage tokenRequest
    )
    {
        using var response = await client.SendAsync(tokenRequest);
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, body);

        using var document = JsonDocument.Parse(body);
        var accessToken = document.RootElement.GetProperty("access_token").GetString();
        Assert.False(string.IsNullOrEmpty(accessToken));
        return accessToken;
    }

    private static async Task<string> IssueAccessTokenAsync(HttpClient client, string scope)
    {
        using var tokenRequest = new HttpRequestMessage(HttpMethod.Post, TokenPath)
        {
            Content = new FormUrlEncodedContent(
                new Dictionary<string, string>
                {
                    ["grant_type"] = OpenIdConstants.GrantTypes.ClientCredentials,
                    ["client_id"] = IssuingClientId,
                    ["client_secret"] = ClientSecret,
                    ["scope"] = scope,
                }
            ),
        };
        return await RequestAccessTokenAsync(client, tokenRequest);
    }

    // The client acts for the resource server and authenticates with a secret the management API reveals once.
    private static async Task<(
        string ClientId,
        string ClientSecret
    )> CreateResourceServerClientAsync(
        HttpClient administrator,
        string resourceServerId,
        IReadOnlyDictionary<string, object>? extraSettings = null
    )
    {
        var settings = new Dictionary<string, object>
        {
            ["token_endpoint_auth_method"] = OpenIdConstants
                .ClientAuthenticationMethods
                .ClientSecretPost,
        };
        foreach (var (name, value) in extraSettings ?? NoSettings)
        {
            settings[name] = value;
        }

        using var createResponse = await administrator.PostAsJsonAsync(
            $"{TenantApiPath}/clients",
            new
            {
                isDisabled = false,
                resourceServerId,
                settings,
            }
        );
        var createBody = await createResponse.Content.ReadAsStringAsync();
        Assert.True(createResponse.StatusCode == HttpStatusCode.Created, createBody);

        using var created = JsonDocument.Parse(createBody);
        Assert.Equal(
            resourceServerId,
            created.RootElement.GetProperty("resourceServerId").GetString()
        );
        var clientId = created.RootElement.GetProperty("clientId").GetString();
        Assert.False(string.IsNullOrEmpty(clientId));

        using var secretResponse = await administrator.PostAsJsonAsync(
            $"{TenantApiPath}/clients/{clientId}/secrets",
            new
            {
                secretType = "symmetric",
                keySizeBits = 256,
                expiresWhen = DateTimeOffset.UtcNow.AddYears(1),
            }
        );
        var secretBody = await secretResponse.Content.ReadAsStringAsync();
        Assert.True(secretResponse.StatusCode == HttpStatusCode.Created, secretBody);

        using var secret = JsonDocument.Parse(secretBody);
        var secretMaterial = secret.RootElement.GetProperty("secretMaterial").GetString();
        Assert.False(string.IsNullOrEmpty(secretMaterial));

        return (clientId, secretMaterial);
    }

    private static async Task<HttpResponseMessage> IntrospectAsync(
        HttpClient client,
        string clientId,
        string clientSecret,
        string token,
        string? accept = null
    )
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, IntrospectPath)
        {
            Content = new FormUrlEncodedContent(
                new Dictionary<string, string>
                {
                    ["client_id"] = clientId,
                    ["client_secret"] = clientSecret,
                    ["token"] = token,
                }
            ),
        };
        if (accept is not null)
        {
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(accept));
        }

        return await client.SendAsync(request);
    }

    private static async Task<JsonElement> IntrospectJsonAsync(
        HttpClient client,
        string clientId,
        string clientSecret,
        string token
    )
    {
        using var response = await IntrospectAsync(client, clientId, clientSecret, token);
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, body);
        return JsonElement.Parse(body);
    }

    private static JsonElement DecodeSegment(string jwt, int index) =>
        JsonElement.Parse(Base64Url.DecodeFromChars(jwt.Split('.')[index]));

    #endregion

    #region Caller and request Tests

    [Fact]
    public async Task PostIntrospect_WithUnknownToken_ReturnsInactive()
    {
        using var factory = await CreateFactoryAsync(NoSettings);
        using var client = CreateHttpClient(factory);

        var root = await IntrospectJsonAsync(
            client,
            IssuingClientId,
            ClientSecret,
            "not-a-real-token"
        );

        Assert.False(root.GetProperty("active").GetBoolean());
    }

    [Fact]
    public async Task PostIntrospect_WithoutToken_ReturnsInvalidRequest()
    {
        using var factory = await CreateFactoryAsync(NoSettings);
        using var client = CreateHttpClient(factory);

        using var content = new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["client_id"] = IssuingClientId,
                ["client_secret"] = ClientSecret,
            }
        );
        using var response = await client.PostAsync(IntrospectPath, content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("invalid_request", document.RootElement.GetProperty("error").GetString());
    }

    [Fact]
    public async Task PostIntrospect_WithoutClientAuthentication_IsRejected()
    {
        using var factory = await CreateFactoryAsync(NoSettings);
        using var client = CreateHttpClient(factory);

        using var content = new FormUrlEncodedContent(
            new Dictionary<string, string> { ["token"] = "not-a-real-token" }
        );
        using var response = await client.PostAsync(IntrospectPath, content);

        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
    }

    #endregion

    #region Disclosure Tests

    [Fact]
    public async Task PostIntrospect_ByAClientActingForAnAudience_ReportsTheTokenNarrowedToIt()
    {
        using var factory = await CreateFactoryAsync(NoSettings);
        await factory.SeedResourceServerWithClientGrantAsync(
            IssuingClientId,
            OrdersResourceServerId,
            OrdersAudience,
            OrdersScope
        );
        await factory.SeedResourceServerWithClientGrantAsync(
            IssuingClientId,
            BillingResourceServerId,
            BillingAudience,
            BillingScope
        );
        using var administrator = await CreateAdministratorClientAsync(factory);
        var (clientId, clientSecret) = await CreateResourceServerClientAsync(
            administrator,
            OrdersResourceServerId
        );
        using var client = CreateHttpClient(factory);
        var accessToken = await IssueAccessTokenAsync(client, $"{OrdersScope} {BillingScope}");

        var root = await IntrospectJsonAsync(client, clientId, clientSecret, accessToken);

        Assert.True(root.GetProperty("active").GetBoolean());
        Assert.Equal(OrdersAudience, root.GetProperty("aud").GetString());
        Assert.Equal(OrdersScope, root.GetProperty("scope").GetString());
        Assert.Equal(IssuingClientId, root.GetProperty("client_id").GetString());
        Assert.Equal(HostApplicationFactory.TestServerIssuer, root.GetProperty("iss").GetString());
    }

    [Fact]
    public async Task PostIntrospect_ByTheClientTheTokenWasIssuedTo_ReportsItInactive()
    {
        using var factory = await CreateFactoryAsync(NoSettings);
        await factory.SeedResourceServerWithClientGrantAsync(
            IssuingClientId,
            OrdersResourceServerId,
            OrdersAudience,
            OrdersScope
        );
        using var client = CreateHttpClient(factory);
        var accessToken = await IssueAccessTokenAsync(client, OrdersScope);

        var root = await IntrospectJsonAsync(client, IssuingClientId, ClientSecret, accessToken);

        Assert.False(root.GetProperty("active").GetBoolean());
        Assert.Single(root.EnumerateObject());
    }

    [Fact]
    public async Task PostIntrospect_ByAClientActingForAnotherResourceServer_ReportsItInactive()
    {
        using var factory = await CreateFactoryAsync(NoSettings);
        await factory.SeedResourceServerWithClientGrantAsync(
            IssuingClientId,
            OrdersResourceServerId,
            OrdersAudience,
            OrdersScope
        );
        await factory.SeedResourceServerWithClientGrantAsync(
            IssuingClientId,
            BillingResourceServerId,
            BillingAudience,
            BillingScope
        );
        using var administrator = await CreateAdministratorClientAsync(factory);
        var (clientId, clientSecret) = await CreateResourceServerClientAsync(
            administrator,
            BillingResourceServerId
        );
        using var client = CreateHttpClient(factory);
        var accessToken = await IssueAccessTokenAsync(client, OrdersScope);

        var root = await IntrospectJsonAsync(client, clientId, clientSecret, accessToken);

        Assert.False(root.GetProperty("active").GetBoolean());
    }

    [Fact]
    public async Task PostToken_ByAClientActingForAResourceServer_IsRefused()
    {
        using var factory = await CreateFactoryAsync(NoSettings);
        await factory.SeedResourceServerWithClientGrantAsync(
            IssuingClientId,
            OrdersResourceServerId,
            OrdersAudience,
            OrdersScope
        );
        using var administrator = await CreateAdministratorClientAsync(factory);
        var (clientId, clientSecret) = await CreateResourceServerClientAsync(
            administrator,
            OrdersResourceServerId
        );
        using var client = CreateHttpClient(factory);

        using var content = new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["grant_type"] = OpenIdConstants.GrantTypes.ClientCredentials,
                ["client_id"] = clientId,
                ["client_secret"] = clientSecret,
            }
        );
        using var response = await client.PostAsync(TokenPath, content);

        var body = await response.Content.ReadAsStringAsync();
        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(body);
        Assert.Equal("invalid_client", document.RootElement.GetProperty("error").GetString());
    }

    [Fact]
    public async Task PostIntrospect_ByAPublicClientActingForAResourceServer_IsRefused()
    {
        using var factory = await CreateFactoryAsync(NoSettings);
        await factory.SeedResourceServerWithClientGrantAsync(
            IssuingClientId,
            OrdersResourceServerId,
            OrdersAudience,
            OrdersScope
        );
        using var administrator = await CreateAdministratorClientAsync(factory);
        using var createResponse = await administrator.PostAsJsonAsync(
            $"{TenantApiPath}/clients",
            new
            {
                isDisabled = false,
                resourceServerId = OrdersResourceServerId,
                settings = new Dictionary<string, object>
                {
                    ["token_endpoint_auth_method"] = OpenIdConstants
                        .ClientAuthenticationMethods
                        .None,
                },
            }
        );
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        using var created = JsonDocument.Parse(await createResponse.Content.ReadAsStringAsync());
        var clientId = created.RootElement.GetProperty("clientId").GetString();
        using var client = CreateHttpClient(factory);

        // A resource server must authenticate to learn about its tokens (RFC 9701 §4).
        using var content = new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["client_id"] = clientId ?? string.Empty,
                ["token"] = "not-a-real-token",
            }
        );
        using var response = await client.PostAsync(IntrospectPath, content);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var document = JsonDocument.Parse(body);
        Assert.Equal("invalid_client", document.RootElement.GetProperty("error").GetString());
    }

    [Fact]
    public async Task PostIntrospect_WhenTheResourceServerIsDisabled_RefusesTheClientActingForIt()
    {
        using var factory = await CreateFactoryAsync(NoSettings);
        await factory.SeedResourceServerWithClientGrantAsync(
            IssuingClientId,
            OrdersResourceServerId,
            OrdersAudience,
            OrdersScope
        );
        using var administrator = await CreateAdministratorClientAsync(factory);
        var (clientId, clientSecret) = await CreateResourceServerClientAsync(
            administrator,
            OrdersResourceServerId
        );
        using var patch = new HttpRequestMessage(
            HttpMethod.Patch,
            $"{TenantApiPath}/resource-servers/{OrdersResourceServerId}"
        )
        {
            Content = new StringContent(
                """[{ "op": "replace", "path": "/isDisabled", "value": true }]""",
                Encoding.UTF8,
                "application/json-patch+json"
            ),
        };
        using var patchResponse = await administrator.SendAsync(patch);
        var patchBody = await patchResponse.Content.ReadAsStringAsync();
        Assert.True(patchResponse.StatusCode == HttpStatusCode.NoContent, patchBody);
        using var client = CreateHttpClient(factory);

        using var response = await IntrospectAsync(
            client,
            clientId,
            clientSecret,
            "not-a-real-token"
        );

        var body = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var document = JsonDocument.Parse(body);
        Assert.Equal("invalid_client", document.RootElement.GetProperty("error").GetString());
    }

    #endregion

    #region Encrypted access token Tests

    [Fact]
    public async Task Server_WhenTheTenantEncryptsAccessTokens_ReadsThemAtTheManagementApiAndIntrospection()
    {
        using var factory = await CreateFactoryAsync(EncryptionRequired);
        await factory.SeedResourceServerWithClientGrantAsync(
            IssuingClientId,
            OrdersResourceServerId,
            OrdersAudience,
            OrdersScope
        );

        // The management token itself is encrypted to the tenant, and the management API reads it.
        using var administrator = await CreateAdministratorClientAsync(factory);
        var managementToken = administrator.DefaultRequestHeaders.Authorization?.Parameter;
        Assert.Equal(5, managementToken?.Split('.').Length);

        var (clientId, clientSecret) = await CreateResourceServerClientAsync(
            administrator,
            OrdersResourceServerId
        );
        using var client = CreateHttpClient(factory);
        var accessToken = await IssueAccessTokenAsync(client, OrdersScope);
        Assert.Equal(5, accessToken.Split('.').Length);

        var root = await IntrospectJsonAsync(client, clientId, clientSecret, accessToken);

        Assert.True(root.GetProperty("active").GetBoolean());
        Assert.Equal(OrdersAudience, root.GetProperty("aud").GetString());
    }

    [Fact]
    public async Task PostIntrospect_WhenTheResourceServerDecryptsItsOwnTokens_TheClientActingForItReadsThem()
    {
        using var factory = await CreateFactoryAsync(NoSettings);
        await factory.SeedResourceServerWithClientGrantAsync(
            IssuingClientId,
            OrdersResourceServerId,
            OrdersAudience,
            EncryptionRequired,
            OrdersScope
        );
        using var administrator = await CreateAdministratorClientAsync(factory);
        using var keyResponse = await administrator.PostAsJsonAsync(
            $"{TenantApiPath}/resource-servers/{OrdersResourceServerId}/secrets",
            new
            {
                secretType = "rsa",
                keySizeBits = 2048,
                use = "enc",
                expiresWhen = DateTimeOffset.UtcNow.AddYears(1),
            }
        );
        Assert.Equal(HttpStatusCode.Created, keyResponse.StatusCode);
        using var key = JsonDocument.Parse(await keyResponse.Content.ReadAsStringAsync());
        var resourceServerKeyId = key.RootElement.GetProperty("secretId").GetString();

        var (clientId, clientSecret) = await CreateResourceServerClientAsync(
            administrator,
            OrdersResourceServerId
        );
        using var client = CreateHttpClient(factory);
        var accessToken = await IssueAccessTokenAsync(client, OrdersScope);
        Assert.Equal(
            resourceServerKeyId,
            DecodeSegment(accessToken, 0).GetProperty("kid").GetString()
        );

        var root = await IntrospectJsonAsync(client, clientId, clientSecret, accessToken);

        Assert.True(root.GetProperty("active").GetBoolean());
    }

    #endregion

    #region JWT response Tests

    [Fact]
    public async Task PostIntrospect_AsJwt_ReturnsASignedTokenIntrospectionResponse()
    {
        using var factory = await CreateFactoryAsync(NoSettings);
        await factory.SeedResourceServerWithClientGrantAsync(
            IssuingClientId,
            OrdersResourceServerId,
            OrdersAudience,
            OrdersScope
        );
        using var administrator = await CreateAdministratorClientAsync(factory);
        var (clientId, clientSecret) = await CreateResourceServerClientAsync(
            administrator,
            OrdersResourceServerId
        );
        using var client = CreateHttpClient(factory);
        var accessToken = await IssueAccessTokenAsync(client, OrdersScope);

        using var response = await IntrospectAsync(
            client,
            clientId,
            clientSecret,
            accessToken,
            JwtResponseMediaType
        );

        var jwt = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, jwt);
        Assert.Equal(JwtResponseMediaType, response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(3, jwt.Split('.').Length);

        var header = DecodeSegment(jwt, 0);
        Assert.Equal("token-introspection+jwt", header.GetProperty("typ").GetString());
        Assert.Equal("RS256", header.GetProperty("alg").GetString());

        var payload = DecodeSegment(jwt, 1);
        Assert.Equal(
            HostApplicationFactory.TestServerIssuer,
            payload.GetProperty("iss").GetString()
        );
        Assert.Equal(OrdersAudience, payload.GetProperty("aud").GetString());
        Assert.True(payload.TryGetProperty("iat", out _));
        Assert.False(payload.TryGetProperty("exp", out _));
        Assert.False(payload.TryGetProperty("sub", out _));

        var introspection = payload.GetProperty("token_introspection");
        Assert.True(introspection.GetProperty("active").GetBoolean());
        Assert.Equal(OrdersAudience, introspection.GetProperty("aud").GetString());
    }

    [Fact]
    public async Task PostIntrospect_AsJwt_ByAnOrdinaryClient_IsNotAcceptable()
    {
        using var factory = await CreateFactoryAsync(NoSettings);
        using var client = CreateHttpClient(factory);

        using var response = await IntrospectAsync(
            client,
            IssuingClientId,
            ClientSecret,
            "not-a-real-token",
            JwtResponseMediaType
        );

        Assert.Equal(HttpStatusCode.NotAcceptable, response.StatusCode);
    }

    [Fact]
    public async Task PostIntrospect_WhenTheClientRegistersEncryptedResponses_EncryptsTheJwtToItsKey()
    {
        using var factory = await CreateFactoryAsync(NoSettings);
        await factory.SeedResourceServerWithClientGrantAsync(
            IssuingClientId,
            OrdersResourceServerId,
            OrdersAudience,
            OrdersScope
        );
        using var administrator = await CreateAdministratorClientAsync(factory);
        var (clientId, clientSecret) = await CreateResourceServerClientAsync(
            administrator,
            OrdersResourceServerId,
            new Dictionary<string, object>
            {
                ["introspection_encrypted_response_alg"] = "RSA-OAEP-256",
            }
        );

        using var rsa = RSA.Create(2048);
        using var importResponse = await administrator.PostAsJsonAsync(
            $"{TenantApiPath}/clients/{clientId}/secrets/import",
            new
            {
                secretType = "rsa",
                format = "pem",
                keyMaterial = rsa.ExportSubjectPublicKeyInfoPem(),
                use = "enc",
                expiresWhen = DateTimeOffset.UtcNow.AddYears(1),
            }
        );
        var importBody = await importResponse.Content.ReadAsStringAsync();
        Assert.True(importResponse.StatusCode == HttpStatusCode.Created, importBody);

        using var client = CreateHttpClient(factory);
        var accessToken = await IssueAccessTokenAsync(client, OrdersScope);

        // A client that registered encrypted responses never receives one in the clear.
        using var jsonResponse = await IntrospectAsync(client, clientId, clientSecret, accessToken);
        Assert.Equal(HttpStatusCode.NotAcceptable, jsonResponse.StatusCode);

        using var response = await IntrospectAsync(
            client,
            clientId,
            clientSecret,
            accessToken,
            JwtResponseMediaType
        );

        var jwt = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, jwt);
        Assert.Equal(5, jwt.Split('.').Length);

        var header = DecodeSegment(jwt, 0);
        Assert.Equal("RSA-OAEP-256", header.GetProperty("alg").GetString());
        Assert.Equal("JWT", header.GetProperty("cty").GetString());
    }

    #endregion

    #region Dynamic Registration Tests

    private static async Task<string> MintInitialAccessTokenAsync(
        HttpClient administrator,
        string resourceServerId
    )
    {
        using var response = await administrator.PostAsJsonAsync(
            $"{TenantApiPath}/grants/initial-access-tokens",
            new { resourceServerId }
        );
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.Created, body);

        using var document = JsonDocument.Parse(body);
        Assert.Equal(
            resourceServerId,
            document.RootElement.GetProperty("resourceServerId").GetString()
        );
        var initialAccessToken = document.RootElement.GetProperty("initialAccessToken").GetString();
        Assert.False(string.IsNullOrEmpty(initialAccessToken));
        return initialAccessToken;
    }

    private static async Task<HttpResponseMessage> RegisterAsync(
        HttpClient client,
        string initialAccessToken,
        string tokenEndpointAuthMethod,
        IReadOnlyDictionary<string, object>? extraMetadata = null
    )
    {
        var metadata = new Dictionary<string, object>
        {
            ["grant_types"] = new[] { OpenIdConstants.GrantTypes.ClientCredentials },
            ["token_endpoint_auth_method"] = tokenEndpointAuthMethod,
            // The binding comes from the initial access token alone; a request member is ignored.
            ["resource_server_id"] = BillingResourceServerId,
        };
        foreach (var (name, value) in extraMetadata ?? NoSettings)
        {
            metadata[name] = value;
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, "/oauth2/register")
        {
            Headers =
            {
                Authorization = new AuthenticationHeaderValue("Bearer", initialAccessToken),
            },
            Content = JsonContent.Create(metadata),
        };
        return await client.SendAsync(request);
    }

    [Fact]
    public async Task PostRegister_WithATokenScopedToAResourceServer_RegistersAClientThatIntrospectsForIt()
    {
        using var factory = await CreateFactoryAsync(NoSettings);
        await factory.SeedResourceServerWithClientGrantAsync(
            IssuingClientId,
            OrdersResourceServerId,
            OrdersAudience,
            OrdersScope
        );
        await factory.SeedResourceServerWithClientGrantAsync(
            IssuingClientId,
            BillingResourceServerId,
            BillingAudience,
            BillingScope
        );
        using var administrator = await CreateAdministratorClientAsync(factory);
        var initialAccessToken = await MintInitialAccessTokenAsync(
            administrator,
            OrdersResourceServerId
        );
        using var client = CreateHttpClient(factory);

        using var registrationResponse = await RegisterAsync(
            client,
            initialAccessToken,
            OpenIdConstants.ClientAuthenticationMethods.ClientSecretPost
        );

        var registrationBody = await registrationResponse.Content.ReadAsStringAsync();
        Assert.True(registrationResponse.StatusCode == HttpStatusCode.Created, registrationBody);
        using var registration = JsonDocument.Parse(registrationBody);
        Assert.Equal(
            OrdersResourceServerId,
            registration.RootElement.GetProperty("resource_server_id").GetString()
        );
        var clientId = registration.RootElement.GetProperty("client_id").GetString();
        var clientSecret = registration.RootElement.GetProperty("client_secret").GetString();
        Assert.False(string.IsNullOrEmpty(clientId));
        Assert.False(string.IsNullOrEmpty(clientSecret));

        var accessToken = await IssueAccessTokenAsync(client, $"{OrdersScope} {BillingScope}");
        var root = await IntrospectJsonAsync(client, clientId, clientSecret, accessToken);

        Assert.True(root.GetProperty("active").GetBoolean());
        Assert.Equal(OrdersAudience, root.GetProperty("aud").GetString());
        Assert.Equal(OrdersScope, root.GetProperty("scope").GetString());
    }

    [Fact]
    public async Task PostRegister_WithIntrospectionEncryption_EncryptsTheJwtToTheRegisteredKey()
    {
        using var factory = await CreateFactoryAsync(NoSettings);
        await factory.SeedResourceServerWithClientGrantAsync(
            IssuingClientId,
            OrdersResourceServerId,
            OrdersAudience,
            OrdersScope
        );
        using var administrator = await CreateAdministratorClientAsync(factory);
        var initialAccessToken = await MintInitialAccessTokenAsync(
            administrator,
            OrdersResourceServerId
        );
        using var client = CreateHttpClient(factory);
        using var rsa = RSA.Create(2048);
        var parameters = rsa.ExportParameters(includePrivateParameters: false);

        using var registrationResponse = await RegisterAsync(
            client,
            initialAccessToken,
            OpenIdConstants.ClientAuthenticationMethods.ClientSecretPost,
            new Dictionary<string, object>
            {
                ["introspection_signed_response_alg"] = "RS256",
                ["introspection_encrypted_response_alg"] = "RSA-OAEP-256",
                ["jwks"] = new Dictionary<string, object>
                {
                    ["keys"] = new[]
                    {
                        new Dictionary<string, object>
                        {
                            ["kty"] = "RSA",
                            ["use"] = "enc",
                            ["alg"] = "RSA-OAEP-256",
                            ["kid"] = "it-introspection-enc",
                            ["n"] = Base64Url.EncodeToString(parameters.Modulus),
                            ["e"] = Base64Url.EncodeToString(parameters.Exponent),
                        },
                    },
                },
            }
        );

        var registrationBody = await registrationResponse.Content.ReadAsStringAsync();
        Assert.True(registrationResponse.StatusCode == HttpStatusCode.Created, registrationBody);
        using var registration = JsonDocument.Parse(registrationBody);
        var root = registration.RootElement;
        Assert.Equal("RS256", root.GetProperty("introspection_signed_response_alg").GetString());
        Assert.Equal(
            "RSA-OAEP-256",
            root.GetProperty("introspection_encrypted_response_alg").GetString()
        );
        var clientId = root.GetProperty("client_id").GetString();
        var clientSecret = root.GetProperty("client_secret").GetString();
        Assert.False(string.IsNullOrEmpty(clientId));
        Assert.False(string.IsNullOrEmpty(clientSecret));

        var accessToken = await IssueAccessTokenAsync(client, OrdersScope);
        using var response = await IntrospectAsync(
            client,
            clientId,
            clientSecret,
            accessToken,
            JwtResponseMediaType
        );

        var jwt = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, jwt);
        Assert.Equal(5, jwt.Split('.').Length);
        var header = DecodeSegment(jwt, 0);
        Assert.Equal("RSA-OAEP-256", header.GetProperty("alg").GetString());
        Assert.Equal("it-introspection-enc", header.GetProperty("kid").GetString());
    }

    [Fact]
    public async Task PostRegister_WithATokenScopedToAResourceServer_RefusesAPublicClient()
    {
        using var factory = await CreateFactoryAsync(NoSettings);
        await factory.SeedResourceServerWithClientGrantAsync(
            IssuingClientId,
            OrdersResourceServerId,
            OrdersAudience,
            OrdersScope
        );
        using var administrator = await CreateAdministratorClientAsync(factory);
        var initialAccessToken = await MintInitialAccessTokenAsync(
            administrator,
            OrdersResourceServerId
        );
        using var client = CreateHttpClient(factory);

        using var response = await RegisterAsync(
            client,
            initialAccessToken,
            OpenIdConstants.ClientAuthenticationMethods.None
        );

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.BadRequest, body);
        using var document = JsonDocument.Parse(body);
        Assert.Equal(
            OpenIdConstants.ErrorCodes.InvalidClientMetadata,
            document.RootElement.GetProperty("error").GetString()
        );
    }

    [Fact]
    public async Task PostRegister_WithAnInitialAccessTokenAlreadySpent_ReturnsUnauthorized()
    {
        using var factory = await CreateFactoryAsync(NoSettings);
        await factory.SeedResourceServerWithClientGrantAsync(
            IssuingClientId,
            OrdersResourceServerId,
            OrdersAudience,
            OrdersScope
        );
        using var administrator = await CreateAdministratorClientAsync(factory);
        var initialAccessToken = await MintInitialAccessTokenAsync(
            administrator,
            OrdersResourceServerId
        );
        using var client = CreateHttpClient(factory);
        using var firstResponse = await RegisterAsync(
            client,
            initialAccessToken,
            OpenIdConstants.ClientAuthenticationMethods.ClientSecretPost
        );
        var firstBody = await firstResponse.Content.ReadAsStringAsync();
        Assert.True(firstResponse.StatusCode == HttpStatusCode.Created, firstBody);

        using var secondResponse = await RegisterAsync(
            client,
            initialAccessToken,
            OpenIdConstants.ClientAuthenticationMethods.ClientSecretPost
        );

        var secondBody = await secondResponse.Content.ReadAsStringAsync();
        Assert.True(secondResponse.StatusCode == HttpStatusCode.Unauthorized, secondBody);
        using var document = JsonDocument.Parse(secondBody);
        Assert.Equal(
            OpenIdConstants.ErrorCodes.InvalidToken,
            document.RootElement.GetProperty("error").GetString()
        );
    }

    [Fact]
    public async Task PostRegister_WhenTheRegistrationIsRefused_LeavesTheInitialAccessTokenUnspent()
    {
        using var factory = await CreateFactoryAsync(NoSettings);
        await factory.SeedResourceServerWithClientGrantAsync(
            IssuingClientId,
            OrdersResourceServerId,
            OrdersAudience,
            OrdersScope
        );
        using var administrator = await CreateAdministratorClientAsync(factory);
        var initialAccessToken = await MintInitialAccessTokenAsync(
            administrator,
            OrdersResourceServerId
        );
        using var client = CreateHttpClient(factory);
        using var refusedResponse = await RegisterAsync(
            client,
            initialAccessToken,
            OpenIdConstants.ClientAuthenticationMethods.None
        );
        var refusedBody = await refusedResponse.Content.ReadAsStringAsync();
        Assert.True(refusedResponse.StatusCode == HttpStatusCode.BadRequest, refusedBody);

        using var response = await RegisterAsync(
            client,
            initialAccessToken,
            OpenIdConstants.ClientAuthenticationMethods.ClientSecretPost
        );

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.Created, body);
    }

    [Fact]
    public async Task PutRegistration_OfAClientBoundToAResourceServer_RefusesMakingItPublic()
    {
        using var factory = await CreateFactoryAsync(NoSettings);
        await factory.SeedResourceServerWithClientGrantAsync(
            IssuingClientId,
            OrdersResourceServerId,
            OrdersAudience,
            OrdersScope
        );
        using var administrator = await CreateAdministratorClientAsync(factory);
        var initialAccessToken = await MintInitialAccessTokenAsync(
            administrator,
            OrdersResourceServerId
        );
        using var client = CreateHttpClient(factory);
        using var registrationResponse = await RegisterAsync(
            client,
            initialAccessToken,
            OpenIdConstants.ClientAuthenticationMethods.ClientSecretPost
        );
        var registrationBody = await registrationResponse.Content.ReadAsStringAsync();
        Assert.True(registrationResponse.StatusCode == HttpStatusCode.Created, registrationBody);
        using var registration = JsonDocument.Parse(registrationBody);
        var clientId = registration.RootElement.GetProperty("client_id").GetString();
        var registrationAccessToken = registration
            .RootElement.GetProperty("registration_access_token")
            .GetString();
        var registrationClientUri = registration
            .RootElement.GetProperty("registration_client_uri")
            .GetString();

        using var request = new HttpRequestMessage(HttpMethod.Put, registrationClientUri)
        {
            Headers =
            {
                Authorization = new AuthenticationHeaderValue("Bearer", registrationAccessToken),
            },
            Content = JsonContent.Create(
                new Dictionary<string, object>
                {
                    ["client_id"] = clientId ?? string.Empty,
                    ["grant_types"] = new[] { OpenIdConstants.GrantTypes.ClientCredentials },
                    ["token_endpoint_auth_method"] = OpenIdConstants
                        .ClientAuthenticationMethods
                        .None,
                }
            ),
        };
        using var response = await client.SendAsync(request);

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.BadRequest, body);
        using var document = JsonDocument.Parse(body);
        Assert.Equal(
            OpenIdConstants.ErrorCodes.InvalidClientMetadata,
            document.RootElement.GetProperty("error").GetString()
        );
    }

    [Fact]
    public async Task PostInitialAccessToken_WhenTheResourceServerDoesNotExist_ReturnsBadRequest()
    {
        using var factory = await CreateFactoryAsync(NoSettings);
        using var administrator = await CreateAdministratorClientAsync(factory);

        using var response = await administrator.PostAsJsonAsync(
            $"{TenantApiPath}/grants/initial-access-tokens",
            new { resourceServerId = "it-missing-rs" }
        );

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.BadRequest, body);
    }

    [Fact]
    public async Task PostInitialAccessToken_WithoutALifetime_ExpiresAfterOneDay()
    {
        using var factory = await CreateFactoryAsync(NoSettings);
        using var administrator = await CreateAdministratorClientAsync(factory);

        using var response = await administrator.PostAsJsonAsync(
            $"{TenantApiPath}/grants/initial-access-tokens",
            new { description = "it-default-lifetime" }
        );

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.Created, body);
        using var document = JsonDocument.Parse(body);
        var createdWhen = document.RootElement.GetProperty("createdWhen").GetDateTimeOffset();
        var expiresWhen = document.RootElement.GetProperty("expiresWhen").GetDateTimeOffset();
        Assert.Equal(TimeSpan.FromDays(1), expiresWhen - createdWhen);
    }

    [Fact]
    public async Task PostInitialAccessToken_WhenTheLifetimeExceedsThirtyDays_ReturnsBadRequest()
    {
        using var factory = await CreateFactoryAsync(NoSettings);
        using var administrator = await CreateAdministratorClientAsync(factory);

        using var response = await administrator.PostAsJsonAsync(
            $"{TenantApiPath}/grants/initial-access-tokens",
            new { lifetimeSeconds = 2_592_001 }
        );

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.BadRequest, body);
    }

    #endregion

    // A tenant that encrypts access tokens to itself lists a key-management algorithm among the keys it is provisioned
    // with (ADR-0169), which only a host without developer keys provisions. The developer-key mode decides what the host
    // registers, so it is supplied as a host setting, which that read observes, rather than as a configuration overlay
    // applied after it.
    private sealed class ProvisionedKeysHostApplicationFactory : HostApplicationFactory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);

            builder.UseSetting("DeveloperKeys:Mode", "None");
            builder.ConfigureAppConfiguration(configurationBuilder =>
                configurationBuilder.AddInMemoryCollection(
                    new Dictionary<string, string?>
                    {
                        ["OpenId:Tenant:KeyProvisioning:Keys:0:Algorithm"] = "RS256",
                        ["OpenId:Tenant:KeyProvisioning:Keys:1:Algorithm"] = "RSA-OAEP-256",
                        ["BootstrapAdmin:ClientId"] = BootstrapClientId,
                        ["BootstrapAdmin:ClientSecret"] = Base64Url.EncodeToString(
                            Encoding.UTF8.GetBytes(BootstrapClientSecret)
                        ),
                        ["BootstrapAdmin:TenantId"] = TenantId,
                    }
                )
            );
        }
    }
}
