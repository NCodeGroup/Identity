// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Messages.Parsers;

/// <summary>
/// Provides extension methods for <see cref="IParameterParser{T}"/>.
/// </summary>
[PublicAPI]
public static class ParameterParserExtensions
{
    extension<T>(IParameterParser<T> parser)
        where T : struct
    {
        /// <summary>
        /// Creates a new <see cref="IParameterParser{T}"/> that can parse nullable value types.
        /// </summary>
        public IParameterParser<T?> AsNullableValue() =>
            new NullableValueTypeParameterParser<T>(parser);
    }
}
