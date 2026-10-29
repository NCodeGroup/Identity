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
using Microsoft.AspNetCore.Mvc.Testing;
using NCode.Identity.OpenId.IntegrationTests.Infrastructure;
using Xunit;

namespace NCode.Identity.OpenId.IntegrationTests.Endpoints;

public class DiscoveryEndpointTests(HostApplicationFactory factory)
    : IClassFixture<HostApplicationFactory>
{
    private HostApplicationFactory Factory { get; } = factory;

    [Fact]
    public async Task GetDiscovery_ReturnsMetadataWithIssuerAndJwksUri()
    {
        var client = Factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }
        );

        using var response = await client.GetAsync("/.well-known/openid-configuration");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        var jsonElement = JsonElement.Parse(json);
        var root = jsonElement;

        Assert.True(root.TryGetProperty("issuer", out var issuer));
        Assert.False(string.IsNullOrEmpty(issuer.GetString()));

        // The JWKS endpoint is auto-advertised via its endpoint name ("jwks_uri").
        Assert.True(root.TryGetProperty("jwks_uri", out var jwksUri));
        Assert.False(string.IsNullOrEmpty(jwksUri.GetString()));

        // 'discovery_endpoint' is not a standard metadata parameter; the document must not advertise itself.
        Assert.False(root.TryGetProperty("discovery_endpoint", out _));
    }

    [Fact]
    public async Task GetDiscovery_DoesNotExposeNonDiscoverableSettings_EvenWithShowAllQuery()
    {
        var client = Factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }
        );

        // The 'showAll' override was removed; passing it must have no effect and non-discoverable
        // settings must stay hidden from anonymous discovery.
        using var response = await client.GetAsync(
            "/.well-known/openid-configuration?showAll=true"
        );

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        var jsonElement = JsonElement.Parse(json);
        var root = jsonElement;

        // These settings carry a default (so they exist in the effective collection) but are
        // marked non-discoverable, so they must never appear in the discovery document.
        Assert.False(root.TryGetProperty("access_token_lifetime", out _));
        Assert.False(root.TryGetProperty("require_pkce", out _));
    }

    [Fact]
    public async Task GetDiscovery_AdvertisesClaimsParameterSupported()
    {
        var client = Factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }
        );

        using var response = await client.GetAsync("/.well-known/openid-configuration");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        var jsonElement = JsonElement.Parse(json);
        var root = jsonElement;

        // The 'claims' request parameter is now honored end-to-end (ADR-0039), so it is advertised.
        Assert.True(
            root.TryGetProperty("claims_parameter_supported", out var claimsParameterSupported)
        );
        Assert.True(claimsParameterSupported.GetBoolean());
    }

    [Fact]
    public async Task GetDiscovery_AdvertisesAuthorizationResponseIssParameterSupported()
    {
        var client = Factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }
        );

        using var response = await client.GetAsync("/.well-known/openid-configuration");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        var jsonElement = JsonElement.Parse(json);
        var root = jsonElement;

        // The authorization response always carries the 'iss' parameter (RFC 9207), so the OP advertises
        // 'authorization_response_iss_parameter_supported' as true so relying parties can validate it.
        Assert.True(
            root.TryGetProperty(
                "authorization_response_iss_parameter_supported",
                out var issParameterSupported
            )
        );
        Assert.True(issParameterSupported.GetBoolean());
    }

    [Fact]
    public async Task GetDiscovery_AdvertisesJwtIntrospectionAndTheEndpointsClientAuthentication()
    {
        var client = Factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }
        );

        using var response = await client.GetAsync("/.well-known/openid-configuration");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        var jsonElement = JsonElement.Parse(json);
        var root = jsonElement;

        // RFC 9701 §7: the algorithms a JWT introspection response may be signed and encrypted with.
        Assert.Contains(
            "RS256",
            root.GetProperty("introspection_signing_alg_values_supported")
                .EnumerateArray()
                .Select(value => value.GetString())
        );
        Assert.True(root.TryGetProperty("introspection_encryption_alg_values_supported", out _));
        Assert.True(root.TryGetProperty("introspection_encryption_enc_values_supported", out _));

        // RFC 8414 §2: the introspection and revocation endpoints authenticate clients as the token endpoint does.
        var tokenMethods = root.GetProperty("token_endpoint_auth_methods_supported").GetRawText();
        Assert.Equal(
            tokenMethods,
            root.GetProperty("introspection_endpoint_auth_methods_supported").GetRawText()
        );
        Assert.Equal(
            tokenMethods,
            root.GetProperty("revocation_endpoint_auth_methods_supported").GetRawText()
        );
        Assert.Equal(
            root.GetProperty("token_endpoint_auth_signing_alg_values_supported").GetRawText(),
            root.GetProperty("introspection_endpoint_auth_signing_alg_values_supported")
                .GetRawText()
        );
    }

    [Fact]
    public async Task GetDiscovery_AdvertisesOnlySecureFlows()
    {
        var client = Factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }
        );

        using var response = await client.GetAsync("/.well-known/openid-configuration");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        var jsonElement = JsonElement.Parse(json);
        var root = jsonElement;

        // Secure-by-default: only the authorization-code flow is advertised — no implicit/hybrid response
        // types, and no 'implicit' grant type (RFC 9700 / OAuth 2.1 direction).
        var responseTypes = GetStringArray(root, "response_types_supported");
        Assert.Equal(["code"], responseTypes);

        var grantTypes = GetStringArray(root, "grant_types_supported");
        Assert.DoesNotContain(OpenIdProtocol.GrantTypes.Implicit, grantTypes);
        Assert.Contains(OpenIdProtocol.GrantTypes.AuthorizationCode, grantTypes);

        // PKCE advertises only S256 (never 'plain').
        var codeChallengeMethods = GetStringArray(root, "code_challenge_methods_supported");
        Assert.Equal(["S256"], codeChallengeMethods);

        // subject_types_supported is required by OIDC Discovery; the default advertises both 'public' and 'pairwise'.
        var subjectTypes = GetStringArray(root, "subject_types_supported");
        Assert.Equal(["public", "pairwise"], subjectTypes);

        // id_token signing crosses a trust boundary, so no symmetric (HS*) algorithm is advertised.
        var idTokenSigningAlgs = GetStringArray(root, "id_token_signing_alg_values_supported");
        Assert.NotEmpty(idTokenSigningAlgs);
        Assert.DoesNotContain(
            idTokenSigningAlgs,
            alg => alg.StartsWith("HS", StringComparison.Ordinal)
        );
    }

    private static string[] GetStringArray(JsonElement root, string propertyName)
    {
        Assert.True(root.TryGetProperty(propertyName, out var element));
        return element.EnumerateArray().Select(value => value.GetString()!).ToArray();
    }
}
