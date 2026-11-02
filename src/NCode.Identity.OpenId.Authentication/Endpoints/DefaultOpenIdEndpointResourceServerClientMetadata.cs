// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.OpenId.Authentication.Endpoints;

/// <summary>
/// Provides a default implementation of the <see cref="IOpenIdEndpointResourceServerClientMetadata"/> abstraction.
/// </summary>
internal class DefaultOpenIdEndpointResourceServerClientMetadata
    : IOpenIdEndpointResourceServerClientMetadata
{
    /// <inheritdoc />
    public bool AcceptsResourceServerClients { get; init; } = true;
}
