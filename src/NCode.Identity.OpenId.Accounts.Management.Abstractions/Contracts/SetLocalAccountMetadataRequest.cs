// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Accounts.Management.Contracts;

/// <summary>
/// Represents the request body to set a local account's server-owned metadata bags. Each bag is replaced only when
/// present in the request; an omitted bag is left unchanged. Because the management API is administrator-gated, this is
/// the server/admin write path through which the server-controlled <c>SystemMetadata</c> may be set — it is never
/// end-user writable.
/// </summary>
// See ADR-1055.
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class SetLocalAccountMetadataRequest
{
    /// <summary>
    /// Gets the replacement owner-updatable profile metadata as a free-form JSON object, or <c>null</c> to leave the
    /// current value unchanged.
    /// </summary>
    public JsonElement? ProfileMetadata { get; init; }

    /// <summary>
    /// Gets the replacement server-controlled system metadata as a free-form JSON object, or <c>null</c> to leave the
    /// current value unchanged.
    /// </summary>
    public JsonElement? SystemMetadata { get; init; }
}
