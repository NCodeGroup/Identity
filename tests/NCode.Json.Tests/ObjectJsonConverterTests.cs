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

using System.Globalization;
using System.Numerics;
using System.Text.Json;
using NCode.Buffers;

namespace NCode.Json.Tests;

public class ObjectJsonConverterTests
{
    private static object? Deserialize(string json, ObjectJsonConverter converter)
    {
        var options = new JsonSerializerOptions { Converters = { converter } };
        return JsonSerializer.Deserialize<object>(json, options);
    }

    #region Read Tests

    [Fact]
    public void Read_WhenNull_ReturnsNull()
    {
        var result = Deserialize("null", ObjectJsonConverter.Default);

        Assert.Null(result);
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData("false", false)]
    public void Read_WhenBoolean_ReturnsBool(string json, bool expected)
    {
        var result = Deserialize(json, ObjectJsonConverter.Default);

        Assert.Equal(expected, Assert.IsType<bool>(result));
    }

    [Fact]
    public void Read_WhenIntegerAndMostSpecific_ReturnsInt32()
    {
        var result = Deserialize("4096", ObjectJsonConverter.Default);

        Assert.Equal(4096, Assert.IsType<int>(result));
    }

    [Fact]
    public void Read_WhenIntegerExceedsInt32AndMostSpecific_ReturnsInt64()
    {
        const long expected = (long)int.MaxValue + 1;

        var result = Deserialize(
            expected.ToString(CultureInfo.InvariantCulture),
            ObjectJsonConverter.Default
        );

        Assert.Equal(expected, Assert.IsType<long>(result));
    }

    [Fact]
    public void Read_WhenIntegerExceedsInt64_ReturnsUInt64()
    {
        const ulong expected = (ulong)long.MaxValue + 1;

        var result = Deserialize(
            expected.ToString(CultureInfo.InvariantCulture),
            ObjectJsonConverter.Default
        );

        Assert.Equal(expected, Assert.IsType<ulong>(result));
    }

    [Fact]
    public void Read_WhenIntegerAndInt64Handling_ReturnsInt64()
    {
        var converter = new ObjectJsonConverter(
            new ObjectJsonConverterOptions { Integer = ObjectJsonIntegerHandling.Int64 }
        );

        var result = Deserialize("4096", converter);

        Assert.Equal(4096L, Assert.IsType<long>(result));
    }

    [Fact]
    public void Read_WhenIntegerExceedsUInt64AndBigIntegerAllowed_ReturnsBigInteger()
    {
        var expected = BigInteger.Parse(
            "123456789012345678901234567890",
            CultureInfo.InvariantCulture
        );

        var result = Deserialize("123456789012345678901234567890", ObjectJsonConverter.Default);

        Assert.Equal(expected, Assert.IsType<BigInteger>(result));
    }

    [Fact]
    public void Read_WhenIntegerExceedsUInt64AndBigIntegerDisallowed_ReturnsDouble()
    {
        var converter = new ObjectJsonConverter(
            new ObjectJsonConverterOptions { AllowBigInteger = false }
        );

        var result = Deserialize("123456789012345678901234567890", converter);

        Assert.IsType<double>(result);
    }

    [Fact]
    public void Read_WhenRealAndDoubleHandling_ReturnsDouble()
    {
        var result = Deserialize("12.34", ObjectJsonConverter.Default);

        Assert.Equal(12.34, Assert.IsType<double>(result));
    }

    [Fact]
    public void Read_WhenRealAndDecimalHandling_ReturnsDecimal()
    {
        var converter = new ObjectJsonConverter(
            new ObjectJsonConverterOptions { Real = ObjectJsonRealHandling.Decimal }
        );

        var result = Deserialize("12.34", converter);

        Assert.Equal(12.34m, Assert.IsType<decimal>(result));
    }

    [Fact]
    public void Read_WhenString_ReturnsString()
    {
        var result = Deserialize("\"hello\"", ObjectJsonConverter.Default);

        Assert.Equal("hello", Assert.IsType<string>(result));
    }

    [Fact]
    public void Read_WhenNumericStringAndCoercionEnabled_ReturnsNumber()
    {
        var result = Deserialize("\"123\"", ObjectJsonConverter.Default);

        Assert.Equal(123, Assert.IsType<int>(result));
    }

    [Fact]
    public void Read_WhenNumericStringAndCoercionDisabled_ReturnsString()
    {
        var converter = new ObjectJsonConverter(
            new ObjectJsonConverterOptions { CoerceStringToNumber = false }
        );

        var result = Deserialize("\"123\"", converter);

        Assert.Equal("123", Assert.IsType<string>(result));
    }

    [Fact]
    public void Read_WhenGuidStringAndCoercionEnabled_ReturnsGuid()
    {
        var expected = Guid.Parse("d94f0d7e-6f1e-4b8a-9c2d-1f3a5b7c9e11");

        var result = Deserialize($"\"{expected}\"", ObjectJsonConverter.Default);

        Assert.Equal(expected, Assert.IsType<Guid>(result));
    }

    [Fact]
    public void Read_WhenGuidStringAndCoercionDisabled_ReturnsString()
    {
        const string guid = "d94f0d7e-6f1e-4b8a-9c2d-1f3a5b7c9e11";

        var converter = new ObjectJsonConverter(
            new ObjectJsonConverterOptions { CoerceStringToGuid = false }
        );

        var result = Deserialize($"\"{guid}\"", converter);

        Assert.Equal(guid, Assert.IsType<string>(result));
    }

    [Fact]
    public void Read_WhenDateStringAndDefault_ReturnsDateTimeOffset()
    {
        var result = Deserialize("\"2024-01-02T03:04:05Z\"", ObjectJsonConverter.Default);

        Assert.IsType<DateTimeOffset>(result);
    }

    [Fact]
    public void Read_WhenDateStringAndNoneHandling_ReturnsString()
    {
        const string date = "2024-01-02T03:04:05Z";

        var converter = new ObjectJsonConverter(
            new ObjectJsonConverterOptions { DateString = ObjectJsonDateStringHandling.None }
        );

        var result = Deserialize($"\"{date}\"", converter);

        Assert.Equal(date, Assert.IsType<string>(result));
    }

    [Fact]
    public void Read_WhenDateStringAndDateTimeHandling_ReturnsDateTime()
    {
        var converter = new ObjectJsonConverter(
            new ObjectJsonConverterOptions { DateString = ObjectJsonDateStringHandling.DateTime }
        );

        var result = Deserialize("\"2024-01-02T03:04:05Z\"", converter);

        Assert.IsType<DateTime>(result);
    }

    [Fact]
    public void Read_WhenDateStringAndDateTimeOffsetHandling_ReturnsDateTimeOffset()
    {
        var converter = new ObjectJsonConverter(
            new ObjectJsonConverterOptions
            {
                DateString = ObjectJsonDateStringHandling.DateTimeOffset,
            }
        );

        var result = Deserialize("\"2024-01-02T03:04:05Z\"", converter);

        Assert.IsType<DateTimeOffset>(result);
    }

    [Fact]
    public void Read_WhenNonDateStringAndDateTimeHandling_ReturnsString()
    {
        var converter = new ObjectJsonConverter(
            new ObjectJsonConverterOptions { DateString = ObjectJsonDateStringHandling.DateTime }
        );

        var result = Deserialize("\"not-a-date\"", converter);

        Assert.Equal("not-a-date", Assert.IsType<string>(result));
    }

    [Fact]
    public void Read_WhenObject_ReturnsDictionary()
    {
        var result = Deserialize("{\"a\":1,\"b\":\"two\"}", ObjectJsonConverter.Default);

        var dictionary = Assert.IsType<Dictionary<string, object?>>(result);
        Assert.Equal(1, dictionary["a"]);
        Assert.Equal("two", dictionary["b"]);
    }

    [Fact]
    public void Read_WhenArray_ReturnsList()
    {
        var result = Deserialize("[1,\"two\",true]", ObjectJsonConverter.Default);

        var list = Assert.IsType<List<object?>>(result);
        Assert.Equal(3, list.Count);
        Assert.Equal(1, list[0]);
        Assert.Equal("two", list[1]);
        Assert.Equal(true, list[2]);
    }

    [Fact]
    public void Read_WhenNestedObjectGraph_ReturnsNestedDictionaries()
    {
        var result = Deserialize("{\"outer\":{\"inner\":[1,2]}}", ObjectJsonConverter.Default);

        var outer = Assert.IsType<Dictionary<string, object?>>(result);
        var inner = Assert.IsType<Dictionary<string, object?>>(outer["outer"]);
        var array = Assert.IsType<List<object?>>(inner["inner"]);
        Assert.Equal([1, 2], array);
    }

    #endregion

    #region Write Tests

    [Fact]
    public void Write_WhenBareObject_WritesEmptyObject()
    {
        var options = new JsonSerializerOptions { Converters = { ObjectJsonConverter.Default } };

        var json = JsonSerializer.Serialize(new object(), options);

        Assert.Equal("{}", json);
    }

    #endregion

    #region NewtonsoftCompatible Preset Tests

    [Fact]
    public void Read_WhenNewtonsoftCompatibleAndInteger_ReturnsInt64()
    {
        var converter = new ObjectJsonConverter(ObjectJsonConverterOptions.NewtonsoftCompatible);

        var result = Deserialize("4096", converter);

        Assert.Equal(4096L, Assert.IsType<long>(result));
    }

    [Fact]
    public void Read_WhenNewtonsoftCompatibleAndDateString_ReturnsDateTime()
    {
        var converter = new ObjectJsonConverter(ObjectJsonConverterOptions.NewtonsoftCompatible);

        var result = Deserialize("\"2024-01-02T03:04:05Z\"", converter);

        Assert.IsType<DateTime>(result);
    }

    [Fact]
    public void Read_WhenNewtonsoftCompatibleAndNumericString_ReturnsString()
    {
        var converter = new ObjectJsonConverter(ObjectJsonConverterOptions.NewtonsoftCompatible);

        var result = Deserialize("\"123\"", converter);

        Assert.Equal("123", Assert.IsType<string>(result));
    }

    #endregion

    #region Round-Trip Tests

    [Fact]
    public void RoundTrip_SerializeDeserializeVariousTypes_ProducesEqualJson()
    {
        var converter = ObjectJsonConverter.Default;

        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            Converters = { converter },
        };

        var i = 0;
        var input = new Dictionary<string, object>
        {
            [$"key{++i}"] = "value1",
            [$"key{++i}"] = true,
            [$"key{++i}"] = 1234,
            [$"key{++i}"] = (long)int.MaxValue + 1,
            [$"key{++i}"] = 12.34,
            [$"key{++i}"] = 12.34m,
            [$"key{++i}"] = DateTimeOffset.Now,
            [$"key{++i}"] = new object[] { "value2", false, -1234 },
        };

        input[$"key{++i}.nested"] = new Dictionary<string, object>(input);

        var expected = JsonSerializer.Serialize(input, options);

        using var stream = new MemoryStream();
        using var writer = new Utf8JsonWriter(stream);
        converter.Write(writer, input, options);
        writer.Flush();

        var bytes = stream.ToArray();
        var json = SecureEncoding.UTF8.GetString(bytes);
        Assert.Equal(expected, json);

        var reader = new Utf8JsonReader(bytes);
        reader.Read();
        var result = converter.Read(ref reader, typeof(object), options);
        Assert.Equal(expected, JsonSerializer.Serialize(result, options));
    }

    #endregion
}
