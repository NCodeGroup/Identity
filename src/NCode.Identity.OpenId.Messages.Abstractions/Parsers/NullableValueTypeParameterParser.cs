// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using Microsoft.Extensions.Primitives;
using NCode.Identity.OpenId.Environments;
using NCode.Identity.OpenId.Messages.Parameters;

namespace NCode.Identity.OpenId.Messages.Parsers;

/// <summary>
/// Provides an implementation of <see cref="IParameterParser{T}"/> that parses nullable value types.
/// </summary>
/// <typeparam name="T">The type of parameter to parse.</typeparam>
[PublicAPI]
public class NullableValueTypeParameterParser<T>(IParameterParser<T> parser) : ParameterParser<T?>
    where T : struct
{
    private IParameterParser<T> Parser { get; } = parser;

    /// <inheritdoc/>
    public override StringValues GetStringValues(
        OpenIdEnvironment openIdEnvironment,
        ParameterDescriptor descriptor,
        T? parsedValue
    ) =>
        parsedValue.HasValue
            ? Parser.GetStringValues(openIdEnvironment, descriptor, parsedValue.Value)
            : StringValues.Empty;

    /// <inheritdoc/>
    public override T? Parse(
        OpenIdEnvironment openIdEnvironment,
        ParameterDescriptor descriptor,
        StringValues stringValues
    ) =>
        stringValues.Count == 0 && descriptor.AllowMissingStringValues
            ? null
            : Parser.Parse(openIdEnvironment, descriptor, stringValues);
}
