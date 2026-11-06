// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.OpenId.Management.Clients;

namespace NCode.Identity.OpenId.Management.Endpoints.Clients;

/// <summary>
/// Provides the default implementation of <see cref="IHostManagedClientChecker"/>, which asks every registered
/// <see cref="IHostManagedClientPolicy"/>.
/// </summary>
// See ADR-1417.
internal class DefaultHostManagedClientChecker(
    IEnumerable<IHostManagedClientPolicy> hostManagedClientPolicies
) : IHostManagedClientChecker
{
    private IReadOnlyList<IHostManagedClientPolicy> HostManagedClientPolicies { get; } =
    [.. hostManagedClientPolicies];

    /// <inheritdoc />
    public bool IsHostManaged(string tenantId, string clientId) =>
        HostManagedClientPolicies.Any(policy => policy.IsHostManaged(tenantId, clientId));
}
