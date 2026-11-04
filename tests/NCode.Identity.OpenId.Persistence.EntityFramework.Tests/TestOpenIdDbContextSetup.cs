// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using IdGen;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NCode.Identity.OpenId.Persistence.EntityFramework.Configuration;
using NCode.Identity.OpenId.Persistence.Tenants;

namespace NCode.Identity.OpenId.Persistence.EntityFramework;

/// <summary>
/// Shared test setup for the <see cref="OpenIdDbContext"/>: registers the id generator, the id-generation convention,
/// the core model contributor (so the <c>DbSet</c>-less context's model contains the core entities), and an unscoped
/// ambient tenant accessor.
/// </summary>
internal static class TestOpenIdDbContextSetup
{
    public static IServiceCollection AddTestOpenIdDbContext(
        this IServiceCollection serviceCollection,
        Action<DbContextOptionsBuilder> configureOptions
    )
    {
        serviceCollection.AddSingleton<IIdGenerator<long>>(new IdGenerator(0));
        serviceCollection.AddSingleton<IdValueGenerator>();
        serviceCollection.AddSingleton<UseIdGeneratorConvention>();
        serviceCollection.TryAddEnumerable(
            ServiceDescriptor.Singleton<IOpenIdModelContributor, CoreModelContributor>()
        );
        serviceCollection.TryAddSingleton<
            IAmbientTenantAccessor,
            TestUnscopedAmbientTenantAccessor
        >();
        serviceCollection.TryAddEnumerable(
            ServiceDescriptor.Singleton<IOpenIdModelContributor, TestPlatformTeamContributor>()
        );
        serviceCollection.AddDbContext<OpenIdDbContext>(configureOptions);

        // Creating the database applies the seeded platform team, which the stores need before they can add a tenant.
        serviceCollection.Replace(
            ServiceDescriptor.Scoped(serviceProvider =>
            {
                var dbContext = ActivatorUtilities.CreateInstance<OpenIdDbContext>(serviceProvider);
                dbContext.Database.EnsureCreated();
                return dbContext;
            })
        );
        return serviceCollection;
    }

    /// <summary>
    /// Finds the team with the given identifier, or seeds a minimal one directly into the context and saves it.
    /// Returns the team entity.
    /// </summary>
    public static TeamEntity SeedTeam(
        this OpenIdDbContext dbContext,
        IIdGenerator<long> idGenerator,
        string teamId
    )
    {
        var normalizedTeamId = teamId.ToLowerInvariant();
        var existing =
            dbContext
                .Set<TeamEntity>()
                .Local.FirstOrDefault(team => team.NormalizedTeamId == normalizedTeamId)
            ?? dbContext
                .Set<TeamEntity>()
                .FirstOrDefault(team => team.NormalizedTeamId == normalizedTeamId);
        if (existing is not null)
            return existing;

        var entity = new TeamEntity
        {
            Id = idGenerator.CreateId(),
            TeamId = teamId,
            NormalizedTeamId = normalizedTeamId,
            ConcurrencyToken = string.Empty,
            SettingsConcurrencyToken = string.Empty,
            IsDisabled = false,
            DisplayName = "Platform",
            SettingsJson = System.Text.Json.JsonSerializer.SerializeToElement(
                new Dictionary<string, object>()
            ),
            Tenants = new List<TenantEntity>(),
        };
        dbContext.Set<TeamEntity>().Add(entity);
        dbContext.SaveChanges();
        return entity;
    }

    /// <summary>
    /// Seeds a minimal persisted tenant directly into the context so tenant-scoped entities can reference it, and
    /// saves it. Returns the seeded tenant entity.
    /// </summary>
    public static TenantEntity SeedTenant(
        this OpenIdDbContext dbContext,
        IIdGenerator<long> idGenerator,
        string tenantId
    )
    {
        var team = dbContext.SeedTeam(idGenerator, "platform");
        var entity = new TenantEntity
        {
            Id = idGenerator.CreateId(),
            TeamId = team.Id,
            Team = team,
            TenantId = tenantId,
            NormalizedTenantId = tenantId.ToLowerInvariant(),
            Issuer = null,
            NormalizedIssuer = null,
            NormalizedIssuerHost = null,
            MtlsEndpointAliasAuthority = null,
            NormalizedMtlsEndpointAliasHost = null,
            ConcurrencyToken = string.Empty,
            SettingsConcurrencyToken = string.Empty,
            SecretsConcurrencyToken = string.Empty,
            IsDisabled = false,
            DisplayName = tenantId,
            SettingsJson = System.Text.Json.JsonSerializer.SerializeToElement(
                new Dictionary<string, object>()
            ),
            Secrets = [],
        };
        dbContext.Set<TenantEntity>().Add(entity);
        dbContext.SaveChanges();
        return entity;
    }
}

/// <summary>
/// An ambient tenant accessor that reports no active tenant, so the tenant-scoping global query filter is a no-op and
/// tests can seed and read rows across tenants without a scope.
/// </summary>
internal sealed class TestUnscopedAmbientTenantAccessor : IAmbientTenantAccessor
{
    public string? TenantId => null;

    public bool IsScoped => false;

    public IDisposable BeginScope(string tenantId) => throw new NotSupportedException();
}

/// <summary>
/// Seeds the <c>platform</c> team into every test database, because a tenant always belongs to a team and the stores
/// place a new tenant in the platform team.
/// </summary>
internal sealed class TestPlatformTeamContributor : IOpenIdModelContributor
{
    public const long PlatformTeamRowId = 1;

    /// <inheritdoc />
    public void Configure(ModelBuilder modelBuilder)
    {
        modelBuilder
            .Entity<TeamEntity>()
            .HasData(
                new
                {
                    Id = PlatformTeamRowId,
                    TeamId = "platform",
                    NormalizedTeamId = "platform",
                    ConcurrencyToken = string.Empty,
                    SettingsConcurrencyToken = string.Empty,
                    IsDisabled = false,
                    DisplayName = "Platform",
                    SettingsJson = System.Text.Json.JsonSerializer.SerializeToElement(
                        new Dictionary<string, object>()
                    ),
                }
            );
    }
}
