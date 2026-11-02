// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.OpenId;

public static partial class OpenIdProtocol
{
    /// <summary>
    /// Contains constant values for well-known paths defined by the <c>OAuth</c> and <c>OpenID Connect</c> specifications.
    /// </summary>
    public static class WellKnownPaths
    {
        /// <summary>
        /// Contains the constant value '/.well-known/openid-configuration'
        /// (OpenID Connect Discovery 1.0, section 4).
        /// </summary>
        public const string OpenIdConfiguration = "/.well-known/openid-configuration";
    }
}
