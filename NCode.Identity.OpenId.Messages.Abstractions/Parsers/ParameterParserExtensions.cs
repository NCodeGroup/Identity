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

using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Messages.Parsers;

/// <summary>
/// Provides extension methods for <see cref="IParameterParser{T}"/>.
/// </summary>
[PublicAPI]
public static class ParameterParserExtensions
{
    /// <summary>
    /// Creates a new <see cref="IParameterParser{T}"/> that can parse nullable value types.
    /// </summary>
    /// <typeparam name="T">The type of parameter to parse.</typeparam>
    public static IParameterParser<T?> AsNullableValue<T>(this IParameterParser<T> parser)
        where T : struct =>
        new NullableValueTypeParameterParser<T>(parser);
}
