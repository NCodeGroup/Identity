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

namespace NCode.Identity.OpenId.IntegrationTests.Infrastructure;

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
