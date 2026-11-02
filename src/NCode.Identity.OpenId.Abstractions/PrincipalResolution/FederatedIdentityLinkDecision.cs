// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.OpenId.PrincipalResolution;

/// <summary>
/// Contains the outcome of a federated-identity linking decision: whether a new external connection identity is
/// attached to an existing principal, and the join key to persist on the new identity.
/// </summary>
[PublicAPI]
public sealed class FederatedIdentityLinkDecision
{
    /// <summary>
    /// Gets the identifier of the existing principal that the new identity is attached to, or <c>null</c> when a new
    /// principal is provisioned instead.
    /// </summary>
    public string? LinkedPrincipalId { get; init; }

    /// <summary>
    /// Gets the join key to persist on the new identity (such as a verified email address), or <c>null</c> when no
    /// usable join key is recorded.
    /// </summary>
    public string? JoinKey { get; init; }
}
