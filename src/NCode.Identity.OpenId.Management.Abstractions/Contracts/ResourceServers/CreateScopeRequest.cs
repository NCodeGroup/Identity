// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Management.Contracts.ResourceServers;

/// <summary>
/// Represents the request body to add a scope to a resource server.
/// </summary>
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class CreateScopeRequest
{
    /// <summary>
    /// Gets the value of the scope (for example, <c>read:messages</c>).
    /// </summary>
    public required string Value { get; init; }

    /// <summary>
    /// Gets the human-readable description for the scope.
    /// </summary>
    public required string? Description { get; init; }
}
