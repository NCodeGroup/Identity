// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Hosting;

namespace NCode.Identity.OpenId.Integration.Tests.Infrastructure;

/// <summary>
/// A <see cref="HostApplicationFactory"/> that seeds the developer clients of the host's Development overlay, with the
/// public key set of the management console's client supplied the way the local-development orchestration supplies it,
/// so the console can log in through the host.
/// </summary>
/// <param name="consoleClientJwks">The JSON text of the console client's public JSON Web Key Set.</param>
public sealed class ManagementConsoleSeedingHostApplicationFactory(string consoleClientJwks)
    : HostApplicationFactory
{
    /// <inheritdoc />
    protected override bool SeedsDeveloperClients => true;

    /// <inheritdoc />
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        // The seed's options are bound while the host's services are composed, which is before the application
        // configuration that ConfigureAppConfiguration contributes is complete.
        builder.UseSetting("DeveloperInteractiveSeed:BffClients:0:Jwks", consoleClientJwks);
    }
}
