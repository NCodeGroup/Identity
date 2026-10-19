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
using NCode.Identity.Settings;

namespace NCode.Identity.OpenId.Settings;

/// <summary>
/// Contains the names of the setting scopes the OpenID server registers: the server, whose settings cascade into every
/// tenant's, and the tenant, whose settings cascade into each of its clients' and resource servers'. These are the known
/// scopes, not all of them: a package that stores settings for an entity of its own registers another
/// <see cref="SettingScopeDescriptor"/> beneath the scope its settings inherit from.
/// </summary>
// See ADR-0149 and ADR-0150.
[PublicAPI]
public static class OpenIdSettingScopes
{
    /// <summary>
    /// Contains the name of the server scope, the root of the cascade.
    /// </summary>
    public const string Server = "server";

    /// <summary>
    /// Contains the name of the tenant scope, beneath the server.
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
