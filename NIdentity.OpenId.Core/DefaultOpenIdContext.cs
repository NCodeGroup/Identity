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
using NCode.Identity;
using NIdentity.OpenId.Endpoints;
using NIdentity.OpenId.Endpoints.Authorization.Messages;
using NIdentity.OpenId.Messages;
using NIdentity.OpenId.Messages.Parameters;
using NIdentity.OpenId.Results;
using NIdentity.OpenId.Serialization;
using NIdentity.OpenId.Tenants;

namespace NIdentity.OpenId;

/// <summary>
/// Provides a default implementation of the <see cref="OpenIdContext"/> abstraction.
/// </summary>
public class DefaultOpenIdContext : OpenIdContext, IOpenIdErrorFactory
{
    private JsonSerializerOptions? JsonSerializerOptionsOrNull { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultOpenIdContext"/> class.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> instance.</param>
    /// <param name="endpointDescriptor">The <see cref="OpenIdEndpointDescriptor"/> instance.</param>
    public DefaultOpenIdContext(HttpContext httpContext, OpenIdEndpointDescriptor endpointDescriptor)
    {
        HttpContext = httpContext;
        EndpointDescriptor = endpointDescriptor;

        Tenant = new DefaultOpenIdTenant(httpContext.Features);
    }

    /// <inheritdoc />
    public override IPropertyBag PropertyBag => new PropertyBag(); // TODO: clone from descriptor

    /// <inheritdoc />
    public override HttpContext HttpContext { get; }

    /// <inheritdoc />
    public override OpenIdEndpointDescriptor EndpointDescriptor { get; }

    /// <inheritdoc />
    public override JsonSerializerOptions JsonSerializerOptions => JsonSerializerOptionsOrNull ??= CreateJsonSerializerOptions();

    /// <inheritdoc />
    public override OpenIdTenant Tenant { get; }

    /// <inheritdoc />
    public override IOpenIdErrorFactory ErrorFactory => this;

    /// <inheritdoc />
    public override IKnownParameterCollection KnownParameters => KnownParameterCollection.Default;

    /// <inheritdoc />
    public IOpenIdError Create(string errorCode) => new OpenIdError(this, errorCode);

    private JsonSerializerOptions CreateJsonSerializerOptions()
    {
        return new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,

            Converters =
            {
                new JsonStringEnumConverter(),
                new OpenIdMessageJsonConverterFactory(this),

                new DelegatingJsonConverter<IRequestClaim, RequestClaim>(),
                new DelegatingJsonConverter<IRequestClaims, RequestClaims>(),

                new AuthorizationRequestJsonConverter(),
                new DelegatingJsonConverter<IAuthorizationRequestMessage, AuthorizationRequestMessage>(),
                new DelegatingJsonConverter<IAuthorizationRequestObject, AuthorizationRequestObject>()
            }
        };
    }
}
