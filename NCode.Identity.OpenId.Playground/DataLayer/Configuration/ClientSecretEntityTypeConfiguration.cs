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

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NCode.Identity.OpenId.Playground.DataLayer.Entities;

namespace NCode.Identity.OpenId.Playground.DataLayer.Configuration;

internal class ClientSecretEntityTypeConfiguration : IEntityTypeConfiguration<ClientSecretEntity>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<ClientSecretEntity> builder)
    {
        builder.HasKey(_ => new { _.ClientId, _.SecretId });

        builder.HasOne(_ => _.Secret).WithMany().HasForeignKey(_ => _.SecretId).IsRequired();
    }
}
