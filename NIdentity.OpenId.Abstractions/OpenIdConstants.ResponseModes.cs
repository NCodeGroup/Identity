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

namespace NIdentity.OpenId;

public static partial class OpenIdConstants
{
    /// <summary>
    /// Contains constants for possible values of the <c>response_mode</c> parameter.
    /// </summary>
    public static class ResponseModes
    {
        /// <summary>
        /// Contains the <c>response_mode</c> parameter value for <c>query</c>.
        /// </summary>
        public const string Query = "query";

        /// <summary>
        /// Contains the <c>response_mode</c> parameter value for <c>fragment</c>.
        /// </summary>
        public const string Fragment = "fragment";

        /// <summary>
        /// Contains the <c>response_mode</c> parameter value for <c>form_post</c>.
        /// </summary>
        public const string FormPost = "form_post";
    }
}