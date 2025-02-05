#region Copyright Preamble

// Copyright @ 2025 NCode Group
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
using JetBrains.Annotations;
using Microsoft.Extensions.Primitives;
using NCode.Identity.OpenId.Environments;
using NCode.Identity.OpenId.Messages.Parameters;
using NCode.Identity.OpenId.Messages.Parsers;

namespace NCode.Identity.OpenId.Tests.Messages.Parsers;

[PublicAPI]
internal class TestParameterParser<T> : ParameterParser<T>
{
    public CreateParameterDelegate? CreateCallback { get; set; }
    public SerializeParameterDelegate<T>? SerializeCallback { get; set; }
    public ParseParameterDelegate<T>? ParseCallback { get; set; }
    public Func<object?>? DeserializeCallback { get; set; }

    public override IParameter<T1> Create<T1>(
        OpenIdEnvironment openIdEnvironment,
        ParameterDescriptor descriptor,
        StringValues stringValues,
        T1? parsedValue
    ) where T1 : default =>
        (IParameter<T1>?)CreateCallback?.Invoke(openIdEnvironment, descriptor, stringValues, parsedValue) ??
        throw new NotImplementedException();

    public override StringValues Serialize(
        OpenIdEnvironment openIdEnvironment,
        ParameterDescriptor descriptor,
        T? parsedValue
    )
    {
        if (SerializeCallback == null)
            throw new NotImplementedException();
        return SerializeCallback(openIdEnvironment, descriptor, parsedValue);
    }

    public override T Parse(
        OpenIdEnvironment openIdEnvironment,
        ParameterDescriptor descriptor,
        StringValues stringValues
    )
    {
        if (ParseCallback == null)
            throw new NotImplementedException();
        return ParseCallback(openIdEnvironment, descriptor, stringValues);
    }

    protected override T1? Deserialize<T1>(
        ref Utf8JsonReader reader,
        JsonSerializerOptions options
    ) where T1 : default
    {
        if (DeserializeCallback == null)
            throw new NotImplementedException();
        return (T1?)DeserializeCallback();
    }
}
