// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.OpenId;

public static partial class OpenIdConstants
{
    /// <summary>
    /// Contains the reserved audience identifiers for the system-owned resource servers seeded into tenants. The OpenID
    /// identity and management resource servers are seeded into every tenant; the management resource server's
    /// control-plane scope families (platform, team and tenant provisioning) are seeded only into the root tenant.
    /// </summary>
    public static class SystemResourceServerIdentifiers
    {
        /// <summary>
        /// Contains the reserved audience identifier for the OpenID Connect identity resource server:
        /// 'urn:ncode:openid'.
        /// </summary>
        public const string OpenId = "urn:ncode:openid";

        /// <summary>
        /// Contains the reserved audience identifier for the single management resource server, which gates the whole
        /// management API: 'urn:ncode:management'. Its tenant-plane scope families are seeded into every tenant; its
        /// control-plane (platform, team and tenant provisioning) families are seeded only into the root tenant.
        /// </summary>
        public const string Management = "urn:ncode:management";
    }
}
