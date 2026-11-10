// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.Jose;
using NCode.Identity.Secrets.Keys;

namespace NCode.Identity.OpenId.Client.Tests.UserInfo;

/// <summary>
/// Provides the optional inputs of a UserInfo response JWT that a test creates.
/// </summary>
internal sealed class UserInfoJwtTestOptions
{
    /// <summary>
    /// Gets the issuer claim of the UserInfo response.
    /// </summary>
    public string Issuer { get; init; } = DefaultUserInfoClientTests.Authority;

    /// <summary>
    /// Gets the audience claim of the UserInfo response.
    /// </summary>
    public string Audience { get; init; } = DefaultUserInfoClientTests.ClientId;

    /// <summary>
    /// Gets the subject claim of the UserInfo response.
    /// </summary>
    public string Subject { get; init; } = "user-1";

    /// <summary>
    /// Gets the signature algorithm of the UserInfo response.
    /// </summary>
    public string Alg { get; init; } = "PS256";

    /// <summary>
    /// Gets the type header of the UserInfo response.
    /// </summary>
    public string TokenType { get; init; } = JoseTokenTypes.Jwt;

    /// <summary>
    /// Gets the key that signs the UserInfo response, or <see langword="null"/> for the signing key of the provider
    /// in the tests.
    /// </summary>
    public SecretKey? SigningKey { get; init; }

    /// <summary>
    /// Gets the expiration of the UserInfo response, or <see langword="null"/> for none.
    /// </summary>
    public DateTimeOffset? Expires { get; init; }

    /// <summary>
    /// Gets a value indicating whether the UserInfo response is encrypted to the client.
    /// </summary>
    public bool Encrypted { get; init; }
}
