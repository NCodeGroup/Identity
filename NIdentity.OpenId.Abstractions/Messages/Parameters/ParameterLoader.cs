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
using NIdentity.OpenId.Endpoints;

namespace NIdentity.OpenId.Messages.Parameters;

/// <summary>
/// Provides the ability load a <see cref="Parameter"/> given its values.
/// </summary>
public class ParameterLoader
{
    /// <summary>
    /// Gets a default implementation of <see cref="ParameterLoader"/> that simply returns a newly initialized <see cref="Parameter"/> object.
    /// </summary>
    public static ParameterLoader Default { get; } = new();

    /// <summary>
    /// Loads a <see cref="Parameter"/> given its string values.
    /// </summary>
    /// <param name="context">The <see cref="OpenIdContext"/> to use while loading the parameter.</param>
    /// <param name="descriptor">The <see cref="ParameterDescriptor"/> that describes the parameter to load.</param>
    /// <param name="stringValues">The string values to parse for the parameter.</param>
    /// <param name="ignoreErrors">Specifies whether errors during parsing should be ignored.</param>
    /// <returns>The newly loaded parameter.</returns>
    public virtual Parameter Load(
        OpenIdContext context,
        ParameterDescriptor descriptor,
        StringValues stringValues,
        bool ignoreErrors = false)
    {
        return new Parameter<StringValues>(descriptor, stringValues, stringValues);
    }

    /// <summary>
    /// Loads a <see cref="Parameter"/> given its string values and parsed value.
    /// </summary>
    /// <param name="context">The <see cref="OpenIdContext"/> to use while loading the parameter.</param>
    /// <param name="descriptor">The <see cref="ParameterDescriptor"/> that describes the parameter to load.</param>
    /// <param name="stringValues">The string values for the parameter.</param>
    /// <param name="parsedValue">The parsed value for the parameter.</param>
    /// <returns>The newly loaded parameter.</returns>
    public virtual Parameter<T> Load<T>(
        OpenIdContext context,
        ParameterDescriptor descriptor,
        StringValues stringValues,
        T? parsedValue)
    {
        return new Parameter<T>(descriptor, stringValues, parsedValue);
    }
}
