// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.OpenId.Authentication.Endpoints;

/// <summary>
/// Provides a default implementation of the <see cref="IOpenIdEndpointMeteredMetadata"/> abstraction.
/// </summary>
internal sealed class DefaultOpenIdEndpointMeteredMetadata : IOpenIdEndpointMeteredMetadata
{
    /// <inheritdoc />
    public bool IsMetered { get; init; } = true;
}
