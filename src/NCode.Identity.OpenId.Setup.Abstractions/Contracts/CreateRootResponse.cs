// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Setup.Contracts;

/// <summary>
/// Represents the response to creating the root administrator.
/// </summary>
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class CreateRootResponse
{
    /// <summary>
    /// Gets the identifier of the control-plane tenant that holds the root administrator.
    /// </summary>
    public required string TenantId { get; init; }

    /// <summary>
    /// Gets the identifier of the root administrator's principal.
    /// </summary>
    public required string PrincipalId { get; init; }

    /// <summary>
    /// Gets the root administrator's username.
    /// </summary>
    public required string UserName { get; init; }

    /// <summary>
    /// Gets the <c>client_id</c> of the management console's client that setup registered, or <c>null</c> when setup
    /// registered none.
    /// </summary>
    public string? ManagementConsoleClientId { get; init; }
}
