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
using NCode.Identity.Jose.Algorithms;
using NCode.Identity.OpenId.Claims;
using NCode.Identity.OpenId.Endpoints.Authorization.Messages;
using NCode.Identity.OpenId.Messages;
using NCode.Identity.OpenId.Messages.Parameters;
using NCode.Identity.OpenId.Results;
using NCode.Identity.OpenId.Serialization;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.Secrets;
using NCode.PropertyBag;

namespace NCode.Identity.OpenId.Servers;

/// <summary>
/// Provides a default implementation of the <see cref="OpenIdServer"/> abstraction.
/// </summary>
public class DefaultOpenIdServer(
    IConfiguration configuration,
    IAlgorithmProvider algorithmProvider,
    ISettingDescriptorCollectionProvider settingDescriptorCollectionProvider,
    ISettingDescriptorJsonProvider settingDescriptorJsonProvider,
    ISecretKeyProvider secretKeyProvider
) : OpenIdServer, IOpenIdErrorFactory
{
    private IReadOnlySettingCollection? SettingsOrNull { get; set; }
    private JsonSerializerOptions? JsonSerializerOptionsOrNull { get; set; }

    private IConfiguration Configuration { get; } = configuration;
    private IAlgorithmProvider AlgorithmProvider { get; } = algorithmProvider;
    private ISettingDescriptorCollectionProvider SettingDescriptorCollectionProvider { get; } = settingDescriptorCollectionProvider;
    private ISettingDescriptorJsonProvider SettingDescriptorJsonProvider { get; } = settingDescriptorJsonProvider;

    /// <inheritdoc />
    public override JsonSerializerOptions JsonSerializerOptions =>
        JsonSerializerOptionsOrNull ??= CreateJsonSerializerOptions();

    /// <inheritdoc />
    public override IOpenIdErrorFactory ErrorFactory => this;

    /// <inheritdoc />
    public override IKnownParameterCollection KnownParameters => KnownParameterCollection.Default;

    /// <inheritdoc />
    public override IReadOnlySettingCollection Settings => SettingsOrNull ??= LoadSettings();

    /// <inheritdoc />
    public override ISecretKeyProvider SecretKeyProvider { get; } = secretKeyProvider;

    /// <inheritdoc />
    public override IPropertyBag PropertyBag { get; } = PropertyBagFactory.Create();

    /// <inheritdoc />
    public IOpenIdError Create(string errorCode) => new OpenIdError(this, errorCode);

    private SettingCollection LoadSettings()
    {
        var settingsSection = Configuration.GetSection("settings");

        var settings = new SettingCollection(SettingDescriptorCollectionProvider);
        var descriptors = SettingDescriptorCollectionProvider.Collection;

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

        settings.Set(SettingKeys.IdTokenSigningAlgValuesSupported, signingAlgValuesSupported);
        settings.Set(SettingKeys.UserInfoSigningAlgValuesSupported, signingAlgValuesSupported);
        settings.Set(SettingKeys.AccessTokenSigningAlgValuesSupported, signingAlgValuesSupported);
        settings.Set(SettingKeys.RequestObjectSigningAlgValuesSupported, signingAlgValuesSupported);

        settings.Set(SettingKeys.IdTokenEncryptionAlgValuesSupported, encryptionAlgValuesSupported);
        settings.Set(SettingKeys.UserInfoEncryptionAlgValuesSupported, encryptionAlgValuesSupported);
        settings.Set(SettingKeys.AccessTokenEncryptionAlgValuesSupported, encryptionAlgValuesSupported);
        settings.Set(SettingKeys.RequestObjectEncryptionAlgValuesSupported, encryptionAlgValuesSupported);

        settings.Set(SettingKeys.IdTokenEncryptionEncValuesSupported, encryptionEncValuesSupported);
        settings.Set(SettingKeys.UserInfoEncryptionEncValuesSupported, encryptionEncValuesSupported);
        settings.Set(SettingKeys.AccessTokenEncryptionEncValuesSupported, encryptionEncValuesSupported);
        settings.Set(SettingKeys.RequestObjectEncryptionEncValuesSupported, encryptionEncValuesSupported);

        settings.Set(SettingKeys.IdTokenEncryptionZipValuesSupported, encryptionZipValuesSupported);
        settings.Set(SettingKeys.UserInfoEncryptionZipValuesSupported, encryptionZipValuesSupported);
        settings.Set(SettingKeys.AccessTokenEncryptionZipValuesSupported, encryptionZipValuesSupported);
        settings.Set(SettingKeys.RequestObjectEncryptionZipValuesSupported, encryptionZipValuesSupported);

        foreach (var descriptor in descriptors)
        {
            if (descriptor.HasDefault)
            {
                settings.Set(descriptor.Create(descriptor.DefaultOrNull));
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
                        Name = settingName
                    };
                }
                else
                {
                    descriptor = new SettingDescriptor<string>
                    {
                        Name = settingName
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
                new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower),
                new OpenIdMessageJsonConverterFactory(this),
                new AuthorizationRequestJsonConverter(),
                new DelegatingJsonConverter<IRequestClaim, RequestClaim>(),
                new DelegatingJsonConverter<IRequestClaims, RequestClaims>(),
                new DelegatingJsonConverter<IAuthorizationRequestMessage, AuthorizationRequestMessage>(),
                new DelegatingJsonConverter<IAuthorizationRequestObject, AuthorizationRequestObject>(),
                new SettingCollectionJsonConverter(SettingDescriptorJsonProvider),
                new ClaimJsonConverter(),
                new ClaimsIdentityJsonConverter()
            }
        };
}
