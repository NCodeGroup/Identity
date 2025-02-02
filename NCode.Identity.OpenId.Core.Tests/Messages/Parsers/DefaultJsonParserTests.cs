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

using System.Buffers;
using System.Text.Json;
using Moq;
using NCode.Identity.OpenId.Environments;
using NCode.Identity.OpenId.Messages.Parameters;
using NCode.Identity.OpenId.Messages.Parsers;
using Xunit;

namespace NCode.Identity.OpenId.Tests.Messages.Parsers;

public class DefaultJsonParserTests : IDisposable
{
    private MockRepository MockRepository { get; }
    private Mock<OpenIdEnvironment> MockOpenIdEnvironment { get; }

    public DefaultJsonParserTests()
    {
        MockRepository = new MockRepository(MockBehavior.Strict);
        MockOpenIdEnvironment = MockRepository.Create<OpenIdEnvironment>();
    }

    public void Dispose()
    {
        MockRepository.Verify();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void Singleton()
    {
        var parser = DefaultJsonParser.Singleton;

        Assert.Same(DefaultJsonParser.Singleton, parser);
    }

    [Fact]
    public void Load_ThenIsValid()
    {
        var parser = new DefaultJsonParser();

        const string parameterName = "parameterName";

        var jsonSerializerOptions = new JsonSerializerOptions();

        var descriptor = new ParameterDescriptor(parameterName);

        var buffer = new ArrayBufferWriter<byte>();
        var writer = new Utf8JsonWriter(buffer);

        var expectedValue = new TestNestedObject { NestedPropertyName1 = "NestedPropertyValue" };
        var expectedStringValue = JsonSerializer.Serialize(expectedValue);
        JsonSerializer.Serialize(writer, expectedValue);

        var reader = new Utf8JsonReader(buffer.WrittenSpan);

        var parameter = parser.Read(ref reader, MockOpenIdEnvironment.Object, descriptor, jsonSerializerOptions);
        var typedParameter = Assert.IsType<Parameter<JsonElement>>(parameter);

        Assert.Equal(expectedStringValue, typedParameter.StringValues);
        Assert.IsType<JsonElement>(typedParameter.ParsedValue);
        Assert.Equal(expectedStringValue, JsonSerializer.Serialize(typedParameter.ParsedValue));
    }
}
