#region Copyright Preamble

// Copyright @ 2024 NCode Group
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

using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using NCode.Identity.Persistence.DataContracts;

namespace NCode.Identity.OpenId.Persistence.EntityFramework.Entities;

internal class TenantEntity : ISupportId, ISupportConcurrencyToken
{
    public required long Id { get; set; }

    [MaxLength(DataConstants.MaxConcurrencyTokenLength)]
    public required string ConcurrencyToken { get; set; }

    [MaxLength(DataConstants.MaxIndexLength)]
    public required string TenantId { get; set; }

    [MaxLength(DataConstants.MaxIndexLength)]
    public required string NormalizedTenantId { get; set; }

    public required string? DomainName { get; set; }

    public required string DisplayName { get; set; }

    public required bool IsDisabled { get; set; }

    public required JsonElement Settings { get; set; }

    public required List<TenantSecretEntity> Secrets { get; set; }
}
