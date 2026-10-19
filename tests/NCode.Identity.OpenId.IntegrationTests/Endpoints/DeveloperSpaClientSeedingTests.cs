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
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NCode.Identity.OpenId.IntegrationTests.Infrastructure;
using NCode.Identity.Server.DevelopmentEnvironment;
using NCode.Identity.Server.Frontend;
using Xunit;

namespace NCode.Identity.OpenId.IntegrationTests.Endpoints;

/// <summary>
/// Guards the reference host's Development overlay: the client ids its bootstrap document publishes and the public
/// clients its developer seed registers are declared side by side, and must not drift apart (ADR-0146).
/// </summary>
public class DeveloperSpaClientSeedingTests(HostApplicationFactory factory)
    : IClassFixture<HostApplicationFactory>
{
    private HostApplicationFactory Factory { get; } = factory;

    private IReadOnlyList<DeveloperSpaClientSeed> GetSeededSpaClients() =>
        Factory
            .Services.GetRequiredService<DeveloperInteractiveSeedOptions>()
            .SpaClients.Where(spaClient => spaClient.ShouldSeed)
            .ToList();

    [Fact]
    public void FrontendBootstrap_InDevelopment_PublishesOnlyClientsTheDeveloperSeedRegisters()
    {
        var frontend = Factory
            .Services.GetRequiredService<IOptions<FrontendBootstrapOptions>>()
            .Value;
        var published = frontend
            .Surfaces.Values.Select(surface => surface.ClientId)
            .Append(frontend.ClientId)
            .OfType<string>()
            .ToHashSet(StringComparer.Ordinal);
        var seeded = GetSeededSpaClients()
            .Select(spaClient => spaClient.ClientId)
            .OfType<string>()
            .ToHashSet(StringComparer.Ordinal);

        Assert.NotEmpty(published);
        Assert.Subset(seeded, published);
    }

    [Fact]
    public async Task GetAuthorize_WithEachSeededSpaClientAndRedirectUri_ResolvesTheClient()
    {
        var seededSpaClients = GetSeededSpaClients();
        Assert.NotEmpty(seededSpaClients);

        // A first request provisions the tenant, which runs the seed fan-out.
        using (var warmupClient = Factory.CreateClient())
        {
            using var _ = await warmupClient.GetAsync("/oauth2/jwks");
        }

        var client = Factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }
        );
        var results = new List<(string ClientId, string RedirectUri, HttpStatusCode StatusCode)>();
        foreach (var spaClient in seededSpaClients)
        {
            foreach (var redirectUri in spaClient.RedirectUris)
            {
                var query =
                    $"?client_id={spaClient.ClientId}"
                    + $"&redirect_uri={Uri.EscapeDataString(redirectUri)}"
                    + "&response_type=code"
                    + "&scope=openid"
                    + "&state=abc123"
                    + TestPkce.QueryParameters;

                using var response = await client.GetAsync("/oauth2/authorize" + query);
                results.Add((spaClient.ClientId!, redirectUri, response.StatusCode));
            }
        }

        // A registered client at a registered redirect URI reaches the interactive challenge (a redirect to the login
        // surface); an unknown client or redirect URI is refused with 400 instead.
        Assert.All(results, result => Assert.Equal(HttpStatusCode.Redirect, result.StatusCode));
    }
}
