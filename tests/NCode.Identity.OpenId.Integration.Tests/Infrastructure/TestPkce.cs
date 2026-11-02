// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.OpenId.Integration.Tests.Infrastructure;

/// <summary>
/// A PKCE <c>S256</c> pair (RFC 7636 Appendix B) for authorization requests: the server binds every authorization code
/// to a <c>code_challenge</c> by default.
/// </summary>
public static class TestPkce
{
    /// <summary>
    /// The <c>code_verifier</c> the token request presents.
    /// </summary>
    public const string CodeVerifier = "dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk";

    /// <summary>
    /// The <c>S256</c> <c>code_challenge</c> of <see cref="CodeVerifier"/>.
    /// </summary>
    public const string CodeChallenge = "E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM";

    /// <summary>
    /// The <c>code_challenge</c> and <c>code_challenge_method</c> as query parameters, each with a leading <c>&amp;</c>.
    /// </summary>
    public const string QueryParameters =
        "&code_challenge=" + CodeChallenge + "&code_challenge_method=S256";

    /// <summary>
    /// The <c>code_challenge</c> and <c>code_challenge_method</c> as form fields.
    /// </summary>
    public static IEnumerable<KeyValuePair<string, string>> FormFields { get; } =
    [new("code_challenge", CodeChallenge), new("code_challenge_method", "S256")];
}
