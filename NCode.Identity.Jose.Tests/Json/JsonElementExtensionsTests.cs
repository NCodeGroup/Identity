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

using System.Text.Json;
using NCode.Identity.Jose.Json;

namespace NCode.Jose.Tests.Json;

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
        var dictionary = new Dictionary<string, object>
        {
            [key] = expected
        };
        var jsonElement = JsonSerializer.SerializeToElement(dictionary);

        var exists = jsonElement.TryGetPropertyValue<DateTime>(key, out var value);
        Assert.True(exists);
        Assert.Equal(expected, value);
    }

    [Fact]
    public void TryGetPropertyValue_Null()
    {
        const string key = nameof(key);

        var dictionary = new Dictionary<string, object?>
        {
            [key] = null
        };
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

        var dictionary = new Dictionary<string, object?>
        {
            [key] = expected
        };
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

        var dictionary = new Dictionary<string, object?>
        {
            [key] = expected
        };
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

        var dictionary = new Dictionary<string, object?>
        {
            [key] = expected
        };
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
        var dictionary = new Dictionary<string, object?>
        {
            [key] = expected.ToString("O")
        };
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
        var dictionary = new Dictionary<string, object?>
        {
            [key] = expected.ToString("O")
        };
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
        var dictionary = new Dictionary<string, object?>
        {
            [key] = expected
        };
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
        var dictionary = new Dictionary<string, object?>
        {
            [key] = expected
        };
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
        var dictionary = new Dictionary<string, object?>
        {
            [key] = expected
        };
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
        var dictionary = new Dictionary<string, object?>
        {
            [key] = expected
        };
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
        var dictionary = new Dictionary<string, object?>
        {
            [key] = expected
        };
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
        var dictionary = new Dictionary<string, object?>
        {
            [key] = expected
        };
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
            [key] = (int)expected.ToUnixTimeSeconds()
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
            [key] = (int)expected.ToUnixTimeSeconds()
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
        var dictionary = new Dictionary<string, object?>
        {
            [key] = expected.ToUnixTimeSeconds()
        };
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
        var dictionary = new Dictionary<string, object?>
        {
            [key] = expected.ToUnixTimeSeconds()
        };
        var jsonElement = JsonSerializer.SerializeToElement(dictionary);

        var exists = jsonElement.TryGetPropertyValue<DateTimeOffset?>(key, out var value);
        Assert.True(exists);
        Assert.Equal(expected, value);
    }

    [Fact]
    public void TryGetPropertyValue_Invalid()
    {
        const string key = nameof(key);

        // use any data type that is not supported/implemented at this time
        var dictionary = new Dictionary<string, object>
        {
            [key] = Guid.NewGuid()
        };
        var jsonElement = JsonSerializer.SerializeToElement(dictionary);

        var exists = jsonElement.TryGetPropertyValue<Guid>(key, out var value);
        Assert.False(exists);
        Assert.Equal(default, value);
    }
}
