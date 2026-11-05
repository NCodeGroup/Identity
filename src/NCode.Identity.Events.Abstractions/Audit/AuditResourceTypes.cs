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

    /// <summary>
    /// An OpenID tenant, shared by the protocol plane and the management plane.
    /// </summary>
    public const string Tenant = "tenant";

    /// <summary>
    /// A secret belonging to an OpenID tenant, shared by the protocol plane and the management plane.
    /// </summary>
    public const string TenantSecret = "tenant.secret";

    /// <summary>
    /// The resource type of a master key, which protects the stored secrets of a tenant, and the Data Protection key ring and host data of the platform.
    /// </summary>
    public const string MasterKey = "master_key";

    /// <summary>
    /// The resource type of the root key that a tenant brings, which wraps the tenant's master keys.
    /// </summary>
    public const string RootKey = "root_key";
}
