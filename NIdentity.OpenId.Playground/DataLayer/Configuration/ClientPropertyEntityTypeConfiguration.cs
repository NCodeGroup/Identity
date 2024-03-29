﻿#region Copyright Preamble
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

using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NIdentity.OpenId.Playground.DataLayer.Entities;

namespace NIdentity.OpenId.Playground.DataLayer.Configuration;

internal class ClientPropertyEntityTypeConfiguration : PropertyEntityTypeConfiguration<ClientPropertyEntity>
{
    protected Expression<Func<ClientPropertyEntity, long>> ParentKey => entity => entity.ClientId;

    /// <inheritdoc />
    protected override void PreConfigure(EntityTypeBuilder<ClientPropertyEntity> builder)
    {
        builder.HasIndex(_ => new { _.ClientId, _.NormalizedCodeName });
    }
}