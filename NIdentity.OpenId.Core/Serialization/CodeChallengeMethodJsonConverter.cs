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

public class CodeChallengeMethodJsonConverter : JsonConverter<CodeChallengeMethod>
{
    /// <inheritdoc />
    public override CodeChallengeMethod Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString();

        if (string.IsNullOrEmpty(value))
            return CodeChallengeMethod.Unspecified;

        return value switch
        {
            OpenIdConstants.CodeChallengeMethods.Plain => CodeChallengeMethod.Plain,
            OpenIdConstants.CodeChallengeMethods.S256 => CodeChallengeMethod.Sha256,
            _ => throw new JsonException($"Invalid value '{value}' for {nameof(CodeChallengeMethod)}"),
        };
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, CodeChallengeMethod value, JsonSerializerOptions options)
    {
        if (value == CodeChallengeMethod.Unspecified)
        {
            writer.WriteNullValue();
            return;
        }

        var text = value switch
        {
            CodeChallengeMethod.Plain => OpenIdConstants.CodeChallengeMethods.Plain,
            CodeChallengeMethod.Sha256 => OpenIdConstants.CodeChallengeMethods.S256,
            _ => throw new JsonException($"Invalid value '{value}' for {nameof(CodeChallengeMethod)}"),
        };

        writer.WriteStringValue(text);
    }
}
