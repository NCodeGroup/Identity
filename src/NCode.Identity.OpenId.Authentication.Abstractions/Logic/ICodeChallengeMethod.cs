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

namespace NCode.Identity.OpenId.Authentication.Logic;

/// <summary>
/// Represents a single PKCE <c>code_challenge_method</c> transformation (RFC 7636): given a client's
/// <c>code_verifier</c>, it computes the expected <c>code_challenge</c> for comparison against the value supplied in
/// the original authorization request. Register one implementation per supported method to extend the set of
/// transformations the server accepts.
/// </summary>
[PublicAPI]
public interface ICodeChallengeMethod
{
    /// <summary>
    /// Gets the case-sensitive <c>code_challenge_method</c> value this transformation handles (for example,
    /// <see cref="OpenIdConstants.CodeChallengeMethods.S256"/>).
    /// </summary>
    string Method { get; }

    /// <summary>
    /// Computes the expected <c>code_challenge</c> for the specified <c>code_verifier</c> using this transformation.
    /// </summary>
    /// <param name="codeVerifier">The client-supplied <c>code_verifier</c> presented at the token endpoint.</param>
    /// <returns>The expected <c>code_challenge</c> value to compare against the one from the authorization request.</returns>
    string ComputeChallenge(string codeVerifier);
}
