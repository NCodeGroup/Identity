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

namespace NCode.Identity.Jwk;

/// <summary>
/// Serializes a <see cref="JsonWebKeySet"/> to and from its RFC 7517 §5 JSON document form (<c>{ "keys": [ … ] }</c>).
/// </summary>
internal sealed class JsonWebKeySetJsonConverter : JsonConverter<JsonWebKeySet>
{
    /// <inheritdoc />
    public override JsonWebKeySet Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options
    ) => new(JsonElement.ParseValue(ref reader));

    /// <inheritdoc />
    public override void Write(
        Utf8JsonWriter writer,
        JsonWebKeySet value,
        JsonSerializerOptions options
    ) => value.WriteTo(writer);
}
