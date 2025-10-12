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
using Microsoft.Extensions.Primitives;

namespace NCode.Identity.OpenId.Serialization;

/// <summary>
/// Provides a <see cref="JsonConverter"/> implementation that can serialize and deserialize <see cref="StringValues"/>
/// instances to and from JSON.
/// </summary>
public class StringValuesJsonConverter : JsonConverter<StringValues>
{
    /// <inheritdoc />
    public override StringValues Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var parsedValue = reader.GetString();
        return string.IsNullOrEmpty(parsedValue) ? StringValues.Empty : parsedValue.Split(OpenIdConstants.ParameterSeparatorChar);
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, StringValues value, JsonSerializerOptions options)
    {
        switch (value.Count)
        {
            case 0:
                writer.WriteNullValue();
                break;
            case 1:
                writer.WriteStringValue(value[0]);
                break;
            default:
                writer.WriteStringValue(string.Join(OpenIdConstants.ParameterSeparatorChar, value.AsEnumerable()));
                break;
        }
    }
}
