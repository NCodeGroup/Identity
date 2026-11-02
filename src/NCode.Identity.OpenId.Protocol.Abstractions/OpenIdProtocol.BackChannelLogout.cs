// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.OpenId;

public static partial class OpenIdProtocol
{
    /// <summary>
    /// Contains constants used by OpenID Connect Back-Channel Logout.
    /// </summary>
    public static class BackChannelLogout
    {
        /// <summary>
        /// Contains the event identifier that appears as a member of a <c>logout_token</c>'s <c>events</c> claim.
        /// </summary>
        public const string EventName = "http://schemas.openid.net/event/backchannel-logout";
    }
}
