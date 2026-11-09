// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NCode.Identity.Secrets.RemoteKeys;

namespace NCode.Identity.OpenId.Integration.Tests.Infrastructure;

/// <summary>
/// A <see cref="BootstrapAdminApplicationFactory"/> with the supplied configuration overlaid and
/// <see cref="TestRemoteKeyProvider"/> registered. The host seeds the bootstrap administrator, runs in the deployment
/// <c>integration</c> and does not sweep, unless the configuration says otherwise, so a test clock never destroys a
/// test's keys.
/// </summary>
public sealed class RemoteKeyApplicationFactory(
    TestRemoteKeyProvider provider,
    IReadOnlyDictionary<string, string?> configuration,
    string controlPlaneTenantId = HostApplicationFactory.TenantId
) : BootstrapAdminApplicationFactory(new Dictionary<string, string?>(), controlPlaneTenantId)
{
    private const string DeploymentKey = "SecretProtection:RemoteKeys:Deployment";
    private const string SweepEnabledKey = "SecretProtection:RemoteKeys:SweepEnabled";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        var overlay = new Dictionary<string, string?>(configuration, StringComparer.Ordinal);
        overlay.TryAdd(DeploymentKey, "integration");
        overlay.TryAdd(SweepEnabledKey, "false");

        builder.ConfigureAppConfiguration(configurationBuilder =>
            configurationBuilder.AddInMemoryCollection(overlay)
        );
        builder.ConfigureServices(services =>
        {
            services.AddSingleton<IRemoteKeyProvider>(provider);
            services.AddSingleton<IRemoteKeyDescriptorSource>(provider);
        });
    }
}
