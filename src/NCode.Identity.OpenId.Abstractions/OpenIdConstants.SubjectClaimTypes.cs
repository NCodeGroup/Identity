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
