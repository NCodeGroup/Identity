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

/// <summary>
/// Confirms that an operator can re-enable the not-recommended capabilities the library omits from its secure
/// defaults (ADR-0083) purely through configuration, and that the widened values are then both advertised in
/// discovery and accepted by the authorization endpoint.
/// </summary>
public class DiscoveryInsecureOverrideTests(InsecureConfigApplicationFactory factory)
    : IClassFixture<InsecureConfigApplicationFactory>
{
    private const string ClientId = "it-insecure-client";
    private const string RedirectUri = "https://client.example/callback";

    private InsecureConfigApplicationFactory Factory { get; } = factory;

    private HttpClient CreateClient() =>
        Factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    [Fact]
    public async Task Discovery_WhenOperatorWidensCeilings_AdvertisesInsecureCapabilities()
    {
        using var response = await CreateClient().GetAsync("/.well-known/openid-configuration");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;

        // The implicit/hybrid response-type combinations are advertised once the ceiling is widened.
        var responseTypes = GetStringArray(root, "response_types_supported");
        Assert.Contains(OpenIdConstants.ResponseTypes.Token, responseTypes);
        Assert.Contains(OpenIdConstants.ResponseTypes.IdToken, responseTypes);
        Assert.Contains("code id_token", responseTypes); // a hybrid combination

        // The 'implicit' grant type is advertised.
        var grantTypes = GetStringArray(root, "grant_types_supported");
        Assert.Contains(OpenIdConstants.GrantTypes.Implicit, grantTypes);

        // PKCE 'plain' is advertised alongside 'S256'.
        var codeChallengeMethods = GetStringArray(root, "code_challenge_methods_supported");
        Assert.Contains(OpenIdConstants.CodeChallengeMethods.Plain, codeChallengeMethods);
    }

    [Fact]
    public async Task GetAuthorize_WithImplicitResponseType_IsAcceptedAfterOverride()
    {
        await Factory.SeedPublicClientAsync(ClientId, RedirectUri);

        var query =
            $"?client_id={ClientId}"
            + $"&redirect_uri={Uri.EscapeDataString(RedirectUri)}"
            + "&response_type=token"
            + "&scope=openid"
            + "&nonce=n-0S6_WzA2Mj"
            + "&state=abc123";

        using var response = await CreateClient().GetAsync("/oauth2/authorize" + query);

        // The widened ceiling accepts the implicit 'token' response type: the request passes supported-values
        // validation and, with no end-user session, proceeds to the interactive challenge (a redirect that is
        // NOT the error-to-client redirect the secure default would return).
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location;
        Assert.NotNull(location);
        Assert.False(
            location.OriginalString.StartsWith(RedirectUri, StringComparison.Ordinal),
            "Expected an interactive challenge, not an error redirected back to the client."
        );
    }

    private static string[] GetStringArray(JsonElement root, string propertyName)
    {
        Assert.True(root.TryGetProperty(propertyName, out var element));
        return element.EnumerateArray().Select(value => value.GetString()!).ToArray();
    }
}
