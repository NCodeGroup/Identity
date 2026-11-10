// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers.Text;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Web;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using NCode.Buffers;
using NCode.Identity.OpenId.Integration.Tests.Infrastructure;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.Secrets.Persistence;
using Xunit;

namespace NCode.Identity.OpenId.Integration.Tests.Endpoints;

public class ClientRegistrationEndpointTests
{
    private const string RedirectUri = "https://client.example/callback";

    [Fact]
    public async Task PostRegister_WithInformationalMetadata_RegistersAClientTheServerCanLoad()
    {
        using var factory = new AnonymousRegistrationApplicationFactory();
        var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }
        );

        using var registrationResponse = await client.PostAsJsonAsync(
            "/oauth2/register",
            new Dictionary<string, object>
            {
                ["grant_types"] = new[] { OpenIdProtocol.GrantTypes.ClientCredentials },
                ["token_endpoint_auth_method"] = OpenIdProtocol
                    .ClientAuthenticationMethods
                    .ClientSecretPost,
                ["client_name"] = "Registered Client",
                ["contacts"] = new[] { "ops@example.com" },
                ["software_id"] = "integration-tests",
                ["software_version"] = "1.0",
                ["scope"] = $"openid {TestPlatformSettingsProvider.ApiScope}",
            }
        );

        var registrationBody = await registrationResponse.Content.ReadAsStringAsync();
        Assert.True(registrationResponse.StatusCode == HttpStatusCode.Created, registrationBody);

        var registration = JsonElement.Parse(registrationBody);
        var root = registration;
        var clientId = root.GetProperty("client_id").GetString();
        var clientSecret = root.GetProperty("client_secret").GetString();
        Assert.NotNull(clientId);
        Assert.NotNull(clientSecret);
        Assert.Equal("Registered Client", root.GetProperty("client_name").GetString());
        Assert.Equal("integration-tests", root.GetProperty("software_id").GetString());

        // The response reports the scopes the client may actually request: it holds no grant to the API, so the
        // requested 'api' scope is not among them.
        var scopes = root.GetProperty("scope").GetString()?.Split(' ') ?? [];
        Assert.Contains(OpenIdProtocol.ScopeTypes.OpenId, scopes);
        Assert.DoesNotContain(TestPlatformSettingsProvider.ApiScope, scopes);

        await factory.SeedResourceServerWithClientGrantAsync(
            clientId,
            "it-rs-api",
            "https://api.integration.test",
            [TestPlatformSettingsProvider.ApiScope],
            CancellationToken.None
        );

        // Authenticating the client loads its persisted settings (every registered metadata key included) through the
        // setting serializer, which rejects any name without a registered descriptor.
        using var tokenContent = new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["grant_type"] = OpenIdProtocol.GrantTypes.ClientCredentials,
                ["client_id"] = clientId,
                ["client_secret"] = clientSecret,
                ["scope"] = TestPlatformSettingsProvider.ApiScope,
            }
        );

        using var tokenResponse = await client.PostAsync("/oauth2/token", tokenContent);

        var tokenBody = await tokenResponse.Content.ReadAsStringAsync();
        Assert.True(tokenResponse.StatusCode == HttpStatusCode.OK, tokenBody);
    }

    [Fact]
    public async Task PostRegister_WhenAnOrdinaryClientRegistersIntrospectionMetadata_ReturnsInvalidClientMetadata()
    {
        using var factory = new AnonymousRegistrationApplicationFactory();
        var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }
        );

        // Only a client bound to a resource server receives JWT introspection responses (ADR-1192).
        using var response = await client.PostAsJsonAsync(
            "/oauth2/register",
            new Dictionary<string, object>
            {
                ["grant_types"] = new[] { OpenIdProtocol.GrantTypes.ClientCredentials },
                ["token_endpoint_auth_method"] = OpenIdProtocol
                    .ClientAuthenticationMethods
                    .ClientSecretPost,
                ["introspection_signed_response_alg"] = "RS256",
            }
        );

        await AssertRegistrationErrorAsync(
            response,
            OpenIdProtocol.ErrorCodes.InvalidClientMetadata,
            "introspection_*_response_*",
            CancellationToken.None
        );
    }

    [Fact]
    public async Task PostRegister_WhenAnonymousRegistrationIsAllowed_StillRejectsAnInvalidInitialAccessToken()
    {
        using var factory = new AnonymousRegistrationApplicationFactory();
        var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }
        );

        // A presented token is always validated, so a scoped token is never silently ignored (ADR-1192).
        using var request = new HttpRequestMessage(HttpMethod.Post, "/oauth2/register")
        {
            Headers = { Authorization = new AuthenticationHeaderValue("Bearer", "not-a-token") },
            Content = JsonContent.Create(
                new Dictionary<string, object>
                {
                    ["grant_types"] = new[] { OpenIdProtocol.GrantTypes.ClientCredentials },
                    ["token_endpoint_auth_method"] = OpenIdProtocol
                        .ClientAuthenticationMethods
                        .ClientSecretPost,
                }
            ),
        };
        using var response = await client.SendAsync(request);

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.Unauthorized, body);
    }

    [Fact]
    public async Task PostRegister_WhenTheTenantIsProduction_RefusesAnonymousRegistrationEvenWhenThePlatformAllowsIt()
    {
        using var factory = new AnonymousRegistrationApplicationFactory();
        using var productionFactory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration(configurationBuilder =>
                configurationBuilder.AddInMemoryCollection(
                    new Dictionary<string, string?>
                    {
                        ["OpenId:Platform:Settings:environment_type"] = OpenIdConstants
                            .TenantEnvironmentTypes
                            .Production,
                    }
                )
            )
        );
        var developmentClient = factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }
        );
        var productionClient = productionFactory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }
        );
        var metadata = new Dictionary<string, object>
        {
            ["grant_types"] = new[] { OpenIdProtocol.GrantTypes.ClientCredentials },
            ["token_endpoint_auth_method"] = OpenIdProtocol
                .ClientAuthenticationMethods
                .ClientSecretPost,
        };

        // The control: the Development host's tenant allows anonymous registration, so the same request is accepted.
        using var developmentResponse = await developmentClient.PostAsJsonAsync(
            "/oauth2/register",
            metadata
        );
        var developmentBody = await developmentResponse.Content.ReadAsStringAsync();
        Assert.True(developmentResponse.StatusCode == HttpStatusCode.Created, developmentBody);

        // The production profile's ceiling turns anonymous registration off for the tenant, whatever the platform says.
        using var productionResponse = await productionClient.PostAsJsonAsync(
            "/oauth2/register",
            metadata
        );
        var productionBody = await productionResponse.Content.ReadAsStringAsync();
        Assert.True(productionResponse.StatusCode == HttpStatusCode.Unauthorized, productionBody);
    }

    [Theory]
    [InlineData("grant_types", "authorisation_code", OpenIdSettingNames.GrantTypesSupported)]
    [InlineData("response_types", "code none", OpenIdSettingNames.ResponseTypesSupported)]
    public async Task PostRegister_WhenAListValueIsOutsideItsDomain_ReturnsInvalidClientMetadata(
        string metadataName,
        string value,
        string settingName
    )
    {
        using var factory = new AnonymousRegistrationApplicationFactory();
        var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }
        );

        using var response = await client.PostAsJsonAsync(
            "/oauth2/register",
            new Dictionary<string, object>
            {
                ["redirect_uris"] = new[] { "https://client.example/callback" },
                [metadataName] = new[] { value },
            }
        );

        await AssertRegistrationErrorAsync(
            response,
            OpenIdProtocol.ErrorCodes.InvalidClientMetadata,
            settingName,
            CancellationToken.None
        );
    }

    [Fact]
    public async Task PostRegister_WhenJwksUriIsNotHttps_ReturnsInvalidClientMetadata()
    {
        using var factory = new AnonymousRegistrationApplicationFactory();
        var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }
        );

        using var response = await client.PostAsJsonAsync(
            "/oauth2/register",
            new Dictionary<string, object>
            {
                ["grant_types"] = new[] { OpenIdProtocol.GrantTypes.ClientCredentials },
                ["jwks_uri"] = "http://client.example/jwks",
            }
        );

        await AssertRegistrationErrorAsync(
            response,
            OpenIdProtocol.ErrorCodes.InvalidClientMetadata,
            OpenIdSettingNames.JwksUri,
            CancellationToken.None
        );
    }

    [Fact]
    public async Task PostRegister_WhenRedirectUriIsRelative_ReturnsInvalidRedirectUri()
    {
        using var factory = new AnonymousRegistrationApplicationFactory();
        var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }
        );

        using var response = await client.PostAsJsonAsync(
            "/oauth2/register",
            new Dictionary<string, object>
            {
                ["grant_types"] = new[] { OpenIdProtocol.GrantTypes.ClientCredentials },
                ["redirect_uris"] = new[] { "/callback" },
            }
        );

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.BadRequest, body);

        var jsonElement = JsonElement.Parse(body);
        Assert.Equal(
            OpenIdProtocol.ErrorCodes.InvalidRedirectUri,
            jsonElement.GetProperty("error").GetString()
        );
    }

    [Fact]
    public async Task PostRegister_WithAHybridResponseType_RegistersAClientTheAuthorizationEndpointHonors()
    {
        using var factory = new ConfiguredHostApplicationFactory(
            CreatePlatformConfiguration(
                [OpenIdProtocol.ResponseTypes.Code, OpenIdProtocol.ResponseTypes.CodeIdToken],
                [OpenIdProtocol.GrantTypes.AuthorizationCode, OpenIdProtocol.GrantTypes.Implicit]
            )
        );
        var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }
        );

        using var registrationResponse = await client.PostAsJsonAsync(
            "/oauth2/register",
            new Dictionary<string, object>
            {
                ["redirect_uris"] = new[] { RedirectUri },
                ["response_types"] = new[] { "id_token code" },
            }
        );

        var registrationBody = await registrationResponse.Content.ReadAsStringAsync();
        Assert.True(registrationResponse.StatusCode == HttpStatusCode.Created, registrationBody);

        var registration = JsonElement.Parse(registrationBody);
        var root = registration;
        var clientId = root.GetProperty("client_id").GetString();
        Assert.NotNull(clientId);

        // The response type is registered, and reported, in canonical form together with the grant types it uses.
        Assert.Equal(
            [OpenIdProtocol.ResponseTypes.CodeIdToken],
            GetStringArray(root, "response_types")
        );
        Assert.Equal(
            [OpenIdProtocol.GrantTypes.AuthorizationCode, OpenIdProtocol.GrantTypes.Implicit],
            GetStringArray(root, "grant_types")
        );

        // The authorization endpoint accepts the registered response type: with no end-user session it issues the
        // interactive challenge rather than an error redirected to the client.
        using var hybridResponse = await client.GetAsync(
            CreateAuthorizeUri(clientId, OpenIdProtocol.ResponseTypes.CodeIdToken)
        );
        Assert.Equal(HttpStatusCode.Redirect, hybridResponse.StatusCode);
        var hybridLocation = hybridResponse.Headers.Location;
        Assert.NotNull(hybridLocation);
        Assert.False(
            hybridLocation.OriginalString.StartsWith(RedirectUri, StringComparison.Ordinal),
            "Expected an interactive challenge, not an error redirected back to the client."
        );

        // The platform supports 'code', but the client did not register it.
        using var codeResponse = await client.GetAsync(
            CreateAuthorizeUri(clientId, OpenIdProtocol.ResponseTypes.Code)
        );
        Assert.Equal(HttpStatusCode.SeeOther, codeResponse.StatusCode);
        var codeLocation = codeResponse.Headers.Location;
        Assert.NotNull(codeLocation);
        var parameters = HttpUtility.ParseQueryString(codeLocation.Query);
        Assert.Equal(OpenIdProtocol.ErrorCodes.UnauthorizedClient, parameters["error"]);
    }

    [Fact]
    public async Task PostRegister_WhenThePlatformDoesNotSupportAResponseType_ReturnsInvalidClientMetadata()
    {
        using var factory = new ConfiguredHostApplicationFactory(
            CreatePlatformConfiguration(
                [OpenIdProtocol.ResponseTypes.Code],
                [OpenIdProtocol.GrantTypes.AuthorizationCode, OpenIdProtocol.GrantTypes.Implicit]
            )
        );
        var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }
        );

        using var response = await client.PostAsJsonAsync(
            "/oauth2/register",
            new Dictionary<string, object>
            {
                ["redirect_uris"] = new[] { RedirectUri },
                ["response_types"] = new[] { OpenIdProtocol.ResponseTypes.CodeIdToken },
            }
        );

        await AssertRegistrationErrorAsync(
            response,
            OpenIdProtocol.ErrorCodes.InvalidClientMetadata,
            OpenIdSettingNames.ResponseTypesSupported,
            CancellationToken.None
        );
    }

    [Fact]
    public async Task PostRegister_WhenThePlatformDoesNotPermitTheAuthMethod_ReturnsInvalidClientMetadata()
    {
        // A client whose registered method the platform's ceiling excludes could never authenticate.
        using var factory = new ConfiguredHostApplicationFactory(
            new Dictionary<string, string?>
            {
                ["OpenId:Platform:Settings:registration_endpoint_allow_anonymous"] = "true",
                ["OpenId:Platform:Settings:token_endpoint_auth_methods_supported:0"] =
                    OpenIdProtocol.ClientAuthenticationMethods.ClientSecretBasic,
            }
        );
        var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }
        );

        using var response = await client.PostAsJsonAsync(
            "/oauth2/register",
            new Dictionary<string, object>
            {
                ["redirect_uris"] = new[] { RedirectUri },
                ["token_endpoint_auth_method"] = OpenIdProtocol
                    .ClientAuthenticationMethods
                    .ClientSecretPost,
            }
        );

        await AssertRegistrationErrorAsync(
            response,
            OpenIdProtocol.ErrorCodes.InvalidClientMetadata,
            "token_endpoint_auth_method",
            CancellationToken.None
        );
    }

    [Fact]
    public async Task PostRegister_WhenAResponseTypeUsesAnUnregisteredGrantType_ReturnsInvalidClientMetadata()
    {
        using var factory = new AnonymousRegistrationApplicationFactory();
        var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }
        );

        using var response = await client.PostAsJsonAsync(
            "/oauth2/register",
            new Dictionary<string, object>
            {
                ["redirect_uris"] = new[] { RedirectUri },
                ["grant_types"] = new[] { OpenIdProtocol.GrantTypes.AuthorizationCode },
                ["response_types"] = new[] { OpenIdProtocol.ResponseTypes.CodeIdToken },
            }
        );

        await AssertRegistrationErrorAsync(
            response,
            OpenIdProtocol.ErrorCodes.InvalidClientMetadata,
            OpenIdProtocol.GrantTypes.Implicit,
            CancellationToken.None
        );
    }

    private static Dictionary<string, string?> CreatePlatformConfiguration(
        string[] responseTypesSupported,
        string[] grantTypesSupported
    )
    {
        var configuration = new Dictionary<string, string?>
        {
            ["OpenId:Platform:Settings:registration_endpoint_allow_anonymous"] = "true",
        };
        for (var index = 0; index < responseTypesSupported.Length; ++index)
            configuration[$"OpenId:Platform:Settings:response_types_supported:{index}"] =
                responseTypesSupported[index];
        for (var index = 0; index < grantTypesSupported.Length; ++index)
            configuration[$"OpenId:Platform:Settings:grant_types_supported:{index}"] =
                grantTypesSupported[index];
        return configuration;
    }

    private static string CreateAuthorizeUri(string clientId, string responseType) =>
        "/oauth2/authorize"
        + $"?client_id={Uri.EscapeDataString(clientId)}"
        + $"&redirect_uri={Uri.EscapeDataString(RedirectUri)}"
        + $"&response_type={Uri.EscapeDataString(responseType)}"
        + "&scope=openid"
        + "&nonce=n-0S6_WzA2Mj"
        + "&state=abc123"
        + TestPkce.QueryParameters;

    private static string[] GetStringArray(JsonElement root, string propertyName) =>
        [
            .. root.GetProperty(propertyName)
                .EnumerateArray()
                .Select(value => value.GetString() ?? string.Empty),
        ];

    private static async ValueTask AssertRegistrationErrorAsync(
        HttpResponseMessage response,
        string expectedError,
        string expectedQuotedName,
        CancellationToken cancellationToken
    )
    {
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        Assert.True(response.StatusCode == HttpStatusCode.BadRequest, body);

        var jsonElement = JsonElement.Parse(body);
        var root = jsonElement;
        Assert.Equal(expectedError, root.GetProperty("error").GetString());
        Assert.Contains(
            $"'{expectedQuotedName}'",
            root.GetProperty("error_description").GetString(),
            StringComparison.Ordinal
        );
    }

    #region Registered Keys

    private static Dictionary<string, object> CreateRsaJwk(
        RSA rsa,
        string? keyId,
        bool includePrivateExponent = false
    )
    {
        var parameters = rsa.ExportParameters(includePrivateExponent);
        var jwk = new Dictionary<string, object>
        {
            ["kty"] = "RSA",
            ["use"] = "sig",
            ["alg"] = "RS256",
            ["n"] = Base64Url.EncodeToString(parameters.Modulus),
            ["e"] = Base64Url.EncodeToString(parameters.Exponent),
        };
        if (keyId is not null)
            jwk["kid"] = keyId;
        if (includePrivateExponent)
            jwk["d"] = Base64Url.EncodeToString(parameters.D);
        return jwk;
    }

    private static Dictionary<string, object> CreateKeyRegistration(
        Dictionary<string, object> jwk
    ) =>
        new()
        {
            ["grant_types"] = new[] { OpenIdProtocol.GrantTypes.ClientCredentials },
            ["token_endpoint_auth_method"] = OpenIdProtocol
                .ClientAuthenticationMethods
                .ClientSecretPost,
            ["jwks"] = new Dictionary<string, object> { ["keys"] = new[] { jwk } },
        };

    private static string[] GetKeyIds(JsonElement root) =>
        [
            .. root.GetProperty("jwks")
                .GetProperty("keys")
                .EnumerateArray()
                .Select(key => key.GetProperty("kid").GetString() ?? string.Empty),
        ];

    [Fact]
    public async Task PostRegister_WhenJwksCarriesPrivateKeyMaterial_ReturnsInvalidClientMetadata()
    {
        using var factory = new AnonymousRegistrationApplicationFactory();
        var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }
        );
        using var rsa = RSA.Create(2048);

        using var response = await client.PostAsJsonAsync(
            "/oauth2/register",
            CreateKeyRegistration(CreateRsaJwk(rsa, "client-key", includePrivateExponent: true))
        );

        await AssertRegistrationErrorAsync(
            response,
            OpenIdProtocol.ErrorCodes.InvalidClientMetadata,
            "jwks",
            CancellationToken.None
        );
    }

    [Fact]
    public async Task PostRegister_WhenAJwksKeyHasNoKid_ReturnsInvalidClientMetadata()
    {
        using var factory = new AnonymousRegistrationApplicationFactory();
        var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }
        );
        using var rsa = RSA.Create(2048);

        using var response = await client.PostAsJsonAsync(
            "/oauth2/register",
            CreateKeyRegistration(CreateRsaJwk(rsa, keyId: null))
        );

        await AssertRegistrationErrorAsync(
            response,
            OpenIdProtocol.ErrorCodes.InvalidClientMetadata,
            "kid",
            CancellationToken.None
        );
    }

    [Fact]
    public async Task PostRegister_WithJwks_ReportsTheRegisteredKeysByTheirKid()
    {
        using var factory = new AnonymousRegistrationApplicationFactory();
        var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }
        );
        using var rsa = RSA.Create(2048);

        using var registrationResponse = await client.PostAsJsonAsync(
            "/oauth2/register",
            CreateKeyRegistration(CreateRsaJwk(rsa, "client-key"))
        );

        var registrationBody = await registrationResponse.Content.ReadAsStringAsync();
        Assert.True(registrationResponse.StatusCode == HttpStatusCode.Created, registrationBody);
        var registration = JsonElement.Parse(registrationBody);
        var root = registration;
        Assert.Equal(["client-key"], GetKeyIds(root));

        using var readRequest = new HttpRequestMessage(
            HttpMethod.Get,
            root.GetProperty("registration_client_uri").GetString()
        );
        readRequest.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            root.GetProperty("registration_access_token").GetString()
        );
        using var readResponse = await client.SendAsync(readRequest);

        var readBody = await readResponse.Content.ReadAsStringAsync();
        Assert.True(readResponse.StatusCode == HttpStatusCode.OK, readBody);
        var read = JsonElement.Parse(readBody);
        Assert.Equal(["client-key"], GetKeyIds(read));
    }

    [Fact]
    public async Task PostRegister_WhenTwoClientsRegisterTheSameKid_RegistersBoth()
    {
        using var factory = new AnonymousRegistrationApplicationFactory();
        var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }
        );
        using var firstRsa = RSA.Create(2048);
        using var secondRsa = RSA.Create(2048);

        // A kid names a key among its own client's keys, so it is not a server-wide identifier.
        using var firstResponse = await client.PostAsJsonAsync(
            "/oauth2/register",
            CreateKeyRegistration(CreateRsaJwk(firstRsa, "key-1"))
        );
        using var secondResponse = await client.PostAsJsonAsync(
            "/oauth2/register",
            CreateKeyRegistration(CreateRsaJwk(secondRsa, "key-1"))
        );

        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);
        var secondBody = await secondResponse.Content.ReadAsStringAsync();
        Assert.True(secondResponse.StatusCode == HttpStatusCode.Created, secondBody);
    }

    [Fact]
    public async Task PutRegistration_WhenJwksUriReplacesJwks_RemovesTheRegisteredKeys()
    {
        using var factory = new AnonymousRegistrationApplicationFactory();
        var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }
        );
        using var rsa = RSA.Create(2048);

        using var registrationResponse = await client.PostAsJsonAsync(
            "/oauth2/register",
            CreateKeyRegistration(CreateRsaJwk(rsa, "client-key"))
        );
        var registrationBody = await registrationResponse.Content.ReadAsStringAsync();
        Assert.True(registrationResponse.StatusCode == HttpStatusCode.Created, registrationBody);
        var registration = JsonElement.Parse(registrationBody);
        var root = registration;

        using var updateRequest = new HttpRequestMessage(
            HttpMethod.Put,
            root.GetProperty("registration_client_uri").GetString()
        )
        {
            Content = JsonContent.Create(
                new Dictionary<string, object>
                {
                    ["client_id"] = root.GetProperty("client_id").GetString() ?? string.Empty,
                    ["grant_types"] = new[] { OpenIdProtocol.GrantTypes.ClientCredentials },
                    ["token_endpoint_auth_method"] = OpenIdProtocol
                        .ClientAuthenticationMethods
                        .ClientSecretPost,
                    ["jwks_uri"] = "https://client.example/jwks",
                }
            ),
        };
        updateRequest.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            root.GetProperty("registration_access_token").GetString()
        );
        using var updateResponse = await client.SendAsync(updateRequest);

        // A client's keys come from one source, so publishing them at a jwks_uri replaces the registered ones.
        var updateBody = await updateResponse.Content.ReadAsStringAsync();
        Assert.True(updateResponse.StatusCode == HttpStatusCode.OK, updateBody);
        var update = JsonElement.Parse(updateBody);
        Assert.Equal("https://client.example/jwks", update.GetProperty("jwks_uri").GetString());
        Assert.False(update.TryGetProperty("jwks", out _));
    }

    [Fact]
    public async Task PostRegister_WithClientSecretJwt_IssuesASecretThatKeysTheClientAssertion()
    {
        // A client registered for client_secret_jwt keys an HMAC from its secret, so it is issued one the server can
        // read back; every other client's secret is stored as a one-way verifier. See ADR-1391.
        using var factory = new AnonymousRegistrationApplicationFactory();
        var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }
        );
        var issuer = await ClientAuthenticationProbe.GetIssuerAsync(client, CancellationToken.None);

        var (clientId, clientSecret, _) = await RegisterSharedSecretClientAsync(
            client,
            OpenIdProtocol.ClientAuthenticationMethods.ClientSecretJwt,
            CancellationToken.None
        );

        var secrets = await factory.GetClientSecretsAsync(clientId, CancellationToken.None);
        var stored = Assert.Single(secrets);
        Assert.Equal(NCode.Identity.Secrets.SecretEncodings.SymmetricRaw, stored.SecretEncoding);
        var revocationError = await ClientAuthenticationProbe.RevokeWithAssertionAsync(
            client,
            clientId,
            clientSecret,
            issuer,
            CancellationToken.None
        );
        Assert.Null(revocationError);
    }

    [Fact]
    public async Task PostRegister_WithClientSecretPost_StoresOnlyAVerifierOfTheIssuedSecret()
    {
        using var factory = new AnonymousRegistrationApplicationFactory();
        var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }
        );

        var (clientId, clientSecret, _) = await RegisterSharedSecretClientAsync(
            client,
            OpenIdProtocol.ClientAuthenticationMethods.ClientSecretPost,
            CancellationToken.None
        );

        var secrets = await factory.GetClientSecretsAsync(clientId, CancellationToken.None);
        var stored = Assert.Single(secrets);
        Assert.Equal(NCode.Identity.Secrets.SecretEncodings.Verifier, stored.SecretEncoding);
        var revocationError = await ClientAuthenticationProbe.RevokeWithSecretAsync(
            client,
            clientId,
            clientSecret,
            CancellationToken.None
        );
        Assert.Null(revocationError);
        var wrongSecretError = await ClientAuthenticationProbe.RevokeWithSecretAsync(
            client,
            clientId,
            clientSecret + "x",
            CancellationToken.None
        );
        Assert.Equal(OpenIdProtocol.ErrorCodes.InvalidClient, wrongSecretError);
    }

    [Fact]
    public async Task PutRegistration_WhenSwitchedToClientSecretJwt_IssuesANewSecretThatKeysTheClientAssertion()
    {
        using var factory = new AnonymousRegistrationApplicationFactory();
        var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }
        );
        var issuer = await ClientAuthenticationProbe.GetIssuerAsync(client, CancellationToken.None);

        var (clientId, verifiedSecret, registration) = await RegisterSharedSecretClientAsync(
            client,
            OpenIdProtocol.ClientAuthenticationMethods.ClientSecretPost,
            CancellationToken.None
        );

        var update = await UpdateAuthenticationMethodAsync(
            client,
            registration,
            OpenIdProtocol.ClientAuthenticationMethods.ClientSecretJwt,
            CancellationToken.None
        );

        // The verifier of the old secret is no HMAC key, so the update issues a replacement (RFC 7592 §2.2) that the
        // client keeps instead of the old one.
        var newSecret = update.GetProperty("client_secret").GetString();
        Assert.False(string.IsNullOrEmpty(newSecret));
        Assert.NotEqual(verifiedSecret, newSecret);
        Assert.Equal(0, update.GetProperty("client_secret_expires_at").GetInt64());

        var secrets = await factory.GetClientSecretsAsync(clientId, CancellationToken.None);
        var stored = Assert.Single(secrets);
        Assert.Equal(NCode.Identity.Secrets.SecretEncodings.SymmetricRaw, stored.SecretEncoding);
        var revocationError = await ClientAuthenticationProbe.RevokeWithAssertionAsync(
            client,
            clientId,
            newSecret,
            issuer,
            CancellationToken.None
        );
        Assert.Null(revocationError);
        var oldSecretError = await ClientAuthenticationProbe.RevokeWithAssertionAsync(
            client,
            clientId,
            verifiedSecret,
            issuer,
            CancellationToken.None
        );
        Assert.Equal(OpenIdProtocol.ErrorCodes.InvalidClient, oldSecretError);
    }

    [Fact]
    public async Task PutRegistration_WhenSwitchedAwayFromClientSecretJwt_KeepsTheSecretAsAVerifier()
    {
        using var factory = new AnonymousRegistrationApplicationFactory();
        var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }
        );

        var (clientId, clientSecret, registration) = await RegisterSharedSecretClientAsync(
            client,
            OpenIdProtocol.ClientAuthenticationMethods.ClientSecretJwt,
            CancellationToken.None
        );

        // The client keeps its secret, and the server cannot read it back: its octets are replaced by the verifier that
        // fits a generated secret. See ADR-1391.
        var update = await UpdateAuthenticationMethodAsync(
            client,
            registration,
            OpenIdProtocol.ClientAuthenticationMethods.ClientSecretPost,
            CancellationToken.None
        );

        Assert.False(update.TryGetProperty("client_secret", out _));
        var secrets = await factory.GetClientSecretsAsync(clientId, CancellationToken.None);
        var stored = Assert.Single(secrets);
        Assert.Equal(NCode.Identity.Secrets.SecretEncodings.Verifier, stored.SecretEncoding);
        Assert.Equal(SecretOrigins.Generated, stored.Origin);
        var revocationError = await ClientAuthenticationProbe.RevokeWithSecretAsync(
            client,
            clientId,
            clientSecret,
            CancellationToken.None
        );
        Assert.Null(revocationError);
    }

    [Fact]
    public async Task PutRegistration_WhenSwitchedFromAKeyMethodToASecretMethod_IssuesASecretAndKeepsTheKeys()
    {
        // A client that held no shared secret is issued one when it moves to a method that verifies one; its
        // registered public keys stay. See ADR-1391.
        using var factory = new AnonymousRegistrationApplicationFactory();
        var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }
        );
        using var rsa = RSA.Create(2048);
        var jwk = CreateRsaJwk(rsa, "client-key");
        var metadata = CreateKeyRegistration(jwk);
        metadata["token_endpoint_auth_method"] = OpenIdProtocol
            .ClientAuthenticationMethods
            .PrivateKeyJwt;
        using var response = await client.PostAsJsonAsync("/oauth2/register", metadata);
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.Created, body);
        var registration = JsonElement.Parse(body);
        Assert.False(registration.TryGetProperty("client_secret", out _));
        var clientId = registration.GetProperty("client_id").GetString() ?? string.Empty;

        var update = await UpdateRegistrationAsync(
            client,
            registration,
            CreateKeyRegistration(jwk),
            CancellationToken.None
        );

        var clientSecret = update.GetProperty("client_secret").GetString();
        Assert.False(string.IsNullOrEmpty(clientSecret));
        Assert.Equal(0, update.GetProperty("client_secret_expires_at").GetInt64());
        Assert.Equal(["client-key"], GetKeyIds(update));

        var stored = await factory.GetClientSecretsAsync(clientId, CancellationToken.None);
        Assert.Contains(stored, secret => secret.SecretType == SecretTypes.Rsa);
        var shared = Assert.Single(stored, secret => secret.SecretType == SecretTypes.Symmetric);
        Assert.Equal(NCode.Identity.Secrets.SecretEncodings.Verifier, shared.SecretEncoding);
        var revocationError = await ClientAuthenticationProbe.RevokeWithSecretAsync(
            client,
            clientId,
            clientSecret,
            CancellationToken.None
        );
        Assert.Null(revocationError);
    }

    [Fact]
    public async Task PutRegistration_WhenTheClientCarriesALimit_KeepsTheLimit()
    {
        // The metadata of a registration update names no limit, and a limit changes only at the authority above the
        // client's tenant, so the update keeps the limit its replaced document carries. See ADR-1418.
        // The factory allows anonymous registration and configures the bootstrap administrator, which holds authority
        // over a client's limits through the management API.
        using var factory = new BootstrapAdminApplicationFactory(
            new Dictionary<string, string?>
            {
                ["OpenId:Platform:Settings:registration_endpoint_allow_anonymous"] = "true",
            }
        );
        using var administrator = await factory.CreateAdministratorClientAsync(
            CancellationToken.None
        );
        var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }
        );
        var (clientId, _, registration) = await RegisterSharedSecretClientAsync(
            client,
            OpenIdProtocol.ClientAuthenticationMethods.ClientSecretPost,
            CancellationToken.None
        );
        var settingsPath =
            $"/api/tenants/{HostApplicationFactory.TenantId}/clients/{clientId}/settings";
        var operations = new[]
        {
            new
            {
                op = "add",
                path = "/rate_limit_client_per_minute",
                value = 30,
            },
        };
        using var patchRequest = new HttpRequestMessage(HttpMethod.Patch, settingsPath)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(operations),
                SecureEncoding.UTF8,
                "application/json-patch+json"
            ),
        };
        using var patchResponse = await administrator.SendAsync(patchRequest);
        var patchBody = await patchResponse.Content.ReadAsStringAsync();
        Assert.True(patchResponse.StatusCode == HttpStatusCode.NoContent, patchBody);

        await UpdateAuthenticationMethodAsync(
            client,
            registration,
            OpenIdProtocol.ClientAuthenticationMethods.ClientSecretPost,
            CancellationToken.None
        );

        using var settingsResponse = await administrator.GetAsync(settingsPath);
        var settingsBody = await settingsResponse.Content.ReadAsStringAsync();
        Assert.True(settingsResponse.StatusCode == HttpStatusCode.OK, settingsBody);
        var settings = JsonElement.Parse(settingsBody).GetProperty("settings");
        Assert.Equal(30, settings.GetProperty("rate_limit_client_per_minute").GetInt32());
    }

    [Fact]
    public async Task PutRegistration_WhenTheClientsOnlySecretHasExpired_IssuesANewSecret()
    {
        // An expired secret authenticates nothing, so an update that keeps a method that verifies a secret issues one.
        using var factory = new AnonymousRegistrationApplicationFactory();
        var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }
        );
        var (clientId, expiredSecret, registration) = await RegisterSharedSecretClientAsync(
            client,
            OpenIdProtocol.ClientAuthenticationMethods.ClientSecretPost,
            CancellationToken.None
        );
        await factory.ExpireClientSecretsAsync(clientId, CancellationToken.None);

        var update = await UpdateAuthenticationMethodAsync(
            client,
            registration,
            OpenIdProtocol.ClientAuthenticationMethods.ClientSecretPost,
            CancellationToken.None
        );

        var newSecret = update.GetProperty("client_secret").GetString();
        Assert.False(string.IsNullOrEmpty(newSecret));
        Assert.NotEqual(expiredSecret, newSecret);
        var secrets = await factory.GetClientSecretsAsync(clientId, CancellationToken.None);
        var stored = Assert.Single(secrets);
        Assert.True(stored.ExpiresWhen > DateTimeOffset.UtcNow);
        var revocationError = await ClientAuthenticationProbe.RevokeWithSecretAsync(
            client,
            clientId,
            newSecret,
            CancellationToken.None
        );
        Assert.Null(revocationError);
        var expiredSecretError = await ClientAuthenticationProbe.RevokeWithSecretAsync(
            client,
            clientId,
            expiredSecret,
            CancellationToken.None
        );
        Assert.Equal(OpenIdProtocol.ErrorCodes.InvalidClient, expiredSecretError);
    }

    [Fact]
    public async Task PutRegistration_WhenTheClientKeepsAnUnexpiredVerifier_IssuesNoNewSecret()
    {
        using var factory = new AnonymousRegistrationApplicationFactory();
        var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }
        );
        var (clientId, clientSecret, registration) = await RegisterSharedSecretClientAsync(
            client,
            OpenIdProtocol.ClientAuthenticationMethods.ClientSecretPost,
            CancellationToken.None
        );

        var update = await UpdateAuthenticationMethodAsync(
            client,
            registration,
            OpenIdProtocol.ClientAuthenticationMethods.ClientSecretPost,
            CancellationToken.None
        );

        Assert.False(update.TryGetProperty("client_secret", out _));
        var secrets = await factory.GetClientSecretsAsync(clientId, CancellationToken.None);
        Assert.Single(secrets);
        var revocationError = await ClientAuthenticationProbe.RevokeWithSecretAsync(
            client,
            clientId,
            clientSecret,
            CancellationToken.None
        );
        Assert.Null(revocationError);
    }

    [Fact]
    public async Task PostRegister_WhenAComparedSecretWouldSignRequestObjects_ReturnsInvalidClientMetadata()
    {
        // A secret stored as a one-way verifier keys no HS256 request object. See ADR-1391.
        using var factory = new AnonymousRegistrationApplicationFactory();
        var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }
        );

        using var response = await client.PostAsJsonAsync(
            "/oauth2/register",
            new Dictionary<string, object>
            {
                ["grant_types"] = new[] { OpenIdProtocol.GrantTypes.ClientCredentials },
                ["token_endpoint_auth_method"] = OpenIdProtocol
                    .ClientAuthenticationMethods
                    .ClientSecretBasic,
                ["request_object_signing_alg"] = "HS256",
            }
        );

        await AssertRegistrationErrorAsync(
            response,
            OpenIdProtocol.ErrorCodes.InvalidClientMetadata,
            "request_object_signing_alg_values_supported",
            CancellationToken.None
        );
    }

    [Fact]
    public async Task PostRegister_WhenAKeyedSecretWouldSignRequestObjects_RegistersTheClient()
    {
        using var factory = new AnonymousRegistrationApplicationFactory();
        var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }
        );

        using var response = await client.PostAsJsonAsync(
            "/oauth2/register",
            new Dictionary<string, object>
            {
                ["grant_types"] = new[] { OpenIdProtocol.GrantTypes.ClientCredentials },
                ["token_endpoint_auth_method"] = OpenIdProtocol
                    .ClientAuthenticationMethods
                    .ClientSecretJwt,
                ["request_object_signing_alg"] = "HS256",
            }
        );

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.Created, body);
        Assert.Equal(
            "HS256",
            JsonElement.Parse(body).GetProperty("request_object_signing_alg").GetString()
        );
    }

    [Fact]
    public async Task DeleteRegistration_RemovesTheClientWithItsKeysAndRegistrationAccessToken()
    {
        using var factory = new AnonymousRegistrationApplicationFactory();
        var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }
        );
        using var rsa = RSA.Create(2048);

        using var registrationResponse = await client.PostAsJsonAsync(
            "/oauth2/register",
            CreateKeyRegistration(CreateRsaJwk(rsa, "client-key"))
        );
        var registrationBody = await registrationResponse.Content.ReadAsStringAsync();
        Assert.True(registrationResponse.StatusCode == HttpStatusCode.Created, registrationBody);
        var registration = JsonElement.Parse(registrationBody);
        var root = registration;
        var registrationClientUri = root.GetProperty("registration_client_uri").GetString();
        var registrationAccessToken = root.GetProperty("registration_access_token").GetString();

        using var deleteRequest = new HttpRequestMessage(HttpMethod.Delete, registrationClientUri);
        deleteRequest.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            registrationAccessToken
        );
        using var deleteResponse = await client.SendAsync(deleteRequest);

        var deleteBody = await deleteResponse.Content.ReadAsStringAsync();
        Assert.True(deleteResponse.StatusCode == HttpStatusCode.NoContent, deleteBody);

        // The registration access token is removed with the client, so it no longer authenticates a read.
        using var readRequest = new HttpRequestMessage(HttpMethod.Get, registrationClientUri);
        readRequest.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            registrationAccessToken
        );
        using var readResponse = await client.SendAsync(readRequest);

        var readBody = await readResponse.Content.ReadAsStringAsync();
        Assert.True(readResponse.StatusCode == HttpStatusCode.Unauthorized, readBody);
    }

    #endregion

    #region Client Authentication Methods

    private static string BuildPrivateKeyJwt(string clientId, RSA privateKey, string audience)
    {
        var now = DateTimeOffset.UtcNow;
        var header = new Dictionary<string, object> { ["alg"] = "RS256", ["typ"] = "JWT" };
        var payload = new Dictionary<string, object>
        {
            ["iss"] = clientId,
            ["sub"] = clientId,
            ["aud"] = audience,
            ["jti"] = Guid.NewGuid().ToString("N"),
            ["iat"] = now.ToUnixTimeSeconds(),
            ["exp"] = now.AddMinutes(5).ToUnixTimeSeconds(),
        };

        var encodedHeader = Base64Url.EncodeToString(JsonSerializer.SerializeToUtf8Bytes(header));
        var encodedPayload = Base64Url.EncodeToString(JsonSerializer.SerializeToUtf8Bytes(payload));
        var signingInput = $"{encodedHeader}.{encodedPayload}";
        var signature = privateKey.SignData(
            SecureEncoding.ASCII.GetBytes(signingInput),
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1
        );

        return $"{signingInput}.{Base64Url.EncodeToString(signature)}";
    }

    [Fact]
    public async Task PostRegister_WithPrivateKeyJwt_RegistersAClientThatAuthenticatesWithItsKey()
    {
        using var factory = new AnonymousRegistrationApplicationFactory();
        var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }
        );
        using var rsa = RSA.Create(2048);
        var registration = CreateKeyRegistration(CreateRsaJwk(rsa, "client-key"));
        registration["token_endpoint_auth_method"] = OpenIdProtocol
            .ClientAuthenticationMethods
            .PrivateKeyJwt;

        using var registrationResponse = await client.PostAsJsonAsync(
            "/oauth2/register",
            registration
        );

        var registrationBody = await registrationResponse.Content.ReadAsStringAsync();
        Assert.True(registrationResponse.StatusCode == HttpStatusCode.Created, registrationBody);
        var registered = JsonElement.Parse(registrationBody);
        var root = registered;
        Assert.Equal(
            OpenIdProtocol.ClientAuthenticationMethods.PrivateKeyJwt,
            root.GetProperty("token_endpoint_auth_method").GetString()
        );
        // A key-based client verifies with its keys; a shared secret would be a second, weaker credential.
        Assert.False(root.TryGetProperty("client_secret", out _));

        var clientId = root.GetProperty("client_id").GetString() ?? string.Empty;
        var discovery = JsonElement.Parse(
            await client.GetStringAsync("/.well-known/openid-configuration")
        );
        var issuer = discovery.GetProperty("issuer").GetString() ?? string.Empty;
        using var tokenResponse = await client.PostAsync(
            "/oauth2/token",
            new FormUrlEncodedContent(
                new Dictionary<string, string>
                {
                    ["grant_type"] = OpenIdProtocol.GrantTypes.ClientCredentials,
                    ["client_assertion_type"] = OpenIdProtocol.ClientAssertionTypes.JwtBearer,
                    ["client_assertion"] = BuildPrivateKeyJwt(clientId, rsa, issuer),
                }
            )
        );

        var tokenBody = await tokenResponse.Content.ReadAsStringAsync();
        var token = JsonElement.Parse(tokenBody);
        Assert.False(
            token.TryGetProperty("error", out var error)
                && error.GetString() == OpenIdProtocol.ErrorCodes.InvalidClient,
            tokenBody
        );
    }

    [Fact]
    public async Task PostRegister_WithPrivateKeyJwtWithoutKeys_ReturnsInvalidClientMetadata()
    {
        using var factory = new AnonymousRegistrationApplicationFactory();
        var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }
        );

        using var response = await client.PostAsJsonAsync(
            "/oauth2/register",
            new Dictionary<string, object>
            {
                ["grant_types"] = new[] { OpenIdProtocol.GrantTypes.ClientCredentials },
                ["token_endpoint_auth_method"] = OpenIdProtocol
                    .ClientAuthenticationMethods
                    .PrivateKeyJwt,
            }
        );

        await AssertRegistrationErrorAsync(
            response,
            OpenIdProtocol.ErrorCodes.InvalidClientMetadata,
            "jwks_uri",
            CancellationToken.None
        );
    }

    [Fact]
    public async Task PostRegister_WithTlsClientAuth_StoresAndReportsTheCertificateIdentity()
    {
        using var factory = new AnonymousRegistrationApplicationFactory();
        var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }
        );

        using var registrationResponse = await client.PostAsJsonAsync(
            "/oauth2/register",
            new Dictionary<string, object>
            {
                ["grant_types"] = new[] { OpenIdProtocol.GrantTypes.ClientCredentials },
                ["token_endpoint_auth_method"] = OpenIdProtocol
                    .ClientAuthenticationMethods
                    .TlsClientAuth,
                ["tls_client_auth_san_dns"] = "client.example.com",
            }
        );

        var registrationBody = await registrationResponse.Content.ReadAsStringAsync();
        Assert.True(registrationResponse.StatusCode == HttpStatusCode.Created, registrationBody);
        var registered = JsonElement.Parse(registrationBody);
        var root = registered;
        Assert.False(root.TryGetProperty("client_secret", out _));
        Assert.Equal("client.example.com", root.GetProperty("tls_client_auth_san_dns").GetString());

        using var readRequest = new HttpRequestMessage(
            HttpMethod.Get,
            root.GetProperty("registration_client_uri").GetString()
        );
        readRequest.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            root.GetProperty("registration_access_token").GetString()
        );
        using var readResponse = await client.SendAsync(readRequest);

        var readBody = await readResponse.Content.ReadAsStringAsync();
        Assert.True(readResponse.StatusCode == HttpStatusCode.OK, readBody);
        var read = JsonElement.Parse(readBody);
        Assert.Equal("client.example.com", read.GetProperty("tls_client_auth_san_dns").GetString());
    }

    [Fact]
    public async Task PostRegister_WithTlsClientAuthWithoutIdentity_ReturnsInvalidClientMetadata()
    {
        using var factory = new AnonymousRegistrationApplicationFactory();
        var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }
        );

        using var response = await client.PostAsJsonAsync(
            "/oauth2/register",
            new Dictionary<string, object>
            {
                ["grant_types"] = new[] { OpenIdProtocol.GrantTypes.ClientCredentials },
                ["token_endpoint_auth_method"] = OpenIdProtocol
                    .ClientAuthenticationMethods
                    .TlsClientAuth,
            }
        );

        await AssertRegistrationErrorAsync(
            response,
            OpenIdProtocol.ErrorCodes.InvalidClientMetadata,
            "tls_client_auth_subject_dn",
            CancellationToken.None
        );
    }

    #endregion

    #region Token Endpoint and CIBA Metadata

    [Fact]
    public async Task PostRegister_WithTokenEndpointAndCibaMetadata_StoresAndReportsThem()
    {
        using var factory = new AnonymousRegistrationApplicationFactory();
        var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }
        );

        using var registrationResponse = await client.PostAsJsonAsync(
            "/oauth2/register",
            new Dictionary<string, object>
            {
                ["grant_types"] = new[] { OpenIdProtocol.GrantTypes.ClientCredentials },
                ["token_endpoint_auth_method"] = OpenIdProtocol
                    .ClientAuthenticationMethods
                    .ClientSecretPost,
                ["token_endpoint_auth_signing_alg"] = "ES256",
                ["backchannel_token_delivery_mode"] = OpenIdProtocol
                    .BackchannelTokenDeliveryModes
                    .Ping,
                ["backchannel_client_notification_endpoint"] = "https://client.example/ciba",
                ["backchannel_authentication_request_signing_alg"] = "PS256",
                ["backchannel_user_code_parameter"] = false,
            }
        );
        var registrationBody = await registrationResponse.Content.ReadAsStringAsync();
        Assert.True(registrationResponse.StatusCode == HttpStatusCode.Created, registrationBody);
        var registration = JsonElement.Parse(registrationBody);
        var root = registration;

        // Reading the registration back loads the stored client, so the metadata round-trips through its settings.
        using var readRequest = new HttpRequestMessage(
            HttpMethod.Get,
            root.GetProperty("registration_client_uri").GetString()
        );
        readRequest.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            root.GetProperty("registration_access_token").GetString()
        );
        using var readResponse = await client.SendAsync(readRequest);
        var readBody = await readResponse.Content.ReadAsStringAsync();
        Assert.True(readResponse.StatusCode == HttpStatusCode.OK, readBody);
        var read = JsonElement.Parse(readBody);

        foreach (var jsonElement in new[] { root, read })
        {
            Assert.Equal(
                "ES256",
                jsonElement.GetProperty("token_endpoint_auth_signing_alg").GetString()
            );
            Assert.Equal(
                OpenIdProtocol.BackchannelTokenDeliveryModes.Ping,
                jsonElement.GetProperty("backchannel_token_delivery_mode").GetString()
            );
            Assert.Equal(
                "https://client.example/ciba",
                jsonElement.GetProperty("backchannel_client_notification_endpoint").GetString()
            );
            Assert.Equal(
                "PS256",
                jsonElement
                    .GetProperty("backchannel_authentication_request_signing_alg")
                    .GetString()
            );
            Assert.False(jsonElement.GetProperty("backchannel_user_code_parameter").GetBoolean());
        }
    }

    [Theory]
    [InlineData("push", null, OpenIdSettingNames.ClientNotificationEndpoint)]
    [InlineData(
        "ping",
        "http://client.example/ciba",
        OpenIdSettingNames.ClientNotificationEndpoint
    )]
    [InlineData("poll", null, OpenIdSettingNames.BackchannelUserCodeParameterSupported)]
    public async Task PostRegister_WhenTheCibaMetadataCannotBeHonored_ReturnsInvalidClientMetadata(
        string deliveryMode,
        string? notificationEndpoint,
        string expectedQuotedName
    )
    {
        using var factory = new AnonymousRegistrationApplicationFactory();
        var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }
        );
        var metadata = new Dictionary<string, object>
        {
            ["grant_types"] = new[] { OpenIdProtocol.GrantTypes.ClientCredentials },
            ["backchannel_token_delivery_mode"] = deliveryMode,
        };
        if (notificationEndpoint is not null)
            metadata["backchannel_client_notification_endpoint"] = notificationEndpoint;

        // The platform does not support the user_code parameter by default, so no client may claim it.
        if (expectedQuotedName == OpenIdSettingNames.BackchannelUserCodeParameterSupported)
            metadata["backchannel_user_code_parameter"] = true;

        using var response = await client.PostAsJsonAsync("/oauth2/register", metadata);

        await AssertRegistrationErrorAsync(
            response,
            OpenIdProtocol.ErrorCodes.InvalidClientMetadata,
            expectedQuotedName,
            CancellationToken.None
        );
    }

    #endregion

    #region Certificate-Bound Access Tokens

    [Fact]
    public async Task PostRegister_WithCertificateBoundTokens_WhenThePlatformDoesNotSupportThem_ReturnsInvalidClientMetadata()
    {
        using var factory = new AnonymousRegistrationApplicationFactory();
        var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }
        );

        using var response = await client.PostAsJsonAsync(
            "/oauth2/register",
            new Dictionary<string, object>
            {
                ["grant_types"] = new[] { OpenIdProtocol.GrantTypes.ClientCredentials },
                ["token_endpoint_auth_method"] = OpenIdProtocol
                    .ClientAuthenticationMethods
                    .ClientSecretPost,
                ["tls_client_certificate_bound_access_tokens"] = true,
            }
        );

        await AssertRegistrationErrorAsync(
            response,
            OpenIdProtocol.ErrorCodes.InvalidClientMetadata,
            "tls_client_certificate_bound_access_tokens",
            CancellationToken.None
        );
    }

    [Fact]
    public async Task PostRegister_WithCertificateBoundTokens_WhenThePlatformSupportsThem_StoresAndReportsThem()
    {
        using var factory = new ConfiguredHostApplicationFactory(
            new Dictionary<string, string?>
            {
                ["OpenId:Platform:Settings:registration_endpoint_allow_anonymous"] = "true",
                ["OpenId:Platform:Settings:tls_client_certificate_bound_access_tokens"] = "true",
            }
        );
        var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }
        );

        using var registrationResponse = await client.PostAsJsonAsync(
            "/oauth2/register",
            new Dictionary<string, object>
            {
                ["grant_types"] = new[] { OpenIdProtocol.GrantTypes.ClientCredentials },
                ["token_endpoint_auth_method"] = OpenIdProtocol
                    .ClientAuthenticationMethods
                    .ClientSecretPost,
                ["tls_client_certificate_bound_access_tokens"] = true,
            }
        );

        var registrationBody = await registrationResponse.Content.ReadAsStringAsync();
        Assert.True(registrationResponse.StatusCode == HttpStatusCode.Created, registrationBody);
        var registered = JsonElement.Parse(registrationBody);
        var root = registered;
        Assert.True(root.GetProperty("tls_client_certificate_bound_access_tokens").GetBoolean());

        using var readRequest = new HttpRequestMessage(
            HttpMethod.Get,
            root.GetProperty("registration_client_uri").GetString()
        );
        readRequest.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            root.GetProperty("registration_access_token").GetString()
        );
        using var readResponse = await client.SendAsync(readRequest);

        var readBody = await readResponse.Content.ReadAsStringAsync();
        Assert.True(readResponse.StatusCode == HttpStatusCode.OK, readBody);
        var read = JsonElement.Parse(readBody);
        Assert.True(read.GetProperty("tls_client_certificate_bound_access_tokens").GetBoolean());
    }

    #endregion

    #region DPoP-Bound Access Tokens

    [Fact]
    public async Task PostRegister_WithDPoPBoundTokens_StoresTheRequirementAndTheTokenEndpointEnforcesIt()
    {
        using var factory = new AnonymousRegistrationApplicationFactory();
        var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }
        );

        using var registrationResponse = await client.PostAsJsonAsync(
            "/oauth2/register",
            new Dictionary<string, object>
            {
                ["grant_types"] = new[] { OpenIdProtocol.GrantTypes.ClientCredentials },
                ["token_endpoint_auth_method"] = OpenIdProtocol
                    .ClientAuthenticationMethods
                    .ClientSecretPost,
                ["dpop_bound_access_tokens"] = true,
            }
        );

        var registrationBody = await registrationResponse.Content.ReadAsStringAsync();
        Assert.True(registrationResponse.StatusCode == HttpStatusCode.Created, registrationBody);
        var registered = JsonElement.Parse(registrationBody);
        var root = registered;
        Assert.True(root.GetProperty("dpop_bound_access_tokens").GetBoolean());

        using var readRequest = new HttpRequestMessage(
            HttpMethod.Get,
            root.GetProperty("registration_client_uri").GetString()
        );
        readRequest.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            root.GetProperty("registration_access_token").GetString()
        );
        using var readResponse = await client.SendAsync(readRequest);

        var readBody = await readResponse.Content.ReadAsStringAsync();
        Assert.True(readResponse.StatusCode == HttpStatusCode.OK, readBody);
        var read = JsonElement.Parse(readBody);
        Assert.True(read.GetProperty("dpop_bound_access_tokens").GetBoolean());

        // The stored requirement is the client's own: a token request without a DPoP proof is refused.
        using var tokenContent = new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["grant_type"] = OpenIdProtocol.GrantTypes.ClientCredentials,
                ["client_id"] = root.GetProperty("client_id").GetString()!,
                ["client_secret"] = root.GetProperty("client_secret").GetString()!,
            }
        );
        using var tokenResponse = await client.PostAsync("/oauth2/token", tokenContent);

        var tokenBody = await tokenResponse.Content.ReadAsStringAsync();
        Assert.True(tokenResponse.StatusCode == HttpStatusCode.BadRequest, tokenBody);
        var token = JsonElement.Parse(tokenBody);
        Assert.Equal(
            OpenIdProtocol.ErrorCodes.InvalidDPoPProof,
            token.GetProperty("error").GetString()
        );
    }

    #endregion

    #region Back-Channel Logout Metadata

    [Fact]
    public async Task PostRegister_WithBackChannelLogoutMetadata_StoresAndReportsIt()
    {
        using var factory = new AnonymousRegistrationApplicationFactory();
        var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }
        );

        using var registrationResponse = await client.PostAsJsonAsync(
            "/oauth2/register",
            new Dictionary<string, object>
            {
                ["grant_types"] = new[] { OpenIdProtocol.GrantTypes.ClientCredentials },
                ["token_endpoint_auth_method"] = OpenIdProtocol
                    .ClientAuthenticationMethods
                    .ClientSecretPost,
                ["backchannel_logout_uri"] = "https://client.example/logout",
                ["backchannel_logout_session_required"] = true,
            }
        );
        var registrationBody = await registrationResponse.Content.ReadAsStringAsync();
        Assert.True(registrationResponse.StatusCode == HttpStatusCode.Created, registrationBody);
        var registration = JsonElement.Parse(registrationBody);
        var root = registration;

        using var readRequest = new HttpRequestMessage(
            HttpMethod.Get,
            root.GetProperty("registration_client_uri").GetString()
        );
        readRequest.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            root.GetProperty("registration_access_token").GetString()
        );
        using var readResponse = await client.SendAsync(readRequest);
        var readBody = await readResponse.Content.ReadAsStringAsync();
        Assert.True(readResponse.StatusCode == HttpStatusCode.OK, readBody);
        var read = JsonElement.Parse(readBody);

        foreach (var jsonElement in new[] { root, read })
        {
            Assert.Equal(
                "https://client.example/logout",
                jsonElement.GetProperty("backchannel_logout_uri").GetString()
            );
            Assert.True(
                jsonElement.GetProperty("backchannel_logout_session_required").GetBoolean()
            );
        }
    }

    [Theory]
    [InlineData("http://client.example/logout")]
    [InlineData("https://client.example/logout#fragment")]
    public async Task PostRegister_WhenTheBackChannelLogoutUriIsNotHttps_ReturnsInvalidClientMetadata(
        string logoutUri
    )
    {
        using var factory = new AnonymousRegistrationApplicationFactory();
        var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }
        );

        using var response = await client.PostAsJsonAsync(
            "/oauth2/register",
            new Dictionary<string, object>
            {
                ["grant_types"] = new[] { OpenIdProtocol.GrantTypes.ClientCredentials },
                ["backchannel_logout_uri"] = logoutUri,
            }
        );

        await AssertRegistrationErrorAsync(
            response,
            OpenIdProtocol.ErrorCodes.InvalidClientMetadata,
            OpenIdSettingNames.BackChannelLogoutUri,
            CancellationToken.None
        );
    }

    [Fact]
    public async Task PostRegister_WithPostLogoutRedirectUris_StoresAndReportsThem()
    {
        using var factory = new AnonymousRegistrationApplicationFactory();
        var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }
        );

        using var registrationResponse = await client.PostAsJsonAsync(
            "/oauth2/register",
            new Dictionary<string, object>
            {
                ["redirect_uris"] = new[] { "https://client.example/callback" },
                ["post_logout_redirect_uris"] = new[] { "https://client.example/signed-out" },
            }
        );
        var registrationBody = await registrationResponse.Content.ReadAsStringAsync();
        Assert.True(registrationResponse.StatusCode == HttpStatusCode.Created, registrationBody);
        var registration = JsonElement.Parse(registrationBody);
        var root = registration;

        using var readRequest = new HttpRequestMessage(
            HttpMethod.Get,
            root.GetProperty("registration_client_uri").GetString()
        );
        readRequest.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            root.GetProperty("registration_access_token").GetString()
        );
        using var readResponse = await client.SendAsync(readRequest);
        var readBody = await readResponse.Content.ReadAsStringAsync();
        Assert.True(readResponse.StatusCode == HttpStatusCode.OK, readBody);
        var read = JsonElement.Parse(readBody);

        foreach (var jsonElement in new[] { root, read })
        {
            var uris = jsonElement.GetProperty("post_logout_redirect_uris");
            Assert.Equal(
                "https://client.example/signed-out",
                Assert.Single(uris.EnumerateArray()).GetString()
            );
        }
    }

    [Fact]
    public async Task PostRegister_WhenTheTenantDoesNotSupportFrontChannelLogout_ReturnsInvalidClientMetadata()
    {
        using var factory = new AnonymousRegistrationApplicationFactory();
        var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }
        );

        using var response = await client.PostAsJsonAsync(
            "/oauth2/register",
            new Dictionary<string, object>
            {
                ["redirect_uris"] = new[] { "https://client.example/callback" },
                ["frontchannel_logout_uri"] = "https://client.example/frontchannel-logout",
            }
        );

        await AssertRegistrationErrorAsync(
            response,
            OpenIdProtocol.ErrorCodes.InvalidClientMetadata,
            OpenIdSettingNames.FrontChannelLogoutUri,
            CancellationToken.None
        );
    }

    #endregion

    #region Response Caching

    private static Dictionary<string, object> CreateClientCredentialsRegistration() =>
        new()
        {
            ["grant_types"] = new[] { OpenIdProtocol.GrantTypes.ClientCredentials },
            ["token_endpoint_auth_method"] = OpenIdProtocol
                .ClientAuthenticationMethods
                .ClientSecretPost,
        };

    private static async ValueTask<HttpResponseMessage> PostClientCredentialsRegistrationAsync(
        HttpClient client,
        CancellationToken cancellationToken
    )
    {
        var registration = CreateClientCredentialsRegistration();
        return await client.PostAsJsonAsync("/oauth2/register", registration, cancellationToken);
    }

    private static async ValueTask<JsonElement> RegisterClientCredentialsClientAsync(
        HttpClient client,
        CancellationToken cancellationToken
    )
    {
        using var response = await PostClientCredentialsRegistrationAsync(
            client,
            cancellationToken
        );
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        Assert.True(response.StatusCode == HttpStatusCode.Created, body);
        return JsonElement.Parse(body);
    }

    // Builds a request to the client's configuration endpoint (RFC 7592), authenticated with its registration access
    // token.
    private static HttpRequestMessage CreateClientConfigurationRequest(
        HttpMethod method,
        JsonElement registration
    )
    {
        var registrationClientUri = registration.GetProperty("registration_client_uri").GetString();
        var registrationAccessToken = registration
            .GetProperty("registration_access_token")
            .GetString();
        var request = new HttpRequestMessage(method, registrationClientUri);
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            registrationAccessToken
        );
        return request;
    }

    [Fact]
    public async Task PostRegister_Always_MarksTheResponseNoStore()
    {
        using var factory = new AnonymousRegistrationApplicationFactory();
        var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }
        );

        using var registrationResponse = await PostClientCredentialsRegistrationAsync(
            client,
            CancellationToken.None
        );

        var registrationBody = await registrationResponse.Content.ReadAsStringAsync();
        Assert.True(registrationResponse.StatusCode == HttpStatusCode.Created, registrationBody);
        NoStoreAssert.IsNoStore(registrationResponse);
    }

    [Fact]
    public async Task GetRegistration_Always_MarksTheResponseNoStore()
    {
        using var factory = new AnonymousRegistrationApplicationFactory();
        var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }
        );
        var registration = await RegisterClientCredentialsClientAsync(
            client,
            CancellationToken.None
        );

        using var readRequest = CreateClientConfigurationRequest(HttpMethod.Get, registration);
        using var readResponse = await client.SendAsync(readRequest);

        var readBody = await readResponse.Content.ReadAsStringAsync();
        Assert.True(readResponse.StatusCode == HttpStatusCode.OK, readBody);
        NoStoreAssert.IsNoStore(readResponse);
    }

    [Fact]
    public async Task PutRegistration_Always_MarksTheResponseNoStore()
    {
        using var factory = new AnonymousRegistrationApplicationFactory();
        var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }
        );
        var registration = await RegisterClientCredentialsClientAsync(
            client,
            CancellationToken.None
        );
        var update = CreateClientCredentialsRegistration();
        update["client_id"] = registration.GetProperty("client_id").GetString() ?? string.Empty;

        using var updateRequest = CreateClientConfigurationRequest(HttpMethod.Put, registration);
        updateRequest.Content = JsonContent.Create(update);
        using var updateResponse = await client.SendAsync(updateRequest);

        var updateBody = await updateResponse.Content.ReadAsStringAsync();
        Assert.True(updateResponse.StatusCode == HttpStatusCode.OK, updateBody);
        NoStoreAssert.IsNoStore(updateResponse);
    }

    [Fact]
    public async Task DeleteRegistration_Always_MarksTheResponseNoStore()
    {
        using var factory = new AnonymousRegistrationApplicationFactory();
        var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }
        );
        var registration = await RegisterClientCredentialsClientAsync(
            client,
            CancellationToken.None
        );

        using var deleteRequest = CreateClientConfigurationRequest(HttpMethod.Delete, registration);
        using var deleteResponse = await client.SendAsync(deleteRequest);

        var deleteBody = await deleteResponse.Content.ReadAsStringAsync();
        Assert.True(deleteResponse.StatusCode == HttpStatusCode.NoContent, deleteBody);
        NoStoreAssert.IsNoStore(deleteResponse);
    }

    #endregion

    private static async ValueTask<(
        string ClientId,
        string ClientSecret,
        JsonElement Registration
    )> RegisterSharedSecretClientAsync(
        HttpClient client,
        string tokenEndpointAuthMethod,
        CancellationToken cancellationToken
    )
    {
        using var response = await client.PostAsJsonAsync(
            "/oauth2/register",
            new Dictionary<string, object>
            {
                ["grant_types"] = new[] { OpenIdProtocol.GrantTypes.ClientCredentials },
                ["token_endpoint_auth_method"] = tokenEndpointAuthMethod,
            },
            cancellationToken
        );
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        Assert.True(response.StatusCode == HttpStatusCode.Created, body);
        var registration = JsonElement.Parse(body);
        var clientId = registration.GetProperty("client_id").GetString();
        var clientSecret = registration.GetProperty("client_secret").GetString();
        Assert.NotNull(clientId);
        Assert.NotNull(clientSecret);
        return (clientId, clientSecret, registration);
    }

    private static ValueTask<JsonElement> UpdateAuthenticationMethodAsync(
        HttpClient client,
        JsonElement registration,
        string tokenEndpointAuthMethod,
        CancellationToken cancellationToken
    ) =>
        UpdateRegistrationAsync(
            client,
            registration,
            new Dictionary<string, object>
            {
                ["grant_types"] = new[] { OpenIdProtocol.GrantTypes.ClientCredentials },
                ["token_endpoint_auth_method"] = tokenEndpointAuthMethod,
            },
            cancellationToken
        );

    private static async ValueTask<JsonElement> UpdateRegistrationAsync(
        HttpClient client,
        JsonElement registration,
        Dictionary<string, object> metadata,
        CancellationToken cancellationToken
    )
    {
        metadata["client_id"] = registration.GetProperty("client_id").GetString() ?? string.Empty;
        using var request = new HttpRequestMessage(
            HttpMethod.Put,
            registration.GetProperty("registration_client_uri").GetString()
        )
        {
            Content = JsonContent.Create(metadata),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            registration.GetProperty("registration_access_token").GetString()
        );

        using var response = await client.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        Assert.True(response.StatusCode == HttpStatusCode.OK, body);
        return JsonElement.Parse(body);
    }
}
