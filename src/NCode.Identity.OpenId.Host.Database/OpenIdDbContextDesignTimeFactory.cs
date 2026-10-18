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
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.DependencyInjection;
using NCode.Identity.OpenId.Persistence.EntityFramework;
using NCode.Identity.OpenId.Persistence.EntityFramework.Accounts;
using NCode.Identity.OpenId.Persistence.EntityFramework.Core;

namespace NCode.Identity.OpenId.Host.Database;

/// <summary>
/// Lets the EF Core tools (<c>dotnet ef migrations …</c>) construct <see cref="OpenIdDbContext"/> without booting the
/// full host. Migrations are generated into this (the host database) assembly and target Npgsql; the published
/// persistence library ships no migrations. The connection string is a design-time placeholder — migrations are
/// generated from the model and never open a connection.
/// </summary>
internal sealed class OpenIdDbContextDesignTimeFactory
    : IDesignTimeDbContextFactory<OpenIdDbContext>
{
    private const string DesignTimeConnectionString =
        "Host=localhost;Port=5432;Database=openiddb;Username=postgres;Password=postgres";

    public OpenIdDbContext CreateDbContext(string[] args)
    {
        // The context resolves its id-generation convention and model contributors from the application service
        // provider; the same contributors must be registered so the generated migration includes every slice.
        var serviceProvider = new ServiceCollection()
            .AddSingleton<IIdGenerator<long>>(new IdGenerator(0))
            .AddEntityFrameworkCorePersistence<OpenIdDbContext>()
            .AddLocalAccountEntityFramework()
            .ServiceCollection.BuildServiceProvider();

        var optionsBuilder = new DbContextOptionsBuilder<OpenIdDbContext>();
        optionsBuilder.UseOpenIdNpgsql(DesignTimeConnectionString);
        optionsBuilder.UseApplicationServiceProvider(serviceProvider);

        return new OpenIdDbContext(
            optionsBuilder.Options,
            new HostDatabase.UnscopedAmbientTenantAccessor(),
            serviceProvider.GetServices<IOpenIdModelContributor>()
        );
    }
}
