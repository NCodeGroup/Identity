#region Copyright Preamble

//
//    Copyright @ 2021 NCode Group
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
using NIdentity.OpenId.Messages.Parameters;

namespace NIdentity.OpenId.Messages.Parsers
{
    /// <summary>
    /// Provides a default implementation of <see cref="IJsonParser"/> that parses JSON into <see cref="JsonElement"/>.
    /// </summary>
    public class DefaultJsonParser : IJsonParser
    {
        /// <summary>
        /// Gets a singleton instance for the <see cref="DefaultJsonParser"/> class.
        /// </summary>
        public static DefaultJsonParser Instance { get; } = new();

        /// <inheritdoc/>
        public Parameter Load(IOpenIdMessageContext context, ParameterDescriptor descriptor, ref Utf8JsonReader reader, JsonSerializerOptions options)
        {
            var converter = (JsonConverter<JsonElement>)options.GetConverter(typeof(JsonElement));
            var jsonElement = converter.Read(ref reader, typeof(JsonElement), options);
            var stringValues = jsonElement.GetRawText();
            return new Parameter(descriptor, stringValues, jsonElement);
        }
    }
}
