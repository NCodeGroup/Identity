// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Authentication.Pkce;

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
    /// <see cref="OpenIdProtocol.CodeChallengeMethods.S256"/>).
    /// </summary>
    string Method { get; }

    /// <summary>
    /// Computes the expected <c>code_challenge</c> for the specified <c>code_verifier</c> using this transformation.
    /// </summary>
    /// <param name="codeVerifier">The client-supplied <c>code_verifier</c> presented at the token endpoint.</param>
    /// <returns>The expected <c>code_challenge</c> value to compare against the one from the authorization request.</returns>
    string ComputeChallenge(string codeVerifier);
}
