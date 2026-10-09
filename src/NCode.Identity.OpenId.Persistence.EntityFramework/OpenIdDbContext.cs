#region Copyright Preamble

//
//    Copyright @ 2023 NCode Group
//
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
//
//        http://www.apache.org/licenses/LICENSE-2.0
//
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.

#endregion

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
