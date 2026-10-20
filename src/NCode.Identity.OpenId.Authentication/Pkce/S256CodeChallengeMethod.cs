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

using System.Security.Cryptography;
using NCode.Buffers;
using NCode.Identity.Cryptography;

namespace NCode.Identity.OpenId.Authentication.Pkce;

/// <summary>
/// Provides the <c>S256</c> PKCE <c>code_challenge_method</c> transformation where the <c>code_challenge</c> is the
/// BASE64URL-encoded SHA-256 hash of the ASCII <c>code_verifier</c> (RFC 7636 §4.2).
/// </summary>
internal sealed class S256CodeChallengeMethod(ICryptoService cryptoService) : ICodeChallengeMethod
{
    private ICryptoService CryptoService { get; } = cryptoService;

    /// <inheritdoc />
    public string Method => OpenIdConstants.CodeChallengeMethods.S256;

    /// <inheritdoc />
    public string ComputeChallenge(string codeVerifier) =>
        CryptoService.HashValue(
            codeVerifier,
            HashAlgorithmName.SHA256,
            BinaryEncodings.Base64Url,
            SecureEncoding.ASCII
        );
}
