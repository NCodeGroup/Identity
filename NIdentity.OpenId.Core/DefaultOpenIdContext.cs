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
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using NCode.Identity;
using NIdentity.OpenId.Endpoints;
using NIdentity.OpenId.Endpoints.Authorization.Messages;
using NIdentity.OpenId.Mediator;
using NIdentity.OpenId.Messages;
using NIdentity.OpenId.Messages.Parameters;
using NIdentity.OpenId.Results;
using NIdentity.OpenId.Serialization;
using NIdentity.OpenId.Settings;
using NIdentity.OpenId.Tenants;

namespace NIdentity.OpenId;

/// <summary>
/// Provides a default implementation of the <see cref="OpenIdContext"/> abstraction.
/// </summary>
public class DefaultOpenIdContext : OpenIdContext, IOpenIdErrorFactory
{
    internal JsonSerializerOptions? JsonSerializerOptionsOrNull { get; private set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultOpenIdContext"/> class.
    /// </summary>
    public DefaultOpenIdContext(
        HttpContext httpContext,
        IMediator mediator,
        OpenIdTenant tenant,
        OpenIdEndpointDescriptor descriptor,
        IPropertyBag propertyBag)
    {
        HttpContext = httpContext;
        Mediator = mediator;
        Tenant = tenant;
        Descriptor = descriptor;
        PropertyBag = propertyBag;
    }

    /// <inheritdoc />
    public override HttpContext HttpContext { get; }

    /// <inheritdoc />
    public override IMediator Mediator { get; }

    /// <inheritdoc />
    public override OpenIdTenant Tenant { get; }

    /// <inheritdoc />
    public override OpenIdEndpointDescriptor Descriptor { get; }

    /// <inheritdoc />
    public override JsonSerializerOptions JsonSerializerOptions =>
        JsonSerializerOptionsOrNull ??= CreateJsonSerializerOptions();

    /// <inheritdoc />
    public override IOpenIdErrorFactory ErrorFactory => this;

    /// <inheritdoc />
    public override IKnownParameterCollection KnownParameters => KnownParameterCollection.Default;

    /// <inheritdoc />
    public override IPropertyBag PropertyBag { get; }

    /// <inheritdoc />
    public IOpenIdError Create(string errorCode) => new OpenIdError(this, errorCode);

    private T GetRequiredService<T>()
        where T : notnull
        => HttpContext.RequestServices.GetRequiredService<T>();

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
                new SettingJsonConverter(GetRequiredService<ISettingDescriptorProvider>()),
                new CodeChallengeMethodJsonConverter(),
                new DisplayTypeJsonConverter(),
                new PromptTypesJsonConverter(),
                new ResponseModeJsonConverter(),
                new ResponseTypesJsonConverter()
            }
        };
}
