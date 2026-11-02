// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.Bff.Endpoints;

/// <summary>
/// Marks the <c>/bff/user</c> endpoint, so it gets the same request checks as a <see cref="BffApiAttribute"/> endpoint.
/// </summary>
internal sealed class BffUserEndpointMetadata
{
    /// <summary>
    /// Gets the singleton instance.
    /// </summary>
    public static BffUserEndpointMetadata Instance { get; } = new();
}
