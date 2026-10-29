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

public class AuthorizationEndpointSeededClientTests(HostApplicationFactory factory)
    : IClassFixture<HostApplicationFactory>
{
    private const string ClientId = "it-authorize-client";
    private const string RedirectUri = "https://client.example/callback";

    private HostApplicationFactory Factory { get; } = factory;

    [Fact]
    public async Task GetAuthorize_WithSeededClient_ResolvesClientAndProcessesRequest()
    {
        await Factory.SeedPublicClientAsync(ClientId, RedirectUri);

        var client = Factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }
        );

        var query =
            $"?client_id={ClientId}"
            + $"&redirect_uri={Uri.EscapeDataString(RedirectUri)}"
            + "&response_type=code"
            + "&scope=openid"
            + "&state=abc123"
            + TestPkce.QueryParameters;

        using var response = await client.GetAsync("/oauth2/authorize" + query);

        // The seeded client resolves and the request is loaded/validated; with no authenticated
        // end-user the authorization handler issues an interactive challenge (a redirect), rather
        // than the missing-client 400 returned when no client can be resolved.
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    }

    [Fact]
    public async Task GetAuthorize_WithPromptNoneAndNoSession_RedirectsToClientWithError()
    {
        await Factory.SeedPublicClientAsync(ClientId, RedirectUri);

        var client = Factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }
        );

        var query =
            $"?client_id={ClientId}"
            + $"&redirect_uri={Uri.EscapeDataString(RedirectUri)}"
            + "&response_type=code"
            + "&scope=openid"
            + "&state=abc123"
            + "&prompt=none"
            + TestPkce.QueryParameters;

        using var response = await client.GetAsync("/oauth2/authorize" + query);

        // prompt=none with no active session cannot interactively re-authenticate, so the server
        // redirects the error back to the client's registered redirect_uri (303 See Other).
        Assert.Equal(HttpStatusCode.SeeOther, response.StatusCode);
        var location = response.Headers.Location;
        Assert.NotNull(location);
        Assert.StartsWith(RedirectUri, location.GetLeftPart(UriPartial.Path));

        // The error response MUST echo the request's state back to the client (OIDC Core 3.1.2.6 / RFC 6749
        // 4.1.2.1); otherwise the client cannot correlate the response and rejects it (conformance CheckState).
        var parameters = System.Web.HttpUtility.ParseQueryString(location.Query);
        Assert.Equal("login_required", parameters["error"]);
        Assert.Equal("abc123", parameters["state"]);
    }

    [Fact]
    public async Task GetAuthorize_WithConcurrentPromptNoneRequests_EachEchoesItsOwnState()
    {
        await Factory.SeedPublicClientAsync(ClientId, RedirectUri);

        // Each concurrent request carries a DISTINCT state; a response that returns a null or another request's
        // state proves request-scoped data (the authorization request / its state) is bleeding across concurrent
        // requests via shared mutable server state — the parallel-safety defect the conformance suite surfaced.
        var states = Enumerable
            .Range(0, 32)
            .Select(i => $"state-{i:D2}-{Guid.NewGuid():N}")
            .ToArray();

        var results = await Task.WhenAll(
            states.Select(async state =>
            {
                var client = Factory.CreateClient(
                    new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }
                );
                var query =
                    $"?client_id={ClientId}"
                    + $"&redirect_uri={Uri.EscapeDataString(RedirectUri)}"
                    + "&response_type=code"
                    + "&scope=openid"
                    + $"&state={state}"
                    + "&prompt=none"
                    + TestPkce.QueryParameters;
                using var response = await client.GetAsync("/oauth2/authorize" + query);
                var location = response.Headers.Location;
                var echoed = location is null
                    ? null
                    : System.Web.HttpUtility.ParseQueryString(location.Query)["state"];
                return (Expected: state, Echoed: echoed);
            })
        );

        var mismatches = results
            .Where(r => r.Echoed != r.Expected)
            .Select(r => $"expected '{r.Expected}' but got '{r.Echoed ?? "<null>"}'")
            .ToArray();
        Assert.True(mismatches.Length == 0, string.Join("; ", mismatches));
    }

    [Fact]
    public async Task GetAuthorize_WithUnregisteredRedirectUri_ReturnsBadRequest()
    {
        await Factory.SeedPublicClientAsync(ClientId, RedirectUri);

        var client = Factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }
        );

        var query =
            $"?client_id={ClientId}"
            + $"&redirect_uri={Uri.EscapeDataString("https://evil.example/callback")}"
            + "&response_type=code"
            + "&scope=openid";

        using var response = await client.GetAsync("/oauth2/authorize" + query);

        // An unregistered redirect_uri is not safe to redirect to: the request must fail with a
        // standard OpenID error response rather than open-redirect to the attacker-controlled URI.
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var jsonElement = JsonElement.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(jsonElement.TryGetProperty("error", out var error));
        Assert.False(string.IsNullOrEmpty(error.GetString()));
    }

    [Fact]
    public async Task GetAuthorize_WithImplicitResponseType_IsRejectedByDefault()
    {
        await Factory.SeedPublicClientAsync(ClientId, RedirectUri);

        var client = Factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }
        );

        var query =
            $"?client_id={ClientId}"
            + $"&redirect_uri={Uri.EscapeDataString(RedirectUri)}"
            + "&response_type=token"
            + "&scope=openid"
            + "&nonce=n-0S6_WzA2Mj"
            + "&state=abc123";

        using var response = await client.GetAsync("/oauth2/authorize" + query);

        // The secure default does not advertise or accept the implicit flow: supported-values validation
        // fails and the error is redirected back to the client's registered redirect_uri (303 See Other),
        // rather than the interactive challenge an accepted request would produce.
        Assert.Equal(HttpStatusCode.SeeOther, response.StatusCode);
        var location = response.Headers.Location;
        Assert.NotNull(location);
        Assert.StartsWith(RedirectUri, location.GetLeftPart(UriPartial.Path));
    }

    [Fact]
    public async Task GetAuthorize_WithoutCodeChallenge_RedirectsInvalidRequestToTheClient()
    {
        // An authorization code is bound to PKCE by default; a missing code_challenge is invalid_request
        // (RFC 7636 §4.4.1).
        await Factory.SeedPublicClientAsync(ClientId, RedirectUri);

        var client = Factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }
        );

        var query =
            $"?client_id={ClientId}"
            + $"&redirect_uri={Uri.EscapeDataString(RedirectUri)}"
            + "&response_type=code"
            + "&scope=openid"
            + "&nonce=n-0S6_WzA2Mj"
            + "&state=abc123";

        using var response = await client.GetAsync("/oauth2/authorize" + query);

        Assert.Equal(HttpStatusCode.SeeOther, response.StatusCode);
        var location = response.Headers.Location;
        Assert.NotNull(location);
        Assert.StartsWith(RedirectUri, location.GetLeftPart(UriPartial.Path));
        var parameters = System.Web.HttpUtility.ParseQueryString(location.Query);
        Assert.Equal("invalid_request", parameters["error"]);
        Assert.Equal("abc123", parameters["state"]);
    }

    [Fact]
    public async Task GetAuthorize_WithPlainCodeChallengeMethod_RedirectsInvalidRequestToTheClient()
    {
        // 'plain' exposes the verifier in the authorization request and is not a method the server offers.
        await Factory.SeedPublicClientAsync(ClientId, RedirectUri);

        var client = Factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }
        );

        var query =
            $"?client_id={ClientId}"
            + $"&redirect_uri={Uri.EscapeDataString(RedirectUri)}"
            + "&response_type=code"
            + "&scope=openid"
            + "&state=abc123"
            + $"&code_challenge={TestPkce.CodeVerifier}"
            + "&code_challenge_method=plain";

        using var response = await client.GetAsync("/oauth2/authorize" + query);

        Assert.Equal(HttpStatusCode.SeeOther, response.StatusCode);
        var location = response.Headers.Location;
        Assert.NotNull(location);
        var parameters = System.Web.HttpUtility.ParseQueryString(location.Query);
        Assert.Equal("invalid_request", parameters["error"]);
    }
}
