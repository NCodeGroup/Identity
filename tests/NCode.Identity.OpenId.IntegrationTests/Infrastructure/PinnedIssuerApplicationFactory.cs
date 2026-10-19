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
/// A <see cref="HostApplicationFactory"/> whose tenant has a pinned issuer, the way a deployment is configured.
/// </summary>
/// <remarks>
/// The issuer is the tenant's request-independent identity and therefore also its base address, so this is what lets a
/// test prove the URLs the server publishes are anchored to that origin rather than to whatever <c>Host</c> header the
/// caller supplied. The base fixture deliberately leaves it unset, because a developer's machine is reached under many
/// hosts.
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
