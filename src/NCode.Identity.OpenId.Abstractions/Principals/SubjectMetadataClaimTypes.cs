// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Principals;

/// <summary>
/// Contains the claim types under which a principal's <see cref="PrincipalMetadata"/> bags are projected — each as a
/// single JSON object claim — into issued tokens and the UserInfo response. Metadata is a principal-level concept
/// resolved at issuance and projected uniformly for every connection kind, so these claim types live with
/// the principal metadata rather than any single connection's account model.
/// </summary>
// See ADR-1055.
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
