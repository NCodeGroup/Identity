// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Events.Audit;

/// <summary>
/// Provides the resource type codes that are shared by more than one plane for an
/// <see cref="AuditResource.Type"/>.
/// </summary>
/// <remarks>
/// Resource type is an extensible string vocabulary, not a closed enumeration. This class declares
/// only the types that both the protocol and the management planes reference; other packages declare
/// their own types beside the entities they own (for example <c>ManagementResourceTypes</c>). Values
/// are lower-case and stable, suitable for use as a log or index dimension.
/// </remarks>
[PublicAPI]
public static class AuditResourceTypes
{
    /// <summary>
    /// The resource type code for a user session, shared by the protocol and management planes.
    /// </summary>
    public const string Session = "session";
}
