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

using System.Security.Claims;
using System.Text.Json;

namespace NCode.Identity.Jwt.Claims;

public class ClaimValueMapperTests
{
    private const string ClaimType = "custom";
    private const string Issuer = "https://issuer.example";

    private static Claim ToClaim(string json)
    {
        var element = JsonElement.Parse(json);
        return ClaimValueMapper.ToClaim(ClaimType, element, Issuer, new ClaimsIdentity());
    }

    private static object ToJsonValue(string value, string valueType)
    {
        var claim = new Claim(ClaimType, value, valueType);
        return ClaimValueMapper.ToJsonValue(claim);
    }

    #region ToClaim Tests

    [Theory]
    [InlineData("\"text\"", "text", ClaimValueTypes.String)]
    [InlineData("\"1.2\"", "1.2", ClaimValueTypes.String)]
    [InlineData("true", "true", ClaimValueTypes.Boolean)]
    [InlineData("false", "false", ClaimValueTypes.Boolean)]
    [InlineData("null", "", JsonClaimValueTypes.JsonNull)]
    [InlineData("{\"a\":1}", "{\"a\":1}", JsonClaimValueTypes.Json)]
    [InlineData("[1,2]", "[1,2]", JsonClaimValueTypes.JsonArray)]
    public void ToClaim_WhenNonNumericValue_MapsKindToValueType(
        string json,
        string expectedValue,
        string expectedValueType
    )
    {
        var claim = ToClaim(json);

        Assert.Equal(expectedValue, claim.Value);
        Assert.Equal(expectedValueType, claim.ValueType);
    }

    [Theory]
    [InlineData("42", ClaimValueTypes.Integer32)]
    [InlineData("-2147483648", ClaimValueTypes.Integer32)]
    [InlineData("2147483648", ClaimValueTypes.Integer64)]
    [InlineData("9007199254740993", ClaimValueTypes.Integer64)]
    [InlineData("9223372036854775808", ClaimValueTypes.UInteger64)]
    [InlineData("18446744073709551616", ClaimValueTypes.Integer)]
    [InlineData("-9223372036854775809", ClaimValueTypes.Integer)]
    [InlineData("1.5", ClaimValueTypes.Double)]
    [InlineData("1.0", ClaimValueTypes.Double)]
    [InlineData("1e3", ClaimValueTypes.Double)]
    public void ToClaim_WhenNumber_KeepsLiteralWithMostSpecificValueType(
        string json,
        string expectedValueType
    )
    {
        var claim = ToClaim(json);

        Assert.Equal(json, claim.Value);
        Assert.Equal(expectedValueType, claim.ValueType);
    }

    [Fact]
    public void ToClaim_WhenCalled_SetsIssuerAndSubject()
    {
        var subject = new ClaimsIdentity();
        var element = JsonElement.Parse("\"value\"");

        var claim = ClaimValueMapper.ToClaim(ClaimType, element, Issuer, subject);

        Assert.Equal(ClaimType, claim.Type);
        Assert.Equal(Issuer, claim.Issuer);
        Assert.Equal(Issuer, claim.OriginalIssuer);
        Assert.Same(subject, claim.Subject);
    }

    [Fact]
    public void ToClaim_WhenUndefined_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ClaimValueMapper.ToClaim(ClaimType, default, Issuer, new ClaimsIdentity())
        );
    }

    #endregion

    #region ToJsonValue Tests

    [Fact]
    public void ToJsonValue_WhenString_ReturnsValue()
    {
        var value = ToJsonValue("text", ClaimValueTypes.String);

        Assert.Equal("text", Assert.IsType<string>(value));
    }

    [Fact]
    public void ToJsonValue_WhenBoolean_ReturnsBool()
    {
        var value = ToJsonValue("true", ClaimValueTypes.Boolean);

        Assert.True(Assert.IsType<bool>(value));
    }

    [Theory]
    [InlineData("42", 42)]
    [InlineData("+042", 42)]
    [InlineData("-2147483648", int.MinValue)]
    public void ToJsonValue_WhenInteger32_ReturnsInt(string claimValue, int expected)
    {
        var value = ToJsonValue(claimValue, ClaimValueTypes.Integer32);

        Assert.Equal(expected, Assert.IsType<int>(value));
    }

    [Fact]
    public void ToJsonValue_WhenInteger64BeyondDoublePrecision_ReturnsExactLong()
    {
        var value = ToJsonValue("9007199254740993", ClaimValueTypes.Integer64);

        Assert.Equal(9007199254740993L, Assert.IsType<long>(value));
    }

    [Fact]
    public void ToJsonValue_WhenUInteger32_ReturnsUInt()
    {
        var value = ToJsonValue("4294967295", ClaimValueTypes.UInteger32);

        Assert.Equal(uint.MaxValue, Assert.IsType<uint>(value));
    }

    [Fact]
    public void ToJsonValue_WhenUInteger64_ReturnsULong()
    {
        var value = ToJsonValue("18446744073709551615", ClaimValueTypes.UInteger64);

        Assert.Equal(ulong.MaxValue, Assert.IsType<ulong>(value));
    }

    [Fact]
    public void ToJsonValue_WhenIntegerFitsLong_ReturnsLong()
    {
        var value = ToJsonValue("1700000000000", ClaimValueTypes.Integer);

        Assert.Equal(1_700_000_000_000L, Assert.IsType<long>(value));
    }

    [Fact]
    public void ToJsonValue_WhenIntegerFitsOnlyULong_ReturnsULong()
    {
        var value = ToJsonValue("18446744073709551615", ClaimValueTypes.Integer);

        Assert.Equal(ulong.MaxValue, Assert.IsType<ulong>(value));
    }

    [Fact]
    public void ToJsonValue_WhenIntegerBeyond64Bits_ReturnsExactJsonNumber()
    {
        var value = ToJsonValue("18446744073709551616", ClaimValueTypes.Integer);

        var element = Assert.IsType<JsonElement>(value);
        Assert.Equal(JsonValueKind.Number, element.ValueKind);
        Assert.Equal("18446744073709551616", element.GetRawText());
    }

    [Fact]
    public void ToJsonValue_WhenDouble_ReturnsDouble()
    {
        var value = ToJsonValue("1.5", ClaimValueTypes.Double);

        Assert.Equal(1.5, Assert.IsType<double>(value));
    }

    [Fact]
    public void ToJsonValue_WhenDateTime_ReturnsDateTimeOffset()
    {
        var value = ToJsonValue("2024-01-02T03:04:05Z", ClaimValueTypes.DateTime);

        Assert.Equal(
            new DateTimeOffset(2024, 1, 2, 3, 4, 5, TimeSpan.Zero),
            Assert.IsType<DateTimeOffset>(value)
        );
    }

    [Theory]
    [InlineData("{\"a\":1}", JsonClaimValueTypes.Json, JsonValueKind.Object)]
    [InlineData("[1,2]", JsonClaimValueTypes.JsonArray, JsonValueKind.Array)]
    public void ToJsonValue_WhenJsonOfDeclaredKind_ReturnsJsonElement(
        string claimValue,
        string valueType,
        JsonValueKind expectedKind
    )
    {
        var value = ToJsonValue(claimValue, valueType);

        var element = Assert.IsType<JsonElement>(value);
        Assert.Equal(expectedKind, element.ValueKind);
        Assert.Equal(claimValue, element.GetRawText());
    }

    [Fact]
    public void ToJsonValue_WhenJsonNull_ReturnsJsonNullElement()
    {
        var value = ToJsonValue(string.Empty, JsonClaimValueTypes.JsonNull);

        var element = Assert.IsType<JsonElement>(value);
        Assert.Equal(JsonValueKind.Null, element.ValueKind);
    }

    [Theory]
    [InlineData("abc", ClaimValueTypes.Integer32)]
    [InlineData("2147483648", ClaimValueTypes.Integer32)]
    [InlineData("1,000", ClaimValueTypes.Integer64)]
    [InlineData("-1", ClaimValueTypes.UInteger32)]
    [InlineData("1.5", ClaimValueTypes.Integer)]
    [InlineData("NaN", ClaimValueTypes.Double)]
    [InlineData("Infinity", ClaimValueTypes.Double)]
    [InlineData("1e400", ClaimValueTypes.Double)]
    [InlineData("yes", ClaimValueTypes.Boolean)]
    [InlineData("not a date", ClaimValueTypes.DateTime)]
    [InlineData("{\"a\":", JsonClaimValueTypes.Json)]
    [InlineData("[1,2]", JsonClaimValueTypes.Json)]
    [InlineData("{\"a\":1}", JsonClaimValueTypes.JsonArray)]
    [InlineData("value", "urn:example:unknown-type")]
    public void ToJsonValue_WhenInvalidForDeclaredType_ReturnsVerbatimString(
        string claimValue,
        string valueType
    )
    {
        var value = ToJsonValue(claimValue, valueType);

        Assert.Equal(claimValue, Assert.IsType<string>(value));
    }

    #endregion
}
