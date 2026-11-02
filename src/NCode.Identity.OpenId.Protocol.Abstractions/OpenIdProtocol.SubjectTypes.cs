// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

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
