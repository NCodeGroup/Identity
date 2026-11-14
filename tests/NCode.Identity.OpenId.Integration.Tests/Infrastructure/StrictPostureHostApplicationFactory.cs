// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers.Text;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NCode.Buffers;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.Secrets.Persistence.Options;

namespace NCode.Identity.OpenId.Integration.Tests.Infrastructure;

/// <summary>
/// A <see cref="HostApplicationFactory"/> that runs the host as a deployment does: the <c>Staging</c> environment with
/// <c>LocalDevelopment:Enabled</c> off, so every deployment guardrail applies, and a configuration that satisfies the
/// deployment gate with the values a deployment supplies. The gate runs unchanged; the one service it registers that
/// needs Kestrel, the transport check, is removed because <see cref="TestServer"/> is not Kestrel.
/// </summary>
/// <param name="configuration">
/// The configuration keys (colon-delimited paths) and values to overlay on the strict posture, so a test supplies the
/// setting it exercises or the host name its client uses.
/// </param>
public sealed class StrictPostureHostApplicationFactory(
    IReadOnlyDictionary<string, string?>? configuration = null
) : HostApplicationFactory
{
    private const string TransportSecurityStartupCheckTypeName =
        "NCode.Identity.AspNetCore.TransportSecurity.TransportSecurityStartupCheck";

    // A deployment mounts its root key as a file; the factory writes its own, which it deletes with the factory.
    private string RootKeyPath { get; } = CreateRootKeyFile();

    private IReadOnlyDictionary<string, string?> Overlay { get; } =
        configuration ?? new Dictionary<string, string?>();

    /// <summary>
    /// Removes the transport check from <paramref name="serviceCollection"/>, because <see cref="TestServer"/> binds no
    /// Kestrel endpoint for the check to inspect.
    /// </summary>
    /// <param name="serviceCollection">The services of a test host that runs the strict posture.</param>
    internal static void RemoveTransportSecurityStartupCheck(IServiceCollection serviceCollection)
    {
        ArgumentNullException.ThrowIfNull(serviceCollection);

        // The check is internal to the AspNetCore package, so it is matched by name.
        var descriptors = serviceCollection
            .Where(descriptor =>
                descriptor.ServiceType == typeof(IHostedService)
                && descriptor.ImplementationType?.FullName == TransportSecurityStartupCheckTypeName
            )
            .ToList();
        foreach (var descriptor in descriptors)
        {
            serviceCollection.Remove(descriptor);
        }
    }

    /// <inheritdoc />
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.UseEnvironment(Microsoft.Extensions.Hosting.Environments.Staging);

        // The deployment gate reads these keys while the host registers its services, so each is also a host setting.
        var settings = new Dictionary<string, string?>
        {
            ["LocalDevelopment:Enabled"] = "false",
            ["AllowedHosts"] = "localhost",
            ["SecretProtection:RootKeyProvider"] = "local",
            ["SecretProtection:RootKeyProviders:Local:Key"] = string.Empty,
            ["SecretProtection:RootKeyProviders:Local:KeyPath"] = RootKeyPath,
            ["SecretProtection:Recovery:Disabled"] = "true",
            [$"OpenId:Platform:Settings:{OpenIdSettingNames.EnvironmentType}"] = OpenIdConstants
                .TenantEnvironmentTypes
                .Production,
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

        builder.ConfigureTestServices(serviceCollection =>
        {
            serviceCollection.Configure<Pbkdf2SecretVerifierOptions>(options =>
                options.Iterations = Pbkdf2SecretVerifierOptions.MinimumDeploymentIterations
            );

            RemoveTransportSecurityStartupCheck(serviceCollection);
        });
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
            File.Delete(RootKeyPath);
        }
    }

    private static string CreateRootKeyFile()
    {
        var path = Path.GetTempFileName();
        File.WriteAllText(
            path,
            Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32)),
            SecureEncoding.UTF8
        );
        return path;
    }
}
