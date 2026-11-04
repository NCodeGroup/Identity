// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using JetBrains.Annotations;
using NCode.Identity.Persistence;

namespace NCode.Identity.OpenId.Persistence.DataContracts;

/// <summary>
/// Contains the data for a persisted <c>OAuth</c> or <c>OpenID Connect</c> Server instance.
/// </summary>
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class PersistedPlatform : BasePersistedPlatformResource
{
    /// <inheritdoc/>
    [MaxLength(MaxLengths.ResourceType)]
    public override string ResourceType => ResourceTypePrefix;

    /// <summary>
    /// Gets or sets the display name for an OpenID Server instance.
    /// </summary>
    public required string DisplayName { get; set; }

    /// <summary>
    /// Gets or sets the JSON settings for an OpenID Server instance.
    /// </summary>
    public required PersistedPlatformSettings Settings { get; set; }

    /// <summary>
    /// Gets or sets the collection of secrets only known to an OpenID Server instance.
    /// </summary>
    public required PersistedPlatformSecrets Secrets { get; set; }

    /// <summary>
    /// Gets or sets the fingerprint of the server's effective settings that every tenant and client of the server
    /// was last reconciled against, or <c>null</c> when they have not been reconciled since the server's settings
    /// last changed.
    /// </summary>
    [MaxLength(MaxLengths.ConcurrencyToken)]
    public string? ReconciledSettingsFingerprint { get; set; }

    /// <summary>
    /// Gets or sets the server's effective settings that every tenant and client of the server was last reconciled
    /// against, as a JSON object of each setting's name and value, or <c>null</c> when there is nothing to compare a
    /// change with: the settings were never reconciled, or they changed twice before they were.
    /// </summary>
    public JsonElement? ReconciledSettings { get; set; }
}
