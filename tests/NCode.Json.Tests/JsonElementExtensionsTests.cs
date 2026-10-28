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

using System.Buffers.Text;
using System.Text.Json;

namespace NCode.Json.Tests;

public class JsonElementExtensionsTests
{
    [Fact]
    public void TryGetPropertyValue_Missing()
    {
        const string key = nameof(key);

        var dictionary = new Dictionary<string, object>();
        var jsonElement = JsonSerializer.SerializeToElement(dictionary);

        var exists = jsonElement.TryGetPropertyValue<string>(key, out var value);
        Assert.False(exists);
        Assert.Null(value);
    }

    [Fact]
    public void TryGetPropertyValue_Exact()
    {
        const string key = nameof(key);

        var expected = DateTime.Now;
        var dictionary = new Dictionary<string, object> { [key] = expected };
        var jsonElement = JsonSerializer.SerializeToElement(dictionary);

        var exists = jsonElement.TryGetPropertyValue<DateTime>(key, out var value);
        Assert.True(exists);
        Assert.Equal(expected, value);
    }

    [Fact]
    public void TryGetPropertyValue_Null()
    {
        const string key = nameof(key);

        var dictionary = new Dictionary<string, object?> { [key] = null };
        var jsonElement = JsonSerializer.SerializeToElement(dictionary);

        var exists = jsonElement.TryGetPropertyValue<string>(key, out var value);
        Assert.False(exists);
        Assert.Null(value);
    }

    [Fact]
    public void TryGetPropertyValue_String()
    {
        const string key = nameof(key);
        const string expected = nameof(expected);

        var dictionary = new Dictionary<string, object?> { [key] = expected };
        var jsonElement = JsonSerializer.SerializeToElement(dictionary);

        var exists = jsonElement.TryGetPropertyValue<string>(key, out var value);
        Assert.True(exists);
        Assert.Equal(expected, value);
    }

    [Fact]
    public void TryGetPropertyValue_True()
    {
        const string key = nameof(key);
        const bool expected = true;

        var dictionary = new Dictionary<string, object?> { [key] = expected };
        var jsonElement = JsonSerializer.SerializeToElement(dictionary);

        var exists = jsonElement.TryGetPropertyValue<bool?>(key, out var value);
        Assert.True(exists);
        Assert.Equal(expected, value);
    }

    [Fact]
    public void TryGetPropertyValue_False()
    {
        const string key = nameof(key);
        const bool expected = false;

        var dictionary = new Dictionary<string, object?> { [key] = expected };
        var jsonElement = JsonSerializer.SerializeToElement(dictionary);

        var exists = jsonElement.TryGetPropertyValue<bool?>(key, out var value);
        Assert.True(exists);
        Assert.Equal(expected, value);
    }

    [Fact]
    public void TryGetPropertyValue_String_DateTime()
    {
        const string key = nameof(key);

        var expected = DateTime.Now;
        var dictionary = new Dictionary<string, object?> { [key] = expected.ToString("O") };
        var jsonElement = JsonSerializer.SerializeToElement(dictionary);

        var exists = jsonElement.TryGetPropertyValue<DateTime?>(key, out var value);
        Assert.True(exists);
        Assert.Equal(expected, value);
    }

    [Fact]
    public void TryGetPropertyValue_String_DateTimeOffset()
    {
        const string key = nameof(key);

        var expected = DateTimeOffset.Now;
        var dictionary = new Dictionary<string, object?> { [key] = expected.ToString("O") };
        var jsonElement = JsonSerializer.SerializeToElement(dictionary);

        var exists = jsonElement.TryGetPropertyValue<DateTimeOffset?>(key, out var value);
        Assert.True(exists);
        Assert.Equal(expected, value);
    }

    [Fact]
    public void TryGetPropertyValue_Number_Float()
    {
        const string key = nameof(key);

        const float expected = 12.34f;
        var dictionary = new Dictionary<string, object?> { [key] = expected };
        var jsonElement = JsonSerializer.SerializeToElement(dictionary);

        var exists = jsonElement.TryGetPropertyValue<float?>(key, out var value);
        Assert.True(exists);
        Assert.Equal(expected, value);
    }

    [Fact]
    public void TryGetPropertyValue_Number_Double()
    {
        const string key = nameof(key);

        const double expected = 12.34;
        var dictionary = new Dictionary<string, object?> { [key] = expected };
        var jsonElement = JsonSerializer.SerializeToElement(dictionary);

        var exists = jsonElement.TryGetPropertyValue<double?>(key, out var value);
        Assert.True(exists);
        Assert.Equal(expected, value);
    }

    [Fact]
    public void TryGetPropertyValue_Number_Byte()
    {
        const string key = nameof(key);

        const byte expected = 10;
        var dictionary = new Dictionary<string, object?> { [key] = expected };
        var jsonElement = JsonSerializer.SerializeToElement(dictionary);

        var exists = jsonElement.TryGetPropertyValue<byte?>(key, out var value);
        Assert.True(exists);
        Assert.Equal(expected, value);
    }

    [Fact]
    public void TryGetPropertyValue_Number_Short()
    {
        const string key = nameof(key);

        const short expected = 10;
        var dictionary = new Dictionary<string, object?> { [key] = expected };
        var jsonElement = JsonSerializer.SerializeToElement(dictionary);

        var exists = jsonElement.TryGetPropertyValue<short?>(key, out var value);
        Assert.True(exists);
        Assert.Equal(expected, value);
    }

    [Fact]
    public void TryGetPropertyValue_Number_Int()
    {
        const string key = nameof(key);

        const int expected = 10;
        var dictionary = new Dictionary<string, object?> { [key] = expected };
        var jsonElement = JsonSerializer.SerializeToElement(dictionary);

        var exists = jsonElement.TryGetPropertyValue<int?>(key, out var value);
        Assert.True(exists);
        Assert.Equal(expected, value);
    }

    [Fact]
    public void TryGetPropertyValue_Number_Long()
    {
        const string key = nameof(key);

        const long expected = (long)int.MaxValue + 10;
        var dictionary = new Dictionary<string, object?> { [key] = expected };
        var jsonElement = JsonSerializer.SerializeToElement(dictionary);

        var exists = jsonElement.TryGetPropertyValue<long?>(key, out var value);
        Assert.True(exists);
        Assert.Equal(expected, value);
    }

    [Fact]
    public void TryGetPropertyValue_Number_Int_DateTime()
    {
        const string key = nameof(key);

        var expected = DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.Now.ToUnixTimeSeconds());
        var dictionary = new Dictionary<string, object?>
        {
            [key] = (int)expected.ToUnixTimeSeconds(),
        };
        var jsonElement = JsonSerializer.SerializeToElement(dictionary);

        var exists = jsonElement.TryGetPropertyValue<DateTime?>(key, out var value);
        Assert.True(exists);
        Assert.Equal(expected.DateTime, value);
    }

    [Fact]
    public void TryGetPropertyValue_Number_Int_DateTimeOffset()
    {
        const string key = nameof(key);

        var expected = DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.Now.ToUnixTimeSeconds());
        var dictionary = new Dictionary<string, object?>
        {
            [key] = (int)expected.ToUnixTimeSeconds(),
        };
        var jsonElement = JsonSerializer.SerializeToElement(dictionary);

        var exists = jsonElement.TryGetPropertyValue<DateTimeOffset?>(key, out var value);
        Assert.True(exists);
        Assert.Equal(expected, value);
    }

    [Fact]
    public void TryGetPropertyValue_Number_Long_DateTime()
    {
        const string key = nameof(key);

        var expected = DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.Now.ToUnixTimeSeconds());
        var dictionary = new Dictionary<string, object?> { [key] = expected.ToUnixTimeSeconds() };
        var jsonElement = JsonSerializer.SerializeToElement(dictionary);

        var exists = jsonElement.TryGetPropertyValue<DateTime?>(key, out var value);
        Assert.True(exists);
        Assert.Equal(expected.DateTime, value);
    }

    [Fact]
    public void TryGetPropertyValue_Number_Long_DateTimeOffset()
    {
        const string key = nameof(key);

        var expected = DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.Now.ToUnixTimeSeconds());
        var dictionary = new Dictionary<string, object?> { [key] = expected.ToUnixTimeSeconds() };
        var jsonElement = JsonSerializer.SerializeToElement(dictionary);

        var exists = jsonElement.TryGetPropertyValue<DateTimeOffset?>(key, out var value);
        Assert.True(exists);
        Assert.Equal(expected, value);
    }

    [Fact]
    public void TryGetPropertyValue_UnsupportedType_ReturnsFalse()
    {
        const string key = nameof(key);

        // use any data type that is not supported/implemented at this time
        var dictionary = new Dictionary<string, object> { [key] = TimeSpan.FromMinutes(5) };
        var jsonElement = JsonSerializer.SerializeToElement(dictionary);

        var exists = jsonElement.TryGetPropertyValue<TimeSpan>(key, out var value);
        Assert.False(exists);
        Assert.Equal(default, value);
    }

    [Fact]
    public void TryGetPropertyValue_String_Guid()
    {
        const string key = nameof(key);

        var expected = Guid.NewGuid();
        var dictionary = new Dictionary<string, object?> { [key] = expected.ToString() };
        var jsonElement = JsonSerializer.SerializeToElement(dictionary);

        var exists = jsonElement.TryGetPropertyValue<Guid?>(key, out var value);
        Assert.True(exists);
        Assert.Equal(expected, value);
    }

    [Fact]
    public void TryGetPropertyValue_String_ByteArray_Base64()
    {
        const string key = nameof(key);

        byte[] expected = [1, 2, 3, 4, 5];
        var dictionary = new Dictionary<string, object?>
        {
            [key] = Convert.ToBase64String(expected),
        };
        var jsonElement = JsonSerializer.SerializeToElement(dictionary);

        var exists = jsonElement.TryGetPropertyValue<byte[]>(key, out var value);
        Assert.True(exists);
        Assert.Equal(expected, value);
    }

    [Fact]
    public void TryGetPropertyValue_String_ByteArray_Base64Url()
    {
        const string key = nameof(key);

        // 0xFB, 0xFF produce '-' and '_' under base64url, which standard base64 cannot decode.
        byte[] expected = [0xFB, 0xFF, 0xBF];
        var dictionary = new Dictionary<string, object?>
        {
            [key] = Base64Url.EncodeToString(expected),
        };
        var jsonElement = JsonSerializer.SerializeToElement(dictionary);

        var exists = jsonElement.TryGetPropertyValue<byte[]>(key, out var value);
        Assert.True(exists);
        Assert.Equal(expected, value);
    }

    [Fact]
    public void TryGetPropertyValue_String_WithoutOffset_DateTime_IsUnshiftedUtc()
    {
        const string key = nameof(key);

        const string input = "2026-01-02T03:04:05";
        var dictionary = new Dictionary<string, object?> { [key] = input };
        var jsonElement = JsonSerializer.SerializeToElement(dictionary);

        var exists = jsonElement.TryGetPropertyValue<DateTime>(key, out var value);
        Assert.True(exists);
        // The wall-clock time is preserved (unshifted) and a zone-less value is labeled UTC, never Unspecified.
        Assert.Equal(new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc), value);
        Assert.Equal(DateTimeKind.Utc, value.Kind);
    }

    [Fact]
    public void TryGetPropertyValue_Number_Decimal()
    {
        const string key = nameof(key);

        const decimal expected = 12.34m;
        var dictionary = new Dictionary<string, object?> { [key] = expected };
        var jsonElement = JsonSerializer.SerializeToElement(dictionary);

        var exists = jsonElement.TryGetPropertyValue<decimal?>(key, out var value);
        Assert.True(exists);
        Assert.Equal(expected, value);
    }

    [Fact]
    public void TryGetPropertyValue_Number_SByte()
    {
        const string key = nameof(key);

        const sbyte expected = -10;
        var dictionary = new Dictionary<string, object?> { [key] = expected };
        var jsonElement = JsonSerializer.SerializeToElement(dictionary);

        var exists = jsonElement.TryGetPropertyValue<sbyte?>(key, out var value);
        Assert.True(exists);
        Assert.Equal(expected, value);
    }

    [Fact]
    public void TryGetPropertyValue_Number_UInt()
    {
        const string key = nameof(key);

        const uint expected = (uint)int.MaxValue + 10;
        var dictionary = new Dictionary<string, object?> { [key] = expected };
        var jsonElement = JsonSerializer.SerializeToElement(dictionary);

        var exists = jsonElement.TryGetPropertyValue<uint?>(key, out var value);
        Assert.True(exists);
        Assert.Equal(expected, value);
    }

    [Fact]
    public void TryGetPropertyValue_Number_ULong()
    {
        const string key = nameof(key);

        const ulong expected = (ulong)long.MaxValue + 10;
        var dictionary = new Dictionary<string, object?> { [key] = expected };
        var jsonElement = JsonSerializer.SerializeToElement(dictionary);

        var exists = jsonElement.TryGetPropertyValue<ulong?>(key, out var value);
        Assert.True(exists);
        Assert.Equal(expected, value);
    }

    [Fact]
    public void TryGetPropertyValue_Number_UShort()
    {
        const string key = nameof(key);

        const ushort expected = (ushort)short.MaxValue + 10;
        var dictionary = new Dictionary<string, object?> { [key] = expected };
        var jsonElement = JsonSerializer.SerializeToElement(dictionary);

        var exists = jsonElement.TryGetPropertyValue<ushort?>(key, out var value);
        Assert.True(exists);
        Assert.Equal(expected, value);
    }

    [Fact]
    public void TryGetPropertyValue_Number_NumericDate_DateTime_IsUtc()
    {
        const string key = nameof(key);

        var expected = DateTimeOffset.FromUnixTimeSeconds(1_700_000_000);
        var dictionary = new Dictionary<string, object?> { [key] = expected.ToUnixTimeSeconds() };
        var jsonElement = JsonSerializer.SerializeToElement(dictionary);

        var exists = jsonElement.TryGetPropertyValue<DateTime>(key, out var value);
        Assert.True(exists);
        Assert.Equal(DateTimeKind.Utc, value.Kind);
        Assert.Equal(expected.UtcDateTime, value);
    }

    [Fact]
    public void TryGetPropertyValue_Number_NumericDate_OutOfRange_ReturnsFalse()
    {
        const string key = nameof(key);

        var dictionary = new Dictionary<string, object?> { [key] = long.MaxValue };
        var jsonElement = JsonSerializer.SerializeToElement(dictionary);

        var exists = jsonElement.TryGetPropertyValue<DateTimeOffset?>(key, out var value);
        Assert.False(exists);
        Assert.Null(value);
    }

    [Fact]
    public void TryGetPropertyValue_Number_RequestedAsString_ReturnsFalse()
    {
        const string key = nameof(key);

        var dictionary = new Dictionary<string, object?> { [key] = 123 };
        var jsonElement = JsonSerializer.SerializeToElement(dictionary);

        var exists = jsonElement.TryGetPropertyValue<string>(key, out var value);
        Assert.False(exists);
        Assert.Null(value);
    }

    [Fact]
    public void TryGetValue_JsonElement_ReturnsSelf()
    {
        var element = JsonSerializer.SerializeToElement(new { a = 1 });

        var exists = element.TryGetValue<JsonElement>(out var value);
        Assert.True(exists);
        Assert.Equal(JsonValueKind.Object, value.ValueKind);
    }

    [Fact]
    public void TryGetPropertyValue_String_DateOnly_DateTime_ParsedLeniently()
    {
        const string key = nameof(key);

        const string input = "2024-01-02";
        var dictionary = new Dictionary<string, object?> { [key] = input };
        var jsonElement = JsonSerializer.SerializeToElement(dictionary);

        var exists = jsonElement.TryGetPropertyValue<DateTime>(key, out var value);
        Assert.True(exists);
        Assert.Equal(new DateTime(2024, 1, 2), value.Date);
    }

    [Fact]
    public void TryGetPropertyValue_String_DateOnly_DateTimeOffset_ParsedLeniently()
    {
        const string key = nameof(key);

        const string input = "2024-01-02";
        var dictionary = new Dictionary<string, object?> { [key] = input };
        var jsonElement = JsonSerializer.SerializeToElement(dictionary);

        var exists = jsonElement.TryGetPropertyValue<DateTimeOffset>(key, out var value);
        Assert.True(exists);
        Assert.Equal(new DateTime(2024, 1, 2), value.UtcDateTime.Date);
    }

    [Fact]
    public void TryGetPropertyValue_String_Guid_NoHyphens_ParsedLeniently()
    {
        const string key = nameof(key);

        var expected = Guid.NewGuid();
        var dictionary = new Dictionary<string, object?> { [key] = expected.ToString("N") };
        var jsonElement = JsonSerializer.SerializeToElement(dictionary);

        var exists = jsonElement.TryGetPropertyValue<Guid?>(key, out var value);
        Assert.True(exists);
        Assert.Equal(expected, value);
    }

    [Fact]
    public void TryGetPropertyValue_Number_FractionalNumericDate_DateTimeOffset()
    {
        var jsonElement = JsonElement.Parse("""{"exp":1700000000.5}""");

        var exists = jsonElement.TryGetPropertyValue<DateTimeOffset>("exp", out var value);

        Assert.True(exists);
        Assert.Equal(
            DateTimeOffset.UnixEpoch.AddSeconds(1_700_000_000).AddMilliseconds(500),
            value
        );
    }

    #region TryGetNumericDate Tests

    [Theory]
    [InlineData("1700000000", 1_700_000_000L)]
    [InlineData("1700000000.0", 1_700_000_000L)]
    [InlineData("1.7e9", 1_700_000_000L)]
    [InlineData("-1", -1L)]
    public void TryGetNumericDate_WhenNumber_ReturnsUtcInstant(string json, long expectedSeconds)
    {
        var jsonElement = JsonElement.Parse(json);

        var exists = jsonElement.TryGetNumericDate(out var value);

        Assert.True(exists);
        Assert.Equal(TimeSpan.Zero, value.Offset);
        Assert.Equal(expectedSeconds, value.ToUnixTimeSeconds());
    }

    [Theory]
    [InlineData("\"1700000000\"")]
    [InlineData("\"2024-01-02T03:04:05Z\"")]
    [InlineData("99999999999999")]
    [InlineData("99999999999999999999")]
    [InlineData("1e300")]
    [InlineData("null")]
    [InlineData("true")]
    [InlineData("{}")]
    public void TryGetNumericDate_WhenNotANumericDate_ReturnsFalse(string json)
    {
        var jsonElement = JsonElement.Parse(json);

        var exists = jsonElement.TryGetNumericDate(out _);

        Assert.False(exists);
    }

    #endregion

    #region TryGetPropertyNumericDate Tests

    [Fact]
    public void TryGetPropertyNumericDate_WhenPresent_ReturnsUtcInstant()
    {
        var jsonElement = JsonElement.Parse("""{"iat":1700000000}""");

        var exists = jsonElement.TryGetPropertyNumericDate("iat", out var value);

        Assert.True(exists);
        Assert.Equal(1_700_000_000L, value.ToUnixTimeSeconds());
    }

    [Theory]
    [InlineData("""{}""")]
    [InlineData("""{"iat":"1700000000"}""")]
    [InlineData("""{"iat":99999999999999}""")]
    public void TryGetPropertyNumericDate_WhenMissingOrInvalid_ReturnsFalse(string json)
    {
        var jsonElement = JsonElement.Parse(json);

        var exists = jsonElement.TryGetPropertyNumericDate("iat", out _);

        Assert.False(exists);
    }

    #endregion

    #region TryGetWholeInt32 Tests

    [Theory]
    [InlineData("60", 60)]
    [InlineData("60.0", 60)]
    [InlineData("6e1", 60)]
    [InlineData("600e-1", 60)]
    [InlineData("-60.0", -60)]
    [InlineData("2147483647.0", int.MaxValue)]
    [InlineData("-2147483648.0", int.MinValue)]
    public void TryGetWholeInt32_WhenWholeNumber_ReturnsValue(string json, int expected)
    {
        var jsonElement = JsonElement.Parse(json);

        var exists = jsonElement.TryGetWholeInt32(out var value);

        Assert.True(exists);
        Assert.Equal(expected, value);
    }

    [Theory]
    [InlineData("60.5")]
    [InlineData("60.0000000000000001")]
    [InlineData("2147483648")]
    [InlineData("2147483648.0")]
    [InlineData("-2147483649.0")]
    [InlineData("1e30")]
    [InlineData("\"60\"")]
    [InlineData("null")]
    [InlineData("true")]
    [InlineData("{}")]
    public void TryGetWholeInt32_WhenNotAWholeInt32_ReturnsFalse(string json)
    {
        var jsonElement = JsonElement.Parse(json);

        var exists = jsonElement.TryGetWholeInt32(out _);

        Assert.False(exists);
    }

    #endregion

    #region TryGetPropertyWholeInt32 Tests

    [Fact]
    public void TryGetPropertyWholeInt32_WhenPresent_ReturnsValue()
    {
        var jsonElement = JsonElement.Parse("""{"expires_in":60.0}""");

        var exists = jsonElement.TryGetPropertyWholeInt32("expires_in", out var value);

        Assert.True(exists);
        Assert.Equal(60, value);
    }

    [Theory]
    [InlineData("""{}""")]
    [InlineData("""{"expires_in":60.5}""")]
    [InlineData("""{"expires_in":"60"}""")]
    public void TryGetPropertyWholeInt32_WhenMissingOrInvalid_ReturnsFalse(string json)
    {
        var jsonElement = JsonElement.Parse(json);

        var exists = jsonElement.TryGetPropertyWholeInt32("expires_in", out _);

        Assert.False(exists);
    }

    #endregion
}
