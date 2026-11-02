// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

namespace NCode.Identity.OpenId.Integration.Tests.Infrastructure;

/// <summary>
/// A <see cref="HostApplicationFactory"/> whose configuration is overlaid with the supplied keys, the way an operator's
/// environment-specific settings or environment variables layer over the host's own <c>appsettings.json</c>.
/// </summary>
/// <param name="configuration">The configuration keys (colon-delimited paths) and values to overlay.</param>
public sealed class ConfiguredHostApplicationFactory(
    IReadOnlyDictionary<string, string?> configuration
) : HostApplicationFactory
{
    /// <inheritdoc />
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.ConfigureAppConfiguration(configurationBuilder =>
            configurationBuilder.AddInMemoryCollection(configuration)
        );
    }
}
