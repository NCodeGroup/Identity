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
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            var connectionString =
                HostDatabaseConnection.Resolve(configuration)
                ?? throw new InvalidOperationException(
                    "No database connection was supplied. The Aspire AppHost supplies the "
                        + "'ConnectionStrings:openiddb' connection string and the Helm chart's migration Job supplies "
                        + "it or the 'OpenIdDatabase' parts; set one of them to run the worker another way."
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
