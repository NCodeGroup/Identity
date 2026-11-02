// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using System.Text.Json.Serialization;
using JetBrains.Annotations;

namespace NCode.Json;

/// <summary>
/// Provides a <see cref="JsonConverter"/> implementation that can serialize and deserialize <see cref="Uri"/>
/// instances to and from JSON.
/// </summary>
[PublicAPI]
public sealed class UriJsonConverter : JsonConverter<Uri>
{
    /// <summary>
    /// Gets a singleton instance for <see cref="UriJsonConverter"/>.
    /// </summary>
    public static UriJsonConverter Singleton { get; } = new();

    /// <inheritdoc />
    public override Uri? Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options
    )
    {
        var envelope = JsonSerializer.Deserialize<UriEnvelope>(ref reader, options);
        if (envelope is null)
        {
            return null;
        }

        var kind = envelope.IsAbsolute ? UriKind.Absolute : UriKind.Relative;
        var uri = new Uri(envelope.EscapedValue, kind);
        return uri;
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, Uri? value, JsonSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        var envelope = new UriEnvelope
        {
            IsAbsolute = value.IsAbsoluteUri,
            EscapedValue = value.GetComponents(
                UriComponents.SerializationInfoString,
                UriFormat.UriEscaped
            ),
        };
        JsonSerializer.Serialize(writer, envelope, options);
    }
}
