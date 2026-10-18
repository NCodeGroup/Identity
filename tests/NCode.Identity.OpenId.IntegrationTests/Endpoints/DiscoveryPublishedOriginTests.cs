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
using NCode.Identity.OpenId.Host;
using NCode.Identity.OpenId.IntegrationTests.Infrastructure;
using Xunit;

namespace NCode.Identity.OpenId.IntegrationTests.Endpoints;

/// <summary>
/// Proves that the absolute URLs the server publishes are anchored to the tenant's issuer rather than to the
/// <c>Host</c> header of the request that asked for them.
/// </summary>
/// <remarks>
/// Every URL in the discovery document tells a client where to send credentials. If the caller's <c>Host</c> chose
/// that destination, a request carrying a foreign host would yield a document whose <c>issuer</c> is correct while its
/// <c>token_endpoint</c> points elsewhere — and a conforming client would post its client secret and authorization
/// code there.
/// </remarks>
public class DiscoveryPublishedOriginTests(HostApplicationFactory factory)
    : IClassFixture<HostApplicationFactory>
{
    private const string ConfiguredIssuer = PinnedIssuerApplicationFactory.Issuer;
    private const string DiscoveryPath = "/.well-known/openid-configuration";

    private HostApplicationFactory Factory { get; } = factory;

    private static readonly string[] CredentialBearingEndpoints =
    [
        "token_endpoint",
        "authorization_endpoint",
        "jwks_uri",
        "userinfo_endpoint",
    ];

    private static HttpRequestMessage CreateDiscoveryRequest(string host)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, DiscoveryPath);
        request.Headers.Host = host;
        return request;
    }

    private static async Task<string> GetDiscoveryAsync(
        WebApplicationFactory<HostApiMarker> applicationFactory,
        string host
    )
    {
        var client = applicationFactory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }
        );

        using var request = CreateDiscoveryRequest(host);
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return await response.Content.ReadAsStringAsync();
    }

    [Fact]
    public async Task GetDiscovery_WhenHostHeaderIsForeign_PublishesTheConfiguredIssuer()
    {
        using var pinned = new PinnedIssuerApplicationFactory();

        var json = await GetDiscoveryAsync(pinned, "attacker.example");
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        Assert.Equal(ConfiguredIssuer, root.GetProperty("issuer").GetString());

        // The issuer alone is not the property that matters: a correct issuer beside an attacker-hosted token
        // endpoint is the dangerous shape, because that is the URL the client actually posts credentials to.
        foreach (var endpointName in CredentialBearingEndpoints)
        {
            Assert.True(
                root.TryGetProperty(endpointName, out var endpoint),
                $"Expected discovery to advertise '{endpointName}'."
            );

            var endpointUrl =
                endpoint.GetString()
                ?? throw new InvalidOperationException($"'{endpointName}' was null.");

            Assert.StartsWith(ConfiguredIssuer, endpointUrl, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task GetDiscovery_WhenHostHeaderIsForeign_PublishesNoForeignUrlAnywhere()
    {
        using var pinned = new PinnedIssuerApplicationFactory();

        // A blunt sweep of the whole document, so an endpoint added later cannot quietly reintroduce the defect.
        var json = await GetDiscoveryAsync(pinned, "attacker.example");

        Assert.DoesNotContain("attacker.example", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetDiscovery_WhenTenantHasNoIssuer_FollowsTheRequest()
    {
        // Guards against the two tests above being vacuous. They are only evidence if the Host header would otherwise
        // reach the published URLs — so pin the documented development fallback: with no issuer, the origin IS derived
        // from the request. That is exactly the behaviour a deployment must not rely on.
        var json = await GetDiscoveryAsync(Factory, "derived.example");

        Assert.Contains("derived.example", json, StringComparison.Ordinal);
    }
}
