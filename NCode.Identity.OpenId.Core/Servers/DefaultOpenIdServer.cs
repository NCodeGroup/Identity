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

using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using NCode.Identity.OpenId.Endpoints.Authorization.Messages;
using NCode.Identity.OpenId.Messages;
using NCode.Identity.OpenId.Messages.Parameters;
using NCode.Identity.OpenId.Results;
using NCode.Identity.OpenId.Serialization;
using NCode.Identity.OpenId.Settings;
using NCode.Jose.Algorithms;
using NCode.PropertyBag;

namespace NCode.Identity.OpenId.Servers;

/// <summary>
/// Provides a default implementation of the <see cref="OpenIdServer"/> abstraction.
/// </summary>
public class DefaultOpenIdServer(
    IConfiguration configuration,
    IAlgorithmProvider algorithmProvider,
    ISettingDescriptorCollectionProvider settingDescriptorCollectionProvider
) : OpenIdServer, IOpenIdErrorFactory
{
    private ISettingCollection? SettingsOrNull { get; set; }
    private JsonSerializerOptions? JsonSerializerOptionsOrNull { get; set; }

    private IConfiguration Configuration { get; } = configuration;
    private IAlgorithmProvider AlgorithmProvider { get; } = algorithmProvider;
    private ISettingDescriptorCollectionProvider SettingDescriptorCollectionProvider { get; } = settingDescriptorCollectionProvider;

    /// <inheritdoc />
    public override JsonSerializerOptions JsonSerializerOptions =>
        JsonSerializerOptionsOrNull ??= CreateJsonSerializerOptions();

    /// <inheritdoc />
    public override IOpenIdErrorFactory ErrorFactory => this;

    /// <inheritdoc />
    public override IKnownParameterCollection KnownParameters => KnownParameterCollection.Default;

    /// <inheritdoc />
    public override ISettingCollection ServerSettings => SettingsOrNull ??= LoadSettings();

    /// <inheritdoc />
    public override IPropertyBag PropertyBag { get; } = new DefaultPropertyBag();

    /// <inheritdoc />
    public IOpenIdError Create(string errorCode) => new OpenIdError(this, errorCode);

    private SettingCollection LoadSettings()
    {
        var settingsSection = Configuration.GetSection("settings");

        var settings = new SettingCollection();
        var descriptors = SettingDescriptorCollectionProvider.Descriptors;

        var signingAlgValuesSupported = new List<string>();
        var encryptionAlgValuesSupported = new List<string>();
        var encryptionEncValuesSupported = new List<string>();
        var encryptionZipValuesSupported = new List<string>();

        foreach (var algorithm in AlgorithmProvider.Collection)
        {
            if (string.IsNullOrEmpty(algorithm.Code)) continue;
            if (algorithm.Code == "dir") continue;
            if (algorithm.Code == "none") continue;

            switch (algorithm.Type)
            {
                case AlgorithmType.DigitalSignature:
                    signingAlgValuesSupported.Add(algorithm.Code);
                    break;

                case AlgorithmType.KeyManagement:
                    encryptionAlgValuesSupported.Add(algorithm.Code);
                    break;

                case AlgorithmType.AuthenticatedEncryption:
                    encryptionEncValuesSupported.Add(algorithm.Code);
                    break;

                case AlgorithmType.Compression:
                    encryptionZipValuesSupported.Add(algorithm.Code);
                    break;
            }
        }

        settings.Set(KnownSettings.IdTokenSigningAlgValuesSupported.Create(signingAlgValuesSupported));
        settings.Set(KnownSettings.UserInfoSigningAlgValuesSupported.Create(signingAlgValuesSupported));
        settings.Set(KnownSettings.AccessTokenSigningAlgValuesSupported.Create(signingAlgValuesSupported));
        settings.Set(KnownSettings.RequestObjectSigningAlgValuesSupported.Create(signingAlgValuesSupported));

        settings.Set(KnownSettings.IdTokenEncryptionAlgValuesSupported.Create(encryptionAlgValuesSupported));
        settings.Set(KnownSettings.UserInfoEncryptionAlgValuesSupported.Create(encryptionAlgValuesSupported));
        settings.Set(KnownSettings.AccessTokenEncryptionAlgValuesSupported.Create(encryptionAlgValuesSupported));
        settings.Set(KnownSettings.RequestObjectEncryptionAlgValuesSupported.Create(encryptionAlgValuesSupported));

        settings.Set(KnownSettings.IdTokenEncryptionEncValuesSupported.Create(encryptionEncValuesSupported));
        settings.Set(KnownSettings.UserInfoEncryptionEncValuesSupported.Create(encryptionEncValuesSupported));
        settings.Set(KnownSettings.AccessTokenEncryptionEncValuesSupported.Create(encryptionEncValuesSupported));
        settings.Set(KnownSettings.RequestObjectEncryptionEncValuesSupported.Create(encryptionEncValuesSupported));

        settings.Set(KnownSettings.IdTokenEncryptionZipValuesSupported.Create(encryptionZipValuesSupported));
        settings.Set(KnownSettings.UserInfoEncryptionZipValuesSupported.Create(encryptionZipValuesSupported));
        settings.Set(KnownSettings.AccessTokenEncryptionZipValuesSupported.Create(encryptionZipValuesSupported));
        settings.Set(KnownSettings.RequestObjectEncryptionZipValuesSupported.Create(encryptionZipValuesSupported));

        foreach (var descriptor in descriptors)
        {
            var defaultValue = descriptor.GetDefaultValueOrNull();
            if (defaultValue is not null)
            {
                settings.Set(descriptor.Create(defaultValue));
            }
        }

        foreach (var settingSection in settingsSection.GetChildren())
        {
            var settingName = settingSection.Key;
            if (!descriptors.TryGet(settingName, out var descriptor))
            {
                if (settingSection.GetChildren().Count() > 1)
                {
                    descriptor = new SettingDescriptor<IReadOnlyCollection<string>>
                    {
                        Name = settingName,
                        OnMerge = KnownSettings.Replace
                    };
                }
                else
                {
                    descriptor = new SettingDescriptor<string>
                    {
                        Name = settingName,
                        OnMerge = KnownSettings.Replace
                    };
                }
            }

            var value = settingSection.Get(descriptor.ValueType);
            if (value is null) continue;

            var setting = descriptor.Create(value);
            settings.Set(setting);
        }

        return settings;
    }

    private JsonSerializerOptions CreateJsonSerializerOptions() =>
        new(JsonSerializerDefaults.Web)
        {
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,

            // TODO: provide a way to customize this list
            Converters =
            {
                new JsonStringEnumConverter(),
                new OpenIdMessageJsonConverterFactory(this),
                new AuthorizationRequestJsonConverter(),
                new DelegatingJsonConverter<IRequestClaim, RequestClaim>(),
                new DelegatingJsonConverter<IRequestClaims, RequestClaims>(),
                new DelegatingJsonConverter<IAuthorizationRequestMessage, AuthorizationRequestMessage>(),
                new DelegatingJsonConverter<IAuthorizationRequestObject, AuthorizationRequestObject>(),
                // TODO make this better
                new SettingCollectionJsonConverter(new SettingDescriptorJsonProvider(SettingDescriptorCollectionProvider.Descriptors)),
                // TODO remove these
                new CodeChallengeMethodJsonConverter(),
                new DisplayTypeJsonConverter(),
                new PromptTypesJsonConverter(),
                new ResponseModeJsonConverter(),
                new ResponseTypesJsonConverter()
            }
        };
}
