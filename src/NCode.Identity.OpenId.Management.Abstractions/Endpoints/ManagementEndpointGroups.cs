// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

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
