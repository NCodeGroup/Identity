// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.Jose;
using NCode.Identity.Jose.Credentials;
using NCode.Identity.Secrets.Keys;

namespace NCode.Identity.OpenId.Client.Tests.Tokens;

/// <summary>
/// Provides the optional inputs of an ID token that a test creates.
/// </summary>
internal sealed class IdTokenTestOptions
{
    /// <summary>
    /// Gets the signature algorithm of the ID token.
    /// </summary>
    public string Alg { get; init; } = "PS256";

    /// <summary>
    /// Gets the issuer claim of the ID token.
    /// </summary>
    public string Issuer { get; init; } = DefaultIdTokenValidatorTests.Issuer;

    /// <summary>
    /// Gets the audience claim of the ID token.
    /// </summary>
    public string Audience { get; init; } = DefaultIdTokenValidatorTests.ClientId;

    /// <summary>
    /// Gets how long after issuance the ID token expires, or <see langword="null"/> for the default lifetime of the
    /// tests.
    /// </summary>
    public TimeSpan? ExpiresIn { get; init; }

    /// <summary>
    /// Gets the offset from the current time at which the ID token is issued, or <see langword="null"/> for none.
    /// </summary>
    public TimeSpan? IssuedOffset { get; init; }

    /// <summary>
    /// Gets the type header of the ID token.
    /// </summary>
    public string TokenType { get; init; } = JoseTokenTypes.Jwt;

    /// <summary>
    /// Gets a value indicating whether the ID token carries a subject claim.
    /// </summary>
    public bool IncludeSub { get; init; } = true;

    /// <summary>
    /// Gets the claims to add to the payload of the ID token, or <see langword="null"/> for none.
    /// </summary>
    public IReadOnlyDictionary<string, object>? ExtraClaims { get; init; }

    /// <summary>
    /// Gets the key that signs the ID token, or <see langword="null"/> for the signing key of the tests.
    /// </summary>
    public SecretKey? SigningKey { get; init; }

    /// <summary>
    /// Gets the claims to add to the signature header of the ID token, or <see langword="null"/> for none.
    /// </summary>
    public IReadOnlyDictionary<string, object>? ExtraHeaderClaims { get; init; }

    /// <summary>
    /// Gets the credentials that encrypt the ID token, or <see langword="null"/> for an ID token that is only signed.
    /// </summary>
    public JoseEncryptionCredentials? EncryptionCredentials { get; init; }

    /// <summary>
    /// Gets the claims to add to the encryption header of the ID token, or <see langword="null"/> for none.
    /// </summary>
    public IReadOnlyDictionary<string, object>? ExtraEncryptionHeaderClaims { get; init; }
}
