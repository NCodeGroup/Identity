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

using Microsoft.Extensions.Primitives;
using NIdentity.OpenId.Messages.Parameters;
using NIdentity.OpenId.Results;

namespace NIdentity.OpenId.Messages.Parsers;

/// <summary>
/// Provides an implementation of <see cref="ParameterParser{T}"/> that can parse <see cref="string"/> values.
/// </summary>
public class StringParser : ParameterParser<string?>
{
    /// <inheritdoc/>
    public override StringValues Serialize(OpenIdContext context, string? value)
    {
        return value;
    }

    /// <inheritdoc/>
    public override string? Parse(
        OpenIdContext context,
        ParameterDescriptor descriptor,
        StringValues stringValues) =>
        stringValues.Count switch
        {
            0 when descriptor.Optional => null,
            0 => throw context.ErrorFactory.MissingParameter(descriptor.ParameterName).AsException(),
            > 1 when descriptor.AllowMultipleValues => string.Join(Separator, stringValues!),
            > 1 => throw context.ErrorFactory.TooManyParameterValues(descriptor.ParameterName).AsException(),
            _ => stringValues[0]
        };
}
