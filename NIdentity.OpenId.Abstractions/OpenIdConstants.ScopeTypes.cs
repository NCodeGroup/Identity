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
    /// Contains constants for possible values of the <c>scope</c> parameter.
    /// </summary>
    public static class ScopeTypes
    {
        /// <summary>
        /// Contains the <c>scope</c> parameter value for <c>openid</c>.
        /// </summary>
        public const string OpenId = "openid";

        /// <summary>
        /// Contains the <c>scope</c> parameter value for <c>profile</c>.
        /// </summary>
        public const string Profile = "profile";

        /// <summary>
        /// Contains the <c>scope</c> parameter value for <c>email</c>.
        /// </summary>
        public const string Email = "email";

        /// <summary>
        /// Contains the <c>scope</c> parameter value for <c>address</c>.
        /// </summary>
        public const string Address = "address";

        /// <summary>
        /// Contains the <c>scope</c> parameter value for <c>phone</c>.
        /// </summary>
        public const string Phone = "phone";

        /// <summary>
        /// Contains the <c>scope</c> parameter value for <c>offline_access</c>.
        /// </summary>
        public const string OfflineAccess = "offline_access";
    }
}