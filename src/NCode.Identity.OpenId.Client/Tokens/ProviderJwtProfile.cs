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

using NCode.Identity.Jose;
using NCode.Identity.Jwt.Validation;

namespace NCode.Identity.OpenId.Client.Tokens;

/// <summary>
/// Describes a kind of JWT that the provider issues to the client: the signature and encryption algorithms the client
/// registered for it, the signature algorithms the provider advertises for it, its <c>typ</c>, its lifetime rules, and
/// the names that failure reasons use for it. <see cref="ProviderJwtValidator"/> validates a token against it.
/// </summary>
// See ADR-0301.
internal sealed record ProviderJwtProfile
{
    private static readonly TimeSpan LogoutTokenMaxLifetime = TimeSpan.FromMinutes(10);

    /// <summary>
    /// Gets the client registration the token is issued to.
    /// </summary>
    public required ClientRegistration Registration { get; init; }

    /// <summary>
    /// Gets the name of the token in failure reasons, such as <c>ID token</c>.
    /// </summary>
    public required string TokenName { get; init; }

    /// <summary>
    /// Gets the name of the token's signing registration in failure reasons, such as <c>ID-token</c>.
    /// </summary>
    public required string SigningName { get; init; }

    /// <summary>
    /// Gets the name of the token's encryption registration in failure reasons, such as <c>ID-token</c>.
    /// </summary>
    public required string EncryptionName { get; init; }

    /// <summary>
    /// Gets the <c>typ</c> header the token must carry when it carries one.
    /// </summary>
    public required string TokenType { get; init; }

    /// <summary>
    /// Gets the signature algorithms the client registered for the token; <see langword="null"/> or empty adds no
    /// client restriction.
    /// </summary>
    public IReadOnlyList<string>? SigningAlgorithms { get; init; }

    /// <summary>
    /// Gets the signature algorithms the provider advertises for the token; <see langword="null"/> or empty adds no
    /// provider restriction.
    /// </summary>
    public IReadOnlyList<string>? AdvertisedSigningAlgorithms { get; init; }

    /// <summary>
    /// Gets the key management algorithms the client registered for the token; <see langword="null"/> or empty means
    /// the client registered no encryption for it.
    /// </summary>
    public IReadOnlyList<string>? KeyManagementAlgorithms { get; init; }

    /// <summary>
    /// Gets the content encryption algorithms the client registered for the token.
    /// </summary>
    public IReadOnlyList<string>? ContentEncryptionAlgorithms { get; init; }

    /// <summary>
    /// Gets the lifetime rules of the token.
    /// </summary>
    public required Action<ValidateTokenLifeTimeOptions> ConfigureLifetime { get; init; }

    /// <summary>
    /// Gets the profile of an ID token (OpenID Connect Core 1.0 §3.1.3.7), which must carry <c>iat</c> and <c>exp</c>.
    /// </summary>
    public static ProviderJwtProfile ForIdToken(
        ClientRegistration registration,
        OpenIdProviderMetadata metadata
    ) =>
        new()
        {
            Registration = registration,
            TokenName = "ID token",
            SigningName = "ID-token",
            EncryptionName = "ID-token",
            TokenType = JoseTokenTypes.Jwt,
            SigningAlgorithms = registration.IdTokenSigningAlgorithms,
            AdvertisedSigningAlgorithms = metadata.IdTokenSigningAlgValuesSupported,
            KeyManagementAlgorithms = registration.IdTokenKeyManagementAlgorithms,
            ContentEncryptionAlgorithms = registration.IdTokenContentEncryptionAlgorithms,
            ConfigureLifetime = static options => options.RequireIssuedAt = true,
        };

    /// <summary>
    /// Gets the profile of a logout token (OpenID Connect Back-Channel Logout 1.0 §2.6), which is signed and encrypted
    /// as the client's ID tokens are and lives at most ten minutes.
    /// </summary>
    public static ProviderJwtProfile ForLogoutToken(
        ClientRegistration registration,
        OpenIdProviderMetadata metadata
    ) =>
        ForIdToken(registration, metadata) with
        {
            TokenName = "logout token",
            SigningName = "logout-token",
            TokenType = JoseTokenTypes.LogoutJwt,
            ConfigureLifetime = static options =>
            {
                options.RequireIssuedAt = true;
                options.MaxLifetime = LogoutTokenMaxLifetime;
            },
        };

    /// <summary>
    /// Gets the profile of a UserInfo response (OpenID Connect Core 1.0 §5.3.2). It is a JWT only when the client
    /// registered UserInfo signing; a signed response needs no lifetime claims but honors the ones it carries.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// The client registers UserInfo encryption without UserInfo signing, or content encryption algorithms without a
    /// key management algorithm.
    /// </exception>
    public static ProviderJwtProfile ForUserInfo(
        ClientRegistration registration,
        OpenIdProviderMetadata metadata
    )
    {
        var encrypted = registration.UserInfoKeyManagementAlgorithms is { Count: > 0 };
        if (!encrypted && registration.UserInfoContentEncryptionAlgorithms is { Count: > 0 })
            throw new InvalidOperationException(
                $"The client '{registration.ClientId}' lists UserInfo content encryption algorithms but no key management algorithm."
            );

        // A JWE that encloses no signature is never accepted, because anyone holding the client's public key can
        // produce one (ADR-0301).
        if (encrypted && registration.UserInfoSigningAlgorithms is not { Count: > 0 })
            throw new InvalidOperationException(
                $"The client '{registration.ClientId}' registers UserInfo encryption without UserInfo signing; an encrypted UserInfo response must be a Nested JWT."
            );

        return new ProviderJwtProfile
        {
            Registration = registration,
            TokenName = "UserInfo response",
            SigningName = "UserInfo",
            EncryptionName = "UserInfo",
            TokenType = JoseTokenTypes.Jwt,
            SigningAlgorithms = registration.UserInfoSigningAlgorithms,
            AdvertisedSigningAlgorithms = metadata.UserInfoSigningAlgValuesSupported,
            KeyManagementAlgorithms = registration.UserInfoKeyManagementAlgorithms,
            ContentEncryptionAlgorithms = registration.UserInfoContentEncryptionAlgorithms,
            ConfigureLifetime = static options => options.RequireExpirationTime = false,
        };
    }
}
