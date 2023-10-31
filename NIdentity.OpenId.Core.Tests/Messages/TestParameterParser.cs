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
using Microsoft.Extensions.Primitives;
using NIdentity.OpenId.Messages.Parameters;
using NIdentity.OpenId.Messages.Parsers;

namespace NIdentity.OpenId.Core.Tests.Messages;

internal delegate Parameter ReadJsonDelegate(ref Utf8JsonReader reader, OpenIdContext context, ParameterDescriptor descriptor, JsonSerializerOptions options);

internal delegate void WriteJsonDelegate(Utf8JsonWriter writer, OpenIdContext context, Parameter parameter, JsonSerializerOptions options);

internal interface ITestParameterParser
{
    StringValues Serialize(OpenIdContext context, string? value);

    string Parse(OpenIdContext context, ParameterDescriptor descriptor, StringValues stringValues, bool ignoreErrors = false);
}

internal class TestParameterParser : ParameterParser<string>
{
    private ITestParameterParser InnerParser { get; }
    private ReadJsonDelegate? ReadJsonDelegate { get; }
    private WriteJsonDelegate? WriteJsonDelegate { get; }

    public TestParameterParser(ITestParameterParser innerParser, ReadJsonDelegate? readJsonDelegate, WriteJsonDelegate? writeJsonDelegate)
    {
        InnerParser = innerParser;
        ReadJsonDelegate = readJsonDelegate;
        WriteJsonDelegate = writeJsonDelegate;
    }

    public override StringValues Serialize(OpenIdContext context, string? value)
    {
        return InnerParser.Serialize(context, value);
    }

    public override string Parse(OpenIdContext context, ParameterDescriptor descriptor, StringValues stringValues)
    {
        return InnerParser.Parse(context, descriptor, stringValues);
    }

    public override Parameter Read(ref Utf8JsonReader reader, OpenIdContext context, ParameterDescriptor descriptor, JsonSerializerOptions options) =>
        ReadJsonDelegate?.Invoke(ref reader, context, descriptor, options) ??
        new Parameter<string>
        {
            Descriptor = descriptor,
            StringValues = string.Empty,
            ParsedValue = string.Empty
        };

    public override void Write(Utf8JsonWriter writer, OpenIdContext context, Parameter parameter, JsonSerializerOptions options)
    {
        WriteJsonDelegate?.Invoke(writer, context, parameter, options);
    }
}
