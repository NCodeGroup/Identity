#region Copyright Preamble

// Copyright @ 2026 NCode Group
//
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
//
//        http://www.apache.org/licenses/LICENSE-2.0
//
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.

#endregion

using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Management.Authorization;

/// <summary>
/// Contains the controlled vocabulary of built-in resource node types that a role assignment may target. A role
/// assigned at a node applies to that node and every resource beneath it (ADR-0034). Applications may assign ownership
/// at their own node types using application-defined values.
/// </summary>
[PublicAPI]
public static class ResourceNodeTypes
{
    /// <summary>
    /// The server root node. A role assigned here applies across the whole server.
    /// </summary>
    public const string Server = "server";

    /// <summary>
    /// A tenant node. A role assigned here applies across the tenant and its child resources.
    /// </summary>
    public const string Tenant = "tenant";

    /// <summary>
    /// A client (application) node.
    /// </summary>
    public const string Client = "client";

    /// <summary>
    /// A resource server (API) node.
    /// </summary>
    public const string ResourceServer = "resource_server";

    /// <summary>
    /// A grant (user authorization) node.
    /// </summary>
    public const string Grant = "grant";
}
