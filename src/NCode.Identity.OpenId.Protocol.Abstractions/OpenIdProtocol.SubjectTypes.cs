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

public static partial class OpenIdProtocol
{
    /// <summary>
    /// Contains constants for the <c>subject_type</c> values supported by the authorization server.
    /// </summary>
    public static class SubjectTypes
    {
        /// <summary>
        /// Indicates that the same subject (<c>sub</c>) value is returned to all clients (<c>public</c>).
        /// </summary>
        public const string Public = "public";

        // The pairwise sub is derived deterministically as base64url(SHA-256(sectorIdentifier + principalId +
        // tenantId + salt)), per the OpenID Connect Core 1.0 §8.1 non-normative example.

        /// <summary>
        /// Indicates that a different subject (<c>sub</c>) value is returned to each client or sector (<c>pairwise</c>).
        /// </summary>
        public const string Pairwise = "pairwise";
    }
}
