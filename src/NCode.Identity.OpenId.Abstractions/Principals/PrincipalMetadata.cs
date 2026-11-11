// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using JetBrains.Annotations;
using NCode.Identity.Json;

namespace NCode.Identity.OpenId.Principals;

/// <summary>
/// Describes the server-owned metadata bags of a principal (the actor), resolved by principal id and projected into
/// issued tokens and the UserInfo response. The bags are a per-human concept — present for every connection kind (local
/// or federated) and every grant flow — not a property of any one connection.
/// </summary>
/// <param name="ProfileMetadata">The owner-updatable profile metadata as a free-form JSON object, defaulting to an
/// empty object <c>{}</c>. As owner-updatable data it must never drive authorization.</param>
/// <param name="SystemMetadata">The server-controlled system metadata as a free-form JSON object, defaulting to an
/// empty object <c>{}</c>. Managed only by the server or an administrator and never end-user writable, it may safely
/// carry authorization-relevant data.</param>
// See ADR-1055.
[PublicAPI]
public readonly record struct PrincipalMetadata(
    JsonElement ProfileMetadata,
    JsonElement SystemMetadata
)
{
    /// <summary>
    /// Gets an empty <see cref="PrincipalMetadata"/> whose bags are both the shared empty JSON object <c>{}</c>.
    /// </summary>
    public static PrincipalMetadata Empty { get; } =
        new(JsonElements.EmptyObject, JsonElements.EmptyObject);
}
