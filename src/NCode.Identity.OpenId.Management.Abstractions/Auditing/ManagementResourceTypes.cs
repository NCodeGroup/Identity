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
    /// An OpenID tenant.
    /// </summary>
    public const string Tenant = "tenant";

    /// <summary>
    /// A secret belonging to an OpenID tenant.
    /// </summary>
    public const string TenantSecret = "tenant.secret";
}
