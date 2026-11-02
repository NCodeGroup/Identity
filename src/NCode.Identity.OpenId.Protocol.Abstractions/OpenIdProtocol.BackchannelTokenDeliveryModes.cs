// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.OpenId;

public static partial class OpenIdProtocol
{
    /// <summary>
    /// Contains constant values for the CIBA (OpenID Connect Client-Initiated Backchannel Authentication) token
    /// delivery modes.
    /// </summary>
    public static class BackchannelTokenDeliveryModes
    {
        /// <summary>
        /// Contains the constant value 'poll': the client polls the token endpoint with its <c>auth_req_id</c> until
        /// the request completes.
        /// </summary>
        public const string Poll = "poll";

        /// <summary>
        /// Contains the constant value 'ping': the OP notifies the client's <c>client_notification_endpoint</c> when
        /// the request completes, and the client then polls the token endpoint once.
        /// </summary>
        public const string Ping = "ping";

        /// <summary>
        /// Contains the constant value 'push': the OP delivers the tokens directly to the client's
        /// <c>client_notification_endpoint</c> when the request completes.
        /// </summary>
        public const string Push = "push";
    }
}
