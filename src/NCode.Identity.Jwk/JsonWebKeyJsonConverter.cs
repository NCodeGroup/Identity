// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using System.Text.Json.Serialization;

namespace NCode.Identity.Jwk;

/// <summary>
/// Serializes a <see cref="JsonWebKey"/> to and from its RFC 7517 JSON object form.
/// </summary>
internal sealed class JsonWebKeyJsonConverter : JsonConverter<JsonWebKey>
{
    /// <inheritdoc />
    public override JsonWebKey Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options
    ) => new(JsonElement.ParseValue(ref reader));

    /// <inheritdoc />
    public override void Write(
        Utf8JsonWriter writer,
        JsonWebKey value,
        JsonSerializerOptions options
    ) => value.WriteTo(writer);
}
