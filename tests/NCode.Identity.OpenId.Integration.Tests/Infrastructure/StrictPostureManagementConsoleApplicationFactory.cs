// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;

namespace NCode.Identity.OpenId.Integration.Tests.Infrastructure;

/// <summary>
/// A <see cref="ManagementConsoleApplicationFactory"/> that runs the console's host as a deployment does: the
/// <c>Staging</c> environment with <c>LocalDevelopment:Enabled</c> off, so every deployment guardrail applies, and an
/// explicit <c>AllowedHosts</c> with https-only URLs. The one check that needs Kestrel, the transport check, is removed
/// because <see cref="TestServer"/> is not Kestrel.
/// </summary>
/// <param name="configuration">
/// The configuration keys (colon-delimited paths) and values to overlay on the strict posture, so a test supplies the
/// setting it exercises.
/// </param>
public sealed class StrictPostureManagementConsoleApplicationFactory(
    IReadOnlyDictionary<string, string?>? configuration = null
) : ManagementConsoleApplicationFactory
{
    private IReadOnlyDictionary<string, string?> Overlay { get; } =
        configuration ?? new Dictionary<string, string?>();

    /// <inheritdoc />
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.UseEnvironment(Microsoft.Extensions.Hosting.Environments.Staging);

        // The host reads the switch and the host names while it registers its services, so each is also a host
        // setting.
        var settings = new Dictionary<string, string?>
        {
            ["LocalDevelopment:Enabled"] = "false",
            ["AllowedHosts"] = "console.integration.test",
            ["Bff:Frontends:mgmt:Authority"] = "https://id.integration.test",
            ["Bff:Frontends:mgmt:PublicOrigin"] = "https://console.integration.test",
            ["ManagementConsole:ManagementApiUrl"] = "https://id.integration.test/api",
        };
        foreach (var (key, value) in Overlay)
        {
            settings[key] = value;
        }

        foreach (var (key, value) in settings)
        {
            builder.UseSetting(key, value);
        }

        builder.ConfigureAppConfiguration(configurationBuilder =>
            configurationBuilder.AddInMemoryCollection(settings)
        );

        builder.ConfigureTestServices(
            StrictPostureHostApplicationFactory.RemoveTransportSecurityStartupCheck
        );
    }
}
