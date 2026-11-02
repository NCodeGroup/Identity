// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Management.Contracts.ResourceServers;

/// <summary>
/// Represents the request body to update the metadata of an existing scope. The scope value is immutable.
/// </summary>
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class UpdateScopeRequest
{
    /// <summary>
    /// Gets the human-readable description for the scope.
    /// </summary>
    public required string? Description { get; init; }
}
