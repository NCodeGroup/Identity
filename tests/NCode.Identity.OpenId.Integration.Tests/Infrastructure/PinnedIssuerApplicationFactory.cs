// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

namespace NCode.Identity.OpenId.Integration.Tests.Infrastructure;

/// <summary>
/// A <see cref="HostApplicationFactory"/> whose tenant has a pinned public issuer, the way a deployment is configured.
/// </summary>
/// <remarks>
/// The issuer is the tenant's request-independent identity and therefore also its base address, so this is what lets a
/// test prove the URLs the server publishes are anchored to that origin rather than to whatever <c>Host</c> header the
/// caller supplied. Unlike the base fixture's issuer, it differs from the TestServer's own origin, so the anchoring is
/// visible in every published URL.
/// </remarks>
public sealed class PinnedIssuerApplicationFactory : HostApplicationFactory
{
    /// <summary>
    /// The issuer seeded onto the tenant, and therefore the origin every published URL must use.
    /// </summary>
    public const string Issuer = "https://op.example";

    /// <inheritdoc />
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        // The tenant is provisioned lazily on first resolution, so the issuer has to reach the provisioner through
        // configuration: the single static tenant's selection settings live under OpenId:TenantResolution.
        builder.ConfigureAppConfiguration(configurationBuilder =>
            configurationBuilder.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["OpenId:TenantResolution:StaticSingle:Issuer"] = Issuer,
                }
            )
        );
    }
}
