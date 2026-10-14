#region Copyright Preamble

// Copyright @ 2024 NCode Group
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

using NCode.Identity.Settings;

namespace NCode.Identity.OpenId.Authentication.Tokens.ProofOfPossession;

/// <summary>
/// Describes a DPoP proof to validate. The same validation covers both the token endpoint (no <see cref="AccessToken"/>)
/// and a protected resource (where <see cref="AccessToken"/> binds the proof to the presented token via the <c>ath</c>
/// claim, RFC 9449 §4.3).
/// </summary>
internal readonly record struct DPoPProofValidationRequest
{
    /// <summary>
    /// Gets the compact DPoP proof JWT from the <c>DPoP</c> request header.
    /// </summary>
    public required string ProofJwt { get; init; }

    /// <summary>
    /// Gets the HTTP method of the request the proof is bound to (the <c>htm</c> claim).
    /// </summary>
    public required string HttpMethod { get; init; }

    /// <summary>
    /// Gets the HTTP target URI of the request the proof is bound to (the <c>htu</c> claim), without query or fragment.
    /// </summary>
    public required string HttpUri { get; init; }

    /// <summary>
    /// Gets the effective settings that govern the accepted algorithms, proof lifetime, and nonce requirement.
    /// </summary>
    public required IReadOnlySettingCollection Settings { get; init; }

    /// <summary>
    /// Gets the access token the proof must be bound to via its <c>ath</c> claim when presented at a protected
    /// resource, or <see langword="null"/> at the token endpoint (where no <c>ath</c> is expected).
    /// </summary>
    public string? AccessToken { get; init; }
}
