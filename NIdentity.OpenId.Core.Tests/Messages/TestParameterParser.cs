#region Copyright Preamble

//
//    Copyright @ 2021 NCode Group
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
using NIdentity.OpenId.Messages;
using NIdentity.OpenId.Messages.Parameters;
using NIdentity.OpenId.Messages.Parsers;

namespace NIdentity.OpenId.Core.Tests.Messages;

internal delegate Parameter LoadJsonDelegate(IOpenIdMessageContext context, ParameterDescriptor descriptor, ref Utf8JsonReader reader, JsonSerializerOptions options);

internal interface ITestParameterParser
{
    StringValues Serialize(IOpenIdMessageContext context, string value);

    string Parse(IOpenIdMessageContext context, ParameterDescriptor descriptor, StringValues stringValues);
}

internal class TestParameterParser : ParameterParser<string>, IJsonParser
{
    private readonly ITestParameterParser _innerParser;
    private readonly LoadJsonDelegate? _loadJsonDelegate;

    public TestParameterParser(ITestParameterParser innerParser, LoadJsonDelegate? loadJsonDelegate)
    {
        _innerParser = innerParser;
        _loadJsonDelegate = loadJsonDelegate;
    }

    public override StringValues Serialize(IOpenIdMessageContext context, string value)
    {
        return _innerParser.Serialize(context, value);
    }

    public override string Parse(IOpenIdMessageContext context, ParameterDescriptor descriptor, StringValues stringValues)
    {
        return _innerParser.Parse(context, descriptor, stringValues);
    }

    public Parameter Load(IOpenIdMessageContext context, ParameterDescriptor descriptor, ref Utf8JsonReader reader, JsonSerializerOptions options)
    {
        return _loadJsonDelegate?.Invoke(context, descriptor, ref reader, options) ?? new Parameter();
    }
}