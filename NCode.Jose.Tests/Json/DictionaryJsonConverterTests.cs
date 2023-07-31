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

using System.Text;
using System.Text.Json;
using NCode.Jose.Json;

namespace NCode.Jose.Tests.Json;

public class DictionaryJsonConverterTests
{
    [Fact]
    public void RoundTrip_Valid()
    {
        var converter = new DictionaryJsonConverter();

        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            Converters = { converter }
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
            [$"key{++i}"] = new object[] { "value2", false, -1234 }
        };

        input[$"key{++i}.nested"] = new Dictionary<string, object>(input);

        var expected = JsonSerializer.Serialize(input, options);

        using var stream = new MemoryStream();
        using var writer = new Utf8JsonWriter(stream);
        converter.Write(writer, input, options);

        var bytes = stream.ToArray();
        var json = Encoding.UTF8.GetString(bytes);
        Assert.Equal(expected, json);

        var reader = new Utf8JsonReader(bytes);
        var result = converter.Read(ref reader, typeof(object), options);
        Assert.Equal(expected, JsonSerializer.Serialize(result, options));
    }
}
