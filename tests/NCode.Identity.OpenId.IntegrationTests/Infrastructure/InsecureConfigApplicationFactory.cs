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
/// not-recommended capabilities the library omits from its secure defaults
/// ([ADR-0083](../../../docs/decisions/0083-secure-by-default-discovery-metadata.md)): the implicit/hybrid response
/// types and <c>implicit</c> grant. Symmetric signing of a token that crosses a trust boundary is outside the signing
/// settings' domain ([ADR-0140](../../../docs/decisions/0140-setting-values-are-validated-against-their-domain.md)), and
/// PKCE <c>plain</c> is not a registered transformation
/// ([ADR-0144](../../../docs/decisions/0144-authorization-codes-are-bound-to-pkce-by-default.md)), so neither can be
/// re-enabled.
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
                    // Re-add an implicit and a hybrid response type. The setting lists whole response_type values,
                    // so exactly these are advertised and accepted.
                    ["OpenId:Server:Settings:response_types_supported:0"] = OpenIdProtocol
                        .ResponseTypes
                        .Code,
                    ["OpenId:Server:Settings:response_types_supported:1"] = OpenIdProtocol
                        .ResponseTypes
                        .Token,
                    ["OpenId:Server:Settings:response_types_supported:2"] = OpenIdProtocol
                        .ResponseTypes
                        .CodeIdToken,

                    // Re-add the 'implicit' grant type alongside the secure defaults.
                    ["OpenId:Server:Settings:grant_types_supported:0"] = OpenIdProtocol
                        .GrantTypes
                        .AuthorizationCode,
                    ["OpenId:Server:Settings:grant_types_supported:1"] = OpenIdProtocol
                        .GrantTypes
                        .Implicit,
                    ["OpenId:Server:Settings:grant_types_supported:2"] = OpenIdProtocol
                        .GrantTypes
                        .RefreshToken,
                    ["OpenId:Server:Settings:grant_types_supported:3"] = OpenIdProtocol
                        .GrantTypes
                        .DeviceCode,
                }
            );
        });
    }
}
