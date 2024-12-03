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
using NCode.Identity.OpenId.Environments;
using NCode.Identity.OpenId.Messages.Parameters;
using NCode.Identity.OpenId.Messages.Parsers;

namespace NCode.Identity.OpenId.Tests.Messages;

internal delegate Parameter ReadJsonDelegate(ref Utf8JsonReader reader, OpenIdEnvironment environment, ParameterDescriptor descriptor, JsonSerializerOptions options);

internal delegate void WriteJsonDelegate(Utf8JsonWriter writer, OpenIdEnvironment environment, Parameter parameter, JsonSerializerOptions options);

internal interface ITestParameterParser
{
    StringValues Serialize(OpenIdEnvironment environment, string? value);

    string Parse(OpenIdEnvironment environment, ParameterDescriptor descriptor, StringValues stringValues, bool ignoreErrors = false);
}

internal class TestParameterParser(
    ITestParameterParser innerParser,
    ReadJsonDelegate? readJsonDelegate,
    WriteJsonDelegate? writeJsonDelegate
) : ParameterParser<string>
{
    private ITestParameterParser InnerParser { get; } = innerParser;
    private ReadJsonDelegate? ReadJsonDelegate { get; } = readJsonDelegate;
    private WriteJsonDelegate? WriteJsonDelegate { get; } = writeJsonDelegate;

    public override StringValues Serialize(OpenIdEnvironment openIdEnvironment, ParameterDescriptor descriptor, string? parsedValue)
    {
        return InnerParser.Serialize(openIdEnvironment, parsedValue);
    }

    public override string Parse(OpenIdEnvironment openIdEnvironment, ParameterDescriptor descriptor, StringValues stringValues)
    {
        return InnerParser.Parse(openIdEnvironment, descriptor, stringValues);
    }

    public override Parameter Read(ref Utf8JsonReader reader, OpenIdEnvironment openIdEnvironment, ParameterDescriptor descriptor, JsonSerializerOptions options) =>
        ReadJsonDelegate?.Invoke(ref reader, openIdEnvironment, descriptor, options) ??
        new Parameter<string>
        {
            Descriptor = descriptor,
            StringValues = string.Empty,
            ParsedValue = string.Empty
        };

    public override void Write(Utf8JsonWriter writer, OpenIdEnvironment openIdEnvironment, Parameter parameter, JsonSerializerOptions options)
    {
        WriteJsonDelegate?.Invoke(writer, openIdEnvironment, parameter, options);
    }
}
