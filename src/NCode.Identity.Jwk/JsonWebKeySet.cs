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
using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;
using JetBrains.Annotations;
using NCode.Buffers;
using NCode.Json;

namespace NCode.Identity.Jwk;

/// <summary>
/// A first-class, immutable <c>JSON Web Key Set</c> (RFC 7517 §5): the <c>keys</c> document published at a
/// <c>jwks_uri</c> and fetched to verify signatures or encrypt payloads.
/// </summary>
/// <remarks>
/// A set deserialized through <c>System.Text.Json</c> types each key with the standard descriptor of its <c>kty</c>
/// only. The constructor and the <see cref="Parse(string, IEnumerable{JsonWebKeyTypeDescriptor})"/> overload that take
/// descriptors type the keys of contributed key types.
/// </remarks>
/// <seealso href="https://datatracker.ietf.org/doc/html/rfc7517#section-5">RFC 7517 Section 5 - JWK Set Format</seealso>
[PublicAPI]
[JsonConverter(typeof(JsonWebKeySetJsonConverter))]
public sealed class JsonWebKeySet
{
    /// <summary>
    /// An empty <see cref="JsonWebKeySet"/>.
    /// </summary>
    public static JsonWebKeySet Empty { get; } = new([]);

    /// <summary>
    /// Gets the keys that make up the set (RFC 7517 §5.1).
    /// </summary>
    public ImmutableArray<JsonWebKey> Keys { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonWebKeySet"/> class with the specified keys.
    /// </summary>
    /// <param name="keys">The keys that make up the set.</param>
    public JsonWebKeySet(IEnumerable<JsonWebKey> keys)
    {
        ArgumentNullException.ThrowIfNull(keys);
        Keys = [.. keys];
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonWebKeySet"/> class from the supplied <see cref="JsonElement"/>,
    /// which must be a JSON object carrying a <c>keys</c> array (RFC 7517 §5.1). Array members that are not JSON
    /// objects or that lack a non-empty string <c>kty</c> are skipped so one malformed key cannot poison the whole set.
    /// Each key uses the standard descriptor of its <c>kty</c>, or none when the key type is not standard.
    /// </summary>
    /// <param name="element">The JWK Set document as a JSON object.</param>
    /// <exception cref="ArgumentException">The element is not a JSON object carrying a <c>keys</c> array.</exception>
    public JsonWebKeySet(JsonElement element)
        : this(element, []) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonWebKeySet"/> class from the supplied <see cref="JsonElement"/>,
    /// which must be a JSON object carrying a <c>keys</c> array (RFC 7517 §5.1), typing the keys of contributed key
    /// types. Array members that are not JSON objects or that lack a non-empty string <c>kty</c> are skipped so one
    /// malformed key cannot poison the whole set. A key whose <c>kty</c> equals the name of a supplied descriptor (an
    /// ordinal comparison) uses that descriptor, even for a standard key type; any other key uses the standard
    /// descriptor of its <c>kty</c>, or none when the key type is not standard.
    /// </summary>
    /// <param name="element">The JWK Set document as a JSON object.</param>
    /// <param name="keyTypeDescriptors">The rules for the contributed key types, each with a distinct name.</param>
    /// <exception cref="ArgumentException">
    /// The element is not a JSON object carrying a <c>keys</c> array, a descriptor is <c>null</c>, or two descriptors
    /// share one name.
    /// </exception>
    public JsonWebKeySet(
        JsonElement element,
        IEnumerable<JsonWebKeyTypeDescriptor> keyTypeDescriptors
    )
        : this(ReadKeys(element, IndexByName(keyTypeDescriptors))) { }

    /// <summary>
    /// Parses a <see cref="JsonWebKeySet"/> from its JSON text representation.
    /// </summary>
    /// <param name="json">The JWK Set JSON text.</param>
    /// <returns>The parsed <see cref="JsonWebKeySet"/>.</returns>
    public static JsonWebKeySet Parse(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        return new JsonWebKeySet(JsonElement.Parse(json));
    }

    /// <summary>
    /// Parses a <see cref="JsonWebKeySet"/> from its JSON text representation, typing the keys of contributed key
    /// types. A key whose <c>kty</c> equals the name of a supplied descriptor (an ordinal comparison) uses that
    /// descriptor, even for a standard key type; any other key uses the standard descriptor of its <c>kty</c>, or none
    /// when the key type is not standard.
    /// </summary>
    /// <param name="json">The JWK Set JSON text.</param>
    /// <param name="keyTypeDescriptors">The rules for the contributed key types, each with a distinct name.</param>
    /// <returns>The parsed <see cref="JsonWebKeySet"/>.</returns>
    /// <exception cref="ArgumentException">
    /// The document is not a JSON object carrying a <c>keys</c> array, a descriptor is <c>null</c>, or two descriptors
    /// share one name.
    /// </exception>
    public static JsonWebKeySet Parse(
        string json,
        IEnumerable<JsonWebKeyTypeDescriptor> keyTypeDescriptors
    )
    {
        ArgumentNullException.ThrowIfNull(json);
        return new JsonWebKeySet(JsonElement.Parse(json), keyTypeDescriptors);
    }

    private static Dictionary<string, JsonWebKeyTypeDescriptor> IndexByName(
        IEnumerable<JsonWebKeyTypeDescriptor> keyTypeDescriptors
    )
    {
        ArgumentNullException.ThrowIfNull(keyTypeDescriptors);

        var descriptorsByName = new Dictionary<string, JsonWebKeyTypeDescriptor>(
            StringComparer.Ordinal
        );
        foreach (var descriptor in keyTypeDescriptors)
        {
            if (descriptor is null)
            {
                throw new ArgumentException(
                    "A JWK key type descriptor is null.",
                    nameof(keyTypeDescriptors)
                );
            }

            if (!descriptorsByName.TryAdd(descriptor.Name, descriptor))
            {
                throw new ArgumentException(
                    $"The JWK key type '{descriptor.Name}' is described more than once.",
                    nameof(keyTypeDescriptors)
                );
            }
        }

        return descriptorsByName;
    }

    private static List<JsonWebKey> ReadKeys(
        JsonElement element,
        Dictionary<string, JsonWebKeyTypeDescriptor> descriptorsByName
    )
    {
        // RFC 7517 §5.1: the "keys" member is REQUIRED and is an array of JWK objects.
        if (
            element.ValueKind != JsonValueKind.Object
            || !element.TryGetProperty(JsonWebKeySetParameterNames.Keys, out var keys)
            || keys.ValueKind != JsonValueKind.Array
        )
        {
            throw new ArgumentException(
                "The JWK Set is missing its 'keys' array.",
                nameof(element)
            );
        }

        var list = new List<JsonWebKey>(keys.GetArrayLength());
        foreach (var item in keys.EnumerateArray())
        {
            // Members with a missing, non-string or empty 'kty' are skipped so one bad key cannot poison the whole set.
            if (
                item.ValueKind != JsonValueKind.Object
                || !item.TryGetPropertyValue<string>(JsonWebKeyParameterNames.KeyType, out var kty)
                || kty.Length == 0
            )
            {
                continue;
            }

            list.Add(
                descriptorsByName.TryGetValue(kty, out var descriptor)
                    ? new JsonWebKey(item, descriptor)
                    : new JsonWebKey(item)
            );
        }

        return list;
    }

    /// <summary>
    /// Attempts to find the key with the specified <c>kid</c>.
    /// </summary>
    /// <param name="keyId">The <c>kid</c> to match.</param>
    /// <param name="key">When this method returns <c>true</c>, the matching key.</param>
    /// <returns><c>true</c> when a matching key was found; otherwise <c>false</c>.</returns>
    public bool TryGetKey(string keyId, out JsonWebKey? key)
    {
        foreach (var candidate in Keys)
        {
            if (string.Equals(candidate.KeyId, keyId, StringComparison.Ordinal))
            {
                key = candidate;
                return true;
            }
        }

        key = null;
        return false;
    }

    /// <summary>
    /// Returns the JSON text representation of this set.
    /// </summary>
    /// <returns>The JWK Set JSON text.</returns>
    public string ToJsonString()
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            WriteTo(writer);
        }

        return SecureEncoding.UTF8.GetString(buffer.WrittenSpan);
    }

    internal void WriteTo(Utf8JsonWriter writer)
    {
        writer.WriteStartObject();
        writer.WritePropertyName(JsonWebKeySetParameterNames.Keys);
        writer.WriteStartArray();
        foreach (var key in Keys)
        {
            key.WriteTo(writer);
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    /// <inheritdoc />
    public override string ToString() => ToJsonString();
}
