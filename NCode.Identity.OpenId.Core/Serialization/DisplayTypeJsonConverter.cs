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

public class DisplayTypeJsonConverter : JsonConverter<DisplayType>
{
    /// <inheritdoc />
    public override DisplayType Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString();

        if (string.IsNullOrEmpty(value))
            return DisplayType.Unspecified;

        return value switch
        {
            OpenIdConstants.DisplayTypes.Page => DisplayType.Page,
            OpenIdConstants.DisplayTypes.Popup => DisplayType.Popup,
            OpenIdConstants.DisplayTypes.Touch => DisplayType.Touch,
            OpenIdConstants.DisplayTypes.Wap => DisplayType.Wap,
            _ => throw new JsonException($"Invalid value '{value}' for {nameof(DisplayType)}"),
        };
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, DisplayType value, JsonSerializerOptions options)
    {
        if (value == DisplayType.Unspecified)
        {
            writer.WriteNullValue();
            return;
        }

        var text = value switch
        {
            DisplayType.Page => OpenIdConstants.DisplayTypes.Page,
            DisplayType.Popup => OpenIdConstants.DisplayTypes.Popup,
            DisplayType.Touch => OpenIdConstants.DisplayTypes.Touch,
            DisplayType.Wap => OpenIdConstants.DisplayTypes.Wap,
            _ => throw new JsonException($"Invalid value '{value}' for {nameof(DisplayType)}"),
        };

        writer.WriteStringValue(text);
    }
}
