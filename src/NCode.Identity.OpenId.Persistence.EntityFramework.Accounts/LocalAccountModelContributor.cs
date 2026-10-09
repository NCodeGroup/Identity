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

using Microsoft.EntityFrameworkCore;
using NCode.Identity.OpenId.Persistence.EntityFramework.Accounts.Entities;

namespace NCode.Identity.OpenId.Persistence.EntityFramework.Accounts;

/// <summary>
/// Contributes the local account entities to the shared <see cref="OpenIdDbContext"/> model so a local account and its
/// self-issued federated identity can be written in the same unit of work.
/// </summary>
internal sealed class LocalAccountModelContributor : IOpenIdModelContributor
{
    /// <inheritdoc />
    public void Configure(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<LocalAccountEntity>(entity =>
        {
            entity.ToTable("LocalAccounts");

            // The store assigns ids from the shared id generator, so the key value is never database-generated.
            entity.Property(account => account.Id).ValueGeneratedNever();

            // One-to-many account → profile claims, keyed by a shadow foreign key.
            entity.HasMany(account => account.Claims).WithOne();
        });

        modelBuilder
            .Entity<LocalAccountClaimEntity>()
            .ToTable("LocalAccountClaims")
            .Property(claim => claim.Id)
            .ValueGeneratedNever();
    }
}
