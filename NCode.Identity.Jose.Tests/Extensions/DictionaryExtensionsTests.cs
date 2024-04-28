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

using NCode.Identity.Jose.Extensions;

namespace NCode.Jose.Tests.Extensions;

public class DictionaryExtensionsTests
{
    [Fact]
    public void TryGetValue_Valid()
    {
        const string key = nameof(key);

        var expected = DateTime.Now;
        var dictionary = new Dictionary<string, object>
        {
            [key] = expected
        };

        var exists = dictionary.TryGetValue<DateTime>(key, out var value);
        Assert.True(exists);
        Assert.Equal(expected, value);
    }

    [Fact]
    public void TryGetValue_AsNullable_NotNull()
    {
        const string key = nameof(key);

        var expected = DateTime.Now;
        var dictionary = new Dictionary<string, object>
        {
            [key] = expected
        };

        var exists = dictionary.TryGetValue<DateTime?>(key, out var value);
        Assert.True(exists);
        Assert.Equal(expected, value);
    }

    [Fact]
    public void TryGetValue_KeyInvalid()
    {
        const string key = nameof(key);
        const string otherKey = nameof(otherKey);

        var dictionary = new Dictionary<string, object>
        {
            [key] = DateTime.Now
        };

        var exists = dictionary.TryGetValue<DateTime>(otherKey, out var value);
        Assert.False(exists);
        Assert.Equal(default, value);
    }

    [Fact]
    public void TryGetValue_TypeInvalid()
    {
        const string key = nameof(key);

        var dictionary = new Dictionary<string, object>
        {
            [key] = DateTime.Now
        };

        var exists = dictionary.TryGetValue<int>(key, out var value);
        Assert.False(exists);
        Assert.Equal(default, value);
    }
}
