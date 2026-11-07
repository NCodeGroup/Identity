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
    /// Gets or sets the identifier of the client's principal: the principal that a token the client obtains for itself
    /// resolves to and that role assignments name. The store assigns a server-generated value when the client is added,
    /// whatever value the caller set, and never changes it.
    /// </summary>
    [MaxLength(OpenIdMaxLengths.PrincipalId)]
    public string PrincipalId { get; set; } = string.Empty;

    /// <summary>
    /// Gets the type of the node that owns this client as its machine client — <c>platform</c> or <c>team</c> — or
    /// <c>null</c> for an ordinary client. A machine client lives in the control-plane tenant, obtains tokens only for
    /// itself, holds roles only at its owning node or beneath it, and is changed only with creation authority at that
    /// node. It is set when the client is created and never changes.
    /// </summary>
    // See ADR-1466.
    [MaxLength(MaxLengths.ResourceType)]
    public string? OwningNodeType { get; init; }

    /// <summary>
    /// Gets the identifier of the node that owns this client: the team's identifier, or <c>platform</c>; <c>null</c>
    /// for an ordinary client.
    /// </summary>
    // See ADR-1466.
    [MaxLength(MaxLengths.ResourceId)]
    public string? OwningNodeId { get; init; }

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
