// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

namespace NCode.Identity.OpenId.Integration.Tests.Infrastructure;

/// <summary>
/// A <see cref="HostApplicationFactory"/> that re-enables — purely through host configuration — the
/// not-recommended capabilities the library omits from its secure defaults: the implicit/hybrid response types and
/// <c>implicit</c> grant. Symmetric signing of a token that crosses a trust boundary is outside the signing settings'
/// domain, and PKCE <c>plain</c> is not a registered transformation, so neither can be re-enabled.
/// </summary>
/// <remarks>
/// Server settings read from <c>OpenId:Platform:Settings</c> are applied last and overwrite the baseline, so an
/// operator widens a ceiling simply by configuring it — this fixture proves that operator override path and that
/// the widened values are then advertised in discovery and accepted by the authorization endpoint.
/// </remarks>
// See ADR-1083, ADR-1140 and ADR-1144.
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
                    ["OpenId:Platform:Settings:response_types_supported:0"] = OpenIdProtocol
                        .ResponseTypes
                        .Code,
                    ["OpenId:Platform:Settings:response_types_supported:1"] = OpenIdProtocol
                        .ResponseTypes
                        .Token,
                    ["OpenId:Platform:Settings:response_types_supported:2"] = OpenIdProtocol
                        .ResponseTypes
                        .CodeIdToken,

                    // Re-add the 'implicit' grant type alongside the secure defaults.
                    ["OpenId:Platform:Settings:grant_types_supported:0"] = OpenIdProtocol
                        .GrantTypes
                        .AuthorizationCode,
                    ["OpenId:Platform:Settings:grant_types_supported:1"] = OpenIdProtocol
                        .GrantTypes
                        .Implicit,
                    ["OpenId:Platform:Settings:grant_types_supported:2"] = OpenIdProtocol
                        .GrantTypes
                        .RefreshToken,
                    ["OpenId:Platform:Settings:grant_types_supported:3"] = OpenIdProtocol
                        .GrantTypes
                        .DeviceCode,
                }
            );
        });
    }
}
