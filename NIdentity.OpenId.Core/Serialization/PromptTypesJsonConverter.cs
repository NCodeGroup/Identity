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

public class PromptTypesJsonConverter : JsonConverter<PromptTypes>
{
    /// <inheritdoc />
    public override PromptTypes Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString();

        if (string.IsNullOrEmpty(value))
            return PromptTypes.None;

        return value switch
        {
            OpenIdConstants.PromptTypes.None => PromptTypes.None,
            OpenIdConstants.PromptTypes.Login => PromptTypes.Login,
            OpenIdConstants.PromptTypes.Consent => PromptTypes.Consent,
            OpenIdConstants.PromptTypes.SelectAccount => PromptTypes.SelectAccount,
            OpenIdConstants.PromptTypes.CreateAccount => PromptTypes.CreateAccount,
            _ => throw new JsonException($"Invalid value '{value}' for {nameof(PromptTypes)}"),
        };
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, PromptTypes value, JsonSerializerOptions options)
    {
        if (value == PromptTypes.Unspecified)
        {
            writer.WriteNullValue();
            return;
        }

        var text = value switch
        {
            PromptTypes.None => OpenIdConstants.PromptTypes.None,
            PromptTypes.Login => OpenIdConstants.PromptTypes.Login,
            PromptTypes.Consent => OpenIdConstants.PromptTypes.Consent,
            PromptTypes.SelectAccount => OpenIdConstants.PromptTypes.SelectAccount,
            PromptTypes.CreateAccount => OpenIdConstants.PromptTypes.CreateAccount,
            _ => throw new JsonException($"Invalid value '{value}' for {nameof(PromptTypes)}"),
        };

        writer.WriteStringValue(text);
    }
}
