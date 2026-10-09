#region Copyright Preamble

// Copyright @ 2026 NCode Group
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

using IdGen;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NCode.Identity.OpenId.Persistence.EntityFramework.Configuration;
using NCode.Identity.OpenId.Persistence.EntityFramework.Entities;
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
        serviceCollection.AddDbContext<OpenIdDbContext>(configureOptions);
        return serviceCollection;
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
        var entity = new TenantEntity
        {
            Id = idGenerator.CreateId(),
            TenantId = tenantId,
            NormalizedTenantId = tenantId.ToLowerInvariant(),
            DomainName = null,
            NormalizedDomainName = null,
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
