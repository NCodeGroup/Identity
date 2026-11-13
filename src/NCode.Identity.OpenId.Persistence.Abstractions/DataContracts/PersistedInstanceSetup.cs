// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;
using NCode.Identity.Persistence;

namespace NCode.Identity.OpenId.Persistence.DataContracts;

/// <summary>
/// Contains the data for the persisted setup state of an instance: whether its first administrator has been created
/// and the hash of the one-time setup token that guards that creation. An instance has exactly one row, whose
/// <see cref="Key"/> is <see cref="SingletonKey"/>, that every replica reads.
/// </summary>
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class PersistedInstanceSetup : ISupportConcurrencyToken
{
    /// <summary>
    /// Specifies the <see cref="Key"/> of the one row that holds the setup state of an instance.
    /// </summary>
    public const string SingletonKey = "instance";

    /// <summary>
    /// Specifies the length in bytes of a <see cref="TokenHash"/>, which is a SHA-256 hash.
    /// </summary>
    public const int TokenHashLength = 32;

    /// <summary>
    /// Gets the key of the row, which is <see cref="SingletonKey"/>.
    /// </summary>
    [MaxLength(MaxLengths.ResourceId)]
    public required string Key { get; init; }

    /// <summary>
    /// Gets or sets a value indicating whether the instance is initialized, which is when its first administrator has
    /// been created. An instance is not initialized until its setup completes.
    /// </summary>
    public bool IsInitialized { get; set; }

    /// <summary>
    /// Gets or sets the SHA-256 hash of the current setup token, or <c>null</c> when no token is stored. Only the
    /// hash is stored, never the token itself.
    /// </summary>
    [MaxLength(TokenHashLength)]
    public byte[]? TokenHash { get; set; }

    /// <summary>
    /// Gets or sets the instant at which the current setup token was issued, or <c>null</c> when no token is stored.
    /// </summary>
    public DateTimeOffset? TokenIssuedAt { get; set; }

    /// <summary>
    /// Gets or sets the instant at which the current setup token expires, or <c>null</c> when no token is stored.
    /// </summary>
    public DateTimeOffset? TokenExpiresAt { get; set; }

    /// <summary>
    /// Gets or sets the instant at which the instance was initialized, or <c>null</c> when it is not initialized.
    /// </summary>
    public DateTimeOffset? InitializedAt { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the principal that was created as the first administrator, or <c>null</c> when
    /// the instance is not initialized.
    /// </summary>
    [MaxLength(OpenIdMaxLengths.PrincipalId)]
    public string? InitializedByPrincipalId { get; set; }

    /// <inheritdoc />
    [MaxLength(MaxLengths.ConcurrencyToken)]
    public required string ConcurrencyToken { get; set; }
}
