// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Options;
using NCode.Identity.OpenId.Management.Clients;

namespace NCode.Identity.Server.DevelopmentEnvironment;

/// <summary>
/// A policy that names the configured bootstrap administrator client in the configured bootstrap tenant as host-managed.
/// The bootstrap administrator client's secret and <c>GlobalAdmin</c> authority come from host configuration, so the
/// management API never changes it.
/// </summary>
internal sealed class BootstrapAdminHostManagedClientPolicy(
    IOptions<BootstrapAdminOptions> optionsAccessor
) : IHostManagedClientPolicy
{
    private BootstrapAdminOptions Options { get; } = optionsAccessor.Value;

    /// <inheritdoc />
    public bool IsHostManaged(string tenantId, string clientId)
    {
        var bootstrapClientId = Options.ClientId;
        if (string.IsNullOrEmpty(bootstrapClientId))
        {
            return false;
        }

        return string.Equals(clientId, bootstrapClientId, StringComparison.Ordinal)
            && string.Equals(tenantId, Options.TenantId, StringComparison.Ordinal);
    }
}
