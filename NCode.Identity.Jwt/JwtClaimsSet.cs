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

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;
using System.Text.Json;

namespace NCode.Identity.Jwt;

/// <summary>
/// Represents a JSON object that contains the claims conveyed by the JWT.
/// </summary>
internal class JwtClaimsSet
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

        if (jsonElement.TryGetDateTime(out _) || jsonElement.TryGetDateTimeOffset(out _))
            valueType = ClaimValueTypes.DateTime;

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

        if (jsonElement.TryGetSingle(out _) || jsonElement.TryGetDouble(out _) || jsonElement.TryGetDecimal(out _))
            valueType = ClaimValueTypes.Double;

        return new Claim(propertyName, stringValue, valueType, issuer);
    }

    protected string GetStringOrEmpty(string propertyName) =>
        TryGetString(propertyName, out var value) ? value : string.Empty;

    protected bool TryGetString(string propertyName, [MaybeNullWhen(false)] out string propertyValue)
    {
        if (RootElement.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String)
        {
            propertyValue = property.GetString();
            Debug.Assert(propertyValue != null);
            return true;
        }

        propertyValue = null;
        return false;
    }

    protected IReadOnlyCollection<string> GetStringCollection(string propertyName)
    {
        var collection = new List<string>();

        if (RootElement.TryGetProperty(propertyName, out var property))
        {
            if (property.ValueKind == JsonValueKind.String)
            {
                var value = property.GetString();
                Debug.Assert(value != null);
                collection.Add(value);
            }
            else if (property.ValueKind == JsonValueKind.Array)
            {
                collection.AddRange(property.EnumerateArray().Select(jsonElement =>
                    jsonElement.GetString() ?? string.Empty));
            }
        }

        return collection;
    }
}

[SuppressMessage("ReSharper", "IdentifierTypo")]
internal static class JwtClaimNames
{
    public const string Alg = "alg";

    public const string Enc = "enc";
    public const string Zip = "zip";

    public const string Jku = "jku";
    public const string Jwk = "jwk";
    public const string Kid = "kid";
    public const string X5u = "x5u";
    public const string X5c = "x5c";
    public const string X5t = "x5t";
    public const string X5tS256 = "x5t#S256";

    public const string Typ = "typ";
    public const string Cty = "cty";
    public const string Crit = "crit";

    public const string Iss = "iss";
    public const string Sub = "sub";
    public const string Aud = "aud";
    public const string Exp = "exp";
    public const string Nbf = "nbf";
    public const string Iat = "iat";
    public const string Jti = "jti";

    public const string Actort = "actort";
}

/// <summary>
/// Constants that indicate how the <see cref="Claim.Value"/> should be evaluated.
/// </summary>
public static class JsonClaimValueTypes
{
    /// <summary>
    /// A value that indicates the <see cref="Claim.Value"/> is <c>null</c>.
    /// </summary>
    /// <remarks>
    /// When creating a <see cref="Claim"/> the <see cref="Claim.Value"/> cannot be null. If the Json value was null,
    /// then the <see cref="Claim.Value"/> will be set to <see cref="string.Empty"/> and the <see cref="Claim.ValueType"/>
    /// will be set to <c>NULL</c>.
    /// </remarks>
    public const string Null = "NULL";

    /// <summary>
    /// A value that indicates the <see cref="Claim.Value"/> is a Json object.
    /// </summary>
    /// <remarks>When creating a <see cref="Claim"/> from Json if the value was not a simple type {String, Null, True, False, Number}
    /// then <see cref="Claim.Value"/> will contain the Json value. If the Json was a JsonObject, the <see cref="Claim.ValueType"/> will be set to "JSON".</remarks>
    public const string Json = "JSON";

    /// <summary>
    /// A value that indicates the <see cref="Claim.Value"/> is a Json object.
    /// </summary>
    /// <remarks>When creating a <see cref="Claim"/> from Json if the value was not a simple type {String, Null, True, False, Number}
    /// then <see cref="Claim.Value"/> will contain the Json value. If the Json was a JsonArray, the <see cref="Claim.ValueType"/> will be set to "JSON_ARRAY".</remarks>
    public const string JsonArray = "JSON_ARRAY";
}

[SuppressMessage("ReSharper", "IdentifierTypo")]
internal class JwtHeader : JwtClaimsSet
{
    private string? JkuOrNull { get; set; }
    private string? JwkOrNull { get; set; }
    private string? KidOrNull { get; set; }
    private string? X5uOrNull { get; set; }
    private string? X5cOrNull { get; set; }
    private string? X5tOrNull { get; set; }
    private string? X5tS256OrNull { get; set; }
    private IReadOnlyCollection<string>? AudOrNull { get; set; }
    private string? ActortOrNull { get; set; }

    /// <inheritdoc />
    public JwtHeader(JsonElement rootElement)
        : base(rootElement)
    {
        // nothing
    }

    public string Jku => JkuOrNull ??= GetStringOrEmpty(JwtClaimNames.Jku);
    public string Jwk => JwkOrNull ??= GetStringOrEmpty(JwtClaimNames.Jwk);
    public string Kid => KidOrNull ??= GetStringOrEmpty(JwtClaimNames.Kid);
    public string X5u => X5uOrNull ??= GetStringOrEmpty(JwtClaimNames.X5u);
    public string X5c => X5cOrNull ??= GetStringOrEmpty(JwtClaimNames.X5c);
    public string X5t => X5tOrNull ??= GetStringOrEmpty(JwtClaimNames.X5t);
    public string X5tS256 => X5tS256OrNull ??= GetStringOrEmpty(JwtClaimNames.X5tS256);

    public IReadOnlyCollection<string> Aud => AudOrNull ??= GetStringCollection(JwtClaimNames.Aud);
    public string Actort => ActortOrNull ??= GetStringOrEmpty(JwtClaimNames.Actort);
}

internal class JwtPayload : JwtClaimsSet
{
    private IReadOnlyCollection<Claim>? ClaimsOrNull { get; set; }

    private string? IssOrNull { get; set; }

    /// <inheritdoc />
    public JwtPayload(JsonElement rootElement)
        : base(rootElement)
    {
        // nothing
    }

    public IReadOnlyCollection<Claim> Claims => ClaimsOrNull ??= CreateClaimCollection(Iss);

    public string Iss => IssOrNull ??= GetStringOrEmpty(JwtClaimNames.Iss);
}
