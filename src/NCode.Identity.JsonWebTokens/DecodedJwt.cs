using System.Text.Json;
using JetBrains.Annotations;
using NCode.Identity.Jose;
using NCode.Identity.Secrets;
using NCode.Identity.Secrets.Keys;

namespace NCode.Identity.JsonWebTokens;

/// <summary>
/// Represents a JSON Web Token (JWT) that has been successfully decoded.
/// </summary>
/// <param name="compactJwt">The JSON Web Token (JWT) that was parsed in compact form.</param>
/// <param name="payload">The deserialized payload from the JSON Web Token (JWT).</param>
/// <param name="secretKey">The <see cref="SecretKey"/> that was used to successfully decode the JSON Web Token (JWT).</param>
[PublicAPI]
public class DecodedJwt(CompactJwt compactJwt, JsonElement payload, SecretKey secretKey)
{
    /// <summary>
    /// Gets the JSON Web Token (JWT) that was parsed in compact form.
    /// </summary>
    private CompactJwt CompactJwt { get; } = compactJwt;

    /// <summary>
    /// Gets the original JSON Web Token (JWT) value that was successfully decoded.
    /// </summary>
    /// <remarks>
    /// This will return the original string and not allocate a new string.
    /// </remarks>
    public string EncodedToken => CompactJwt.Segments.Original.ToString();

    /// <summary>
    /// Gets the deserialized header from the JSON Web Token (JWT).
    /// </summary>
    public JsonElement Header => CompactJwt.DeserializedHeader;

    /// <summary>
    /// Gets the deserialized payload from the JSON Web Token (JWT).
    /// </summary>
    public JsonElement Payload { get; } = payload;

    /// <summary>
    /// Gets the <see cref="SecretKey"/> that was used to successfully decode the JSON Web Token (JWT).
    /// </summary>
    public SecretKey SecretKey { get; } = secretKey;
}
