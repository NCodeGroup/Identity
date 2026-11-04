// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using NCode.Identity.Settings;

namespace NCode.Identity.OpenId.Settings;

/// <summary>
/// Contains the names of the setting scopes the platform registers: the platform, whose settings cascade into every
/// team's, the team, whose settings cascade into each of its tenants', and the tenant, whose settings cascade into each of its clients' and resource servers'. These are the known
/// scopes, not all of them: a package that stores settings for an entity of its own registers another
/// <see cref="SettingScopeDescriptor"/> beneath the scope its settings inherit from.
/// </summary>
// See ADR-1149 and ADR-1150.
[PublicAPI]
public static class OpenIdSettingScopes
{
    /// <summary>
    /// Contains the name of the platform scope, the root of the cascade.
    /// </summary>
    public const string Platform = "platform";

    /// <summary>
    /// Contains the name of the team scope, beneath the platform and above the tenant.
    /// </summary>
    public const string Team = "team";

    /// <summary>
    /// Contains the name of the tenant scope, beneath the team.
    /// </summary>
    public const string Tenant = "tenant";

    /// <summary>
    /// Contains the name of the client scope, beneath the tenant.
    /// </summary>
    public const string Client = "client";

    /// <summary>
    /// Contains the name of the resource-server scope, beneath the tenant.
    /// </summary>
    public const string ResourceServer = "resource_server";
}
