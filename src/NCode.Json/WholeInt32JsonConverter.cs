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

using System.Text.Json;
using System.Text.Json.Serialization;
using JetBrains.Annotations;

namespace NCode.Json;

/// <summary>
/// Provides a <see cref="JsonConverter"/> implementation that reads an <see cref="int"/> from a JSON number with a
/// whole value in any form, such as <c>60</c>, <c>60.0</c> or <c>6e1</c>, and writes it as a JSON integer.
/// A number with a non-zero fraction, a number outside the range of <see cref="int"/>, or any other JSON value kind
/// fails with a <see cref="JsonException"/>. Apply it with <c>[JsonConverter(typeof(WholeInt32JsonConverter))]</c>
/// to an <see cref="int"/> or nullable <see cref="int"/> member.
/// </summary>
[PublicAPI]
public sealed class WholeInt32JsonConverter : JsonConverter<int>
{
    /// <inheritdoc />
    public override int Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options
    )
    {
        var element = JsonElement.ParseValue(ref reader);
        if (!element.TryGetWholeInt32(out var value))
        {
            throw new JsonException(
                "The JSON value is not a whole number within the range of a 32-bit integer."
            );
        }

        return value;
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, int value, JsonSerializerOptions options)
    {
        writer.WriteNumberValue(value);
    }
}
