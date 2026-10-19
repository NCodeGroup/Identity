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
using System.Web;
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

        // The implicit and hybrid response types the operator listed are advertised exactly as listed: discovery does
        // not expand them into combinations the operator did not enable ('id_token' alone, 'code token', ...).
        var responseTypes = GetStringArray(root, "response_types_supported");
        Assert.Equal(
            [
                OpenIdConstants.ResponseTypes.Code,
                OpenIdConstants.ResponseTypes.CodeIdToken,
                OpenIdConstants.ResponseTypes.Token,
            ],
            responseTypes.Order(StringComparer.Ordinal)
        );

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

    [Fact]
    public async Task GetAuthorize_WithAResponseTypeThatIsNotListed_IsRejectedAsUnsupported()
    {
        const string clientId = "it-insecure-unlisted-client";
        await Factory.SeedPublicClientAsync(clientId, RedirectUri);

        var query =
            $"?client_id={clientId}"
            + $"&redirect_uri={Uri.EscapeDataString(RedirectUri)}"
            + "&response_type=id_token"
            + "&scope=openid"
            + "&nonce=n-0S6_WzA2Mj"
            + "&state=abc123";

        using var response = await CreateClient().GetAsync("/oauth2/authorize" + query);

        // 'id_token' is not listed, although 'code id_token' is: a response type is enabled as a whole value, never
        // through the names it contains. The error returns to the client in the fragment, the default for 'id_token'.
        Assert.Equal(HttpStatusCode.SeeOther, response.StatusCode);
        var location = response.Headers.Location;
        Assert.NotNull(location);
        Assert.StartsWith(RedirectUri, location.GetLeftPart(UriPartial.Path));
        var parameters = HttpUtility.ParseQueryString(location.Fragment.TrimStart('#'));
        Assert.Equal(OpenIdConstants.ErrorCodes.UnsupportedResponseType, parameters["error"]);
    }

    [Fact]
    public async Task GetAuthorize_WithAHybridResponseType_ReturnsTheResponseInTheFragment()
    {
        const string clientId = "it-insecure-hybrid-client";
        await Factory.SeedPublicClientAsync(clientId, RedirectUri);

        var query =
            $"?client_id={clientId}"
            + $"&redirect_uri={Uri.EscapeDataString(RedirectUri)}"
            + "&response_type=id_token%20code"
            + "&scope=openid"
            + "&nonce=n-0S6_WzA2Mj"
            + "&state=abc123"
            + "&prompt=none";

        using var response = await CreateClient().GetAsync("/oauth2/authorize" + query);

        // A response type that returns an ID token is answered in the fragment by default, never in the query string:
        // with no end-user session, prompt=none yields login_required, which travels the same way a token would.
        Assert.Equal(HttpStatusCode.SeeOther, response.StatusCode);
        var location = response.Headers.Location;
        Assert.NotNull(location);
        Assert.Equal(RedirectUri, location.GetLeftPart(UriPartial.Path));
        Assert.Empty(location.Query);
        var parameters = HttpUtility.ParseQueryString(location.Fragment.TrimStart('#'));
        Assert.Equal(OpenIdConstants.ErrorCodes.LoginRequired, parameters["error"]);
        Assert.Equal("abc123", parameters["state"]);
    }

    private static string[] GetStringArray(JsonElement root, string propertyName)
    {
        Assert.True(root.TryGetProperty(propertyName, out var element));
        return element.EnumerateArray().Select(value => value.GetString()!).ToArray();
    }
}
