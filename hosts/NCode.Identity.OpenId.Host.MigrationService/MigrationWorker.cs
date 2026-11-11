// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.OpenId.Host.Database;
using NCode.Identity.OpenId.Host.MigrationService.Logging;

namespace NCode.Identity.OpenId.Host.MigrationService;

/// <summary>
/// Applies the host database migrations once on startup, then stops the host. A non-zero exit on failure makes
/// Aspire mark this resource failed and hold back the resources that <c>WaitForCompletion</c> on it, and fails the
/// Helm chart's pre-deploy Job so the release stops before its rollout.
/// </summary>
internal sealed class MigrationWorker(
    IServiceProvider serviceProvider,
    IConfiguration configuration,
    IHostApplicationLifetime lifetime,
    ILogger<MigrationWorker> logger
) : BackgroundService
{
    // Matches the Aspire database resource name in the AppHost graph and the host's connection string name.
    private const string ConnectionStringName = "openiddb";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            var connectionString =
                configuration.GetConnectionString(ConnectionStringName)
                ?? throw new InvalidOperationException(
                    $"The '{ConnectionStringName}' connection string was not supplied. The Aspire AppHost and the "
                        + "Helm chart's migration Job each supply it; set 'ConnectionStrings:openiddb' to run the "
                        + "worker another way."
                );

            logger.MigrationsApplying();
            await MigrationRunner.MigrateAllAsync(serviceProvider, connectionString, stoppingToken);
            logger.MigrationsApplied();
        }
        catch (Exception exception)
        {
            logger.MigrationsFailed(exception);
            Environment.ExitCode = 1;
        }
        finally
        {
            lifetime.StopApplication();
        }
    }
}
