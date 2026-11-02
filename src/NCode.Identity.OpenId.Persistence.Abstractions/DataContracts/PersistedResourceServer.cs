// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;
using NCode.Identity.Persistence;

namespace NCode.Identity.OpenId.Persistence.DataContracts;

/// <summary>
/// Contains the data for a persisted resource server (an API, in Auth0 terms). A resource server owns its scopes,
/// settings, and secrets, and is identified by its audience <see cref="Identifier"/>.
/// </summary>
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class PersistedResourceServer : BasePersistedResourceServerResource
{
    /// <inheritdoc/>
    [MaxLength(MaxLengths.ResourceType)]
    public override string ResourceType => ResourceTypePrefix;

    //

    /// <summary>
    /// Gets the operator-supplied audience identifier for this resource server.
    /// </summary>
    [MaxLength(OpenIdMaxLengths.ResourceServerIdentifier)]
    public required string Identifier { get; init; }

    /// <summary>
    /// Gets or sets the human-readable name for this resource server.
    /// </summary>
    [MaxLength(OpenIdMaxLengths.DisplayName)]
    public required string Name { get; set; }

    /// <summary>
    /// Gets a value indicating whether this resource server is a reserved, system-owned resource server that
    /// cannot be deleted.
    /// </summary>
    public required bool IsSystem { get; init; }

    /// <summary>
    /// Gets or sets a value indicating whether this resource server is disabled.
    /// </summary>
    public required bool IsDisabled { get; set; }

    /// <summary>
    /// Gets or sets the JSON settings for this resource server.
    /// </summary>
    public required PersistedResourceServerSettings Settings { get; set; }

    /// <summary>
    /// Gets or sets the collection of scopes owned by this resource server.
    /// </summary>
    public required IReadOnlyCollection<PersistedScope> Scopes { get; set; }

    /// <summary>
    /// Gets or sets the collection of secrets owned by this resource server.
    /// </summary>
    public required PersistedResourceServerSecrets Secrets { get; set; }
}
