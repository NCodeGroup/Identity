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
using IdGen;
using Microsoft.EntityFrameworkCore;
using NCode.Identity.OpenId.Playground.DataLayer.Configuration;
using NCode.Identity.OpenId.Playground.DataLayer.Entities;
using NCode.Identity.OpenId.Playground.Stores;

namespace NCode.Identity.OpenId.Playground.DataLayer;

internal class IdentityDbContext : DbContext, IIdentityDbContext
{
    private IdValueGenerator IdValueGenerator { get; }

    public DbSet<SecretEntity> Secrets => Set<SecretEntity>();

    public DbSet<ClientEntity> Clients => Set<ClientEntity>();

    public IdentityDbContext(DbContextOptions<IdentityDbContext> options)
        : base(options)
    {
        // TODO: verify id generator arguments
        IdValueGenerator = new IdValueGenerator(new IdGenerator(0));
    }

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("Identity");

        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

        modelBuilder.UseUtcDateTime();
        modelBuilder.UseIdGenerator(IdValueGenerator);
    }
}
