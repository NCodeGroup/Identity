#region Copyright Preamble

// Copyright @ 2025 NCode Group
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

using System.Text.Json;
using System.Text.Json.Serialization;

namespace NCode.Identity.OpenId.Serialization;

/// <summary>
/// Provides a <see cref="JsonConverter"/> implementation that can serialize and deserialize <see cref="Uri"/>
/// instances to and from JSON.
/// </summary>
public class UriJsonConverter : JsonConverter<Uri>
{
    private class UriEnvelope
    {
        public bool IsAbsolute { get; init; }
        public required string EscapedValue { get; init; }
    }

    /// <inheritdoc />
    public override Uri? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
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
            EscapedValue = value.GetComponents(UriComponents.SerializationInfoString, UriFormat.UriEscaped),
        };
        JsonSerializer.Serialize(writer, envelope, options);
    }
}
