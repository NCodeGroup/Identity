// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers.Text;
using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using NCode.Buffers;
using Xunit;

namespace NCode.Identity.OpenId.Integration.Tests.Infrastructure;

/// <summary>
/// Checks whether a client authenticates with a credential by revoking an unknown token: revocation is a no-op once the
/// client authenticates (RFC 7009 §2.2), so the response reports only whether the client was accepted.
/// </summary>
public static class ClientAuthenticationProbe
{
    /// <summary>
    /// Reads the tenant's issuer from its discovery document, which a client assertion names as its audience.
    /// </summary>
    /// <param name="client">The <see cref="HttpClient"/> of the host.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The issuer.</returns>
    public static async ValueTask<string> GetIssuerAsync(
        HttpClient client,
        CancellationToken cancellationToken
    )
    {
        using var response = await client.GetAsync(
            "/.well-known/openid-configuration",
            cancellationToken
        );
        var jsonElement = JsonElement.Parse(
            await response.Content.ReadAsStringAsync(cancellationToken)
        );
        var issuer = jsonElement.GetProperty("issuer").GetString();
        Assert.NotNull(issuer);
        return issuer;
    }

    /// <summary>
    /// Revokes an unknown token with the specified client credentials.
    /// </summary>
    /// <param name="client">The <see cref="HttpClient"/> of the host.</param>
    /// <param name="credentials">The form fields that authenticate the client.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns><see langword="null"/> when the client was accepted; otherwise, the error the endpoint returned.</returns>
    public static async ValueTask<string?> RevokeAsync(
        HttpClient client,
        Dictionary<string, string> credentials,
        CancellationToken cancellationToken
    )
    {
        credentials["token"] = "not-a-real-token";
        using var content = new FormUrlEncodedContent(credentials);
        using var response = await client.PostAsync("/oauth2/revoke", content, cancellationToken);
        if (response.StatusCode == HttpStatusCode.OK)
        {
            return null;
        }

        var jsonElement = JsonElement.Parse(
            await response.Content.ReadAsStringAsync(cancellationToken)
        );
        return jsonElement.GetProperty("error").GetString();
    }

    /// <summary>
    /// Revokes an unknown token with a <c>client_secret_post</c> credential.
    /// </summary>
    /// <param name="client">The <see cref="HttpClient"/> of the host.</param>
    /// <param name="clientId">The identifier of the client.</param>
    /// <param name="clientSecret">The secret the client presents.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns><see langword="null"/> when the client was accepted; otherwise, the error the endpoint returned.</returns>
    public static ValueTask<string?> RevokeWithSecretAsync(
        HttpClient client,
        string clientId,
        string clientSecret,
        CancellationToken cancellationToken
    ) =>
        RevokeAsync(
            client,
            new Dictionary<string, string>
            {
                ["client_id"] = clientId,
                ["client_secret"] = clientSecret,
            },
            cancellationToken
        );

    /// <summary>
    /// Revokes an unknown token with a <c>client_secret_jwt</c> assertion keyed by the specified secret.
    /// </summary>
    /// <param name="client">The <see cref="HttpClient"/> of the host.</param>
    /// <param name="clientId">The identifier of the client.</param>
    /// <param name="clientSecret">The secret the assertion is keyed by.</param>
    /// <param name="audience">The audience of the assertion, such as the tenant's issuer.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns><see langword="null"/> when the client was accepted; otherwise, the error the endpoint returned.</returns>
    public static ValueTask<string?> RevokeWithAssertionAsync(
        HttpClient client,
        string clientId,
        string clientSecret,
        string audience,
        CancellationToken cancellationToken
    ) =>
        RevokeAsync(
            client,
            new Dictionary<string, string>
            {
                ["client_assertion_type"] = OpenIdProtocol.ClientAssertionTypes.JwtBearer,
                ["client_assertion"] = BuildClientSecretJwt(clientId, clientSecret, audience),
            },
            cancellationToken
        );

    /// <summary>
    /// Builds a <c>client_secret_jwt</c> assertion (OpenID Connect Core §9): an <c>HS256</c> JWT keyed by the UTF-8
    /// bytes of the secret.
    /// </summary>
    /// <param name="clientId">The identifier of the client, its issuer and subject.</param>
    /// <param name="clientSecret">The secret the assertion is keyed by.</param>
    /// <param name="audience">The audience of the assertion.</param>
    /// <returns>The compact JWT.</returns>
    public static string BuildClientSecretJwt(string clientId, string clientSecret, string audience)
    {
        var now = DateTimeOffset.UtcNow;
        var header = new Dictionary<string, object> { ["alg"] = "HS256", ["typ"] = "JWT" };
        var payload = new Dictionary<string, object>
        {
            ["iss"] = clientId,
            ["sub"] = clientId,
            ["aud"] = audience,
            ["jti"] = Guid.NewGuid().ToString("N"),
            ["iat"] = now.ToUnixTimeSeconds(),
            ["exp"] = now.AddMinutes(5).ToUnixTimeSeconds(),
        };

        var encodedHeader = Base64Url.EncodeToString(JsonSerializer.SerializeToUtf8Bytes(header));
        var encodedPayload = Base64Url.EncodeToString(JsonSerializer.SerializeToUtf8Bytes(payload));
        var signingInput = $"{encodedHeader}.{encodedPayload}";

        var signature = HMACSHA256.HashData(
            SecureEncoding.UTF8.GetBytes(clientSecret),
            SecureEncoding.ASCII.GetBytes(signingInput)
        );

        return $"{signingInput}.{Base64Url.EncodeToString(signature)}";
    }
}
