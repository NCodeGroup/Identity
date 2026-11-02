// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using NCode.Identity.OpenId.Persistence;

namespace NCode.Identity.OpenId.Management.Authorization;

/// <summary>
/// Contains the controlled vocabulary of built-in resource node types that a role assignment may target. A role
/// assigned at a node applies to that node and every resource beneath it. Applications may assign ownership
/// at their own node types using application-defined values. A built-in node's type is the resource type of the
/// persisted resource, so a store that removes a resource finds the role assignments at its node.
/// </summary>
[PublicAPI]
public static class ResourceNodeTypes
{
    /// <summary>
    /// The server root node. A role assigned here applies across the whole server.
    /// </summary>
    public const string Server = OpenIdResourceTypes.Server;

    /// <summary>
    /// A tenant node. A role assigned here applies across the tenant and its child resources.
    /// </summary>
    public const string Tenant = OpenIdResourceTypes.Tenant;

    /// <summary>
    /// A client (application) node.
    /// </summary>
    public const string Client = OpenIdResourceTypes.Client;

    /// <summary>
    /// A resource server (API) node.
    /// </summary>
    public const string ResourceServer = OpenIdResourceTypes.ResourceServer;

    /// <summary>
    /// A grant (user authorization) node.
    /// </summary>
    public const string Grant = "grant";
}
