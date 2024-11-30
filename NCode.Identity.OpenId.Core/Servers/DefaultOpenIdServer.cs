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
    IKnownParameterCollectionProvider knownParametersProvider,
    ISettingDescriptorJsonProvider settingDescriptorJsonProvider,
    IReadOnlySettingCollectionProvider settingsProvider,
    ISecretKeyCollectionProvider secretsProvider,
    IPropertyBag propertyBag
) : OpenIdServer, IOpenIdErrorFactory
{
    private JsonSerializerOptions? JsonSerializerOptionsOrNull { get; set; }
    private IKnownParameterCollectionProvider KnownParametersProvider { get; } = knownParametersProvider;
    private ISettingDescriptorJsonProvider SettingDescriptorJsonProvider { get; } = settingDescriptorJsonProvider;

    /// <inheritdoc />
    public override JsonSerializerOptions JsonSerializerOptions =>
        JsonSerializerOptionsOrNull ??= CreateJsonSerializerOptions();

    /// <inheritdoc />
    public override IOpenIdErrorFactory ErrorFactory => this;

    /// <inheritdoc />
    public override IKnownParameterCollection KnownParameters => KnownParametersProvider.Collection;

    /// <inheritdoc />
    public override IReadOnlySettingCollectionProvider SettingsProvider { get; } = settingsProvider;

    /// <inheritdoc />
    public override ISecretKeyCollectionProvider SecretsProvider { get; } = secretsProvider;

    /// <inheritdoc />
    public override IPropertyBag PropertyBag { get; } = propertyBag;

    /// <inheritdoc />
    public IOpenIdError Create(string errorCode) => new OpenIdError(this, errorCode);

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
