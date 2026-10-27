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

namespace NCode.Identity.OpenId.Client;

/// <summary>
/// Describes an ID token to validate and the values to check it against.
/// </summary>
[PublicAPI]
public sealed class IdTokenValidationRequest
{
    /// <summary>
    /// Gets the compact-serialized ID token.
    /// </summary>
    public required string IdToken { get; init; }

    /// <summary>
    /// Gets the nonce sent in the authentication request. When set, the <c>nonce</c> claim must be a string equal to it
    /// ordinally.
    /// </summary>
    public string? Nonce { get; init; }

    /// <summary>
    /// Gets the maximum authentication age. When set, the <c>auth_time</c> claim must be a number and the current time
    /// must not be later than <c>auth_time</c> plus this value plus five minutes of clock skew.
    /// </summary>
    public TimeSpan? MaxAge { get; init; }

    /// <summary>
    /// Gets the access token issued with the ID token. When set and the <c>at_hash</c> claim is present, the claim must
    /// equal the left-most half of the access token's hash, computed through the signing algorithm's token hash
    /// function. A missing claim is accepted.
    /// </summary>
    public string? AccessToken { get; init; }

    /// <summary>
    /// Gets the authorization code issued with the ID token. When set and the <c>c_hash</c> claim is present, the claim
    /// must equal the left-most half of the code's hash, computed through the signing algorithm's token hash function.
    /// A missing claim is accepted.
    /// </summary>
    public string? AuthorizationCode { get; init; }

    /// <summary>
    /// Gets the state sent in the authentication request. When set and the <c>s_hash</c> claim is present, the claim
    /// must equal the left-most half of the state's hash, computed through the signing algorithm's token hash function.
    /// A missing claim is accepted.
    /// </summary>
    public string? State { get; init; }

    /// <summary>
    /// Gets the subject that a refreshed ID token must keep. When set, the <c>sub</c> claim must equal it ordinally.
    /// </summary>
    public string? Subject { get; init; }
}
