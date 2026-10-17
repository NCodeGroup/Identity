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

using System.Buffers;
using System.Buffers.Text;
using System.Collections.Frozen;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using JetBrains.Annotations;
using NCode.Json;

namespace NCode.Identity.Jwk;

/// <summary>
/// A first-class, immutable <c>JSON Web Key</c> (RFC 7517): the JSON representation of a cryptographic key. The type
/// round-trips any key type (<c>RSA</c>, <c>EC</c>, <c>OKP</c>, <c>oct</c>, <c>AKP</c>, …) with full member fidelity,
/// surfaces the standard members as typed accessors, exposes the key-type-specific members by name, and computes the
/// <c>JWK Thumbprint</c> (RFC 7638) and the <c>JWK Thumbprint URI</c> (RFC 9278) used to derive a <c>kid</c> and to
/// bind a token to its key (the <c>jkt</c> confirmation for DPoP and mutual-TLS).
/// </summary>
/// <seealso href="https://datatracker.ietf.org/doc/html/rfc7517">RFC 7517 - JSON Web Key (JWK)</seealso>
/// <seealso href="https://datatracker.ietf.org/doc/html/rfc7638">RFC 7638 - JWK Thumbprint</seealso>
/// <seealso href="https://datatracker.ietf.org/doc/html/rfc9278">RFC 9278 - JWK Thumbprint URI</seealso>
[PublicAPI]
[JsonConverter(typeof(JsonWebKeyJsonConverter))]
public sealed class JsonWebKey
{
    /// <summary>
    /// The URN prefix of a <c>JWK Thumbprint URI</c> as defined by RFC 9278 §3.
    /// </summary>
    public const string ThumbprintUriPrefix = "urn:ietf:params:oauth:jwk-thumbprint:";

    // The RFC 7638 §3.2 "Required Members" for each registered key type, already in lexicographic order so the
    // canonical thumbprint input can be written directly.
    private static readonly FrozenDictionary<string, string[]> RequiredThumbprintMembers =
        new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["RSA"] =
            [
                JsonWebKeyParameterNames.Exponent,
                JsonWebKeyParameterNames.KeyType,
                JsonWebKeyParameterNames.Modulus,
            ],
            ["EC"] =
            [
                JsonWebKeyParameterNames.Curve,
                JsonWebKeyParameterNames.KeyType,
                JsonWebKeyParameterNames.X,
                JsonWebKeyParameterNames.Y,
            ],
            ["OKP"] =
            [
                JsonWebKeyParameterNames.Curve,
                JsonWebKeyParameterNames.KeyType,
                JsonWebKeyParameterNames.X,
            ],
            ["oct"] = [JsonWebKeyParameterNames.SymmetricKey, JsonWebKeyParameterNames.KeyType],
            ["AKP"] = [JsonWebKeyParameterNames.KeyType, JsonWebKeyParameterNames.PublicKey],
        }.ToFrozenDictionary(StringComparer.Ordinal);

    // The members that carry private or secret material; stripped by ToPublicKey and used by ContainsPrivateKeyMaterial.
    private static readonly FrozenSet<string> PrivateMemberNames = new[]
    {
        JsonWebKeyParameterNames.EccPrivateKey,
        JsonWebKeyParameterNames.PrivateKey,
        JsonWebKeyParameterNames.SymmetricKey,
        "p",
        "q",
        "dp",
        "dq",
        "qi",
        "oth",
    }.ToFrozenSet(StringComparer.Ordinal);

    // The member tree, a detached immutable JsonElement (net9+): no backing JsonDocument to dispose, no cloning.
    private JsonElement Element { get; }

    /// <summary>
    /// Initializes a new <see cref="JsonWebKey"/> over the supplied <see cref="JsonElement"/>, which must be a JSON
    /// object carrying a non-empty string <c>kty</c> member.
    /// </summary>
    /// <param name="element">The JWK members as a JSON object.</param>
    public JsonWebKey(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
            throw new ArgumentException("The JWK is not a JSON object.", nameof(element));

        if (
            !element.TryGetPropertyValue<string>(JsonWebKeyParameterNames.KeyType, out var kty)
            || kty.Length == 0
        )
        {
            throw new ArgumentException(
                "The JWK is missing a non-empty string 'kty' member.",
                nameof(element)
            );
        }

        Element = element;
        KeyType = kty;
    }

    /// <summary>
    /// Initializes a new <see cref="JsonWebKey"/> from the supplied members, which must carry a non-empty string
    /// <c>kty</c> member.
    /// </summary>
    /// <param name="members">The JWK members as a JSON object.</param>
    public JsonWebKey(JsonObject members)
        : this(ToElement(members)) { }

    /// <summary>
    /// Parses a <see cref="JsonWebKey"/> from its JSON text representation.
    /// </summary>
    /// <param name="json">The JWK JSON text.</param>
    /// <returns>The parsed <see cref="JsonWebKey"/>.</returns>
    public static JsonWebKey Parse(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        return new JsonWebKey(JsonElement.Parse(json));
    }

    private static JsonElement ToElement(JsonObject members)
    {
        ArgumentNullException.ThrowIfNull(members);
        return members.Deserialize<JsonElement>();
    }

    /// <summary>
    /// Gets the <c>kty</c> member, identifying the cryptographic family of the key (RFC 7517 §4.1).
    /// </summary>
    public string KeyType { get; }

    /// <summary>
    /// Gets the <c>use</c> member (<c>sig</c> / <c>enc</c>), or <c>null</c> when absent (RFC 7517 §4.2).
    /// </summary>
    public string? PublicKeyUse => GetMember(JsonWebKeyParameterNames.PublicKeyUse);

    /// <summary>
    /// Gets the <c>key_ops</c> member, or <c>null</c> when absent (RFC 7517 §4.3).
    /// </summary>
    public IReadOnlyList<string>? KeyOperations =>
        GetStringArray(JsonWebKeyParameterNames.KeyOperations);

    /// <summary>
    /// Gets the <c>alg</c> member, or <c>null</c> when absent (RFC 7517 §4.4).
    /// </summary>
    public string? Algorithm => GetMember(JsonWebKeyParameterNames.Algorithm);

    /// <summary>
    /// Gets the <c>kid</c> member, or <c>null</c> when absent (RFC 7517 §4.5).
    /// </summary>
    public string? KeyId => GetMember(JsonWebKeyParameterNames.KeyId);

    /// <summary>
    /// Gets the <c>x5u</c> member, or <c>null</c> when absent (RFC 7517 §4.6).
    /// </summary>
    public string? X509Url => GetMember(JsonWebKeyParameterNames.X509Url);

    /// <summary>
    /// Gets the <c>x5c</c> X.509 certificate chain, or <c>null</c> when absent (RFC 7517 §4.7).
    /// </summary>
    public IReadOnlyList<string>? X509CertificateChain =>
        GetStringArray(JsonWebKeyParameterNames.X509CertificateChain);

    /// <summary>
    /// Gets the <c>x5t</c> SHA-1 certificate thumbprint, or <c>null</c> when absent (RFC 7517 §4.8).
    /// </summary>
    public string? X509CertificateSha1Thumbprint =>
        GetMember(JsonWebKeyParameterNames.X509CertificateSha1Thumbprint);

    /// <summary>
    /// Gets the <c>x5t#S256</c> SHA-256 certificate thumbprint, or <c>null</c> when absent (RFC 7517 §4.9).
    /// </summary>
    public string? X509CertificateSha256Thumbprint =>
        GetMember(JsonWebKeyParameterNames.X509CertificateSha256Thumbprint);

    /// <summary>
    /// Gets a value indicating whether this key carries any private or secret key material (for example the RSA/EC
    /// <c>d</c>, the AKP <c>priv</c> seed, or the symmetric <c>k</c>).
    /// </summary>
    public bool ContainsPrivateKeyMaterial
    {
        get
        {
            foreach (var member in Element.EnumerateObject())
            {
                if (PrivateMemberNames.Contains(member.Name))
                    return true;
            }

            return false;
        }
    }

    /// <summary>
    /// Gets the string value of a member, or <c>null</c> when it is absent or not a string.
    /// </summary>
    /// <param name="name">The member name (see <see cref="JsonWebKeyParameterNames"/>).</param>
    /// <returns>The member's string value, or <c>null</c>.</returns>
    public string? GetMember(string name) =>
        Element.TryGetPropertyValue<string>(name, out var value) ? value : null;

    /// <summary>
    /// Attempts to read a string-valued member of this key.
    /// </summary>
    /// <param name="name">The member name (see <see cref="JsonWebKeyParameterNames"/>).</param>
    /// <param name="value">When this method returns <c>true</c>, the member's string value.</param>
    /// <returns><c>true</c> when the member is present and string-valued; otherwise <c>false</c>.</returns>
    public bool TryGetMember(string name, out string? value)
    {
        value = GetMember(name);
        return value is not null;
    }

    /// <summary>
    /// Returns a copy of this key with every private or secret member removed, or this same instance when the key
    /// already carries only public material.
    /// </summary>
    /// <returns>A public-only <see cref="JsonWebKey"/>.</returns>
    public JsonWebKey ToPublicKey()
    {
        if (!ContainsPrivateKeyMaterial)
            return this;

        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            foreach (var member in Element.EnumerateObject())
            {
                if (!PrivateMemberNames.Contains(member.Name))
                    member.WriteTo(writer);
            }

            writer.WriteEndObject();
        }

        return new JsonWebKey(JsonElement.Parse(buffer.WrittenSpan));
    }

    /// <summary>
    /// Computes the base64url-encoded <c>JWK Thumbprint</c> (RFC 7638) of this key using SHA-256.
    /// </summary>
    /// <returns>The base64url-encoded thumbprint.</returns>
    public string ComputeThumbprint() => ComputeThumbprint(HashAlgorithmName.SHA256);

    /// <summary>
    /// Computes the base64url-encoded <c>JWK Thumbprint</c> (RFC 7638) of this key using the given hash.
    /// </summary>
    /// <param name="hashAlgorithmName">The hash algorithm to use.</param>
    /// <returns>The base64url-encoded thumbprint.</returns>
    public string ComputeThumbprint(HashAlgorithmName hashAlgorithmName)
    {
        if (!RequiredThumbprintMembers.TryGetValue(KeyType, out var requiredMembers))
            throw new NotSupportedException(
                $"A JWK thumbprint cannot be computed for the key type '{KeyType}'."
            );

        var buffer = new ArrayBufferWriter<byte>(256);
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            foreach (var name in requiredMembers)
            {
                var value =
                    GetMember(name)
                    ?? throw new InvalidOperationException(
                        $"The JWK is missing the required '{name}' member needed for its '{KeyType}' thumbprint."
                    );
                writer.WriteString(name, value);
            }

            writer.WriteEndObject();
        }

        using var hasher = IncrementalHash.CreateHash(hashAlgorithmName);
        hasher.AppendData(buffer.WrittenSpan);
        return Base64Url.EncodeToString(hasher.GetHashAndReset());
    }

    /// <summary>
    /// Computes the <c>JWK Thumbprint URI</c> (RFC 9278) of this key using SHA-256, for example
    /// <c>urn:ietf:params:oauth:jwk-thumbprint:sha-256:…</c>.
    /// </summary>
    /// <returns>The thumbprint URI.</returns>
    public string ComputeThumbprintUri() => ComputeThumbprintUri(HashAlgorithmName.SHA256);

    /// <summary>
    /// Computes the <c>JWK Thumbprint URI</c> (RFC 9278) of this key using the given hash.
    /// </summary>
    /// <param name="hashAlgorithmName">The hash algorithm to use.</param>
    /// <returns>The thumbprint URI.</returns>
    public string ComputeThumbprintUri(HashAlgorithmName hashAlgorithmName)
    {
        var hashName = hashAlgorithmName.Name switch
        {
            "SHA256" => "sha-256",
            "SHA384" => "sha-384",
            "SHA512" => "sha-512",
            _ => throw new NotSupportedException(
                $"The hash algorithm '{hashAlgorithmName.Name}' is not supported for a JWK thumbprint URI."
            ),
        };

        return $"{ThumbprintUriPrefix}{hashName}:{ComputeThumbprint(hashAlgorithmName)}";
    }

    /// <summary>
    /// Returns the underlying members as an immutable <see cref="JsonElement"/>.
    /// </summary>
    /// <returns>The JWK element.</returns>
    public JsonElement AsElement() => Element;

    /// <summary>
    /// Returns the JSON text representation of this key.
    /// </summary>
    /// <returns>The JWK JSON text.</returns>
    public string ToJsonString() => Element.GetRawText();

    /// <inheritdoc />
    public override string ToString() => ToJsonString();

    internal void WriteTo(Utf8JsonWriter writer) => Element.WriteTo(writer);

    private List<string>? GetStringArray(string name)
    {
        if (!Element.TryGetProperty(name, out var array) || array.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var result = new List<string>(array.GetArrayLength());
        foreach (var item in array.EnumerateArray())
        {
            if (item.TryGetValue<string>(out var text))
                result.Add(text);
        }

        return result;
    }
}
