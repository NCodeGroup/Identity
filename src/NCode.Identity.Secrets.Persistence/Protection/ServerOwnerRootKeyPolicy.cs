// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;

namespace NCode.Identity.Secrets.Persistence.Protection;

/// <summary>
/// Refuses the <c>server</c> owner, whose master keys protect the deployment's own Data Protection key ring and are
/// therefore always wrapped by the deployment's root key.
/// </summary>
// See ADR-1411.
internal sealed class ServerOwnerRootKeyPolicy : IOwnerRootKeyPolicy
{
    /// <inheritdoc />
    public bool CanBringRootKey(SecretOwner owner, [NotNullWhen(false)] out string? reason)
    {
        if (
            string.Equals(
                owner.OwnerType,
                SecretOwnerTypes.Server,
                StringComparison.OrdinalIgnoreCase
            )
        )
        {
            reason =
                "The server's master keys protect the deployment's own Data Protection key ring, so they are always wrapped by the deployment's root key.";
            return false;
        }

        reason = null;
        return true;
    }
}
