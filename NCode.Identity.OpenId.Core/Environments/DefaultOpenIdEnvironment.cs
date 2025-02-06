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
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NCode.Identity.DataProtection;
using NCode.Identity.OpenId.Claims;
using NCode.Identity.OpenId.Endpoints.Authorization.Messages;
using NCode.Identity.OpenId.Errors;
using NCode.Identity.OpenId.Messages;
using NCode.Identity.OpenId.Messages.Parameters;
using NCode.Identity.OpenId.Messages.Parsers;
using NCode.Identity.OpenId.Options;
using NCode.Identity.OpenId.Serialization;
using NCode.Identity.OpenId.Settings;
using NCode.PropertyBag;

namespace NCode.Identity.OpenId.Environments;

/// <summary>
/// Provides a default implementation of the <see cref="OpenIdEnvironment"/> abstraction.
/// </summary>
public class DefaultOpenIdEnvironment(
    IServiceProvider serviceProvider,
    IKnownParameterCollectionProvider knownParametersProvider,
    ISettingDescriptorJsonProvider settingDescriptorJsonProvider,
    ISecureDataProtectionProvider secureDataProtectionProvider,
    IOpenIdMessageFactorySelector messageFactorySelector
) : OpenIdEnvironment
{
    // to prevent issues with recursive dependencies, we use lazy initialization
    private OpenIdOptions? OpenIdOptionsOrNull { get; set; }
    private JsonSerializerOptions? JsonSerializerOptionsOrNull { get; set; }
    private IOpenIdErrorFactory? ErrorFactoryOrNull { get; set; }

    private IServiceProvider ServiceProvider { get; } = serviceProvider;
    private IKnownParameterCollectionProvider KnownParametersProvider { get; } = knownParametersProvider;
    private ISettingDescriptorJsonProvider SettingDescriptorJsonProvider { get; } = settingDescriptorJsonProvider;
    private IOpenIdMessageFactorySelector MessageFactorySelector { get; } = messageFactorySelector;

    private OpenIdOptions OpenIdOptions => OpenIdOptionsOrNull ??=
        ServiceProvider.GetRequiredService<IOptions<OpenIdOptions>>().Value;

    /// <inheritdoc />
    public override JsonSerializerOptions JsonSerializerOptions =>
        JsonSerializerOptionsOrNull ??= CreateJsonSerializerOptions();

    /// <inheritdoc />
    public override ISecureDataProtector SecureDataProtector { get; } =
        secureDataProtectionProvider.CreateProtector("NCode.Identity.OpenId");

    /// <inheritdoc />
    public override IKnownParameterCollection KnownParameters => KnownParametersProvider.Collection;

    /// <inheritdoc />
    public override IOpenIdErrorFactory ErrorFactory =>
        ErrorFactoryOrNull ??= ServiceProvider.GetRequiredService<IOpenIdErrorFactory>();

    /// <inheritdoc />
    public override IPropertyBag PropertyBag { get; } = PropertyBagFactory.Create();

    /// <inheritdoc />
    public override ParameterDescriptor GetParameterDescriptor(string parameterName)
    {
        return KnownParameters.TryGet(parameterName, out var knownParameter) ?
            new ParameterDescriptor(knownParameter) :
            new ParameterDescriptor(parameterName, ParameterLoader.Default);
    }

    /// <inheritdoc />
    public override IOpenIdMessage CreateMessage(string typeDiscriminator, IEnumerable<IParameter> parameters) =>
        MessageFactorySelector.GetFactory(typeDiscriminator).Create(parameters);

    private JsonSerializerOptions CreateJsonSerializerOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,

            Converters =
            {
                new StringValuesJsonConverter(),
                new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower),
                new OpenIdMessageJsonConverterFactory(this),
                new AuthorizationRequestJsonConverter(),
                new DelegatingJsonConverter<IRequestClaim, RequestClaim>(),
                new DelegatingJsonConverter<IRequestClaims, RequestClaims>(),
                new SettingCollectionJsonConverter(SettingDescriptorJsonProvider),
                new ClaimJsonConverter(),
                new ClaimsIdentityJsonConverter()
            }
        };

        foreach (var configurator in OpenIdOptions.JsonOptionsConfigurators)
        {
            configurator(options);
        }

        options.TypeInfoResolver ??= new DefaultJsonTypeInfoResolver();

        options.MakeReadOnly();

        return options;
    }
}
