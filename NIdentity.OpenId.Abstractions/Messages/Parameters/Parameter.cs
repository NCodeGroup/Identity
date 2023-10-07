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

namespace NIdentity.OpenId.Messages.Parameters;

/// <summary>
/// Contains the parsed value and string values from which a parameter was parsed from.
/// </summary>
public abstract class Parameter
{
    /// <summary>
    /// Gets the <see cref="ParameterDescriptor"/> that describes this parameter.
    /// </summary>
    public ParameterDescriptor Descriptor { get; }

    /// <summary>
    /// Gets the <see cref="StringValues"/> that this parameter was loaded with.
    /// </summary>
    public StringValues StringValues { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="Parameter"/> class.
    /// </summary>
    /// <param name="descriptor">The <see cref="ParameterDescriptor"/> that describes this parameter.</param>
    /// <param name="stringValues">The <see cref="StringValues"/> that this parameter was loaded with.</param>
    protected Parameter(ParameterDescriptor descriptor, StringValues stringValues)
    {
        Descriptor = descriptor;
        StringValues = stringValues;
    }

    /// <summary>
    /// Helper method to parse and load a <see cref="Parameter"/> given its string values.
    /// </summary>
    /// <param name="context">The <see cref="IOpenIdMessageContext"/> to use while loading the parameter.</param>
    /// <param name="parameterName">The name of parameter.</param>
    /// <param name="stringValues">The string values to parse for the parameter.</param>
    /// <param name="ignoreErrors">Specifies whether errors during parsing should be ignored.</param>
    /// <returns>The newly parsed and loaded parameter.</returns>
    public static Parameter Load(IOpenIdMessageContext context, string parameterName, IEnumerable<string> stringValues, bool ignoreErrors = false)
    {
        return Load(context, parameterName, stringValues.ToArray(), ignoreErrors);
    }

    /// <summary>
    /// Helper method to parse and load a <see cref="Parameter"/> given its string values.
    /// </summary>
    /// <param name="context">The <see cref="IOpenIdMessageContext"/> to use while loading the parameter.</param>
    /// <param name="parameterName">The name of parameter.</param>
    /// <param name="stringValues">The string values to parse for the parameter.</param>
    /// <param name="ignoreErrors">Specifies whether errors during parsing should be ignored.</param>
    /// <returns>The newly parsed and loaded parameter.</returns>
    public static Parameter Load(IOpenIdMessageContext context, string parameterName, StringValues stringValues, bool ignoreErrors = false)
    {
        var descriptor = context.TryGetKnownParameter(parameterName, out var knownParameter) ?
            new ParameterDescriptor(knownParameter) :
            new ParameterDescriptor(parameterName);

        return descriptor.Loader.Load(context, descriptor, stringValues, ignoreErrors);
    }
}

/// <summary>
/// Contains the parsed value and string values from which a parameter was parsed from.
/// </summary>
/// <typeparam name="T">The type of the parameter's parsed value.</typeparam>
public class Parameter<T> : Parameter
{
    /// <summary>
    /// Gets the value that this parameter was parsed with.
    /// </summary>
    public T? ParsedValue { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="Parameter"/> struct.
    /// </summary>
    /// <param name="descriptor">The <see cref="ParameterDescriptor"/> that describes this parameter.</param>
    /// <param name="stringValues">The <see cref="StringValues"/> that this parameter was loaded with.</param>
    /// <param name="parsedValue">The value that this parameter was parsed with.</param>
    public Parameter(ParameterDescriptor descriptor, StringValues stringValues, T? parsedValue = default)
        : base(descriptor, stringValues)
    {
        ParsedValue = parsedValue;
    }
}
