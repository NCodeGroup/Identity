// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Reflection;
using System.Text.Json;
using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using NCode.Identity.OpenId.Persistence.EntityFramework.Configuration;
using NCode.Identity.OpenId.Persistence.EntityFramework.Converters;
using NCode.Identity.OpenId.Persistence.EntityFramework.Entities;
using NCode.Identity.OpenId.Persistence.Tenants;

namespace NCode.Identity.OpenId.Persistence.EntityFramework;

/// <summary>
/// Contains the entity framework <see cref="DbContext"/> for <c>OAuth</c> and <c>OpenID Connect</c> entities.
/// </summary>
[PublicAPI]
public class OpenIdDbContext(
    DbContextOptions<OpenIdDbContext> options,
    IAmbientTenantAccessor ambientTenantAccessor,
    IEnumerable<IOpenIdModelContributor> modelContributors
) : DbContext(options)
{
    private IAmbientTenantAccessor AmbientTenantAccessor { get; } = ambientTenantAccessor;

    // Stateless singletons (the model is built once and cached, and this context is pooled).
    private IEnumerable<IOpenIdModelContributor> ModelContributors { get; } = modelContributors;

    /// <summary>
    /// Gets the normalized identifier of the ambient tenant for the current request, or <c>null</c> when tenant-bound
    /// data access is unscoped. Referenced by the tenant-scoping global query filters.
    /// </summary>
    public string? NormalizedAmbientTenantId => AmbientTenantAccessor.TenantId?.ToLowerInvariant();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Satellite packages contribute their entities to the shared model so their data participates in this
        // context's unit of work and transactions; contributing first lets the relationship and tenant-scoping passes
        // below cover the added entities too.
        foreach (var contributor in ModelContributors)
        {
            contributor.Configure(modelBuilder);
        }

        foreach (
            var relationship in modelBuilder
                .Model.GetEntityTypes()
                .SelectMany(e => e.GetForeignKeys())
        )
        {
            relationship.DeleteBehavior = DeleteBehavior.Restrict;
        }

        // Tenant-scoping global query filters: every tenant-child entity (ISupportTenantEntity) is scoped by the
        // ambient tenant, so that when a scope is active a cross-tenant row is never materialized; when no scope is
        // active (central-admin surfaces / runtime), NormalizedAmbientTenantId is null and the filter is a no-op.
        // Applying it by interface is fail-closed — a newly-added tenant-child entity is scoped automatically. The
        // tenant root itself has no owning-tenant foreign key, so it is not ISupportTenantEntity and stays unscoped
        // (discoverable by id during resolution); its secrets, however, ARE tenant-scoped like every other
        // tenant-owned row (tenant resolution and tenant management read them with no ambient scope).
        var applyTenantScopeFilter =
            typeof(OpenIdDbContext).GetMethod(
                nameof(ApplyTenantScopeFilter),
                BindingFlags.Instance | BindingFlags.NonPublic
            )
            ?? throw new InvalidOperationException(
                $"The '{nameof(ApplyTenantScopeFilter)}' method could not be found."
            );

        var tenantChildEntityTypes = modelBuilder
            .Model.GetEntityTypes()
            .Select(entityType => entityType.ClrType)
            .Where(clrType => typeof(ISupportTenantEntity).IsAssignableFrom(clrType))
            .ToList();

        foreach (var clrType in tenantChildEntityTypes)
        {
            applyTenantScopeFilter.MakeGenericMethod(clrType).Invoke(this, [modelBuilder]);
        }
    }

    /// <summary>
    /// Applies the tenant-scoping global query filter to a single tenant-child entity type, scoping by the owning
    /// tenant's normalized identifier denormalized onto the entity (<see cref="ISupportTenantEntity.NormalizedTenantId"/>).
    /// Comparing a single indexed column keeps the filter a navigation-free predicate and keeps the framework decoupled
    /// from any concrete tenant entity type. Invoked per entity type via reflection from <see cref="OnModelCreating"/>.
    /// </summary>
    [UsedImplicitly]
    private void ApplyTenantScopeFilter<TEntity>(ModelBuilder modelBuilder)
        where TEntity : class, ISupportTenantEntity
    {
        modelBuilder
            .Entity<TEntity>()
            .HasQueryFilter(entity =>
                NormalizedAmbientTenantId == null
                || entity.NormalizedTenantId == NormalizedAmbientTenantId
            );
    }

    /// <inheritdoc />
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.AddInterceptors(new ConcurrencyTokenSaveChangesInterceptor());
        UseSplitQueriesByDefault(optionsBuilder);
    }

    // A query that includes several collections runs one SQL query per collection, unless the host chose a behavior.
    // The framework owns the context, so every relational provider and host gets this default, not only the reference
    // host's Npgsql setup. AddOrUpdateExtension updates the provider's own relational extension and keeps the rest of
    // the host's provider setup. Split queries read one consistent snapshot only inside a transaction, so a host that
    // needs one query sets UseQuerySplittingBehavior(QuerySplittingBehavior.SingleQuery), and a single query calls
    // AsSingleQuery().
    private static void UseSplitQueriesByDefault(DbContextOptionsBuilder optionsBuilder)
    {
        var relational = FindRelationalExtension(optionsBuilder);

        if (relational is { QuerySplittingBehavior: null })
        {
            var splitting = relational.WithUseQuerySplittingBehavior(
                QuerySplittingBehavior.SplitQuery
            );
            var infrastructure = (IDbContextOptionsBuilderInfrastructure)optionsBuilder;
            infrastructure.AddOrUpdateExtension(splitting);
        }
    }

    private static RelationalOptionsExtension? FindRelationalExtension(
        DbContextOptionsBuilder optionsBuilder
    )
    {
        foreach (var extension in optionsBuilder.Options.Extensions)
        {
            if (extension is RelationalOptionsExtension relational)
            {
                return relational;
            }
        }

        return null;
    }

    /// <inheritdoc />
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<DateTime>().HaveConversion<DateTimeConverter>();

        configurationBuilder.Properties<DateTimeOffset>().HaveConversion<DateTimeOffsetConverter>();

        configurationBuilder.Properties<JsonElement>().HaveConversion<JsonElementConverter>();

        configurationBuilder.Conventions.Add(_ => this.GetService<UseIdGeneratorConvention>());
    }
}
