// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.OpenId;

public static partial class OpenIdProtocol
{
    /// <summary>
    /// Contains constants that specify how Claim Values are provided by the OpenID Provider.
    /// https://openid.net/specs/openid-connect-core-1_0.html#ClaimTypes
    /// </summary>
    public static class ClaimTypes
    {
        /// <summary>
        /// Indicates that Claims are directly asserted by the OpenID Provider.
        /// </summary>
        public const string Normal = "normal";

        /// <summary>
        /// Indicates that Claims are asserted by a Claims Provider other than the OpenID Provider but are returned by the OpenID Provider.
        /// </summary>
        public const string Aggregated = "aggregated";

        /// <summary>
        /// Indicates that Claims are asserted by a Claims Provider other than the OpenID Provider but are returned as references by the OpenID Provider.
        /// </summary>
        public const string Distributed = "distributed";
    }
}
