// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.Jose;

namespace NCode.Identity.OpenId;

public static partial class OpenIdConstants
{
    /// <summary>
    /// Contains the default, ordered list of claim types used to extract a subject identifier from a
    /// <see cref="System.Security.Claims.ClaimsPrincipal"/> when a tenant does not override the
    /// <c>subject_claim_types</c> setting. The short <c>sub</c> claim is preferred, then the short
    /// <see cref="JoseClaimNames.Payload.NameId"/> claim (present when a handler sets <c>MapInboundClaims = false</c>),
    /// then the long-URI <see cref="System.Security.Claims.ClaimTypes.NameIdentifier"/> claim (emitted by
    /// <c>Microsoft.AspNetCore.Identity</c> and by the default inbound JWT claim-type map).
    /// </summary>
    public static IReadOnlyCollection<string> DefaultSubjectClaimTypes { get; } =
    [
        JoseClaimNames.Payload.Sub,
        JoseClaimNames.Payload.NameId,
        System.Security.Claims.ClaimTypes.NameIdentifier,
    ];
}
