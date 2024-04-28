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

using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using NCode.Identity.Jose;
using NCode.Identity.Jose.Json;

namespace NCode.Identity.JsonWebTokens;

/// <summary>
/// Factory class that provides the ability to create a <see cref="ClaimsIdentity"/> instance from a Json Web Token (JWT) payload.
/// </summary>
public static class DefaultClaimsIdentityFactory
{
    /// <summary>
    /// Factory method that provides the ability to create a <see cref="ClaimsIdentity"/> instance from a Json Web Token (JWT) payload.
    /// </summary>
    /// <param name="authenticationType">A <see cref="string"/> for the <c>AuthenticationType</c> that is used when creating a <see cref="ClaimsIdentity"/> instance.</param>
    /// <param name="nameClaimType">A <see cref="string"/> that specifies which <see cref="Claim.Type"/> is used to store the <c>Name</c>
    /// claim for a <see cref="ClaimsIdentity"/> instance.</param>
    /// <param name="roleClaimType">A <see cref="string"/> that specifies which <see cref="Claim.Type"/> is used to store the <c>Role</c>
    /// claim for a <see cref="ClaimsIdentity"/> instance.</param>
    /// <param name="payload">A <see cref="JsonElement"/> that contains the Json Web Token (JWT) payload.</param>
    /// <returns>The newly created <see cref="ClaimsIdentity"/> instance.</returns>
    public static ClaimsIdentity CreateClaimsIdentity(
        string authenticationType,
        string nameClaimType,
        string roleClaimType,
        JsonElement payload)
    {
        var effectiveNameClaimType = string.IsNullOrEmpty(nameClaimType) ?
            ClaimsIdentity.DefaultNameClaimType :
            nameClaimType;

        var effectiveRoleClaimType = string.IsNullOrEmpty(roleClaimType) ?
            ClaimsIdentity.DefaultRoleClaimType :
            roleClaimType;

        var subject = new ClaimsIdentity(
            authenticationType,
            effectiveNameClaimType,
            effectiveRoleClaimType);

        if (!payload.TryGetPropertyValue<string>(JoseClaimNames.Payload.Iss, out var issuer) || string.IsNullOrEmpty(issuer))
        {
            issuer = ClaimsIdentity.DefaultIssuer;
        }

        foreach (var property in payload.EnumerateObject())
        {
            var name = property.Name;
            var value = property.Value;

            if (value.ValueKind == JsonValueKind.Array)
            {
                subject.AddClaims(
                    value.EnumerateArray().Select(item =>
                        CreateClaim(name, item, issuer, subject)));
            }
            else
            {
                subject.AddClaim(CreateClaim(name, value, issuer, subject));
            }
        }

        return subject;
    }

    /// <summary>
    /// Creates a <see cref="Claim"/> instance from the specified <paramref name="jsonElement"/>.
    /// </summary>
    /// <param name="propertyName">A <see cref="string"/> that contains the claim name.</param>
    /// <param name="jsonElement">A <see cref="JsonElement"/> that contains the claim value.</param>
    /// <param name="issuer">The issuer of the claim.</param>
    /// <param name="subject">The subject of the claim.</param>
    /// <returns>The newly created <see cref="Claim"/> instance.</returns>
    public static Claim CreateClaim(string propertyName, JsonElement jsonElement, string issuer, ClaimsIdentity subject) =>
        jsonElement.ValueKind switch
        {
            JsonValueKind.Undefined => throw new NotSupportedException(),
            JsonValueKind.Null => new Claim(propertyName, string.Empty, JsonClaimValueTypes.JsonNull, issuer, issuer, subject),
            JsonValueKind.Object => new Claim(propertyName, jsonElement.ToString(), JsonClaimValueTypes.Json, issuer, issuer, subject),
            JsonValueKind.Array => new Claim(propertyName, jsonElement.ToString(), JsonClaimValueTypes.JsonArray, issuer, issuer, subject),
            JsonValueKind.String => CreateStringClaim(propertyName, jsonElement, issuer, subject),
            JsonValueKind.Number => CreateNumberClaim(propertyName, jsonElement, issuer, subject),
            JsonValueKind.True => new Claim(propertyName, "true", ClaimValueTypes.Boolean, issuer, issuer, subject),
            JsonValueKind.False => new Claim(propertyName, "false", ClaimValueTypes.Boolean, issuer, issuer, subject),
            _ => throw new ArgumentOutOfRangeException(nameof(jsonElement), "Unsupported JsonValueKind.")
        };

    /// <summary>
    /// Creates a <see cref="Claim"/> instance from the specified <paramref name="jsonElement"/> that contains a string value.
    /// </summary>
    /// <param name="propertyName">A <see cref="string"/> that contains the claim name.</param>
    /// <param name="jsonElement">A <see cref="JsonElement"/> that contains the claim value.</param>
    /// <param name="issuer">The issuer of the claim.</param>
    /// <param name="subject">The subject of the claim.</param>
    /// <returns>The newly created <see cref="Claim"/> instance.</returns>
    public static Claim CreateStringClaim(string propertyName, JsonElement jsonElement, string issuer, ClaimsIdentity subject)
    {
        var stringValue = jsonElement.ToString();
        var valueType = ClaimValueTypes.String;

        try
        {
            if (DateTime.TryParse(
                    stringValue,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal |
                    DateTimeStyles.AdjustToUniversal,
                    out var dateTimeValue))
            {
                stringValue = dateTimeValue.ToString("O", CultureInfo.InvariantCulture);
                valueType = ClaimValueTypes.DateTime;
            }
        }
        catch
        {
            // ignored
        }

        return new Claim(propertyName, stringValue, valueType, issuer, issuer, subject);
    }

    /// <summary>
    /// Creates a <see cref="Claim"/> instance from the specified <paramref name="jsonElement"/> that contains a numerical value.
    /// </summary>
    /// <param name="propertyName">A <see cref="string"/> that contains the claim name.</param>
    /// <param name="jsonElement">A <see cref="JsonElement"/> that contains the claim value.</param>
    /// <param name="issuer">The issuer of the claim.</param>
    /// <param name="subject">The subject of the claim.</param>
    /// <returns>The newly created <see cref="Claim"/> instance.</returns>
    public static Claim CreateNumberClaim(string propertyName, JsonElement jsonElement, string issuer, ClaimsIdentity subject)
    {
        var stringValue = jsonElement.ToString();
        var valueType = ClaimValueTypes.String;

        try
        {
            if (jsonElement.TryGetInt16(out _))
                valueType = ClaimValueTypes.Integer;

            if (jsonElement.TryGetInt32(out _))
                valueType = ClaimValueTypes.Integer32;

            if (jsonElement.TryGetInt64(out _))
                valueType = ClaimValueTypes.Integer64;

            if (jsonElement.TryGetUInt32(out _))
                valueType = ClaimValueTypes.UInteger32;

            if (jsonElement.TryGetUInt64(out _))
                valueType = ClaimValueTypes.UInteger64;

            // no need to check single or decimal since the range of double is larger
            if (jsonElement.TryGetDouble(out _))
                valueType = ClaimValueTypes.Double;
        }
        catch
        {
            // ignored
        }

        return new Claim(propertyName, stringValue, valueType, issuer, issuer, subject);
    }
}
