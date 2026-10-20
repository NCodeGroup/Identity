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

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;

namespace NCode.Identity.Jose.DPoP;

/// <summary>
/// Creates and validates DPoP proofs (RFC 9449): compact <c>dpop+jwt</c> JWS tokens signed by an asymmetric key whose
/// public half travels in the proof's own <c>jwk</c> header. Validation covers everything a single proof can prove on
/// its own — structure, signature, HTTP binding, freshness, and access-token hash — and returns the proof's JWK
/// thumbprint (<c>jkt</c>), <c>jti</c>, and <c>nonce</c>; the stateful replay and server-nonce checks belong to the
/// caller, which owns the state they need.
/// </summary>
[PublicAPI]
public interface IDPoPProofService
{
    /// <summary>
    /// Creates a DPoP proof for one HTTP request.
    /// </summary>
    /// <param name="options">The signing credentials and the request the proof is bound to.</param>
    /// <returns>The compact <c>dpop+jwt</c> proof.</returns>
    /// <exception cref="ArgumentException">The signing key is not an asymmetric key, or its public key cannot be
    /// exported as a JSON Web Key.</exception>
    string CreateProof(DPoPProofCreationOptions options);

    /// <summary>
    /// Validates a DPoP proof against the HTTP request it accompanies.
    /// </summary>
    /// <param name="proofJwt">The compact proof from the <c>DPoP</c> request header.</param>
    /// <param name="parameters">The request and policy the proof is validated against.</param>
    /// <param name="proof">When this method returns <see langword="true"/>, the validated proof.</param>
    /// <param name="error">When this method returns <see langword="false"/>, a description of why the proof is
    /// invalid, suitable for an <c>error_description</c>.</param>
    /// <returns><see langword="true"/> when the proof is valid; otherwise <see langword="false"/>.</returns>
    bool TryValidateProof(
        string proofJwt,
        DPoPProofValidationParameters parameters,
        [NotNullWhen(true)] out DPoPProof? proof,
        [NotNullWhen(false)] out string? error
    );
}
