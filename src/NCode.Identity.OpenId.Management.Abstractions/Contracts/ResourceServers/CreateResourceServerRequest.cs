// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Management.Contracts.ResourceServers;

/// <summary>
/// Represents the request body to create a new resource server.
/// </summary>
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class CreateResourceServerRequest
{
    /// <summary>
    /// Gets the operator-supplied audience identifier for the resource server.
    /// </summary>
    public required string Identifier { get; init; }

    /// <summary>
    /// Gets the human-readable name for the resource server.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets a value indicating whether the resource server is created in a disabled state.
    /// </summary>
    public required bool IsDisabled { get; init; }

    /// <summary>
    /// Gets the initial JSON settings for the resource server.
    /// </summary>
    public required JsonElement Settings { get; init; }
}
