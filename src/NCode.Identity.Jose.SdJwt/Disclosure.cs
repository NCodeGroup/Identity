// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using JetBrains.Annotations;
using NCode.Buffers;
using NCode.Encoders;
using NCode.Identity.Jose.SdJwt.Digests;
using NCode.Identity.Jose.SdJwt.Verification;

namespace NCode.Identity.Jose.SdJwt;

/// <summary>
/// A disclosure of a Selective Disclosure JWT (RFC 9901 §4.2.1): the base64url encoding of a JSON array of a salt, a
/// claim name and a value for an object property, or of a salt and a value for an array element. The digest that the
/// issuer-signed JWT carries is computed over <see cref="Encoded"/> exactly as it was issued, so a disclosure is never
/// re-encoded.
/// </summary>
[PublicAPI]
public sealed class Disclosure
{
    private const int ObjectPropertyLength = 3;
    private const int ArrayElementLength = 2;

    private static JsonWriterOptions WriterOptions { get; } = new() { Indented = false };

    private Disclosure(string encoded, string salt, string? claimName, JsonElement value)
    {
        Encoded = encoded;
        Salt = salt;
        ClaimName = claimName;
        Value = value;
    }

    /// <summary>
    /// Gets the base64url-encoded disclosure as issued: the input of its digest.
    /// </summary>
    public string Encoded { get; }

    /// <summary>
    /// Gets the salt of the disclosure.
    /// </summary>
    public string Salt { get; }

    /// <summary>
    /// Gets the name of the disclosed claim, or <c>null</c> for an array element.
    /// </summary>
    public string? ClaimName { get; }

    /// <summary>
    /// Gets the disclosed value, which may itself carry digests of further disclosures.
    /// </summary>
    public JsonElement Value { get; }

    /// <summary>
    /// Gets a value indicating whether the disclosure discloses an array element rather than an object property.
    /// </summary>
    [MemberNotNullWhen(false, nameof(ClaimName))]
    public bool IsArrayElement => ClaimName is null;

    /// <summary>
    /// Creates the disclosure of an object property.
    /// </summary>
    /// <param name="salt">The salt, a base64url string of at least 128 random bits.</param>
    /// <param name="claimName">The name of the disclosed claim, which is never <c>_sd</c>, <c>...</c> or
    /// <c>_sd_alg</c>.</param>
    /// <param name="value">The disclosed value.</param>
    /// <returns>The disclosure.</returns>
    /// <exception cref="ArgumentException">The salt or the claim name is empty, or the claim name is reserved.</exception>
    public static Disclosure Create(string salt, string claimName, JsonElement value)
    {
        ArgumentException.ThrowIfNullOrEmpty(salt);
        ArgumentException.ThrowIfNullOrEmpty(claimName);

        if (SdJwtClaimRules.IsReserved(claimName))
            throw new ArgumentException(
                $"The claim name '{claimName}' is reserved and cannot be disclosed.",
                nameof(claimName)
            );

        return new Disclosure(Encode(salt, claimName, value), salt, claimName, value);
    }

    /// <summary>
    /// Creates the disclosure of an array element.
    /// </summary>
    /// <param name="salt">The salt, a base64url string of at least 128 random bits.</param>
    /// <param name="value">The disclosed element.</param>
    /// <returns>The disclosure.</returns>
    /// <exception cref="ArgumentException">The salt is empty.</exception>
    public static Disclosure Create(string salt, JsonElement value)
    {
        ArgumentException.ThrowIfNullOrEmpty(salt);

        return new Disclosure(Encode(salt, claimName: null, value), salt, claimName: null, value);
    }

    /// <summary>
    /// Parses a base64url-encoded disclosure as received, refusing one that does not decode to a JSON array of a
    /// non-empty string salt, an optional non-empty string claim name that is not reserved, and a value.
    /// </summary>
    /// <param name="encoded">The base64url-encoded disclosure.</param>
    /// <returns>The parsed disclosure.</returns>
    /// <exception cref="SdJwtException">The disclosure is not of the expected shape; its
    /// <see cref="SdJwtException.FailureCode"/> is <see cref="SdJwtVerificationFailureCodes.InvalidDisclosure"/>.</exception>
    public static Disclosure Parse(string encoded)
    {
        ArgumentNullException.ThrowIfNull(encoded);

        return TryParse(encoded, out var disclosure, out var error)
            ? disclosure
            : throw new SdJwtException(SdJwtVerificationFailureCodes.InvalidDisclosure, error);
    }

    /// <summary>
    /// Attempts to parse a base64url-encoded disclosure as received, refusing one that does not decode to a JSON
    /// array of a non-empty string salt, an optional non-empty string claim name that is not reserved, and a value.
    /// </summary>
    /// <param name="encoded">The base64url-encoded disclosure.</param>
    /// <param name="disclosure">When this method returns <c>true</c>, the parsed disclosure.</param>
    /// <param name="error">When this method returns <c>false</c>, why the disclosure is refused.</param>
    /// <returns><c>true</c> when the disclosure is of the expected shape; otherwise <c>false</c>.</returns>
    public static bool TryParse(
        string encoded,
        [NotNullWhen(true)] out Disclosure? disclosure,
        [NotNullWhen(false)] out string? error
    )
    {
        ArgumentNullException.ThrowIfNull(encoded);

        disclosure = null;

        if (encoded.Length == 0)
        {
            error = "A disclosure must not be empty.";
            return false;
        }

        JsonElement array;
        try
        {
            using var lease = DecodeBase64Url(encoded, out var utf8Json);
            array = JsonElement.Parse(utf8Json);
        }
        catch (Exception exception) when (exception is FormatException or JsonException)
        {
            error = "A disclosure must be the base64url encoding of a JSON array.";
            return false;
        }

        if (array.ValueKind != JsonValueKind.Array)
        {
            error = "A disclosure must be the base64url encoding of a JSON array.";
            return false;
        }

        var length = array.GetArrayLength();
        if (length is not (ObjectPropertyLength or ArrayElementLength))
        {
            error =
                "A disclosure must be a JSON array of a salt, a claim name and a value, or of a salt and a value.";
            return false;
        }

        var saltElement = array[0];
        if (saltElement.ValueKind != JsonValueKind.String)
        {
            error = "The salt of a disclosure must be a string.";
            return false;
        }

        var salt = saltElement.GetString() ?? string.Empty;
        if (salt.Length == 0)
        {
            error = "The salt of a disclosure must not be empty.";
            return false;
        }

        if (length == ArrayElementLength)
        {
            disclosure = new Disclosure(encoded, salt, claimName: null, array[1]);
            error = null;
            return true;
        }

        var claimNameElement = array[1];
        if (claimNameElement.ValueKind != JsonValueKind.String)
        {
            error = "The claim name of a disclosure must be a string.";
            return false;
        }

        var claimName = claimNameElement.GetString() ?? string.Empty;
        if (claimName.Length == 0)
        {
            error = "The claim name of a disclosure must not be empty.";
            return false;
        }

        if (SdJwtClaimRules.IsReserved(claimName))
        {
            error = $"The claim name '{claimName}' is reserved and cannot be disclosed.";
            return false;
        }

        disclosure = new Disclosure(encoded, salt, claimName, array[2]);
        error = null;
        return true;
    }

    /// <summary>
    /// Computes the digest of this disclosure with the specified algorithm: the base64url-encoded hash of the ASCII
    /// octets of <see cref="Encoded"/> (RFC 9901 §4.2.3).
    /// </summary>
    /// <param name="algorithm">The hash algorithm the SD-JWT names in its <c>_sd_alg</c> claim.</param>
    /// <returns>The base64url-encoded digest.</returns>
    public string ComputeDigest(SdJwtDigestAlgorithm algorithm)
    {
        ArgumentNullException.ThrowIfNull(algorithm);

        return algorithm.ComputeDigest(Encoded);
    }

    /// <summary>
    /// Returns the base64url-encoded disclosure.
    /// </summary>
    public override string ToString() => Encoded;

    private static string Encode(string salt, string? claimName, JsonElement value)
    {
        using var buffer = BufferFactory.CreatePooledBufferWriter(isSensitive: false);
        using (var writer = new Utf8JsonWriter(buffer, WriterOptions))
        {
            writer.WriteStartArray();
            writer.WriteStringValue(salt);
            if (claimName is not null)
                writer.WriteStringValue(claimName);
            value.WriteTo(writer);
            writer.WriteEndArray();
        }

        return Base64Url.Encode(buffer.AsReadOnlySequence);
    }

    /// <summary>
    /// Decodes the base64url text of a disclosure into pooled memory.
    /// </summary>
    /// <param name="chars">The base64url text to decode.</param>
    /// <param name="bytes">The decoded bytes, which are valid until the returned lease is disposed.</param>
    /// <returns>The lease that returns the memory to the pool, which the caller disposes.</returns>
    /// <exception cref="FormatException"><paramref name="chars"/> is not valid base64url text.</exception>
    private static IDisposable DecodeBase64Url(
        ReadOnlySpan<char> chars,
        out ReadOnlySpan<byte> bytes
    )
    {
        var byteCount = Base64Url.GetByteCountForDecode(chars.Length);
        var lease = BufferFactory.Rent(byteCount, isSensitive: false, out Span<byte> buffer);
        try
        {
            // The disclosure is wire input, so one that does not decode whole is refused rather than read from a
            // buffer the decoder filled only in part.
            if (
                !Base64Url.TryDecode(chars, buffer, out var bytesWritten)
                || bytesWritten != byteCount
            )
                throw new FormatException("The disclosure is not valid base64url.");
        }
        catch
        {
            lease.Dispose();
            throw;
        }

        bytes = buffer;
        return lease;
    }
}
