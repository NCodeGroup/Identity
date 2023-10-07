using System.Text.Json;
using NCode.Jose;
using NCode.Jose.SecretKeys;

namespace NCode.Identity.JsonWebTokens;

/// <summary>
/// Represents a Json Web Token (JWT) that has been successfully decoded.
/// </summary>
public class DecodedJwt
{
    /// <summary>
    /// Gets the Json Web Token (JWT) that was parsed in compact form.
    /// </summary>
    private CompactJwt CompactJwt { get; }

    /// <summary>
    /// Gets the original Json Web Token (JWT) value that was successfully decoded.
    /// </summary>
    /// <remarks>
    /// This will return the original string and not allocate a new string.
    /// </remarks>
    public string EncodedToken => CompactJwt.Segments.Original.ToString();

    /// <summary>
    /// Gets the deserialized header from the Json Web Token (JWT).
    /// </summary>
    public JsonElement Header => CompactJwt.DeserializedHeader;

    /// <summary>
    /// Gets the deserialized payload from the Json Web Token (JWT).
    /// </summary>
    public JsonElement Payload { get; }

    /// <summary>
    /// Gets the <see cref="SecretKey"/> that was used to successfully decode the Json Web Token (JWT).
    /// </summary>
    public SecretKey SecretKey { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="DecodedJwt"/> class.
    /// </summary>
    /// <param name="compactJwt">The Json Web Token (JWT) that was parsed in compact form.</param>
    /// <param name="payload">The deserialized payload from the Json Web Token (JWT).</param>
    /// <param name="secretKey">The <see cref="SecretKey"/> that was used to successfully decode the Json Web Token (JWT).</param>
    public DecodedJwt(CompactJwt compactJwt, JsonElement payload, SecretKey secretKey)
    {
        CompactJwt = compactJwt;
        Payload = payload;
        SecretKey = secretKey;
    }
}
