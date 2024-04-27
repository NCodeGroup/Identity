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

namespace NCode.Identity.OpenId.Serialization;

public class ResponseModeJsonConverter : JsonConverter<ResponseMode>
{
    /// <inheritdoc />
    public override ResponseMode Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString();

        if (string.IsNullOrEmpty(value))
            return ResponseMode.Unspecified;

        return value switch
        {
            OpenIdConstants.ResponseModes.Query => ResponseMode.Query,
            OpenIdConstants.ResponseModes.Fragment => ResponseMode.Fragment,
            OpenIdConstants.ResponseModes.FormPost => ResponseMode.FormPost,
            _ => throw new JsonException($"Invalid value '{value}' for {nameof(ResponseMode)}"),
        };
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, ResponseMode value, JsonSerializerOptions options)
    {
        if (value == ResponseMode.Unspecified)
        {
            writer.WriteNullValue();
            return;
        }

        var text = value switch
        {
            ResponseMode.Query => OpenIdConstants.ResponseModes.Query,
            ResponseMode.Fragment => OpenIdConstants.ResponseModes.Fragment,
            ResponseMode.FormPost => OpenIdConstants.ResponseModes.FormPost,
            _ => throw new JsonException($"Invalid value '{value}' for {nameof(ResponseMode)}"),
        };

        writer.WriteStringValue(text);
    }
}
