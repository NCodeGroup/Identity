// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;
using NCode.Identity.Persistence;

namespace NCode.Identity.OpenId.Persistence.DataContracts;

/// <summary>
/// Contains a lightweight read projection of an enabled resource server and its scope values, without the server's
/// settings or secrets. Used by runtime scope/audience resolution to push row filtering into the DBMS while fetching
/// only the columns those readers need.
/// </summary>
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class ResourceServerScopeProjection
{
    /// <summary>
    /// Gets the operator-supplied audience identifier for this resource server.
    /// </summary>
    [MaxLength(OpenIdMaxLengths.ResourceServerIdentifier)]
    public required string Identifier { get; init; }

    /// <summary>
    /// Gets the opaque, server-generated public identifier for this resource server.
    /// </summary>
    [MaxLength(MaxLengths.ResourceId)]
    public required string ResourceServerId { get; init; }

    /// <summary>
    /// Gets a value indicating whether this resource server is a reserved, system-owned resource server whose scopes
    /// are implicitly available to every client.
    /// </summary>
    public required bool IsSystem { get; init; }

    /// <summary>
    /// Gets the case-sensitive scope values owned by this resource server.
    /// </summary>
    public required IReadOnlyCollection<string> ScopeValues { get; init; }
}
