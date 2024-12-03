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

using JetBrains.Annotations;
using Microsoft.Extensions.Primitives;
using NCode.Identity.OpenId.Environments;

namespace NCode.Identity.OpenId.Messages.Parameters;

/// <summary>
/// Contains the parsed value and string values from which a parameter was parsed from.
/// </summary>
[PublicAPI]
public abstract class Parameter
{
    /// <summary>
    /// Gets the <see cref="ParameterDescriptor"/> that describes this parameter.
    /// </summary>
    public required ParameterDescriptor Descriptor { get; init; }

    /// <summary>
    /// Gets the <see cref="StringValues"/> that this parameter was loaded with.
    /// </summary>
    public required StringValues StringValues { get; init; }

    /// <summary>
    /// Clones this <see cref="Parameter"/> instance.
    /// </summary>
    /// <returns>The newly cloned <see cref="Parameter"/> instance.</returns>
    public abstract Parameter Clone();

    /// <summary>
    /// Helper method to parse and load a <see cref="Parameter"/> given its string values.
    /// </summary>
    /// <param name="openIdEnvironment">The <see cref="OpenIdEnvironment"/> to use while loading the parameter.</param>
    /// <param name="parameterName">The name of parameter.</param>
    /// <param name="stringValues">The string values to parse for the parameter.</param>
    /// <returns>The newly parsed and loaded parameter.</returns>
    public static Parameter Load(
        OpenIdEnvironment openIdEnvironment,
        string parameterName,
        IEnumerable<string> stringValues)
    {
        return Load(openIdEnvironment, parameterName, stringValues.ToArray());
    }

    /// <summary>
    /// Helper method to parse and load a <see cref="Parameter"/> given its string values.
    /// </summary>
    /// <param name="openIdEnvironment">The <see cref="OpenIdEnvironment"/> to use while loading the parameter.</param>
    /// <param name="parameterName">The name of parameter.</param>
    /// <param name="stringValues">The string values to parse for the parameter.</param>
    /// <returns>The newly parsed and loaded parameter.</returns>
    public static Parameter Load(
        OpenIdEnvironment openIdEnvironment,
        string parameterName,
        StringValues stringValues)
    {
        var descriptor = openIdEnvironment.KnownParameters.TryGet(parameterName, out var knownParameter) ?
            new ParameterDescriptor(knownParameter) :
            new ParameterDescriptor(parameterName);

        return descriptor.Loader.Load(openIdEnvironment, descriptor, stringValues);
    }
}

/// <summary>
/// Contains the parsed value and string values from which a parameter was parsed from.
/// </summary>
/// <typeparam name="T">The type of the parameter's parsed value.</typeparam>
[PublicAPI]
public class Parameter<T> : Parameter
{
    /// <summary>
    /// Gets the value that this parameter was parsed with.
    /// </summary>
    public T? ParsedValue { get; init; }

    /// <inheritdoc />
    public override Parameter Clone() => new Parameter<T>
    {
        Descriptor = Descriptor,
        StringValues = StringValues,
        ParsedValue = ParsedValue
    };
}
