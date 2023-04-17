#region Copyright Preamble

//
//    Copyright @ 2023 NCode Group
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

namespace NIdentity.OpenId.Serialization;

internal class DictionaryJsonConverter : JsonConverter<object>
{
    public override object? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var type = typeToConvert;

        switch (reader.TokenType)
        {
            case JsonTokenType.Null:
                return null;

            case JsonTokenType.True:
                return true;

            case JsonTokenType.False:
                return false;

            case JsonTokenType.String when reader.TryGetDateTimeOffset(out var value):
                return value;

            case JsonTokenType.String:
                return reader.GetString();

            case JsonTokenType.Number when reader.TryGetInt64(out var value):
                return value;

            case JsonTokenType.Number:
                return reader.GetDecimal();

            case JsonTokenType.StartObject:
                type = typeof(IReadOnlyDictionary<string, object>);
                break;

            case JsonTokenType.StartArray:
                type = typeof(IEnumerable<object>);
                break;
        }

        return JsonSerializer.Deserialize(ref reader, type, options);
    }

    public override void Write(Utf8JsonWriter writer, object value, JsonSerializerOptions options) =>
        JsonSerializer.Serialize(writer, value, value.GetType(), options);
}
