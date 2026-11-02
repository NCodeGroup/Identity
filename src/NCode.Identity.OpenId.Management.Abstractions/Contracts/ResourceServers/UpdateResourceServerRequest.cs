// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Management.Contracts.ResourceServers;

/// <summary>
/// Represents the mutable metadata of a resource server that a JSON Patch document may modify.
/// </summary>
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class UpdateResourceServerRequest
{
    /// <summary>
    /// Gets or sets the human-readable name for the resource server.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the resource server is disabled.
    /// </summary>
    public required bool IsDisabled { get; set; }
}
