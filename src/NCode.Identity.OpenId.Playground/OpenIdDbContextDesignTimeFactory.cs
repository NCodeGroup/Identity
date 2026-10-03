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

namespace NCode.Identity.OpenId.Playground;

/// <summary>
/// Lets the EF Core tools (<c>dotnet ef migrations …</c>) construct <see cref="OpenIdDbContext"/> without booting the
/// full host. Migrations are generated into this (the Playground) assembly and target SQLite; the published persistence
/// library ships no migrations.
/// </summary>
internal sealed class OpenIdDbContextDesignTimeFactory
    : IDesignTimeDbContextFactory<OpenIdDbContext>
{
    public OpenIdDbContext CreateDbContext(string[] args)
    {
        var migrationsAssembly = typeof(OpenIdDbContextDesignTimeFactory).Assembly.GetName().Name;

        // The context resolves its id-generation convention from the application service provider.
        var serviceProvider = new ServiceCollection()
            .AddSingleton<IIdGenerator<long>>(new IdGenerator(0))
            .AddEntityFrameworkPersistenceServices<OpenIdDbContext>()
            .BuildServiceProvider();

        var options = new DbContextOptionsBuilder<OpenIdDbContext>()
            .UseSqlite(
                "Data Source=App_Data/openid-dev.db",
                sqlite => sqlite.MigrationsAssembly(migrationsAssembly)
            )
            .UseApplicationServiceProvider(serviceProvider)
            .Options;

        return new OpenIdDbContext(options);
    }
}
