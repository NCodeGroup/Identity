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

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Nodes;
using NCode.Jose.Json;

namespace NCode.Jose.Jwt;

/// <summary>
/// Represents a JSON object that contains the claims conveyed by the JWT.
/// </summary>
public class JwtClaimsSet
{
    protected JsonElement RootElement { get; }

    public JwtClaimsSet(JsonElement rootElement)
    {
        RootElement = rootElement;
    }

    public string GetRawJson() =>
        RootElement.GetRawText();

    public T Deserialize<T>(JsonSerializerOptions options) =>
        RootElement.Deserialize<T>(options) ?? throw new InvalidOperationException();

    protected List<Claim> CreateClaimCollection(string issuer)
    {
        var collection = new List<Claim>();

        foreach (var jsonProperty in RootElement.EnumerateObject())
        {
            if (jsonProperty.Value.ValueKind == JsonValueKind.Array)
            {
                collection.AddRange(
                    jsonProperty.Value.EnumerateArray().Select(jsonElement =>
                        CreateClaim(issuer, jsonProperty.Name, jsonElement)));
            }
            else
            {
                collection.Add(CreateClaim(issuer, jsonProperty.Name, jsonProperty.Value));
            }
        }

        return collection;
    }

    private static Claim CreateClaim(string issuer, string propertyName, JsonElement jsonElement) =>
        jsonElement.ValueKind switch
        {
            JsonValueKind.Undefined => throw new NotSupportedException(),
            JsonValueKind.Object => new Claim(propertyName, jsonElement.ToString(), JsonClaimValueTypes.Json, issuer),
            JsonValueKind.Array => new Claim(propertyName, jsonElement.ToString(), JsonClaimValueTypes.JsonArray, issuer),
            JsonValueKind.String => CreateStringClaim(issuer, propertyName, jsonElement),
            JsonValueKind.Number => CreateNumberClaim(issuer, propertyName, jsonElement),
            JsonValueKind.True => new Claim(propertyName, "true", ClaimValueTypes.Boolean, issuer),
            JsonValueKind.False => new Claim(propertyName, "false", ClaimValueTypes.Boolean, issuer),
            JsonValueKind.Null => new Claim(propertyName, string.Empty, JsonClaimValueTypes.Null, issuer),
            _ => throw new ArgumentOutOfRangeException(nameof(jsonElement), "Unsupported JsonValueKind.")
        };

    private static Claim CreateStringClaim(string issuer, string propertyName, JsonElement jsonElement)
    {
        var stringValue = jsonElement.ToString();
        var valueType = ClaimValueTypes.String;

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

        return new Claim(propertyName, stringValue, valueType, issuer);
    }

    private static Claim CreateNumberClaim(string issuer, string propertyName, JsonElement jsonElement)
    {
        var stringValue = jsonElement.ToString();
        var valueType = ClaimValueTypes.String;

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

        return new Claim(propertyName, stringValue, valueType, issuer);
    }

    //

    public bool TryGetValue<T>(string propertyName, [MaybeNullWhen(false)] out T valueOrNull)
    {
        if (!RootElement.TryGetProperty(propertyName, out var property))
        {
            valueOrNull = default;
            return false;
        }

        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            Converters = { JoseObjectJsonConverter.Singleton }
        };

        valueOrNull = property.Deserialize<T>(options);
        return valueOrNull != null;
    }

    protected string GetString(string propertyName) =>
        TryGetString(propertyName, out var stringOrNull) ? stringOrNull : string.Empty;

    protected bool TryGetString(string propertyName, [MaybeNullWhen(false)] out string valueOrNull)
    {
        if (!RootElement.TryGetProperty(propertyName, out var property))
        {
            valueOrNull = null;
            return false;
        }

        valueOrNull = property.ToString();
        return true;
    }

    protected IReadOnlyCollection<string> GetStringCollection(string propertyName)
    {
        if (!RootElement.TryGetProperty(propertyName, out var property))
            return Array.Empty<string>();

        if (property.ValueKind == JsonValueKind.Array)
            return property
                .EnumerateArray()
                .Select(jsonElement => jsonElement.ToString())
                .ToList();

        return new[] { property.ToString() };
    }

    protected bool TryGetInt64(string propertyName, [NotNullWhen(true)] out long? valueOrNull)
    {
        if (!RootElement.TryGetProperty(propertyName, out var property))
        {
            valueOrNull = null;
            return false;
        }

        if (property.ValueKind == JsonValueKind.Number)
        {
            if (property.TryGetInt64(out var longValue))
            {
                valueOrNull = longValue;
                return true;
            }

            if (property.TryGetDouble(out var doubleValue))
            {
                valueOrNull = (long)doubleValue;
                return true;
            }
        }

        var stringValue = property.ToString();

        if (long.TryParse(
                stringValue,
                NumberStyles.Integer |
                NumberStyles.AllowThousands,
                CultureInfo.InvariantCulture,
                out var parsedLong))
        {
            valueOrNull = parsedLong;
            return true;
        }

        if (double.TryParse(
                stringValue,
                NumberStyles.Float |
                NumberStyles.AllowThousands,
                CultureInfo.InvariantCulture,
                out var parsedDouble))
        {
            valueOrNull = (long)parsedDouble;
            return true;
        }

        valueOrNull = null;
        return false;
    }

    protected bool TryGetDateTimeOffset(string propertyName, [NotNullWhen(true)] out DateTimeOffset? valueOrNull)
    {
        if (!TryGetInt64(propertyName, out var secondsSinceEpoch))
        {
            valueOrNull = null;
            return false;
        }

        valueOrNull = DateTimeOffset.FromUnixTimeSeconds(secondsSinceEpoch.Value);
        return true;
    }

    protected DateTimeOffset GetDateTimeOffset(string propertyName, DateTimeOffset defaultValue) =>
        TryGetDateTimeOffset(propertyName, out var dateTimeOrNull) ? dateTimeOrNull.Value : defaultValue;
}
