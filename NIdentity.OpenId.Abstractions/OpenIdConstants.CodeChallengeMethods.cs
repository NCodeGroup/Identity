#region Copyright Preamble

//
//    Copyright @ 2021 NCode Group
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

namespace NIdentity.OpenId;

public static partial class OpenIdConstants
{
    /// <summary>
    /// Contains constants for possible values of the <c>code_challenge_method</c> parameter.
    /// </summary>
    public static class CodeChallengeMethods
    {
        /// <summary>
        /// Contains the <c>code_challenge_method</c> parameter value for <c>plain</c>.
        /// </summary>
        public const string Plain = "plain";

        /// <summary>
        /// Contains the <c>code_challenge_method</c> parameter value for <c>S256</c>.
        /// </summary>
        public const string S256 = "S256";
    }
}