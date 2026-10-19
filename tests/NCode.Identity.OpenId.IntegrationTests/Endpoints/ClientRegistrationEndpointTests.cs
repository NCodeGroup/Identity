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
using System.Net.Http.Json;
using System.Text.Json;
using System.Web;
using Microsoft.AspNetCore.Mvc.Testing;
using NCode.Identity.OpenId.IntegrationTests.Infrastructure;
using NCode.Identity.OpenId.Settings;
using Xunit;

namespace NCode.Identity.OpenId.IntegrationTests.Endpoints;

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
                ["grant_types"] = new[] { OpenIdConstants.GrantTypes.ClientCredentials },
                ["token_endpoint_auth_method"] = OpenIdConstants
                    .ClientAuthenticationMethods
                    .ClientSecretPost,
                ["client_name"] = "Registered Client",
                ["contacts"] = new[] { "ops@example.com" },
                ["software_id"] = "integration-tests",
                ["software_version"] = "1.0",
                ["scope"] = $"openid {TestServerSettingsProvider.ApiScope}",
            }
        );

        var registrationBody = await registrationResponse.Content.ReadAsStringAsync();
        Assert.True(registrationResponse.StatusCode == HttpStatusCode.Created, registrationBody);

        using var registration = JsonDocument.Parse(registrationBody);
        var root = registration.RootElement;
        var clientId = root.GetProperty("client_id").GetString();
        var clientSecret = root.GetProperty("client_secret").GetString();
        Assert.NotNull(clientId);
        Assert.NotNull(clientSecret);
        Assert.Equal("Registered Client", root.GetProperty("client_name").GetString());
        Assert.Equal("integration-tests", root.GetProperty("software_id").GetString());

        // The response reports the scopes the client may actually request: it holds no grant to the API, so the
        // requested 'api' scope is not among them.
        var scopes = root.GetProperty("scope").GetString()?.Split(' ') ?? [];
        Assert.Contains(OpenIdConstants.ScopeTypes.OpenId, scopes);
        Assert.DoesNotContain(TestServerSettingsProvider.ApiScope, scopes);

        await factory.SeedResourceServerWithClientGrantAsync(
            clientId,
            "it-rs-api",
            "https://api.integration.test",
            TestServerSettingsProvider.ApiScope
        );

        // Authenticating the client loads its persisted settings (every registered metadata key included) through the
        // setting serializer, which rejects any name without a registered descriptor.
        using var tokenContent = new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["grant_type"] = OpenIdConstants.GrantTypes.ClientCredentials,
                ["client_id"] = clientId,
                ["client_secret"] = clientSecret,
                ["scope"] = TestServerSettingsProvider.ApiScope,
            }
        );

        using var tokenResponse = await client.PostAsync("/oauth2/token", tokenContent);

        var tokenBody = await tokenResponse.Content.ReadAsStringAsync();
        Assert.True(tokenResponse.StatusCode == HttpStatusCode.OK, tokenBody);
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
            OpenIdConstants.ErrorCodes.InvalidClientMetadata,
            settingName
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
                ["grant_types"] = new[] { OpenIdConstants.GrantTypes.ClientCredentials },
                ["jwks_uri"] = "http://client.example/jwks",
            }
        );

        await AssertRegistrationErrorAsync(
            response,
            OpenIdConstants.ErrorCodes.InvalidClientMetadata,
            OpenIdSettingNames.JwksUri
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
                ["grant_types"] = new[] { OpenIdConstants.GrantTypes.ClientCredentials },
                ["redirect_uris"] = new[] { "/callback" },
            }
        );

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.BadRequest, body);

        using var document = JsonDocument.Parse(body);
        Assert.Equal(
            OpenIdConstants.ErrorCodes.InvalidRedirectUri,
            document.RootElement.GetProperty("error").GetString()
        );
    }

    [Fact]
    public async Task PostRegister_WithAHybridResponseType_RegistersAClientTheAuthorizationEndpointHonors()
    {
        using var factory = new ConfiguredHostApplicationFactory(
            CreateServerConfiguration(
                [OpenIdConstants.ResponseTypes.Code, OpenIdConstants.ResponseTypes.CodeIdToken],
                [OpenIdConstants.GrantTypes.AuthorizationCode, OpenIdConstants.GrantTypes.Implicit]
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

        using var registration = JsonDocument.Parse(registrationBody);
        var root = registration.RootElement;
        var clientId = root.GetProperty("client_id").GetString();
        Assert.NotNull(clientId);

        // The response type is registered, and reported, in canonical form together with the grant types it uses.
        Assert.Equal(
            [OpenIdConstants.ResponseTypes.CodeIdToken],
            GetStringArray(root, "response_types")
        );
        Assert.Equal(
            [OpenIdConstants.GrantTypes.AuthorizationCode, OpenIdConstants.GrantTypes.Implicit],
            GetStringArray(root, "grant_types")
        );

        // The authorization endpoint accepts the registered response type: with no end-user session it issues the
        // interactive challenge rather than an error redirected to the client.
        using var hybridResponse = await client.GetAsync(
            CreateAuthorizeUri(clientId, OpenIdConstants.ResponseTypes.CodeIdToken)
        );
        Assert.Equal(HttpStatusCode.Redirect, hybridResponse.StatusCode);
        var hybridLocation = hybridResponse.Headers.Location;
        Assert.NotNull(hybridLocation);
        Assert.False(
            hybridLocation.OriginalString.StartsWith(RedirectUri, StringComparison.Ordinal),
            "Expected an interactive challenge, not an error redirected back to the client."
        );

        // The server supports 'code', but the client did not register it.
        using var codeResponse = await client.GetAsync(
            CreateAuthorizeUri(clientId, OpenIdConstants.ResponseTypes.Code)
        );
        Assert.Equal(HttpStatusCode.SeeOther, codeResponse.StatusCode);
        var codeLocation = codeResponse.Headers.Location;
        Assert.NotNull(codeLocation);
        var parameters = HttpUtility.ParseQueryString(codeLocation.Query);
        Assert.Equal(OpenIdConstants.ErrorCodes.UnauthorizedClient, parameters["error"]);
    }

    [Fact]
    public async Task PostRegister_WhenTheServerDoesNotSupportAResponseType_ReturnsInvalidClientMetadata()
    {
        using var factory = new ConfiguredHostApplicationFactory(
            CreateServerConfiguration(
                [OpenIdConstants.ResponseTypes.Code],
                [OpenIdConstants.GrantTypes.AuthorizationCode, OpenIdConstants.GrantTypes.Implicit]
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
                ["response_types"] = new[] { OpenIdConstants.ResponseTypes.CodeIdToken },
            }
        );

        await AssertRegistrationErrorAsync(
            response,
            OpenIdConstants.ErrorCodes.InvalidClientMetadata,
            "response_types"
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
                ["grant_types"] = new[] { OpenIdConstants.GrantTypes.AuthorizationCode },
                ["response_types"] = new[] { OpenIdConstants.ResponseTypes.CodeIdToken },
            }
        );

        await AssertRegistrationErrorAsync(
            response,
            OpenIdConstants.ErrorCodes.InvalidClientMetadata,
            OpenIdConstants.GrantTypes.Implicit
        );
    }

    private static Dictionary<string, string?> CreateServerConfiguration(
        string[] responseTypesSupported,
        string[] grantTypesSupported
    )
    {
        var configuration = new Dictionary<string, string?>
        {
            ["OpenId:Server:Settings:registration_endpoint_allow_anonymous"] = "true",
        };
        for (var index = 0; index < responseTypesSupported.Length; ++index)
            configuration[$"OpenId:Server:Settings:response_types_supported:{index}"] =
                responseTypesSupported[index];
        for (var index = 0; index < grantTypesSupported.Length; ++index)
            configuration[$"OpenId:Server:Settings:grant_types_supported:{index}"] =
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
        + "&state=abc123";

    private static string[] GetStringArray(JsonElement root, string propertyName) =>
        [
            .. root.GetProperty(propertyName)
                .EnumerateArray()
                .Select(value => value.GetString() ?? string.Empty),
        ];

    private static async Task AssertRegistrationErrorAsync(
        HttpResponseMessage response,
        string expectedError,
        string expectedQuotedName
    )
    {
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.BadRequest, body);

        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;
        Assert.Equal(expectedError, root.GetProperty("error").GetString());
        Assert.Contains(
            $"'{expectedQuotedName}'",
            root.GetProperty("error_description").GetString(),
            StringComparison.Ordinal
        );
    }
}
