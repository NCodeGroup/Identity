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

using System.Numerics;
using System.Text.Json;

namespace NCode.Json.Tests;

// Audits that the two loosely-typed JSON mappers in NCode.Json stay consistent with each other:
// ObjectJsonConverter (JSON -> most-specific CLR type, target unknown) and JsonElements.TryGetValue<T>
// (JSON -> an explicitly requested CLR type). The parity tests guard the invariant that every scalar
// coercion the converter performs is also recognized by the typed accessor for the same type; the
// divergence tests pin the small set of intentional, documented differences so they cannot drift silently.
public class ObjectJsonConverterJsonElementsConsistencyTests
{
    private static object? Deserialize(string json, ObjectJsonConverter converter)
    {
        var options = new JsonSerializerOptions { Converters = { converter } };
        return JsonSerializer.Deserialize<object>(json, options);
    }

    private static void AssertTypedAccessorAgrees(JsonElement element, object expected)
    {
        switch (expected)
        {
            case bool v:
                AssertAgrees(element, v);
                break;
            case int v:
                AssertAgrees(element, v);
                break;
            case long v:
                AssertAgrees(element, v);
                break;
            case ulong v:
                AssertAgrees(element, v);
                break;
            case double v:
                AssertAgrees(element, v);
                break;
            case decimal v:
                AssertAgrees(element, v);
                break;
            case Guid v:
                AssertAgrees(element, v);
                break;
            case DateTime v:
                AssertAgrees(element, v);
                break;
            case DateTimeOffset v:
                AssertAgrees(element, v);
                break;
            default:
                Assert.Fail(
                    $"ObjectJsonConverter produced an unexpected type '{expected.GetType()}'. Either extend this "
                        + "consistency audit to cover it or move the case to the documented-divergence tests."
                );
                break;
        }
    }

    private static void AssertAgrees<T>(JsonElement element, T expected)
    {
        var recognized = element.TryGetValue<T>(out var actual);
        Assert.True(
            recognized,
            $"JsonElements.TryGetValue<{typeof(T).Name}> failed for a value that ObjectJsonConverter mapped to "
                + $"{typeof(T).Name}; the two mappers have diverged."
        );
        Assert.Equal(expected, actual);
    }

    #region Parity — converter coercions are recognized by the typed accessor

    public static TheoryData<string, Type> OverlappingScalars =>
        new()
        {
            { "true", typeof(bool) },
            { "false", typeof(bool) },
            { "0", typeof(int) },
            { "4096", typeof(int) },
            { "-1", typeof(int) },
            { "2147483648", typeof(long) }, // int.MaxValue + 1
            { "9223372036854775808", typeof(ulong) }, // long.MaxValue + 1
            { "12.34", typeof(double) },
            { "1e3", typeof(double) },
            { "\"2024-01-02T03:04:05Z\"", typeof(DateTimeOffset) },
            { "\"2024-01-02T03:04:05+02:00\"", typeof(DateTimeOffset) },
            { "\"2024-01-02\"", typeof(DateTimeOffset) }, // date-only: accepted only via lenient parsing
            { "\"6F9619FF-8B86-D011-B42D-00CF4FC964FF\"", typeof(Guid) },
            { "\"6F9619FF8B86D011B42D00CF4FC964FF\"", typeof(Guid) }, // 'N' form: lenient parsing
        };

    [Theory]
    [MemberData(nameof(OverlappingScalars))]
    public void TryGetValue_WhenConverterMapsScalar_AgreesWithTypedAccessor(
        string json,
        Type expectedType
    )
    {
        var converted = Deserialize(json, ObjectJsonConverter.Default);
        Assert.NotNull(converted);
        Assert.IsType(expectedType, converted);

        var element = JsonSerializer.Deserialize<JsonElement>(json);
        AssertTypedAccessorAgrees(element, converted);
    }

    [Theory]
    [InlineData("\"2024-01-02T03:04:05Z\"")] // 'Z': must stay UTC, not shift to machine-local time
    [InlineData("\"2024-01-02T03:04:05+02:00\"")] // explicit offset
    [InlineData("\"2024-01-02T03:04:05\"")] // zone-less: labeled UTC, unshifted
    [InlineData("\"2024-01-02\"")] // date-only: accepted only via lenient parsing
    public void TryGetValue_WhenConverterMapsStringToDateTime_AgreesWithTypedAccessor(string json)
    {
        var converter = new ObjectJsonConverter(
            new ObjectJsonConverterOptions { DateString = ObjectJsonDateStringHandling.DateTime }
        );

        var converted = Deserialize(json, converter);
        var expected = Assert.IsType<DateTime>(converted);

        var element = JsonSerializer.Deserialize<JsonElement>(json);
        Assert.True(element.TryGetValue<DateTime>(out var actual));
        Assert.Equal(expected, actual);
        Assert.Equal(expected.Kind, actual.Kind);
        Assert.NotEqual(DateTimeKind.Unspecified, actual.Kind);
    }

    [Theory]
    [InlineData("\"2024-01-02T03:04:05Z\"", DateTimeKind.Utc)]
    [InlineData("\"2024-01-02T03:04:05+02:00\"", DateTimeKind.Local)]
    [InlineData("\"2024-01-02T03:04:05\"", DateTimeKind.Utc)] // zone-less -> UTC (never Unspecified)
    [InlineData("\"2024-01-02\"", DateTimeKind.Utc)] // date-only -> UTC
    public void TryGetValue_DateTimeKind_IsNormalized_NeverUnspecified(
        string json,
        DateTimeKind expectedKind
    )
    {
        var element = JsonSerializer.Deserialize<JsonElement>(json);
        Assert.True(element.TryGetValue<DateTime>(out var value));
        Assert.Equal(expectedKind, value.Kind);
        Assert.NotEqual(DateTimeKind.Unspecified, value.Kind);
    }

    #endregion

    #region Documented, intentional divergences (executable documentation)

    [Theory]
    [InlineData("\"123\"")]
    [InlineData("\"-5\"")]
    public void TryGetValue_WhenNumberLikeString_DivergesFromConverter_ByDesign(string json)
    {
        // ObjectJsonConverter.Default coerces a number-like string to the most specific numeric type ...
        var converted = Deserialize(json, ObjectJsonConverter.Default);
        Assert.IsType<int>(converted);

        // ... but the typed accessor deliberately never coerces a JSON string to a number.
        var element = JsonSerializer.Deserialize<JsonElement>(json);
        Assert.False(element.TryGetValue<int>(out _));
    }

    [Fact]
    public void TryGetValue_WhenOversizedInteger_DivergesFromConverter_ByDesign()
    {
        const string json = "123456789012345678901234567890";

        // ObjectJsonConverter.Default maps an oversized integer to a lossless BigInteger ...
        var converted = Deserialize(json, ObjectJsonConverter.Default);
        Assert.IsType<BigInteger>(converted);

        // ... but the typed accessor does not support BigInteger.
        var element = JsonSerializer.Deserialize<JsonElement>(json);
        Assert.False(element.TryGetValue<BigInteger>(out _));
    }

    [Fact]
    public void TryGetValue_WhenBareNumber_DivergesFromConverter_OnNumericDate_ByDesign()
    {
        const long epochSeconds = 1_700_000_000;
        const string json = "1700000000";

        // ObjectJsonConverter never interprets a bare number as a date (the target type is ambiguous) ...
        var converted = Deserialize(json, ObjectJsonConverter.Default);
        Assert.IsType<int>(converted);

        // ... whereas the typed accessor interprets the same number as an RFC 7519 NumericDate when a date is requested.
        var element = JsonSerializer.Deserialize<JsonElement>(json);
        Assert.True(element.TryGetValue<DateTimeOffset>(out var actual));
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(epochSeconds), actual);
    }

    #endregion
}
