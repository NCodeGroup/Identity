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

using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using NCode.Identity.OpenId.Persistence.EntityFramework.Configuration;
using NCode.Identity.Persistence;

namespace NCode.Identity.OpenId.Persistence.EntityFramework.Core.Entities;

/// <summary>
/// Represents an entity framework data contract for the last batch of usage that one replica added to the ledger.
/// A replica numbers its batches in the order it adds them, so a batch numbered at or below
/// <see cref="LastBatchNumber"/> was added before and is skipped when the replica retries it.
/// </summary>
// See ADR-0209.
[Index(nameof(ReplicaId), IsUnique = true)]
[Index(nameof(LastDay))]
public sealed class TenantUsageCheckpointEntity : ISupportConcurrencyToken
{
    /// <summary>
    /// Gets the surrogate identifier for this entity.
    /// </summary>
    [Key]
    [UseIdGenerator]
    public long Id { get; init; }

    /// <summary>
    /// Gets the identifier that the replica drew when it started.
    /// </summary>
    public required Guid ReplicaId { get; init; }

    /// <summary>
    /// Gets or sets the number of the last batch the replica added.
    /// </summary>
    public required long LastBatchNumber { get; set; }

    /// <summary>
    /// Gets or sets the latest UTC calendar day of the usage the replica added; the checkpoint is removed with the
    /// days the ledger no longer keeps.
    /// </summary>
    public required DateOnly LastDay { get; set; }

    /// <inheritdoc />
    [Unicode(false)]
    [MaxLength(MaxLengths.ConcurrencyToken)]
    [ConcurrencyCheck]
    public required string ConcurrencyToken { get; set; }
}
