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

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;
using Microsoft.Extensions.Primitives;
using NCode.Identity.OpenId.Environments;
using NCode.Identity.OpenId.Errors;
using NCode.Identity.OpenId.Messages.Parameters;

namespace NCode.Identity.OpenId.Messages.Parsers;

/// <summary>
/// Provides an implementation of <see cref="IParameterParser{T}"/> that parses string collections which are separated by
/// the space ' ' character.
/// </summary>
[PublicAPI]
public class StringListParser : ParameterParser<List<string>>
{
    /// <inheritdoc/>
    public override StringValues GetStringValues(
        OpenIdEnvironment openIdEnvironment,
        ParameterDescriptor descriptor,
        List<string>? parsedValue)
    {
        if (parsedValue is null)
            return StringValues.Empty;

        if (parsedValue.Count == 0)
            return StringValues.Empty;

        var values = descriptor.SortStringValues ?
            parsedValue.Order() :
            parsedValue.AsEnumerable();

        return values.ToArray();
    }

    /// <inheritdoc/>
    public override List<string>? Parse(
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
        }

        var results = new List<string>();

        foreach (var stringValue in stringValues)
        {
            var stringSpan = stringValue.AsSpan();
            foreach (var range in stringSpan.Split(OpenIdConstants.ParameterSeparatorChar))
            {
                var part = stringSpan[range];
                results.Add(part.ToString());
            }
        }

        return results;
    }

    /// <inheritdoc/>
    [return: NotNullIfNotNull(nameof(value))]
    public override List<string>? Clone(List<string>? value) =>
        value?.ToList();
}
