// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.Jose;
using NCode.Identity.Jose.Credentials;
using NCode.Identity.Secrets.Keys;

namespace NCode.Identity.OpenId.Client.Tests.Tokens;

/// <summary>
/// Provides the optional inputs of a logout token that a test creates.
/// </summary>
internal sealed class LogoutTokenTestOptions
{
    /// <summary>
    /// Gets the issuer claim of the logout token.
    /// </summary>
    public string Issuer { get; init; } = DefaultLogoutTokenValidatorTests.Issuer;

    /// <summary>
    /// Gets the audience claim of the logout token.
    /// </summary>
    public string Audience { get; init; } = DefaultLogoutTokenValidatorTests.ClientId;

    /// <summary>
    /// Gets the offset from the current time at which the logout token is issued, or <see langword="null"/> for none.
    /// </summary>
    public TimeSpan? IssuedOffset { get; init; }

    /// <summary>
    /// Gets how long after issuance the logout token expires, or <see langword="null"/> for the default lifetime of
    /// the tests.
    /// </summary>
    public TimeSpan? Lifetime { get; init; }

    /// <summary>
    /// Gets the type header of the logout token.
    /// </summary>
    public string TokenType { get; init; } = JoseTokenTypes.LogoutJwt;

    /// <summary>
    /// Gets a value indicating whether the logout token carries a subject claim.
    /// </summary>
    public bool IncludeSub { get; init; } = true;

    /// <summary>
    /// Gets a value indicating whether the logout token carries a session identifier claim.
    /// </summary>
    public bool IncludeSid { get; init; } = true;

    /// <summary>
    /// Gets a value indicating whether the logout token carries a token identifier claim.
    /// </summary>
    public bool IncludeJti { get; init; } = true;

    /// <summary>
    /// Gets a value indicating whether the logout token carries the events claim with the back-channel logout event.
    /// </summary>
    public bool IncludeEvents { get; init; } = true;

    /// <summary>
    /// Gets the claims to add to the payload of the logout token, which replace a claim of the same name, or
    /// <see langword="null"/> for none.
    /// </summary>
    public IReadOnlyDictionary<string, object>? ExtraClaims { get; init; }

    /// <summary>
    /// Gets the token identifier claim of the logout token.
    /// </summary>
    public string Jti { get; init; } = "jti-1";

    /// <summary>
    /// Gets the key that signs the logout token, or <see langword="null"/> for the signing key of the tests.
    /// </summary>
    public SecretKey? SigningKey { get; init; }

    /// <summary>
    /// Gets the credentials that encrypt the logout token, or <see langword="null"/> for a logout token that is only
    /// signed.
    /// </summary>
    public JoseEncryptionCredentials? EncryptionCredentials { get; init; }

    /// <summary>
    /// Gets the claims to add to the encryption header of the logout token, or <see langword="null"/> for none.
    /// </summary>
    public IReadOnlyDictionary<string, object>? ExtraEncryptionHeaderClaims { get; init; }
}
