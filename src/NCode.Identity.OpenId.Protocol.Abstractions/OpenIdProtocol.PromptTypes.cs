// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.OpenId;

public static partial class OpenIdProtocol
{
    /// <summary>
    /// Contains constants for possible values of the <c>prompt</c> parameter.
    /// </summary>
    public static class PromptTypes
    {
        /// <summary>
        /// Contains the <c>prompt</c> parameter value for <c>none</c>.
        /// </summary>
        public const string None = "none";

        /// <summary>
        /// Contains the <c>prompt</c> parameter value for <c>login</c>.
        /// </summary>
        public const string Login = "login";

        /// <summary>
        /// Contains the <c>prompt</c> parameter value for <c>consent</c>.
        /// </summary>
        public const string Consent = "consent";

        /// <summary>
        /// Contains the <c>prompt</c> parameter value for <c>select_account</c>.
        /// </summary>
        public const string SelectAccount = "select_account";

        /// <summary>
        /// Contains the <c>prompt</c> parameter value for <c>create</c>.
        /// </summary>
        public const string CreateAccount = "create";
    }
}
