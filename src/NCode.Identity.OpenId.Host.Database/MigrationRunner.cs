// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NCode.Identity.OpenId.Persistence.EntityFramework;

namespace NCode.Identity.OpenId.Host.Database;

/// <summary>
/// Applies the EF Core migrations for every host context against the Postgres database. Invoked by the
/// <c>MigrationService</c> worker so schema application is a dedicated Aspire resource: a migration failure surfaces as
/// a failed dashboard resource instead of crashing the <c>api</c> host.
/// </summary>
// See ADR-1073.
[PublicAPI]
public static class MigrationRunner
{
    /// <summary>
    /// Applies pending migrations for the <see cref="OpenIdDbContext"/> and the
    /// <see cref="DeveloperDataProtectionDbContext"/>, which share one Postgres database.
    /// </summary>
    /// <param name="serviceProvider">A provider that has the OpenID persistence slice registered (id generator and
    /// model contributors), used to construct the contexts outside a request scope.</param>
    /// <param name="connectionString">The Postgres connection string supplied by Aspire.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    public static async Task MigrateAllAsync(
        IServiceProvider serviceProvider,
        string connectionString,
        CancellationToken cancellationToken
    )
    {
        var openIdOptionsBuilder = new DbContextOptionsBuilder<OpenIdDbContext>();
        openIdOptionsBuilder.UseOpenIdNpgsql(connectionString);
        openIdOptionsBuilder.UseApplicationServiceProvider(serviceProvider);

        await using (
            var openIdContext = new OpenIdDbContext(
                openIdOptionsBuilder.Options,
                new HostDatabase.UnscopedAmbientTenantAccessor(),
                serviceProvider.GetServices<IOpenIdModelContributor>()
            )
        )
        {
            await openIdContext.Database.MigrateAsync(cancellationToken);
        }

        var dataProtectionOptionsBuilder =
            new DbContextOptionsBuilder<DeveloperDataProtectionDbContext>();
        dataProtectionOptionsBuilder.UseDataProtectionNpgsql(connectionString);

        await using (
            var dataProtectionContext = new DeveloperDataProtectionDbContext(
                dataProtectionOptionsBuilder.Options
            )
        )
        {
            await dataProtectionContext.Database.MigrateAsync(cancellationToken);
        }
    }
}
