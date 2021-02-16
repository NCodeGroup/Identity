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

namespace NIdentity.OpenId
{
    public static partial class OpenIdConstants
    {
        /// <summary>
        /// Contains constants for possible values of the <c>response_type</c> parameter.
        /// </summary>
        public static class ResponseTypes
        {
            /// <summary>
            /// Contains the <c>response_type</c> parameter value for <c>none</c>.
            /// </summary>
            public const string None = "none";

            /// <summary>
            /// Contains the <c>response_type</c> parameter value for <c>code</c>.
            /// </summary>
            public const string Code = "code";

            /// <summary>
            /// Contains the <c>response_type</c> parameter value for <c>id_token</c>.
            /// </summary>
            public const string IdToken = "id_token";

            /// <summary>
            /// Contains the <c>response_type</c> parameter value for <c>token</c>.
            /// </summary>
            public const string Token = "token";
        }
    }
}
