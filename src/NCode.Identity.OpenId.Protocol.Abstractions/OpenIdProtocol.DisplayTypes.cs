// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.OpenId;

public static partial class OpenIdProtocol
{
    /// <summary>
    /// Contains constants for possible values of the <c>display</c> parameter.
    /// </summary>
    public static class DisplayTypes
    {
        /// <summary>
        /// Contains the <c>display</c> parameter value for <c>page</c>.
        /// </summary>
        public const string Page = "page";

        /// <summary>
        /// Contains the <c>display</c> parameter value for <c>popup</c>.
        /// </summary>
        public const string Popup = "popup";

        /// <summary>
        /// Contains the <c>display</c> parameter value for <c>touch</c>.
        /// </summary>
        public const string Touch = "touch";

        /// <summary>
        /// Contains the <c>display</c> parameter value for <c>wap</c>.
        /// </summary>
        public const string Wap = "wap";
    }
}
