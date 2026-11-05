// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Management.Clients;

/// <summary>
/// Names the clients whose credentials and authority the host configures. The management API refuses every change to
/// a client that any registered policy names, so authority over a tenant's clients never yields the tokens of a client
/// the host configures. A package that configures a client's credentials and authority registers a policy for it.
/// </summary>
// See ADR-1417.
[PublicAPI]
public interface IHostManagedClientPolicy
{
    /// <summary>
    /// Determines whether the host configures the specified client.
    /// </summary>
    /// <param name="tenantId">The identifier of the OpenID Tenant that owns the client.</param>
    /// <param name="clientId">The identifier of the OpenID Client.</param>
    /// <returns><c>true</c> when the host configures the client; otherwise, <c>false</c>.</returns>
    bool IsHostManaged(string tenantId, string clientId);
}
