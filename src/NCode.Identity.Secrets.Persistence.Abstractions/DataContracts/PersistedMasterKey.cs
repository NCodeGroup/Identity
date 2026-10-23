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
using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;
using NCode.Identity.Persistence;
using NCode.Identity.Secrets.Persistence.Protection;

namespace NCode.Identity.Secrets.Persistence.DataContracts;

/// <summary>
/// Contains the data for a persisted master key, which is the key that protects the secrets of a single owner and
/// that is itself stored only in a wrapped form.
/// </summary>
/// <remarks>
/// An owner has at most one active master key, which is the one that has not been retired, and zero or more retired
/// master keys. A retired master key is kept so that the values it protected can still be read.
/// </remarks>
// See ADR-0228.
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class PersistedMasterKey : ISupportConcurrencyToken
{
    /// <summary>
    /// Gets the identifier of the master key, which is written into every value that the master key protects.
    /// </summary>
    public required Guid KeyId { get; init; }

    /// <summary>
    /// Gets the kind of owner of the master key, such as one of the values in <see cref="SecretOwnerTypes"/>.
    /// </summary>
    public required string OwnerType { get; init; }

    /// <summary>
    /// Gets the identifier of the owner of the master key within its kind.
    /// </summary>
    [MaxLength(MaxLengths.ResourceId)]
    public required string OwnerId { get; init; }

    /// <summary>
    /// Gets the version of the master key, which starts at one and increases by one for each new master key of the
    /// same owner.
    /// </summary>
    public required int Version { get; init; }

    /// <summary>
    /// Gets the <see cref="DateTimeOffset"/> when the master key was created.
    /// </summary>
    public required DateTimeOffset CreatedWhen { get; init; }

    /// <summary>
    /// Gets or sets the <see cref="DateTimeOffset"/> when the master key was retired, or <c>null</c> when the master
    /// key is the active master key of its owner.
    /// </summary>
    public DateTimeOffset? RetiredWhen { get; set; }

    /// <summary>
    /// Gets or sets the name of the root key provider that wrapped <see cref="RootWrappedKey"/>, such as one of the
    /// values in <see cref="RootKeyProviderNames"/>.
    /// </summary>
    public required string RootKeyProvider { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the root key that wrapped <see cref="RootWrappedKey"/>, as reported by the root
    /// key provider. For example this is the ARN of an AWS KMS key or the fingerprint of a local root key.
    /// </summary>
    public required string RootKeyId { get; set; }

    /// <summary>
    /// Gets or sets the master key as wrapped by the root key provider, encoded as base64url.
    /// </summary>
    public required string RootWrappedKey { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the recovery key that wrapped <see cref="RecoveryWrappedKey"/>, or <c>null</c>
    /// when recovery is disabled.
    /// </summary>
    public string? RecoveryKeyId { get; set; }

    /// <summary>
    /// Gets or sets the master key as wrapped by the recovery key, encoded as base64url, or <c>null</c> when recovery
    /// is disabled.
    /// </summary>
    public string? RecoveryWrappedKey { get; set; }

    /// <summary>
    /// Gets the base64url encoded check value that binds the key identifier, the owner and the version to the master
    /// key, so that a master key which was moved to a different row is detected when it is unwrapped.
    /// </summary>
    public required string KeyCheck { get; init; }

    /// <inheritdoc/>
    [MaxLength(MaxLengths.ConcurrencyToken)]
    public string ConcurrencyToken { get; set; } = string.Empty;

    /// <summary>
    /// Gets the owner of the master key.
    /// </summary>
    public SecretOwner Owner => new(OwnerType, OwnerId);
}
