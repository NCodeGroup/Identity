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
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NCode.Identity.OpenId.Errors;
using NCode.Identity.OpenId.IntegrationTests.Infrastructure;
using NCode.Identity.OpenId.Settings;
using Xunit;

namespace NCode.Identity.OpenId.IntegrationTests.Endpoints;

/// <summary>
/// Verifies the <c>security_profiles</c> mechanism end to end: selecting <c>fapi2</c> tightens a client's effective
/// settings through the merge lattice, so weak (<c>client_secret</c>) authentication is rejected while a non-profiled
/// client with the same credentials succeeds; and a profile selected at the server binds every client below it, which
/// can add a profile but never clear or replace one.
/// </summary>
public class TokenEndpointSecurityProfileTests
{
    private const string ClientSecret = "confidential-secret-value-0123456789";
    private const string TokenPath = "/oauth2/token";
    private const string DiscoveryPath = "/.well-known/openid-configuration";

    private static string[] StrongAuthMethods { get; } =
    [
        OpenIdConstants.ClientAuthenticationMethods.PrivateKeyJwt,
        OpenIdConstants.ClientAuthenticationMethods.TlsClientAuth,
        OpenIdConstants.ClientAuthenticationMethods.SelfSignedTlsClientAuth,
    ];

    private static HttpClient CreateClient(HostApplicationFactory factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    private static FormUrlEncodedContent ClientSecretPostBody(string clientId) =>
        new(
            new Dictionary<string, string>
            {
                ["grant_type"] = OpenIdProtocol.GrantTypes.ClientCredentials,
                ["scope"] = TestServerSettingsProvider.ApiScope,
                ["client_id"] = clientId,
                ["client_secret"] = ClientSecret,
            }
        );

    private static async Task SeedClientCredentialsClientAsync(
        HostApplicationFactory factory,
        string clientId,
        IReadOnlyCollection<string> securityProfiles
    )
    {
        await factory.SeedConfidentialClientWithSecurityProfilesAsync(
            clientId,
            ClientSecret,
            securityProfiles,
            OpenIdProtocol.GrantTypes.ClientCredentials
        );
        await factory.SeedResourceServerWithClientGrantAsync(
            clientId,
            $"{clientId}-rs-api",
            $"https://api.{clientId}.integration.test",
            TestServerSettingsProvider.ApiScope
        );
    }

    private static async Task<string?> GetErrorDescriptionAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.TryGetProperty("error_description", out var description)
            ? description.GetString()
            : null;
    }

    [Fact]
    public async Task PostToken_ClientSecretPost_WithoutProfile_ReturnsAccessToken()
    {
        const string clientId = "it-profile-baseline";

        using var factory = new HostApplicationFactory();
        await SeedClientCredentialsClientAsync(factory, clientId, []);

        var client = CreateClient(factory);
        using var response = await client.PostAsync(TokenPath, ClientSecretPostBody(clientId));

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, body);
    }

    [Fact]
    public async Task PostToken_ClientSecretPost_UnderFapi2_Returns400()
    {
        const string clientId = "it-profile-fapi2";

        using var factory = new HostApplicationFactory();
        await SeedClientCredentialsClientAsync(
            factory,
            clientId,
            [OpenIdConstants.SecurityProfiles.Fapi2]
        );

        var client = CreateClient(factory);
        using var response = await client.PostAsync(TokenPath, ClientSecretPostBody(clientId));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(
            OpenIdProtocol.ErrorCodes.InvalidClient,
            document.RootElement.GetProperty("error").GetString()
        );
    }

    [Fact]
    public async Task PostToken_ClientSecretPost_UnderFapi1Advanced_Returns400()
    {
        const string clientId = "it-profile-fapi1-advanced";

        using var factory = new HostApplicationFactory();
        await SeedClientCredentialsClientAsync(
            factory,
            clientId,
            [OpenIdConstants.SecurityProfiles.Fapi1Advanced]
        );

        var client = CreateClient(factory);
        using var response = await client.PostAsync(TokenPath, ClientSecretPostBody(clientId));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(
            OpenIdProtocol.ErrorCodes.InvalidClient,
            document.RootElement.GetProperty("error").GetString()
        );
    }

    [Fact]
    public async Task PostToken_WhenStoredProfileIsNotRegistered_IsRefusedRatherThanServedWithoutIt()
    {
        const string clientId = "it-profile-unknown";

        using var factory = new HostApplicationFactory();
        // The store is written directly, past every entry point that would reject the misspelled profile name.
        await SeedClientCredentialsClientAsync(factory, clientId, ["fapi-2"]);

        var client = CreateClient(factory);
        using var response = await client.PostAsync(TokenPath, ClientSecretPostBody(clientId));

        var body = await response.Content.ReadAsStringAsync();
        Assert.False(response.IsSuccessStatusCode, body);
        Assert.DoesNotContain("access_token", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PostToken_WhenTheSelectedProfilesImposeNoRequirement_ReturnsAccessToken()
    {
        const string clientId = "it-profile-permissive";

        using var factory = new ProfiledHostApplicationFactory(PermissiveProfile.Name);
        await SeedClientCredentialsClientAsync(factory, clientId, []);

        var client = CreateClient(factory);
        using var response = await client.PostAsync(TokenPath, ClientSecretPostBody(clientId));

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, body);
    }

    [Fact]
    public async Task PostToken_WhenTheServerSelectsAProfile_AClientSelectingNoneIsStillBoundByIt()
    {
        const string clientId = "it-profile-server-mandated";

        using var factory = new ProfiledHostApplicationFactory(RefusingProfile.Name);
        await SeedClientCredentialsClientAsync(factory, clientId, []);

        var client = CreateClient(factory);
        using var response = await client.PostAsync(TokenPath, ClientSecretPostBody(clientId));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(RefusingRequirement.Description, await GetErrorDescriptionAsync(response));
    }

    [Fact]
    public async Task PostToken_WhenAClientSelectsAnotherProfile_TheServerProfileStillApplies()
    {
        const string clientId = "it-profile-server-and-client";

        using var factory = new ProfiledHostApplicationFactory(RefusingProfile.Name);
        await SeedClientCredentialsClientAsync(factory, clientId, [PermissiveProfile.Name]);

        var client = CreateClient(factory);
        using var response = await client.PostAsync(TokenPath, ClientSecretPostBody(clientId));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(RefusingRequirement.Description, await GetErrorDescriptionAsync(response));
    }

    [Fact]
    public async Task GetDiscovery_WhenTheServerSelectsFapi2_AdvertisesItsCeilings()
    {
        using var factory = new ProfiledHostApplicationFactory(
            OpenIdConstants.SecurityProfiles.Fapi2
        );

        var client = CreateClient(factory);
        using var response = await client.GetAsync(DiscoveryPath);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var metadata = document.RootElement;
        Assert.True(metadata.GetProperty("require_pushed_authorization_requests").GetBoolean());
        var authMethods = metadata
            .GetProperty("token_endpoint_auth_methods_supported")
            .EnumerateArray()
            .Select(method => method.GetString() ?? string.Empty)
            .ToList();
        Assert.NotEmpty(authMethods);
        Assert.All(authMethods, method => Assert.Contains(method, StrongAuthMethods));
    }

    [Fact]
    public async Task GetDiscovery_WhenTheServerSelectsFapi1Advanced_AdvertisesItsCeilings()
    {
        using var factory = new ProfiledHostApplicationFactory(
            OpenIdConstants.SecurityProfiles.Fapi1Advanced
        );

        var client = CreateClient(factory);
        using var response = await client.GetAsync(DiscoveryPath);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var metadata = document.RootElement;
        Assert.True(metadata.GetProperty("require_pushed_authorization_requests").GetBoolean());
        Assert.True(
            metadata.GetProperty("tls_client_certificate_bound_access_tokens").GetBoolean()
        );
        var authMethods = ReadStrings(metadata, "token_endpoint_auth_methods_supported");
        Assert.NotEmpty(authMethods);
        Assert.All(authMethods, method => Assert.Contains(method, StrongAuthMethods));
        Assert.Equal(
            [OpenIdConstants.CodeChallengeMethods.S256],
            ReadStrings(metadata, "code_challenge_methods_supported")
        );
        Assert.Equal(
            [OpenIdProtocol.ResponseTypes.Code],
            ReadStrings(metadata, "response_types_supported")
        );
        string[] fapi1SigningAlgorithms = ["PS256", "ES256"];
        var signingAlgorithms = ReadStrings(metadata, "id_token_signing_alg_values_supported");
        Assert.NotEmpty(signingAlgorithms);
        Assert.All(signingAlgorithms, alg => Assert.Contains(alg, fapi1SigningAlgorithms));
    }

    [Fact]
    public async Task GetDiscovery_WhenThePersistedServerSelectsFapi2_AdvertisesItsCeilings()
    {
        using var factory = new HostApplicationFactory();
        await factory.SeedServerSettingsAsync(
            new Dictionary<string, object>
            {
                ["security_profiles"] = (string[])[OpenIdConstants.SecurityProfiles.Fapi2],
            }
        );

        var client = CreateClient(factory);
        using var response = await client.GetAsync(DiscoveryPath);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(
            document.RootElement.GetProperty("require_pushed_authorization_requests").GetBoolean()
        );
    }

    #region Scaffolding

    private static List<string> ReadStrings(JsonElement metadata, string name) =>
        metadata
            .GetProperty(name)
            .EnumerateArray()
            .Select(value => value.GetString() ?? string.Empty)
            .ToList();

    /// <summary>
    /// A host whose server settings select the supplied security profiles, with two test profiles registered.
    /// </summary>
    private sealed class ProfiledHostApplicationFactory(params string[] serverProfiles)
        : HostApplicationFactory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);

            var configuration = serverProfiles.Select(
                (profileName, index) =>
                    KeyValuePair.Create<string, string?>(
                        $"OpenId:Server:Settings:security_profiles:{index}",
                        profileName
                    )
            );
            builder.ConfigureAppConfiguration(configurationBuilder =>
                configurationBuilder.AddInMemoryCollection(configuration)
            );

            builder.ConfigureTestServices(serviceCollection =>
            {
                serviceCollection.TryAddEnumerable(
                    ServiceDescriptor.Singleton<ISecurityProfile, RefusingProfile>()
                );
                serviceCollection.TryAddEnumerable(
                    ServiceDescriptor.Singleton<ISecurityProfile, PermissiveProfile>()
                );
            });
        }
    }

    private sealed class RefusingProfile : ISecurityProfile
    {
        public const string Name = "it-refusing";

        public string ProfileName => Name;

        public IReadOnlyCollection<ISecurityProfileRequirement> Requirements { get; } =
        [new RefusingRequirement()];

        public void ApplyCeilingSettings(ISecurityProfileCeilingBuilder ceiling) { }

        public void ApplyDefaultSettings(ISecurityProfileDefaultsBuilder defaults) { }
    }

    private sealed class PermissiveProfile : ISecurityProfile
    {
        public const string Name = "it-permissive";

        public string ProfileName => Name;

        public IReadOnlyCollection<ISecurityProfileRequirement> Requirements { get; } = [];

        public void ApplyCeilingSettings(ISecurityProfileCeilingBuilder ceiling) { }

        public void ApplyDefaultSettings(ISecurityProfileDefaultsBuilder defaults) { }
    }

    private sealed class RefusingRequirement : ITokenRequestProfileRequirement
    {
        public const string Description = "The it-refusing profile refuses every token request.";

        public ValueTask EvaluateAsync(
            TokenRequestProfileContext context,
            CancellationToken cancellationToken
        ) => throw context.OpenIdContext.ErrorFactory.InvalidRequest(Description).AsException();
    }

    #endregion
}
