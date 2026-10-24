#region Copyright Preamble

//
//    Copyright @ 2026 NCode Group
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

namespace NCode.Identity.Jose.DPoP;

/// <summary>
/// Contains the request and policy a DPoP proof is validated against (RFC 9449 §4.3).
/// </summary>
[PublicAPI]
public sealed class DPoPProofValidationParameters
{
    /// <summary>
    /// Gets the HTTP method of the request, which the proof's <c>htm</c> claim must equal.
    /// </summary>
    public required string HttpMethod { get; init; }

    /// <summary>
    /// Gets the HTTP URI of the request, which the proof's <c>htu</c> claim must equal, ignoring any query and
    /// fragment.
    /// </summary>
    public required string HttpUri { get; init; }

    /// <summary>
    /// Gets the JWS <c>alg</c> values the proof may be signed with. Only asymmetric signature algorithms are ever
    /// accepted, whatever this set contains.
    /// </summary>
    public required IReadOnlyCollection<string> AllowedAlgorithms { get; init; }

    /// <summary>
    /// Gets how far the proof's <c>iat</c> claim may lie from the current time, in either direction.
    /// </summary>
    public required TimeSpan Lifetime { get; init; }

    /// <summary>
    /// Gets the access token the proof accompanies at a protected resource, if any; when set, the proof's <c>ath</c>
    /// claim must equal its base64url SHA-256 hash.
    /// </summary>
    public string? AccessToken { get; init; }
}
