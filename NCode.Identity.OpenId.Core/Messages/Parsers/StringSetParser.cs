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
using NCode.Identity.OpenId.Environments;
using NCode.Identity.OpenId.Errors;
using NCode.Identity.OpenId.Messages.Parameters;

namespace NCode.Identity.OpenId.Messages.Parsers;

/// <summary>
/// Provides an implementation of <see cref="IParameterParser{T}"/> that parses string collections which are separated by
/// the space ' ' character.
/// </summary>
public class StringSetParser : ParameterParser<IReadOnlyCollection<string>?>
{
    /// <inheritdoc/>
    public override StringValues Format(
        OpenIdEnvironment openIdEnvironment,
        ParameterDescriptor descriptor,
        IReadOnlyCollection<string>? parsedValue)
    {
        if (parsedValue is null)
            return StringValues.Empty;

        if (parsedValue.Count == 0)
            return StringValues.Empty;

        var values = descriptor.SortStringValues ?
            parsedValue.Order() :
            parsedValue.AsEnumerable();

        return string.Join(Separator, values);
    }

    /// <inheritdoc/>
    public override IReadOnlyCollection<string>? Parse(
        OpenIdEnvironment openIdEnvironment,
        ParameterDescriptor descriptor,
        StringValues stringValues)
    {
        // ReSharper disable once ConvertSwitchStatementToSwitchExpression
        // That makes the code unreadable.
        switch (stringValues.Count)
        {
            case 0 when descriptor.AllowMissingStringValues:
                return null;

            case 0:
                throw openIdEnvironment
                    .ErrorFactory
                    .MissingParameter(descriptor.ParameterName)
                    .AsException();

            case > 1 when descriptor.AllowMultipleStringValues:
                return stringValues
                    .SelectMany(stringValue => stringValue!.Split(Separator))
                    .ToHashSet(StringComparer.Ordinal);

            case > 1:
                throw openIdEnvironment
                    .ErrorFactory
                    .TooManyParameterValues(descriptor.ParameterName)
                    .AsException();

            default:
                return stringValues[0]!
                    .Split(Separator)
                    .ToHashSet(StringComparer.Ordinal);
        }
    }
}
