#region Copyright Preamble

// Copyright @ 2024 NCode Group
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
using System.Text.Json.Serialization.Metadata;
using Microsoft.AspNetCore.DataProtection;
using NCode.Identity.OpenId.Errors;
using NCode.Identity.OpenId.Messages;
using NCode.Identity.OpenId.Messages.Parameters;
using NCode.Identity.OpenId.Messages.Parsers;
using NCode.Identity.OpenId.Serialization;
using NCode.PropertyBag;

namespace NCode.Identity.OpenId.Environments;

/// <summary>
/// Provides a default implementation of the <see cref="OpenIdEnvironment"/> abstraction.
/// </summary>
public class DefaultOpenIdEnvironment(
    IDataProtector dataProtector,
    IKnownParameterCollectionProvider knownParameterCollectionProvider,
    IOpenIdMessageFactorySelector openIdMessageFactorySelector,
    IEnumerable<IOpenIdJsonConverterProvider> jsonConverterProviders
) : OpenIdEnvironment, IOpenIdErrorFactory
{
    private IOpenIdMessageFactorySelector OpenIdMessageFactorySelector { get; } = openIdMessageFactorySelector;
    private IEnumerable<IOpenIdJsonConverterProvider> JsonConverterProviders { get; } = jsonConverterProviders;

    private JsonSerializerOptions? JsonSerializerOptionsOrNull { get; set; }

    /// <inheritdoc />
    public override JsonSerializerOptions JsonSerializerOptions => JsonSerializerOptionsOrNull ??= CreateJsonSerializerOptions();

    /// <inheritdoc />
    public override IDataProtector DataProtector { get; } = dataProtector;

    /// <inheritdoc />
    public override IKnownParameterCollection KnownParameters => knownParameterCollectionProvider.Collection;

    /// <inheritdoc />
    public override IOpenIdErrorFactory ErrorFactory => this;

    /// <inheritdoc />
    public override IPropertyBag PropertyBag { get; } = PropertyBagFactory.Create();

    /// <inheritdoc />
    public override ParameterDescriptor GetParameterDescriptor(string parameterName)
    {
        return KnownParameters.TryGet(parameterName, out var knownParameter) ?
            new ParameterDescriptor(knownParameter) :
            new ParameterDescriptor(parameterName, ParameterLoader.Default);
    }

    IOpenIdError IOpenIdErrorFactory.Create(string errorCode) =>
        CreateError(errorCode);

    /// <inheritdoc />
    public override IOpenIdError CreateError(string errorCode) =>
        new OpenIdError(this, errorCode);

    /// <inheritdoc />
    public override IOpenIdMessage CreateMessage(string typeDiscriminator, IEnumerable<IParameter> parameters) =>
        OpenIdMessageFactorySelector.GetFactory(typeDiscriminator).Create(this, parameters);

    private JsonSerializerOptions CreateJsonSerializerOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        };

        var jsonConverters = JsonConverterProviders.SelectMany(provider => provider.GetJsonConverters(this));
        foreach (var converter in jsonConverters)
        {
            options.Converters.Add(converter);
        }

        options.TypeInfoResolver ??= new DefaultJsonTypeInfoResolver();

        options.MakeReadOnly();

        return options;
    }
}
