// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;

namespace NCode.Identity.Secrets.Persistence.Protection;

/// <summary>
/// Refuses the <c>platform</c> owner, whose master keys protect the deployment's Data Protection key ring and the host's
/// own data and are therefore always wrapped by the deployment's root key.
/// </summary>
// See ADR-1414.
internal sealed class PlatformOwnerRootKeyPolicy : IOwnerRootKeyPolicy
{
    /// <inheritdoc />
    public bool CanBringRootKey(SecretOwner owner, [NotNullWhen(false)] out string? reason)
    {
        if (
            string.Equals(
                owner.OwnerType,
                SecretOwnerTypes.Platform,
                StringComparison.OrdinalIgnoreCase
            )
        )
        {
            reason =
                "The platform's master keys protect the deployment's Data Protection key ring and the host's own data, so they are always wrapped by the deployment's root key.";
            return false;
        }

        reason = null;
        return true;
    }
}
