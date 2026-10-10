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

namespace NCode.Identity.OpenId.Management.Endpoints;

/// <summary>
/// Declares the well-known endpoint-group name keys for the management API's route groups. A capability package that
/// plugs an endpoint into an existing group registers its <c>IEndpointProvider</c> as a keyed singleton under the owning
/// group's name using these constants, so the endpoint nests under that group's route prefix and shared conventions.
/// </summary>
[PublicAPI]
public static class ManagementEndpointGroups
{
    /// <summary>
    /// The name of the tenant-plane management group (<c>/api/tenants/{tenantId}</c>) that confines its endpoints to the
    /// addressed tenant.
    /// </summary>
    public const string Tenant = "api/tenant";
}
