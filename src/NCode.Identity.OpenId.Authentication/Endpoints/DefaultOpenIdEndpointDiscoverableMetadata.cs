// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.OpenId.Authentication.Endpoints;

/// <summary>
/// Provides a default implementation of the <see cref="IOpenIdEndpointDiscoverableMetadata"/> abstraction.
/// </summary>
internal class DefaultOpenIdEndpointDiscoverableMetadata : IOpenIdEndpointDiscoverableMetadata
{
    /// <inheritdoc />
    public bool IsDiscoverable { get; init; } = true;
}
