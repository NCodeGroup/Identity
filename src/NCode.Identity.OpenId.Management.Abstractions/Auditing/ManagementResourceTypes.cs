// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using NCode.Identity.Events.Audit;

namespace NCode.Identity.OpenId.Management.Auditing;

/// <summary>
/// Defines the well-known <see cref="IResourceChangeAuditEvent.ResourceType"/> values emitted by the
/// management API's audit events.
/// </summary>
[PublicAPI]
public static class ManagementResourceTypes
{
    /// <summary>
    /// An OpenID client.
    /// </summary>
    public const string Client = "client";

    /// <summary>
    /// A secret belonging to an OpenID client.
    /// </summary>
    public const string ClientSecret = "client.secret";

    /// <summary>
    /// A team, a grouping of related tenants.
    /// </summary>
    public const string Team = "team";

    /// <summary>
    /// An OpenID tenant.
    /// </summary>
    public const string Tenant = "tenant";

    /// <summary>
    /// A secret belonging to an OpenID tenant.
    /// </summary>
    public const string TenantSecret = "tenant.secret";

    /// <summary>
    /// The platform, the deployment root.
    /// </summary>
    public const string Platform = "platform";

    /// <summary>
    /// A resource server (API, in Auth0 terms).
    /// </summary>
    public const string ResourceServer = "resource_server";

    /// <summary>
    /// A scope belonging to a resource server.
    /// </summary>
    public const string ResourceServerScope = "resource_server.scope";

    /// <summary>
    /// A secret belonging to a resource server.
    /// </summary>
    public const string ResourceServerSecret = "resource_server.secret";

    /// <summary>
    /// An initial access token (RFC 7591) that authorizes protected dynamic client registration.
    /// </summary>
    public const string InitialAccessToken = "initial_access_token";

    /// <summary>
    /// An end user's federated principal.
    /// </summary>
    public const string Principal = "principal";
}
