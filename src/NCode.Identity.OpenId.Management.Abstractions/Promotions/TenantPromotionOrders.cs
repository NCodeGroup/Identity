// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Management.Promotions;

/// <summary>
/// Defines the <see cref="ITenantPromotionContributor.Order"/> of each built-in contributor. A contributor that
/// depends on another's resources takes a later position.
/// </summary>
// See ADR-1425.
[PublicAPI]
public static class TenantPromotionOrders
{
    /// <summary>
    /// The position of the contributor that promotes the tenant's settings document.
    /// </summary>
    public const int TenantSettings = 100;

    /// <summary>
    /// The position of the contributor that promotes the tenant's resource servers and their scopes.
    /// </summary>
    public const int ResourceServers = 200;

    /// <summary>
    /// The position of the contributor that promotes the tenant's clients.
    /// </summary>
    public const int Clients = 300;

    /// <summary>
    /// The position of the contributor that promotes the grants of the promoted clients.
    /// </summary>
    public const int ClientGrants = 400;
}
