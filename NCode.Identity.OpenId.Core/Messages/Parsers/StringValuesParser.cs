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
using Microsoft.Extensions.Primitives;
using NCode.Identity.OpenId.Environments;
using NCode.Identity.OpenId.Errors;
using NCode.Identity.OpenId.Messages.Parameters;

namespace NCode.Identity.OpenId.Messages.Parsers;

/// <summary>
/// Provides an implementation of <see cref="IParameterParser{T}"/> that can parse <see cref="StringValues"/> values.
/// </summary>
[PublicAPI]
public class StringValuesParser : ParameterParser<StringValues>
{
    /// <inheritdoc/>
    public override StringValues GetStringValues(
        OpenIdEnvironment openIdEnvironment,
        ParameterDescriptor descriptor,
        StringValues parsedValue
    ) => parsedValue;

    /// <inheritdoc/>
    public override StringValues Parse(
        OpenIdEnvironment openIdEnvironment,
        ParameterDescriptor descriptor,
        StringValues stringValues
    )
    {
        if (stringValues.Count == 0 && !descriptor.AllowMissingStringValues)
        {
            throw openIdEnvironment
                .ErrorFactory
                .MissingParameter(descriptor.ParameterName)
                .AsException();
        }

        return stringValues;
    }
}
