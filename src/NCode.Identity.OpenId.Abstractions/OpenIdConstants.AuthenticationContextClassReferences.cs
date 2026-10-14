#region Copyright Preamble

// Copyright @ 2026 NCode Group
//
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
//
//        http://www.apache.org/licenses/LICENSE-2.0
//
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.

#endregion

namespace NCode.Identity.OpenId;

public static partial class OpenIdConstants
{
    /// <summary>
    /// Contains constants for well-known <c>authentication context class reference (acr)</c> values the authorization
    /// server can assert into the <c>acr</c> claim. The <c>acr</c> value is an opaque string whose meaning is agreed
    /// between the OP and the relying party; OpenID Connect deliberately defers the full set to the deployment, so
    /// these are the commonly-interoperable defaults and a deployment may assert any other value.
    /// </summary>
    public static class AuthenticationContextClassReferences
    {
        /// <summary>
        /// Indicates that the end-user authentication did not meet the requirements of ISO/IEC 29115 level 1, i.e. the
        /// OP has no confidence that the same person is actually present (OpenID Connect Core 1.0 §2). Typically used
        /// when a long-lived browser session is reused without a fresh authentication.
        /// </summary>
        public const string NoAssurance = "0";

        /// <summary>
        /// Indicates that multiple authentication factors were used (the PAPE multi-factor policy URN). This is the
        /// de-facto interoperable <c>acr</c> value RPs request to require multi-factor authentication.
        /// </summary>
        public const string MultiFactor =
            "http://schemas.openid.net/pape/policies/2007/06/multi-factor";

        /// <summary>
        /// Indicates that multiple authentication factors were used where at least one factor is a physical factor (the
        /// PAPE multi-factor-physical policy URN).
        /// </summary>
        public const string MultiFactorPhysical =
            "http://schemas.openid.net/pape/policies/2007/06/multi-factor-physical";

        /// <summary>
        /// Indicates that a phishing-resistant authentication mechanism was used (the PAPE phishing-resistant policy
        /// URN).
        /// </summary>
        public const string PhishingResistant =
            "http://schemas.openid.net/pape/policies/2007/06/phishing-resistant";
    }
}
