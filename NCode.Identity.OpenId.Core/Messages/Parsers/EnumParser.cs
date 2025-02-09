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
using Microsoft.Extensions.Primitives;
using NCode.Identity.OpenId.Environments;
using NCode.Identity.OpenId.Errors;
using NCode.Identity.OpenId.Messages.Parameters;

namespace NCode.Identity.OpenId.Messages.Parsers;

/// <summary>
/// Provides an implementation of <see cref="IParameterParser{T}"/> that can parse <see cref="Enum"/> values.
/// </summary>
/// <typeparam name="T">The type of the <see cref="Enum"/> to parse.</typeparam>
public class EnumParser<T> : ParameterParser<T>
    where T : struct, Enum
{
    /// <summary>
    /// Gets the singleton instance for <see cref="EnumParser{T}"/>.
    /// </summary>
    public static EnumParser<T> Singleton { get; } = new();

    private static JsonNamingPolicy JsonNamingPolicy => JsonNamingPolicy.SnakeCaseLower;
    private static bool HasFlagsAttribute { get; } = typeof(T).IsDefined(typeof(FlagsAttribute), false);

    private static Dictionary<string, T> NameMap { get; } = Enum
        .GetValues<T>()
        .ToDictionary(
            ConvertToString,
            value => value,
            StringComparer.Ordinal
        );

    private static string ConvertToString(T value)
    {
        return JsonNamingPolicy.ConvertName(value.ToString());
    }

    private static bool TryParse(string value, out T result)
    {
        return NameMap.TryGetValue(value, out result) || Enum.TryParse(value, ignoreCase: false, out result);
    }

    /// <inheritdoc/>
    public override StringValues Format(
        OpenIdEnvironment openIdEnvironment,
        ParameterDescriptor descriptor,
        T parsedValue
    )
    {
        if (!HasFlagsAttribute)
        {
            return ConvertToString(parsedValue);
        }

        var values = Enum
            .GetValues<T>()
            .Where(flagValue => parsedValue.HasFlag(flagValue))
            .Select(ConvertToString);

        return string.Join(Separator, values);
    }

    /// <inheritdoc/>
    public override T Parse(
        OpenIdEnvironment openIdEnvironment,
        ParameterDescriptor descriptor,
        StringValues stringValues
    )
    {
        switch (stringValues.Count)
        {
            case 0 when descriptor.AllowMissingStringValues:
                return default;

            case 0:
                throw openIdEnvironment
                    .ErrorFactory
                    .MissingParameter(descriptor.ParameterName)
                    .AsException();

            case > 1 when !descriptor.AllowMultipleStringValues:
                throw openIdEnvironment
                    .ErrorFactory
                    .TooManyParameterValues(descriptor.ParameterName)
                    .AsException();

            default:
                var valueToParse = HasFlagsAttribute ?
                    string.Join(',', stringValues.AsEnumerable().SelectMany(stringValue => (stringValue ?? string.Empty).Split(Separator))) :
                    stringValues.ToString();

                if (!TryParse(valueToParse, out var parsedValue))
                {
                    throw openIdEnvironment
                        .ErrorFactory
                        .InvalidParameterValue(descriptor.ParameterName)
                        .AsException();
                }

                return parsedValue;
        }
    }
}
