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

using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NCode.Identity.OpenId.Claims;

/// <inheritdoc />
public class ClaimsIdentityJsonConverter : JsonConverter<ClaimsIdentity>
{
    /// <inheritdoc />
    public override ClaimsIdentity? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var serializableIdentity = JsonSerializer.Deserialize<SerializableClaimsIdentity>(ref reader, options);

        return HydrateIdentity(serializableIdentity);
    }

    [return: NotNullIfNotNull(nameof(serializableIdentity))]
    private static ClaimsIdentity? HydrateIdentity(SerializableClaimsIdentity? serializableIdentity)
    {
        if (serializableIdentity is null)
        {
            return null;
        }

        var claimsIdentity = new ClaimsIdentity(
            identity: null,
            claims: null,
            serializableIdentity.AuthenticationType,
            serializableIdentity.NameClaimType,
            serializableIdentity.RoleClaimType)
        {
            Label = serializableIdentity.Label,
            BootstrapContext = serializableIdentity.BootstrapContext,
            Actor = HydrateIdentity(serializableIdentity.Actor)
        };

        foreach (var serializableClaim in serializableIdentity.Claims)
        {
            HydrateClaim(serializableClaim, claimsIdentity);
        }

        return claimsIdentity;
    }

    private static void HydrateClaim(SerializableClaim serializableClaim, ClaimsIdentity subject)
    {
        var claim = new Claim(
            serializableClaim.Type,
            serializableClaim.Value,
            serializableClaim.ValueType,
            serializableClaim.Issuer,
            serializableClaim.OriginalIssuer,
            subject
        );

        foreach (var (key, value) in serializableClaim.Properties)
        {
            claim.Properties[key] = value;
        }

        subject.AddClaim(claim);
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, ClaimsIdentity value, JsonSerializerOptions options)
    {
        var serializableClaimsIdentity = SerializeIdentity(value);
        JsonSerializer.Serialize(serializableClaimsIdentity, options);
    }

    [return: NotNullIfNotNull(nameof(identity))]
    private static SerializableClaimsIdentity? SerializeIdentity(ClaimsIdentity? identity)
    {
        if (identity is null)
        {
            return null;
        }

        var actor = SerializeIdentity(identity.Actor);

        var claims = identity.Claims.Select(claim => new SerializableClaim
        {
            Type = claim.Type,
            Value = claim.Value,
            ValueType = claim.ValueType,
            Issuer = claim.Issuer,
            OriginalIssuer = claim.OriginalIssuer,
            Properties = claim.Properties,
        }).ToList();

        return new SerializableClaimsIdentity
        {
            Label = identity.Label,
            BootstrapContext = identity.BootstrapContext as string, // we only allow strings
            AuthenticationType = identity.AuthenticationType,
            NameClaimType = identity.NameClaimType,
            RoleClaimType = identity.RoleClaimType,
            Actor = actor,
            Claims = claims
        };
    }
}
