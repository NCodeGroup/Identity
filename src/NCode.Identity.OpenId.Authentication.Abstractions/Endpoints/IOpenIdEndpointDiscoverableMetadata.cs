// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Authentication.Endpoints;

/// <summary>
/// Represents metadata used during <c>OAuth</c> or <c>OpenID Connect</c> authorization server metadata discovery.
/// If <see cref="IsDiscoverable"/> is <c>true</c>, then the associated endpoint will be used for metadata discovery.
/// </summary>
[PublicAPI]
public interface IOpenIdEndpointDiscoverableMetadata
{
    /// <summary>
    /// Gets a value indicating whether the associated endpoint should be used for metadata discovery.
    /// </summary>
    bool IsDiscoverable { get; }
}
