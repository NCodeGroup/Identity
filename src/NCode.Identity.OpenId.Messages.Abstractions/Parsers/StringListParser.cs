// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

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
        List<string>? parsedValue
    )
    {
        if (parsedValue is null)
            return StringValues.Empty;

        if (parsedValue.Count == 0)
            return StringValues.Empty;

        var values = descriptor.SortStringValues ? parsedValue.Order() : parsedValue.AsEnumerable();

        return values.ToArray();
    }

    /// <inheritdoc/>
    public override List<string>? Parse(
        OpenIdEnvironment openIdEnvironment,
        ParameterDescriptor descriptor,
        StringValues stringValues
    )
    {
        // ReSharper disable once ConvertSwitchStatementToSwitchExpression
        // That makes the code unreadable.
        switch (stringValues.Count)
        {
            case 0 when descriptor.AllowMissingStringValues:
                return null;

            case 0:
                throw openIdEnvironment
                    .ErrorFactory.MissingParameter(descriptor.ParameterName)
                    .AsException();
        }

        var results = new List<string>();

        foreach (var stringValue in stringValues)
        {
            var stringSpan = stringValue.AsSpan();
            foreach (var range in stringSpan.Split(OpenIdProtocol.ParameterSeparatorChar))
            {
                var part = stringSpan[range];
                results.Add(part.ToString());
            }
        }

        return results;
    }

    /// <inheritdoc/>
    [return: NotNullIfNotNull(nameof(value))]
    public override List<string>? Clone(List<string>? value) => value?.ToList();
}
