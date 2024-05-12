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

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using NCode.Identity.Persistence.DataContracts;

namespace NCode.Identity.OpenId.Persistence.EntityFramework.Entities;

[Index(nameof(TenantId), nameof(NormalizedSecretId), IsUnique = true)]
internal class SecretEntity : ISupportId, ISupportTenant, ISupportConcurrencyToken
{
    [Key]
    public required long Id { get; init; }

    [ForeignKey(nameof(Tenant))]
    public required long TenantId { get; init; }

    [Unicode(false)]
    [MaxLength(MaxLengths.SecretId)]
    public required string SecretId { get; init; }

    /// <summary>
    /// Gets or sets the value of <see cref="SecretId"/> in uppercase so that lookups can be sargable for DBMS
    /// engines that don't support case-insensitive indices.
    /// </summary>
    [Unicode(false)]
    [MaxLength(MaxLengths.SecretId)]
    public required string NormalizedSecretId { get; init; }

    //

    [Unicode(false)]
    [MaxLength(MaxLengths.ConcurrencyToken)]
    [ConcurrencyCheck]
    public required string ConcurrencyToken { get; init; }

    [Unicode(false)]
    [MaxLength(MaxLengths.SecretUse)]
    public required string? Use { get; init; }

    [Unicode(false)]
    [MaxLength(MaxLengths.SecretAlgorithm)]
    public required string? Algorithm { get; init; }

    public required DateTimeOffset CreatedWhen { get; init; }

    public required DateTimeOffset ExpiresWhen { get; init; }

    [Unicode(false)]
    [MaxLength(MaxLengths.SecretType)]
    public required string SecretType { get; init; }

    public required int KeySizeBits { get; init; }

    public required int UnprotectedSizeBytes { get; init; }

    [Unicode(false)]
    public required string ProtectedValue { get; set; }

    //

    // ReSharper disable once EntityFramework.ModelValidation.CircularDependency
    // We use DTOs to avoid circular dependencies.
    public required TenantEntity Tenant { get; init; }
}
