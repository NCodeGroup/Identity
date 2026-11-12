// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Jose.SdJwt.Profiles;

/// <summary>
/// The known <see cref="SdJwtProfile"/> instances. An application defines another profile by constructing one.
/// </summary>
[PublicAPI]
public static class SdJwtProfiles
{
    /// <summary>
    /// Gets the SD-JWT-based Verifiable Credential profile (draft-ietf-oauth-sd-jwt-vc): the issuer-signed JWT is
    /// typed <c>dc+sd-jwt</c>, carries the credential type <c>vct</c> as a non-empty string, and keeps <c>iss</c>,
    /// <c>nbf</c>, <c>exp</c>, <c>cnf</c>, <c>vct</c>, <c>vct#integrity</c> and <c>status</c> in its payload, never in
    /// a disclosure. The <c>status</c> claim, such as a Token Status List reference, passes through to the verified
    /// claims as the issuer wrote it.
    /// </summary>
    // See ADR-1405.
    public static SdJwtProfile SdJwtVc { get; } =
        new(
            "SD-JWT VC",
            JoseTokenTypes.SdJwtVc,
            [JoseClaimNames.Payload.Vct],
            [
                JoseClaimNames.Payload.Iss,
                JoseClaimNames.Payload.Nbf,
                JoseClaimNames.Payload.Exp,
                JoseClaimNames.Payload.Cnf,
                JoseClaimNames.Payload.Vct,
                JoseClaimNames.Payload.VctIntegrity,
                JoseClaimNames.Payload.Status,
            ]
        );
}
