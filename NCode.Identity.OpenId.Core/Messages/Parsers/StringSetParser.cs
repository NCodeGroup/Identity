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
using NCode.Identity.OpenId.Messages.Parameters;
using NCode.Identity.OpenId.Results;
using NCode.Identity.OpenId.Servers;

namespace NCode.Identity.OpenId.Messages.Parsers;

/// <summary>
/// Provides an implementation of <see cref="ParameterParser{T}"/> that parses string collections which are separated by
/// the space ' ' character.
/// </summary>
public class StringSetParser : ParameterParser<IReadOnlyCollection<string>?>
{
    /// <inheritdoc/>
    public override StringValues Serialize(
        OpenIdServer openIdServer,
        IReadOnlyCollection<string>? value)
    {
        if (value is null)
            return StringValues.Empty;

        if (value.Count == 0)
            return StringValues.Empty;

        return string.Join(Separator, value);
    }

    /// <inheritdoc/>
    public override IReadOnlyCollection<string>? Parse(
        OpenIdServer openIdServer,
        ParameterDescriptor descriptor,
        StringValues stringValues) =>
        stringValues.Count switch
        {
            0 when descriptor.Optional => null,
            0 => throw openIdServer.ErrorFactory.MissingParameter(descriptor.ParameterName).AsException(),
            > 1 when descriptor.AllowMultipleStringValues =>
                stringValues.SelectMany(stringValue => stringValue!.Split(Separator)).ToHashSet(StringComparer.Ordinal),
            > 1 => throw openIdServer.ErrorFactory.TooManyParameterValues(descriptor.ParameterName).AsException(),
            _ => stringValues[0]!.Split(Separator).ToHashSet(StringComparer.Ordinal)
        };
}
