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
using NCode.Identity.Jose.Credentials;

namespace NCode.Identity.Jose.DPoP;

/// <summary>
/// Contains the inputs for creating a DPoP proof (RFC 9449 §4.2).
/// </summary>
[PublicAPI]
public sealed class DPoPProofCreationOptions
{
    /// <summary>
    /// Gets the asymmetric key and signature algorithm that sign the proof. The key's public half is embedded in the
    /// proof's <c>jwk</c> header and is the key an access token is bound to.
    /// </summary>
    public required JoseSigningCredentials SigningCredentials { get; init; }

    /// <summary>
    /// Gets the HTTP method of the request the proof accompanies, emitted as the <c>htm</c> claim.
    /// </summary>
    public required string HttpMethod { get; init; }

    /// <summary>
    /// Gets the HTTP URI of the request the proof accompanies; any query and fragment are removed before it is
    /// emitted as the <c>htu</c> claim.
    /// </summary>
    public required string HttpUri { get; init; }

    /// <summary>
    /// Gets the access token the proof accompanies at a protected resource, if any; its base64url SHA-256 hash is
    /// emitted as the <c>ath</c> claim.
    /// </summary>
    public string? AccessToken { get; init; }

    /// <summary>
    /// Gets the most recent server-provided <c>DPoP-Nonce</c> value, if any, emitted as the <c>nonce</c> claim.
    /// </summary>
    public string? Nonce { get; init; }
}
