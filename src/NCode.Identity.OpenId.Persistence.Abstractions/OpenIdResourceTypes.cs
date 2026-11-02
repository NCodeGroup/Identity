// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Persistence;

/// <summary>
/// Contains constants used to identify various resource types.
/// </summary>
[PublicAPI]
[ExcludeFromCodeCoverage]
public static class OpenIdResourceTypes
{
    /// <summary>
    /// Contains the value used to identify the resource type for a collection of settings.
    /// </summary>
    public const string Settings = "settings";

    /// <summary>
    /// Contains the value used to identify the resource type for a server.
    /// </summary>
    public const string Server = "server";

    /// <summary>
    /// Contains the value used to identify the resource type for a tenant.
    /// </summary>
    public const string Tenant = "tenant";

    /// <summary>
    /// Contains the value used to identify the resource type for a client.
    /// </summary>
    public const string Client = "client";

    /// <summary>
    /// Contains the value used to identify the resource type for a resource server (an API, in Auth0 terms).
    /// </summary>
    public const string ResourceServer = "resource_server";
}
