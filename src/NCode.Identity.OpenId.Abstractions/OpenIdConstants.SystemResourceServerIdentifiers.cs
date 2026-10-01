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
    /// Contains the reserved audience identifiers for the system-owned resource servers seeded into each tenant.
    /// </summary>
    public static class SystemResourceServerIdentifiers
    {
        /// <summary>
        /// Contains the reserved audience identifier for the OpenID Connect identity resource server:
        /// 'urn:ncode:openid'.
        /// </summary>
        public const string OpenId = "urn:ncode:openid";

        /// <summary>
        /// Contains the reserved audience identifier for the tenant-plane management resource server
        /// (administering a tenant's clients, resource servers, scopes, and grants): 'urn:ncode:management'.
        /// </summary>
        public const string Management = "urn:ncode:management";
    }
}
