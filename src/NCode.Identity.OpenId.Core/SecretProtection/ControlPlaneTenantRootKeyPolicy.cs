// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using NCode.Identity.OpenId.Tenants;
using NCode.Identity.Secrets.Persistence.Protection;

namespace NCode.Identity.OpenId.SecretProtection;

/// <summary>
/// Refuses the control-plane tenant, which the management API authenticates every administrator against. A revoked root
/// key on that tenant would fail every management request, including the ones that restore it, so its master keys always
/// stay under the deployment's root key.
/// </summary>
// See ADR-1191 and ADR-1414.
internal sealed class ControlPlaneTenantRootKeyPolicy(ITenantResolver tenantResolver)
    : IOwnerRootKeyPolicy
{
    private ITenantResolver TenantResolver { get; } = tenantResolver;

    /// <inheritdoc />
    public bool CanBringRootKey(SecretOwner owner, [NotNullWhen(false)] out string? reason)
    {
        // The manager passes the normalized owner, so the control-plane tenant is normalized the same way.
        var controlPlane = SecretOwner
            .ForTenant(TenantResolver.GetControlPlaneTenantId())
            .Normalize();

        if (owner == controlPlane)
        {
            reason =
                "The management API authenticates against the control-plane tenant, so a revoked root key would lock out the administrators who restore it. Its keys stay under the deployment's root key.";
            return false;
        }

        reason = null;
        return true;
    }
}
