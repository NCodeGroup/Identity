#region Copyright Preamble
//
//    Copyright @ 2023 NCode Group
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
