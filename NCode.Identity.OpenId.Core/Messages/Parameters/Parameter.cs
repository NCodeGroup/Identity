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

using System.Runtime.CompilerServices;
using JetBrains.Annotations;
using Microsoft.Extensions.Primitives;
using NCode.Identity.OpenId.Environments;

namespace NCode.Identity.OpenId.Messages.Parameters;

/// <summary>
/// Contains the parsed value and string values from which a parameter was parsed from.
/// </summary>
[PublicAPI]
public abstract class Parameter : IParameter
{
    /// <inheritdoc />
    public required ParameterDescriptor Descriptor { get; init; }

    /// <inheritdoc />
    public required StringValues StringValues { get; init; }

    /// <inheritdoc />
    public abstract object? GetParsedValue();

    /// <inheritdoc />
    public abstract IParameter Clone();

    /// <summary>
    /// Helper method to parse and load a <see cref="IParameter"/> given its string values.
    /// </summary>
    /// <param name="openIdEnvironment">The <see cref="OpenIdEnvironment"/> to use while loading the parameter.</param>
    /// <param name="parameterName">The name of parameter.</param>
    /// <param name="stringValues">The string values to parse for the parameter.</param>
    /// <returns>The newly parsed and loaded parameter.</returns>
    [OverloadResolutionPriority(-1)]
    public static IParameter Load(
        OpenIdEnvironment openIdEnvironment,
        string parameterName,
        params IEnumerable<string> stringValues)
    {
        return Load(openIdEnvironment, parameterName, stringValues.ToArray());
    }

    /// <summary>
    /// Helper method to parse and load a <see cref="IParameter"/> given its string values.
    /// </summary>
    /// <param name="openIdEnvironment">The <see cref="OpenIdEnvironment"/> to use while loading the parameter.</param>
    /// <param name="parameterName">The name of parameter.</param>
    /// <param name="stringValues">The string values to parse for the parameter.</param>
    /// <returns>The newly parsed and loaded parameter.</returns>
    public static IParameter Load(
        OpenIdEnvironment openIdEnvironment,
        string parameterName,
        StringValues stringValues)
    {
        var descriptor = openIdEnvironment.GetParameterDescriptor(parameterName);
        return descriptor.Loader.Load(openIdEnvironment, descriptor, stringValues);
    }

    /// <summary>
    /// Creates a <see cref="IParameter"/> given its string values and parsed value.
    /// </summary>
    /// <param name="openIdEnvironment">The <see cref="OpenIdEnvironment"/> to use while creating the parameter.</param>
    /// <param name="descriptor">The <see cref="ParameterDescriptor"/> that describes the parameter to create.</param>
    /// <param name="stringValues">The string values for the parameter.</param>
    /// <param name="parsedValue">The parsed value for the parameter.</param>
    /// <returns>The newly created parameter.</returns>
    public static IParameter<T> Create<T>(
        OpenIdEnvironment openIdEnvironment,
        ParameterDescriptor descriptor,
        StringValues stringValues,
        T? parsedValue
    ) => new Parameter<T>
    {
        Descriptor = descriptor,
        StringValues = stringValues,
        ParsedValue = parsedValue
    };
}

/// <summary>
/// Contains the parsed value and string values from which a parameter was parsed from.
/// </summary>
/// <typeparam name="T">The type of the parameter's parsed value.</typeparam>
[PublicAPI]
public class Parameter<T> : Parameter, IParameter<T>
{
    /// <inheritdoc />
    public T? ParsedValue { get; init; }

    /// <inheritdoc />
    public override object? GetParsedValue() => ParsedValue;

    /// <inheritdoc />
    public override Parameter Clone() => new Parameter<T>
    {
        Descriptor = Descriptor,
        StringValues = StringValues,
        ParsedValue = ParsedValue
    };
}
