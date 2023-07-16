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