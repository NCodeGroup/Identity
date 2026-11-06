// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.OpenId.Management.Endpoints.Clients;

/// <summary>
/// Defines a service that decides whether the host configures a client, which no management caller changes.
/// </summary>
// See ADR-1417.
internal interface IHostManagedClientChecker
{
    /// <summary>
    /// Determines whether any registered <see cref="NCode.Identity.OpenId.Management.Clients.IHostManagedClientPolicy"/>
    /// names the client.
    /// </summary>
    /// <param name="tenantId">The identifier of the OpenID Tenant that holds the client.</param>
    /// <param name="clientId">The identifier of the OpenID Client.</param>
    /// <returns><see langword="true"/> when the host configures the client; otherwise, <see langword="false"/>.</returns>
    bool IsHostManaged(string tenantId, string clientId);
}
