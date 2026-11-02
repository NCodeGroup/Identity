// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

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
    public string Method => OpenIdProtocol.CodeChallengeMethods.S256;

    /// <inheritdoc />
    public string ComputeChallenge(string codeVerifier) =>
        CryptoService.HashValue(
            codeVerifier,
            HashAlgorithmName.SHA256,
            BinaryEncodings.Base64Url,
            SecureEncoding.ASCII
        );
}
