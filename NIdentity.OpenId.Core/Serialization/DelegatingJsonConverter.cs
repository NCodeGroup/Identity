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

public class DelegatingJsonConverter<TInterface, TImplementation> : JsonConverter<TInterface>
    where TImplementation : TInterface, new()
{
    /// <summary>
    /// Gets or sets a value indicating whether the converter requires the type to convert to exactly match
    /// <typeparamref name="TInterface"/> or if it can be assignable from <typeparamref name="TInterface"/>.
    /// </summary>
    public bool MatchExact { get; set; } = true;

    /// <inheritdoc />
    public override bool CanConvert(Type typeToConvert) =>
        MatchExact ?
            typeToConvert == typeof(TInterface) :
            typeof(TInterface).IsAssignableFrom(typeToConvert);

    /// <inheritdoc />
    public override TInterface? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        JsonSerializer.Deserialize<TImplementation>(ref reader, options);

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, TInterface value, JsonSerializerOptions options) =>
        JsonSerializer.Serialize(writer, value, typeof(TImplementation), options);
}
