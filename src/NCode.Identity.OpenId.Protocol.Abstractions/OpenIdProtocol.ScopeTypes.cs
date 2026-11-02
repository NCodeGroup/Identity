// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.OpenId;

public static partial class OpenIdProtocol
{
    /// <summary>
    /// Contains constants for possible values of the <c>scope</c> parameter.
    /// </summary>
    public static class ScopeTypes
    {
        /// <summary>
        /// Contains the <c>scope</c> parameter value for <c>openid</c>.
        /// </summary>
        public const string OpenId = "openid";

        /// <summary>
        /// Contains the <c>scope</c> parameter value for <c>profile</c>.
        /// </summary>
        public const string Profile = "profile";

        /// <summary>
        /// Contains the <c>scope</c> parameter value for <c>email</c>.
        /// </summary>
        public const string Email = "email";

        /// <summary>
        /// Contains the <c>scope</c> parameter value for <c>address</c>.
        /// </summary>
        public const string Address = "address";

        /// <summary>
        /// Contains the <c>scope</c> parameter value for <c>phone</c>.
        /// </summary>
        public const string Phone = "phone";

        /// <summary>
        /// Contains the <c>scope</c> parameter value for <c>offline_access</c>.
        /// </summary>
        public const string OfflineAccess = "offline_access";
    }
}
