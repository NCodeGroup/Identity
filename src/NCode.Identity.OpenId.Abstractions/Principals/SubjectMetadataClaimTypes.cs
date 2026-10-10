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

using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Principals;

/// <summary>
/// Contains the claim types under which a principal's <see cref="PrincipalMetadata"/> bags are projected — each as a
/// single JSON object claim — into issued tokens and the UserInfo response. Metadata is a principal-level concept
/// resolved at issuance and projected uniformly for every connection kind (ADR-0055), so these claim types live with
/// the principal metadata rather than any single connection's account model.
/// </summary>
[PublicAPI]
public static class SubjectMetadataClaimTypes
{
    /// <summary>
    /// The claim type under which a principal's owner-updatable <c>ProfileMetadata</c> bag is projected.
    /// </summary>
    public const string ProfileMetadata = "profile_metadata";

    /// <summary>
    /// The claim type under which a principal's server-controlled <c>SystemMetadata</c> bag is projected. This bag may
    /// carry authorization-relevant data and is never end-user-writable.
    /// </summary>
    public const string SystemMetadata = "system_metadata";
}
