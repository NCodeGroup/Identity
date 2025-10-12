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

using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text.Json;
using JetBrains.Annotations;
using Microsoft.Extensions.Primitives;
using NCode.Identity.OpenId.Environments;
using NCode.Identity.OpenId.Errors;
using NCode.Identity.OpenId.Messages.Parameters;

namespace NCode.Identity.OpenId.Messages.Parsers;

/// <summary>
/// Provides an implementation of <see cref="IParameterParser{T}"/> that can parse <see cref="Enum"/> values.
/// </summary>
/// <typeparam name="T">The type of the <see cref="Enum"/> to parse.</typeparam>
[PublicAPI]
public class EnumParser<T> : ParameterParser<T>
    where T : struct, Enum
{
    /// <summary>
    /// Gets the singleton instance for <see cref="EnumParser{T}"/>.
    /// </summary>
    public static EnumParser<T> Singleton { get; } = new();

    private static JsonNamingPolicy JsonNamingPolicy => JsonNamingPolicy.SnakeCaseLower;
    private static bool HasFlagsAttribute { get; } = typeof(T).IsDefined(typeof(FlagsAttribute), false);

    private static T[] Values { get; } = Enum.GetValues<T>();

    private static Dictionary<string, T> NameMap { get; } =
        Values.ToDictionary(
            ConvertToString,
            value => value,
            StringComparer.OrdinalIgnoreCase
        );

    private static Dictionary<string, T>.AlternateLookup<ReadOnlySpan<char>> NameLookup { get; } =
        NameMap.GetAlternateLookup<ReadOnlySpan<char>>();

    private static string ConvertToString(T value)
    {
        return JsonNamingPolicy.ConvertName(value.ToString());
    }

    private static bool TryParseValue(ReadOnlySpan<char> value, out T result)
    {
        return NameLookup.TryGetValue(value, out result) || Enum.TryParse(value, ignoreCase: true, out result);
    }

    /// <inheritdoc/>
    public override StringValues GetStringValues(
        OpenIdEnvironment openIdEnvironment,
        ParameterDescriptor descriptor,
        T parsedValue
    )
    {
        if (!HasFlagsAttribute)
        {
            return ConvertToString(parsedValue);
        }

        var stringValues = Values
            .Where(flagValue => parsedValue.HasFlag(flagValue))
            .Select(ConvertToString)
            .ToArray();

        return stringValues;
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

            case > 1 when !HasFlagsAttribute:
                throw openIdEnvironment
                    .ErrorFactory
                    .TooManyParameterValues(descriptor.ParameterName)
                    .AsException();
        }

        if (HasFlagsAttribute)
        {
            return ParseFlags(openIdEnvironment, descriptor, stringValues);
        }

        var stringValue = stringValues[0];
        Debug.Assert(stringValue is not null);

        return ParseValue(
            openIdEnvironment,
            descriptor,
            stringValue.AsSpan()
        );
    }

    private static T ParseValue(
        OpenIdEnvironment openIdEnvironment,
        ParameterDescriptor descriptor,
        ReadOnlySpan<char> value
    )
    {
        if (!TryParseValue(value, out var parsedValue))
        {
            throw openIdEnvironment
                .ErrorFactory
                .InvalidParameterValue(descriptor.ParameterName)
                .AsException();
        }

        return parsedValue;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static T ParseFlags(
        OpenIdEnvironment openIdEnvironment,
        ParameterDescriptor descriptor,
        StringValues stringValues
    )
    {
        Unsafe.SkipInit(out T result);

        var underlyingType = typeof(T).GetEnumUnderlyingType();

        if (underlyingType == typeof(byte))
        {
            ParseFlags(openIdEnvironment, descriptor, stringValues, out Unsafe.As<T, byte>(ref result));
        }
        else if (underlyingType == typeof(sbyte))
        {
            ParseFlags(openIdEnvironment, descriptor, stringValues, out Unsafe.As<T, sbyte>(ref result));
        }
        else if (underlyingType == typeof(short))
        {
            ParseFlags(openIdEnvironment, descriptor, stringValues, out Unsafe.As<T, short>(ref result));
        }
        else if (underlyingType == typeof(ushort))
        {
            ParseFlags(openIdEnvironment, descriptor, stringValues, out Unsafe.As<T, ushort>(ref result));
        }
        else if (underlyingType == typeof(int))
        {
            ParseFlags(openIdEnvironment, descriptor, stringValues, out Unsafe.As<T, int>(ref result));
        }
        else if (underlyingType == typeof(uint))
        {
            ParseFlags(openIdEnvironment, descriptor, stringValues, out Unsafe.As<T, uint>(ref result));
        }
        else if (underlyingType == typeof(long))
        {
            ParseFlags(openIdEnvironment, descriptor, stringValues, out Unsafe.As<T, long>(ref result));
        }
        else if (underlyingType == typeof(ulong))
        {
            ParseFlags(openIdEnvironment, descriptor, stringValues, out Unsafe.As<T, ulong>(ref result));
        }
        else
        {
            // we chose to not support the rare enum underlying types
            throw new InvalidOperationException("Unsupported enum underlying type encountered.");
        }

        return result;
    }

    private static void ParseFlags<TUnderlying>(
        OpenIdEnvironment openIdEnvironment,
        ParameterDescriptor descriptor,
        StringValues stringValues,
        out TUnderlying result
    )
        where TUnderlying : struct, INumber<TUnderlying>, IBitwiseOperators<TUnderlying, TUnderlying, TUnderlying>
    {
        TUnderlying accumulator = default;

        foreach (var stringValue in stringValues.AsEnumerable())
        {
            if (string.IsNullOrEmpty(stringValue))
                continue;

            var stringSpan = stringValue.AsSpan();
            foreach (var range in stringSpan.Split(OpenIdConstants.ParameterSeparatorChar))
            {
                var valueToParse = stringSpan[range];
                var parsedValue = ParseValue(openIdEnvironment, descriptor, valueToParse);

                accumulator |= Unsafe.As<T, TUnderlying>(ref parsedValue);
            }
        }

        result = accumulator;
    }
}
