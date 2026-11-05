// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Management.Contracts.EncryptionKeys;

/// <summary>
/// Contains the states that a tenant root key reports. The set is open: a client treats a state it does not know as
/// unavailable.
/// </summary>
// See ADR-1411.
[PublicAPI]
public static class EncryptionRootKeyStates
{
    /// <summary>
    /// The key wraps the tenant's master keys.
    /// </summary>
    public const string Active = "active";

    /// <summary>
    /// The last probe of the key failed, so the tenant's encryption keys cannot be used until it is restored.
    /// </summary>
    public const string Unavailable = "unavailable";

    /// <summary>
    /// The key is being removed: the tenant's master keys move back under the deployment's root key.
    /// </summary>
    public const string Removing = "removing";
}
