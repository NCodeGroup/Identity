// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Management.Contracts;

/// <summary>
/// Represents the request to grant a principal ownership of a resource.
/// </summary>
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class AddOwnerRequest
{
    /// <summary>
    /// Gets the identifier of the principal to grant ownership to.
    /// </summary>
    public required string PrincipalId { get; init; }
}
