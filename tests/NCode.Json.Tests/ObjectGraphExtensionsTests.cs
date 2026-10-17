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

namespace NCode.Json.Tests;

public class ObjectGraphExtensionsTests
{
    #region TryGetValue Tests

    [Fact]
    public void TryGetValue_KeyExistsWithCorrectType_ReturnsTrueWithValue()
    {
        const string key = nameof(key);

        var expected = DateTime.Now;
        var dictionary = new Dictionary<string, object> { [key] = expected };

        var exists = dictionary.TryGetValue<DateTime>(key, out var value);

        Assert.True(exists);
        Assert.Equal(expected, value);
    }

    [Fact]
    public void TryGetValue_AsNullable_NotNull()
    {
        const string key = nameof(key);

        var expected = DateTime.Now;
        var dictionary = new Dictionary<string, object> { [key] = expected };

        var exists = dictionary.TryGetValue<DateTime?>(key, out var value);

        Assert.True(exists);
        Assert.Equal(expected, value);
    }

    [Fact]
    public void TryGetValue_KeyDoesNotExist_ReturnsFalse()
    {
        const string key = nameof(key);
        const string otherKey = nameof(otherKey);

        var dictionary = new Dictionary<string, object> { [key] = DateTime.Now };

        var exists = dictionary.TryGetValue<DateTime>(otherKey, out var value);

        Assert.False(exists);
        Assert.Equal(default, value);
    }

    [Fact]
    public void TryGetValue_IncompatibleType_ReturnsFalse()
    {
        const string key = nameof(key);

        var dictionary = new Dictionary<string, object> { [key] = DateTime.Now };

        var exists = dictionary.TryGetValue<int>(key, out var value);

        Assert.False(exists);
        Assert.Equal(default, value);
    }

    [Fact]
    public void TryGetValue_StringStoredAsString_ReturnsTrue()
    {
        const string key = nameof(key);
        const string expected = "hello";

        var dictionary = new Dictionary<string, object> { [key] = expected };

        var exists = dictionary.TryGetValue<string>(key, out var value);

        Assert.True(exists);
        Assert.Equal(expected, value);
    }

    [Fact]
    public void TryGetValue_NullValue_ReturnsFalse()
    {
        const string key = nameof(key);

        var dictionary = new Dictionary<string, object> { [key] = null! };

        var exists = dictionary.TryGetValue<string>(key, out var value);

        Assert.False(exists);
        Assert.Null(value);
    }

    [Fact]
    public void TryGetValue_ReadOnlyDictionaryReceiver_ReturnsTrue()
    {
        const string key = nameof(key);
        const string expected = "hello";

        IReadOnlyDictionary<string, object> dictionary = new Dictionary<string, object>
        {
            [key] = expected,
        };

        var exists = dictionary.TryGetValue<string>(key, out var value);

        Assert.True(exists);
        Assert.Equal(expected, value);
    }

    [Fact]
    public void TryGetValue_DictionaryReceiver_ReturnsTrue()
    {
        const string key = nameof(key);
        const string expected = "hello";

        IDictionary<string, object> dictionary = new Dictionary<string, object>
        {
            [key] = expected,
        };

        var exists = dictionary.TryGetValue<string>(key, out var value);

        Assert.True(exists);
        Assert.Equal(expected, value);
    }

    [Fact]
    public void TryGetValue_EnumerableReceiver_ScansForKey()
    {
        const string key = nameof(key);
        const string expected = "hello";

        IEnumerable<KeyValuePair<string, object>> pairs = [new("other", 1), new(key, expected)];

        var exists = pairs.TryGetValue<string>(key, out var value);

        Assert.True(exists);
        Assert.Equal(expected, value);
    }

    #endregion

    #region TryGetValue Numeric-Width Tolerance Tests

    [Fact]
    public void TryGetValue_IntBoxedReadAsLong_ReturnsTrue()
    {
        // Regression: a deserialized NumericDate such as 'iat' boxes as int and must still read back as long.
        const string key = nameof(key);
        const int stored = 1_700_000_000;

        var dictionary = new Dictionary<string, object> { [key] = stored };

        var exists = dictionary.TryGetValue<long>(key, out var value);

        Assert.True(exists);
        Assert.Equal(1_700_000_000L, value);
    }

    [Fact]
    public void TryGetValue_IntBoxedReadAsUlong_ReturnsTrue()
    {
        const string key = nameof(key);
        const int stored = 4096;

        var dictionary = new Dictionary<string, object> { [key] = stored };

        var exists = dictionary.TryGetValue<ulong>(key, out var value);

        Assert.True(exists);
        Assert.Equal(4096UL, value);
    }

    [Fact]
    public void TryGetValue_IntBoxedReadAsBigInteger_ReturnsTrue()
    {
        const string key = nameof(key);
        const int stored = 4096;

        var dictionary = new Dictionary<string, object> { [key] = stored };

        var exists = dictionary.TryGetValue<BigInteger>(key, out var value);

        Assert.True(exists);
        Assert.Equal(new BigInteger(4096), value);
    }

    [Fact]
    public void TryGetValue_LongBoxedReadAsInt_InRange_ReturnsTrue()
    {
        const string key = nameof(key);
        const long stored = 4096L;

        var dictionary = new Dictionary<string, object> { [key] = stored };

        var exists = dictionary.TryGetValue<int>(key, out var value);

        Assert.True(exists);
        Assert.Equal(4096, value);
    }

    [Fact]
    public void TryGetValue_LongBoxedReadAsInt_OutOfRange_ReturnsFalse()
    {
        const string key = nameof(key);
        const long stored = long.MaxValue;

        var dictionary = new Dictionary<string, object> { [key] = stored };

        var exists = dictionary.TryGetValue<int>(key, out var value);

        Assert.False(exists);
        Assert.Equal(default, value);
    }

    [Fact]
    public void TryGetValue_UlongBoxedReadAsLong_OutOfRange_ReturnsFalse()
    {
        const string key = nameof(key);
        const ulong stored = ulong.MaxValue;

        var dictionary = new Dictionary<string, object> { [key] = stored };

        var exists = dictionary.TryGetValue<long>(key, out var value);

        Assert.False(exists);
        Assert.Equal(default, value);
    }

    [Fact]
    public void TryGetValue_BigIntegerBoxedReadAsLong_InRange_ReturnsTrue()
    {
        const string key = nameof(key);
        var stored = new BigInteger(1_700_000_000);

        var dictionary = new Dictionary<string, object> { [key] = stored };

        var exists = dictionary.TryGetValue<long>(key, out var value);

        Assert.True(exists);
        Assert.Equal(1_700_000_000L, value);
    }

    [Fact]
    public void TryGetValue_IntBoxedReadAsDouble_ReturnsTrue()
    {
        const string key = nameof(key);
        const int stored = 42;

        var dictionary = new Dictionary<string, object> { [key] = stored };

        var exists = dictionary.TryGetValue<double>(key, out var value);

        Assert.True(exists);
        Assert.Equal(42d, value);
    }

    [Fact]
    public void TryGetValue_DoubleBoxedReadAsDecimal_ReturnsTrue()
    {
        const string key = nameof(key);
        const double stored = 1.5;

        var dictionary = new Dictionary<string, object> { [key] = stored };

        var exists = dictionary.TryGetValue<decimal>(key, out var value);

        Assert.True(exists);
        Assert.Equal(1.5m, value);
    }

    [Fact]
    public void TryGetValue_DoubleBoxedReadAsDecimal_OutOfRange_ReturnsFalse()
    {
        const string key = nameof(key);
        const double stored = double.MaxValue;

        var dictionary = new Dictionary<string, object> { [key] = stored };

        var exists = dictionary.TryGetValue<decimal>(key, out var value);

        Assert.False(exists);
        Assert.Equal(default, value);
    }

    #endregion

    #region TryGetValue NumericDate Tests

    [Fact]
    public void TryGetValue_IntBoxedReadAsDateTimeOffset_ReturnsNumericDate()
    {
        const string key = nameof(key);
        const int stored = 1_700_000_000;

        var dictionary = new Dictionary<string, object> { [key] = stored };

        var exists = dictionary.TryGetValue<DateTimeOffset>(key, out var value);

        Assert.True(exists);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1_700_000_000), value);
    }

    [Fact]
    public void TryGetValue_LongBoxedReadAsDateTime_ReturnsNumericDate()
    {
        const string key = nameof(key);
        const long stored = 1_700_000_000L;

        var dictionary = new Dictionary<string, object> { [key] = stored };

        var exists = dictionary.TryGetValue<DateTime>(key, out var value);

        Assert.True(exists);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1_700_000_000).UtcDateTime, value);
    }

    [Fact]
    public void TryGetValue_IntBoxedReadAsNullableDateTimeOffset_ReturnsNumericDate()
    {
        const string key = nameof(key);
        const int stored = 1_700_000_000;

        var dictionary = new Dictionary<string, object> { [key] = stored };

        var exists = dictionary.TryGetValue<DateTimeOffset?>(key, out var value);

        Assert.True(exists);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1_700_000_000), value);
    }

    [Fact]
    public void TryGetValue_NumericDateOutOfRange_ReturnsFalse()
    {
        const string key = nameof(key);
        var stored = new BigInteger(long.MaxValue);

        var dictionary = new Dictionary<string, object> { [key] = stored };

        var exists = dictionary.TryGetValue<DateTimeOffset>(key, out var value);

        Assert.False(exists);
        Assert.Equal(default, value);
    }

    #endregion
}
