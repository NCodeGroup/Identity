// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using JetBrains.Annotations;
using Microsoft.Extensions.Primitives;
using NCode.Identity.OpenId.Environments;
using NCode.Identity.OpenId.Messages.Parameters;
using NCode.Identity.OpenId.Messages.Parsers;

namespace NCode.Identity.OpenId.Core.Tests.Messages.Parsers;

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
        IParameterParser<T1> parser,
        T1? parsedValue
    )
        where T1 : default
    {
        if (CreateCallback == null)
            throw new NotImplementedException();
        return (IParameter<T1>)CreateCallback(openIdEnvironment, descriptor, parser, parsedValue);
    }

    public override StringValues GetStringValues(
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

    protected override T1? Deserialize<T1>(ref Utf8JsonReader reader, JsonSerializerOptions options)
        where T1 : default
    {
        if (DeserializeCallback == null)
            throw new NotImplementedException();
        return (T1?)DeserializeCallback();
    }
}
