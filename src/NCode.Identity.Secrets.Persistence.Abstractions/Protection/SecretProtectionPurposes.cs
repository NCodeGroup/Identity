// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Secrets.Persistence.Protection;

/// <summary>
/// Contains the known values for <see cref="SecretProtectionContext.Purpose"/>. A value protected for one purpose
/// cannot be unprotected for another purpose, even when the owner and the item are the same.
/// </summary>
// See ADR-1235.
[PublicAPI]
public static class SecretProtectionPurposes
{
    /// <summary>
    /// Contains the purpose for the private or symmetric key material of a persisted secret.
    /// </summary>
    public const string PersistedSecret = "persisted-secret";

    /// <summary>
    /// Contains the purpose for the ASP.NET Core Data Protection key ring.
    /// </summary>
    public const string DataProtectionKeyRing = "data-protection-key-ring";

    /// <summary>
    /// The purpose of the payload of a persisted grant.
    /// </summary>
    public const string GrantPayload = "grant-payload";
}
