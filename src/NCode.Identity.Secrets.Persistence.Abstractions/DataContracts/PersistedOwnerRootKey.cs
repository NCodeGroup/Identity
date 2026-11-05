// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;
using NCode.Identity.Persistence;
using NCode.Identity.Secrets.KeyWrapping;
using NCode.Identity.Secrets.Persistence.Protection;

namespace NCode.Identity.Secrets.Persistence.DataContracts;

/// <summary>
/// Contains the data for the root key that a secret owner brings: the key in the owner's own key management service
/// that wraps every master key of the owner in place of the deployment's root key. The record names the key and holds
/// the state of its last validation, and never any key material.
/// </summary>
/// <remarks>
/// An owner has at most one owner root key. The record is not part of the settings of the owner: it is set by an
/// operation that probes the key, rotates the owner's master key and rewraps the owner's master keys.
/// </remarks>
// See ADR-1411.
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class PersistedOwnerRootKey : ISupportConcurrencyToken
{
    /// <summary>
    /// The maximum length of <see cref="Failure"/>.
    /// </summary>
    public const int MaxFailureLength = 1024;

    /// <summary>
    /// Gets the kind of owner of the root key, such as one of the values in <see cref="SecretOwnerTypes"/>.
    /// </summary>
    public required string OwnerType { get; init; }

    /// <summary>
    /// Gets the identifier of the owner of the root key within its kind.
    /// </summary>
    [MaxLength(MaxLengths.ResourceId)]
    public required string OwnerId { get; init; }

    /// <summary>
    /// Gets or sets the name of the key wrapper that wraps under the root key, such as one of the values in
    /// <see cref="KeyWrapperNames"/>.
    /// </summary>
    public required string KeyWrapperName { get; set; }

    /// <summary>
    /// Gets or sets the reference to the root key that the owner gave, such as the ARN of an AWS KMS key.
    /// </summary>
    public required string KeyReference { get; set; }

    /// <summary>
    /// Gets or sets the state of the root key: one of the values in <see cref="OwnerRootKeyStates"/>.
    /// </summary>
    public required string State { get; set; }

    /// <summary>
    /// Gets or sets the <see cref="DateTimeOffset"/> when the current key reference was set.
    /// </summary>
    public required DateTimeOffset CreatedWhen { get; set; }

    /// <summary>
    /// Gets or sets the <see cref="DateTimeOffset"/> when <see cref="State"/> last changed.
    /// </summary>
    public required DateTimeOffset StateChangedWhen { get; set; }

    /// <summary>
    /// Gets or sets the <see cref="DateTimeOffset"/> of the last successful probe of the root key, or <c>null</c>
    /// when none has succeeded.
    /// </summary>
    public DateTimeOffset? ValidatedWhen { get; set; }

    /// <summary>
    /// Gets or sets the <see cref="DateTimeOffset"/> of the last failed probe or failed completion, or <c>null</c>
    /// when none has failed.
    /// </summary>
    public DateTimeOffset? FailedWhen { get; set; }

    /// <summary>
    /// Gets or sets the description of the last failure, at most <see cref="MaxFailureLength"/> characters and never
    /// key material, or <c>null</c> after a success.
    /// </summary>
    [MaxLength(MaxFailureLength)]
    public string? Failure { get; set; }

    /// <inheritdoc/>
    [MaxLength(MaxLengths.ConcurrencyToken)]
    public string ConcurrencyToken { get; set; } = string.Empty;

    /// <summary>
    /// Gets the owner of the root key.
    /// </summary>
    public SecretOwner Owner => new(OwnerType, OwnerId);
}
