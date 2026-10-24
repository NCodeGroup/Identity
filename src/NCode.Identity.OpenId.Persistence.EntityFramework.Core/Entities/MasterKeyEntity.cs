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
using NCode.Identity.Secrets.Persistence.DataContracts;

namespace NCode.Identity.OpenId.Persistence.EntityFramework.Core.Entities;

/// <summary>
/// Represents an entity framework data contract for a master key, the per-owner key that protects the owner's
/// persisted secrets. The complimentary DTO for this entity is <see cref="PersistedMasterKey"/>.
/// </summary>
/// <remarks>
/// This entity is intentionally not tenant-scoped: server-owned rows have no tenant, so the store always filters by
/// owner explicitly.
/// </remarks>
[Index(nameof(KeyId), IsUnique = true)]
[Index(nameof(OwnerType), nameof(NormalizedOwnerId), nameof(Version), IsUnique = true)]
public sealed class MasterKeyEntity : ISupportConcurrencyToken
{
    /// <summary>
    /// Specifies the maximum length of <see cref="OwnerType"/>.
    /// </summary>
    private const int OwnerTypeMaxLength = 50;

    /// <summary>
    /// Specifies the maximum length of <see cref="RootKeyProvider"/> and <see cref="RecoveryKeyId"/>.
    /// </summary>
    private const int NameMaxLength = 100;

    /// <summary>
    /// Specifies the maximum length of <see cref="RootKeyId"/>.
    /// </summary>
    private const int RootKeyIdMaxLength = 2048;

    /// <summary>
    /// Specifies the maximum length of <see cref="RootWrappedKey"/> and <see cref="RecoveryWrappedKey"/>.
    /// </summary>
    private const int WrappedKeyMaxLength = 4096;

    /// <summary>
    /// Specifies the maximum length of <see cref="KeyCheck"/>.
    /// </summary>
    private const int KeyCheckMaxLength = 100;

    /// <summary>
    /// Gets the surrogate identifier for this entity.
    /// </summary>
    [Key]
    [UseIdGenerator]
    public required long Id { get; init; }

    /// <summary>
    /// Gets the identifier of this master key, which is written into every envelope that it protects.
    /// </summary>
    public required Guid KeyId { get; init; }

    /// <summary>
    /// Gets the type of owner of this master key, such as <c>server</c> or <c>tenant</c>.
    /// </summary>
    [Unicode(false)]
    [MaxLength(OwnerTypeMaxLength)]
    public required string OwnerType { get; init; }

    /// <summary>
    /// Gets the identifier of the owner of this master key, as given.
    /// </summary>
    [Unicode(false)]
    [MaxLength(MaxLengths.ResourceId)]
    public required string OwnerId { get; init; }

    /// <summary>
    /// Gets the value of <see cref="OwnerId"/> in a normalized form so that lookups can be sargable for DBMS
    /// engines that don't support case-insensitive indices.
    /// </summary>
    [Unicode(false)]
    [MaxLength(MaxLengths.ResourceId)]
    public required string NormalizedOwnerId { get; init; }

    /// <summary>
    /// Gets the version of this master key, which starts at <c>1</c> and increments for each owner.
    /// </summary>
    public required int Version { get; init; }

    /// <summary>
    /// Gets the <see cref="DateTimeOffset"/> when this master key was created.
    /// </summary>
    public required DateTimeOffset CreatedWhen { get; init; }

    /// <summary>
    /// Gets or sets the <see cref="DateTimeOffset"/> when this master key was retired, or <c>null</c> when this is
    /// the active master key for its owner.
    /// </summary>
    public required DateTimeOffset? RetiredWhen { get; set; }

    /// <summary>
    /// Gets or sets the name of the root key provider that wrapped <see cref="RootWrappedKey"/>.
    /// </summary>
    [Unicode(false)]
    [MaxLength(NameMaxLength)]
    public required string RootKeyProvider { get; set; }

    /// <summary>
    /// Gets or sets the root key provider's identifier of the key that wrapped <see cref="RootWrappedKey"/>.
    /// </summary>
    [Unicode(false)]
    [MaxLength(RootKeyIdMaxLength)]
    public required string RootKeyId { get; set; }

    /// <summary>
    /// Gets or sets the base64url encoded master key as wrapped by the root key provider.
    /// </summary>
    [Unicode(false)]
    [MaxLength(WrappedKeyMaxLength)]
    public required string RootWrappedKey { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the recovery key that wrapped <see cref="RecoveryWrappedKey"/>, or <c>null</c>
    /// when recovery is disabled.
    /// </summary>
    [Unicode(false)]
    [MaxLength(NameMaxLength)]
    public required string? RecoveryKeyId { get; set; }

    /// <summary>
    /// Gets or sets the base64url encoded master key as wrapped by the recovery key, or <c>null</c> when recovery is
    /// disabled.
    /// </summary>
    [Unicode(false)]
    [MaxLength(WrappedKeyMaxLength)]
    public required string? RecoveryWrappedKey { get; set; }

    /// <summary>
    /// Gets the base64url encoded key check value that verifies the unwrapped master key.
    /// </summary>
    [Unicode(false)]
    [MaxLength(KeyCheckMaxLength)]
    public required string KeyCheck { get; init; }

    /// <inheritdoc />
    [Unicode(false)]
    [MaxLength(MaxLengths.ConcurrencyToken)]
    [ConcurrencyCheck]
    public required string ConcurrencyToken { get; set; }
}
