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

using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

namespace NCode.Identity.OpenId.IntegrationTests.Infrastructure;

/// <summary>
/// A <see cref="HostApplicationFactory"/> that re-enables — purely through host configuration — the
/// insecure/not-recommended capabilities the library omits from its secure defaults
/// ([ADR-0083](../../../docs/adr/0083-secure-by-default-discovery-metadata.md)): the implicit/hybrid response
/// types and <c>implicit</c> grant, PKCE <c>plain</c>, and symmetric (<c>HS256</c>) id-token signing.
/// </summary>
/// <remarks>
/// Server settings read from <c>OpenId:Server:Settings</c> are applied last and overwrite the baseline, so an
/// operator widens a ceiling simply by configuring it — this fixture proves that operator override path and that
/// the widened values are then advertised in discovery and accepted by the authorization endpoint.
/// </remarks>
public sealed class InsecureConfigApplicationFactory : HostApplicationFactory
{
    /// <inheritdoc />
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.ConfigureAppConfiguration(configurationBuilder =>
        {
            configurationBuilder.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    // Re-add the implicit/hybrid response types (the formatter expands these into the implicit
                    // and hybrid front-channel-token combinations).
                    ["OpenId:Server:Settings:response_types_supported:0"] = OpenIdConstants
                        .ResponseTypes
                        .Code,
                    ["OpenId:Server:Settings:response_types_supported:1"] = OpenIdConstants
                        .ResponseTypes
                        .IdToken,
                    ["OpenId:Server:Settings:response_types_supported:2"] = OpenIdConstants
                        .ResponseTypes
                        .Token,

                    // Re-add the 'implicit' grant type alongside the secure defaults.
                    ["OpenId:Server:Settings:grant_types_supported:0"] = OpenIdConstants
                        .GrantTypes
                        .AuthorizationCode,
                    ["OpenId:Server:Settings:grant_types_supported:1"] = OpenIdConstants
                        .GrantTypes
                        .Implicit,
                    ["OpenId:Server:Settings:grant_types_supported:2"] = OpenIdConstants
                        .GrantTypes
                        .RefreshToken,
                    ["OpenId:Server:Settings:grant_types_supported:3"] = OpenIdConstants
                        .GrantTypes
                        .DeviceCode,

                    // Re-add PKCE 'plain' alongside 'S256'.
                    ["OpenId:Server:Settings:code_challenge_methods_supported:0"] = OpenIdConstants
                        .CodeChallengeMethods
                        .Plain,
                    ["OpenId:Server:Settings:code_challenge_methods_supported:1"] = OpenIdConstants
                        .CodeChallengeMethods
                        .S256,

                    // Re-add symmetric HS256 id-token signing alongside an asymmetric algorithm.
                    ["OpenId:Server:Settings:id_token_signing_alg_values_supported:0"] = "HS256",
                    ["OpenId:Server:Settings:id_token_signing_alg_values_supported:1"] = "RS256",
                }
            );
        });
    }
}
