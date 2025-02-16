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

using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using JetBrains.Annotations;
using Microsoft.Extensions.Primitives;
using NCode.Identity.OpenId.Environments;
using NCode.Identity.OpenId.Errors;
using NCode.Identity.OpenId.Messages.Parameters;

namespace NCode.Identity.OpenId.Messages.Parsers;

/// <summary>
/// Provides an implementation of <see cref="IParameterParser{T}"/> that can parse JSON payloads.
/// </summary>
/// <typeparam name="T">
/// The type of object to parse from JSON. If the type is an interface, then a custom <see cref="JsonConverter{T}"/>
/// must be configured within <see cref="OpenIdEnvironment.JsonSerializerOptions"/>.
/// </typeparam>
[PublicAPI]
public class JsonParser<T> : ParameterParser<T>
{
    /// <inheritdoc/>
    public override StringValues GetStringValues(
        OpenIdEnvironment openIdEnvironment,
        ParameterDescriptor descriptor,
        T? parsedValue
    ) =>
        JsonSerializer.Serialize(parsedValue, openIdEnvironment.JsonSerializerOptions);

    /// <inheritdoc/>
    public override T? Parse(
        OpenIdEnvironment openIdEnvironment,
        ParameterDescriptor descriptor,
        StringValues stringValues)
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

            case > 1:
                throw openIdEnvironment
                    .ErrorFactory
                    .TooManyParameterValues(descriptor.ParameterName)
                    .AsException();
        }

        var stringValue = stringValues[0];
        Debug.Assert(stringValue is not null);

        try
        {
            return JsonSerializer.Deserialize<T>(stringValue, openIdEnvironment.JsonSerializerOptions);
        }
        catch (Exception exception)
        {
            throw openIdEnvironment.ErrorFactory
                .FailedToDeserializeJson(OpenIdConstants.ErrorCodes.InvalidRequest)
                .WithException(exception)
                .AsException();
        }
    }
}
