#region Copyright Preamble

// Copyright @ 2025 NCode Group
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
using NCode.Identity.OpenId.Persistence.Tenants;

namespace NCode.Identity.OpenId.Persistence.EntityFramework.Accounts;

/// <summary>
/// Shared test setup for the <see cref="OpenIdDbContext"/> over the local-account slice: registers the id generator,
/// the reusable framework persistence services, the <see cref="LocalAccountModelContributor"/> (so the <c>DbSet</c>-less
/// context's model contains the account entities), and a settable ambient tenant accessor.
/// </summary>
internal static class TestOpenIdDbContextSetup
{
    public static IServiceCollection AddTestOpenIdDbContext(
        this IServiceCollection serviceCollection,
        Action<DbContextOptionsBuilder> configureOptions
    )
    {
        serviceCollection.AddSingleton<IIdGenerator<long>>(new IdGenerator(0));
        serviceCollection.AddEntityFrameworkPersistenceServices<OpenIdDbContext>();
        serviceCollection.TryAddEnumerable(
            ServiceDescriptor.Singleton<IOpenIdModelContributor, LocalAccountModelContributor>()
        );
        serviceCollection.AddSingleton<TestAmbientTenantAccessor>();
        serviceCollection.AddSingleton<IAmbientTenantAccessor>(serviceProvider =>
            serviceProvider.GetRequiredService<TestAmbientTenantAccessor>()
        );
        serviceCollection.AddDbContext<OpenIdDbContext>(configureOptions);
        return serviceCollection;
    }
}

/// <summary>
/// An ambient tenant accessor whose active tenant is settable, so a test can leave data access unscoped
/// (<see cref="TenantId"/> is <c>null</c>) or scope it to a specific tenant.
/// </summary>
internal sealed class TestAmbientTenantAccessor : IAmbientTenantAccessor
{
    public string? TenantId { get; set; }

    public bool IsScoped => TenantId is not null;

    public IDisposable BeginScope(string tenantId)
    {
        TenantId = tenantId;
        return new NoopScope();
    }

    private sealed class NoopScope : IDisposable
    {
        public void Dispose() { }
    }
}
