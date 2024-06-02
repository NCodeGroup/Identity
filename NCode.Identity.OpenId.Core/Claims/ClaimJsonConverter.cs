#region Copyright Preamble

// Copyright @ 2024 NCode Group
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

using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NCode.Identity.OpenId.Claims;

public class ClaimJsonConverter : JsonConverter<Claim>
{
    /// <inheritdoc />
    public override Claim? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var serializableClaim = JsonSerializer.Deserialize<SerializableClaim>(ref reader, options);

        if (serializableClaim is null)
        {
            return null;
        }

        var claim = new Claim(
            serializableClaim.Type,
            serializableClaim.Value,
            serializableClaim.ValueType,
            serializableClaim.Issuer,
            serializableClaim.OriginalIssuer);

        foreach (var (key, value) in serializableClaim.Properties)
        {
            claim.Properties[key] = value;
        }

        return claim;
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, Claim value, JsonSerializerOptions options)
    {
        var serializableClaim = new SerializableClaim
        {
            Type = value.Type,
            Value = value.Value,
            ValueType = value.ValueType,
            Issuer = value.Issuer,
            OriginalIssuer = value.OriginalIssuer,
            Properties = value.Properties
        };
        JsonSerializer.Serialize(writer, serializableClaim, options);
    }
}
