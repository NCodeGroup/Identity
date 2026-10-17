using System.Text.Json;
using JetBrains.Annotations;
using NCode.Identity.Jose;
using NCode.Identity.Secrets;
using NCode.Identity.Secrets.Keys;

namespace NCode.Identity.Jwt;

/// <summary>
/// Represents a JSON Web Token (JWT) that has been successfully decoded.
/// </summary>
/// <param name="token">The parsed JSON Web Token (JWT).</param>
/// <param name="payload">The deserialized payload from the JSON Web Token (JWT).</param>
/// <param name="secretKey">The <see cref="SecretKey"/> that was used to successfully decode the JSON Web Token (JWT).</param>
[PublicAPI]
public sealed class DecodedJwt(JoseToken token, JsonElement payload, SecretKey secretKey)
{
    /// <summary>
    /// Gets the parsed JSON Web Token (JWT).
    /// </summary>
    private JoseToken Token { get; } = token;

    /// <summary>
    /// Gets the original JSON Web Token (JWT) value that was successfully decoded.
    /// </summary>
    public string EncodedToken => Token.ToString();

    /// <summary>
    /// Gets the deserialized header from the JSON Web Token (JWT).
    /// </summary>
    public JsonElement Header => Token.Header;

    /// <summary>
    /// Gets the deserialized payload from the JSON Web Token (JWT).
    /// </summary>
    public JsonElement Payload { get; } = payload;

    /// <summary>
    /// Gets the <see cref="SecretKey"/> that was used to successfully decode the JSON Web Token (JWT).
    /// </summary>
    public SecretKey SecretKey { get; } = secretKey;
}
