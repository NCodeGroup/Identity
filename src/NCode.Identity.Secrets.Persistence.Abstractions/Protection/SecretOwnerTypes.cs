// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Secrets.Persistence.Protection;

/// <summary>
/// Contains the known values for <see cref="SecretOwner.OwnerType"/>. The owner type is an open string, so these are
/// the owner types that are known to the library and not necessarily all of the owner types that exist.
/// </summary>
[PublicAPI]
public static class SecretOwnerTypes
{
    /// <summary>
    /// Contains the owner type for the platform, the deployment root; its master key protects the Data Protection key ring.
    /// </summary>
    public const string Platform = "platform";

    /// <summary>
    /// Contains the owner type for secrets that belong to a single tenant.
    /// </summary>
    public const string Tenant = "tenant";
}
