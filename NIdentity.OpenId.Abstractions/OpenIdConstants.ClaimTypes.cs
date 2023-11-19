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
    /// Contains constants that specify how Claim Values are provided by the OpenID Provider.
    /// https://openid.net/specs/openid-connect-core-1_0.html#ClaimTypes
    /// </summary>
    public static class ClaimTypes
    {
        /// <summary>
        /// Indicates that Claims are directly asserted by the OpenID Provider.
        /// </summary>
        public const string Normal = "normal";

        /// <summary>
        /// Indicates that Claims are asserted by a Claims Provider other than the OpenID Provider but are returned by the OpenID Provider.
        /// </summary>
        public const string Aggregated = "aggregated";

        /// <summary>
        /// Indicates that Claims are asserted by a Claims Provider other than the OpenID Provider but are returned as references by the OpenID Provider.
        /// </summary>
        public const string Distributed = "distributed";
    }
}
