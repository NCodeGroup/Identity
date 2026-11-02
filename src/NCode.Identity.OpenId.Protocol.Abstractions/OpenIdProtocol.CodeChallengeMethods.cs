// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.OpenId;

public static partial class OpenIdProtocol
{
    /// <summary>
    /// Contains constants for possible values of the <c>code_challenge_method</c> parameter. The server offers only
    /// transformations that do not expose the code verifier in the authorization request.
    /// </summary>
    public static class CodeChallengeMethods
    {
        /// <summary>
        /// Contains the <c>code_challenge_method</c> parameter value for <c>S256</c>.
        /// </summary>
        public const string S256 = "S256";
    }
}
