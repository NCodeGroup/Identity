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
    /// <see cref="DeveloperDataProtectionDbContext"/>, which share one Postgres database. Before it applies any, it
    /// checks that every migration each context's history records is one this assembly holds, and applies nothing when
    /// one is not.
    /// </summary>
    /// <param name="serviceProvider">A provider that has the OpenID persistence slice registered (id generator and
    /// model contributors), used to construct the contexts outside a request scope.</param>
    /// <param name="connectionString">The Postgres connection string supplied by Aspire.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <exception cref="InvalidOperationException">A context's migrations history records a migration this assembly
    /// does not hold.</exception>
    public static async ValueTask MigrateAllAsync(
        IServiceProvider serviceProvider,
        string connectionString,
        CancellationToken cancellationToken
    )
    {
        var openIdOptionsBuilder = new DbContextOptionsBuilder<OpenIdDbContext>();
        openIdOptionsBuilder.UseOpenIdNpgsql(connectionString);
        openIdOptionsBuilder.UseApplicationServiceProvider(serviceProvider);

        var dataProtectionOptionsBuilder =
            new DbContextOptionsBuilder<DeveloperDataProtectionDbContext>();
        dataProtectionOptionsBuilder.UseDataProtectionNpgsql(connectionString);

        await using var openIdContext = new OpenIdDbContext(
            openIdOptionsBuilder.Options,
            new HostDatabase.UnscopedAmbientTenantAccessor(),
            serviceProvider.GetServices<IOpenIdModelContributor>()
        );
        await using var dataProtectionContext = new DeveloperDataProtectionDbContext(
            dataProtectionOptionsBuilder.Options
        );

        await ThrowIfUnknownMigrationsAppliedAsync(openIdContext, cancellationToken);
        await ThrowIfUnknownMigrationsAppliedAsync(dataProtectionContext, cancellationToken);

        await openIdContext.Database.MigrateAsync(cancellationToken);
        await dataProtectionContext.Database.MigrateAsync(cancellationToken);
    }

    private static async ValueTask ThrowIfUnknownMigrationsAppliedAsync(
        DbContext context,
        CancellationToken cancellationToken
    )
    {
        var appliedMigrations = await context.Database.GetAppliedMigrationsAsync(cancellationToken);
        ThrowIfUnknownMigrationsApplied(
            context.GetType().Name,
            appliedMigrations,
            context.Database.GetMigrations()
        );
    }

    /// <summary>
    /// Throws when <paramref name="appliedMigrations"/> holds an identifier that <paramref name="migrations"/> does not,
    /// so the migration worker stops with a message that says how to recover instead of a provider error from a
    /// <c>CREATE TABLE</c> over a schema another <c>InitialCreate</c> created.
    /// </summary>
    /// <param name="contextName">The name of the context whose migrations are compared.</param>
    /// <param name="appliedMigrations">The migration identifiers the context's migrations history records.</param>
    /// <param name="migrations">The migration identifiers this assembly holds for the context.</param>
    /// <exception cref="InvalidOperationException"><paramref name="appliedMigrations"/> holds an identifier that
    /// <paramref name="migrations"/> does not.</exception>
    // See ADR-1180.
    internal static void ThrowIfUnknownMigrationsApplied(
        string contextName,
        IEnumerable<string> appliedMigrations,
        IEnumerable<string> migrations
    )
    {
        var known = migrations.ToHashSet(StringComparer.Ordinal);
        var unknown = appliedMigrations.Where(id => !known.Contains(id)).ToList();
        if (unknown.Count == 0)
            return;

        throw new InvalidOperationException(
            $"The database's {contextName} migrations history records {string.Join(", ", unknown)}, which this host "
                + $"does not hold, so its {contextName} migrations cannot be applied to it. Each host database "
                + "context has one InitialCreate, which is re-scaffolded with a fresh identifier on each model "
                + "change. Stop the AppHost and reset the local database with build/dev-db-reset.ps1; the next "
                + "AppHost run creates the schema and seeds the development data."
        );
    }
}
