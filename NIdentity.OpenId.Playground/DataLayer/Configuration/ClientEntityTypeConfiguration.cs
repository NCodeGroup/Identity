#region Copyright Preamble

//
//    Copyright @ 2021 NCode Group
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
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NIdentity.OpenId.Playground.DataLayer.Entities;

namespace NIdentity.OpenId.Playground.DataLayer.Configuration;

internal class ClientEntityTypeConfiguration : IEntityTypeConfiguration<ClientEntity>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<ClientEntity> builder)
    {
        builder.HasKey(_ => _.Id);
        builder.HasIndex(_ => _.NormalizedClientId).IsUnique();

        builder.Property(_ => _.Id).UseIdGenerator();
        builder.Property(_ => _.ConcurrencyToken).AsStandardConcurrencyToken();
        builder.Property(_ => _.ClientId).AsStandardIndex();
        builder.Property(_ => _.NormalizedClientId).AsStandardIndex();

        builder.Property(_ => _.IsDisabled);
        builder.Property(_ => _.AllowUnsafeTokenResponse);
        builder.Property(_ => _.AllowLoopback);
        builder.Property(_ => _.RequireRequestObject);
        builder.Property(_ => _.RequirePkce);
        builder.Property(_ => _.AllowPlainCodeChallengeMethod);

        builder.HasMany(_ => _.ClientSecrets).WithOne(_ => _.Client).HasForeignKey(_ => _.ClientId).IsRequired();
        builder.HasMany(_ => _.Urls).WithOne().HasForeignKey(_ => _.ClientId).IsRequired();
        builder.HasMany(_ => _.Properties).WithOne(_ => _.Client).HasForeignKey(_ => _.ClientId).IsRequired();
    }
}