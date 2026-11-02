// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;
using NCode.Identity.Persistence;

namespace NCode.Identity.OpenId.Persistence.DataContracts;

/// <summary>
/// Contains the data for a persisted <c>OAuth</c> or <c>OpenID Connect</c> Client instance.
/// </summary>
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class PersistedClient : BasePersistedClientResource
{
    /// <inheritdoc/>
    [MaxLength(MaxLengths.ResourceType)]
    public override string ResourceType => ResourceTypePrefix;

    //

    /// <summary>
    /// Gets or sets a value indicating whether the client is disabled.
    /// </summary>
    public required bool IsDisabled { get; set; }

    /// <summary>
    /// Gets the identifier of the resource server this client acts for, or <c>null</c> for an ordinary client. A
    /// client bound to a resource server is that resource server's credential at the token introspection endpoint and
    /// is accepted nowhere else. The binding is set when the client is created and never changes.
    /// </summary>
    [MaxLength(MaxLengths.ResourceId)]
    public string? ResourceServerId { get; init; }

    /// <summary>
    /// Gets or sets the JSON settings for an OpenID Client instance.
    /// </summary>
    public required PersistedClientSettings Settings { get; set; }

    /// <summary>
    /// Gets or sets the collection of secrets only known to an OpenID Client instance.
    /// </summary>
    public required PersistedClientSecrets Secrets { get; set; }
}
