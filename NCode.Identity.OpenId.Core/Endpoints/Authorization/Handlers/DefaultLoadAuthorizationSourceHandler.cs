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

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using NCode.Identity.OpenId.Endpoints.Authorization.Commands;
using NCode.Identity.OpenId.Endpoints.Authorization.Messages;
using NCode.Identity.OpenId.Mediator;
using NCode.Identity.OpenId.Messages.Parameters;
using NCode.Identity.OpenId.Results;

namespace NCode.Identity.OpenId.Endpoints.Authorization.Handlers;

/// <summary>
/// Provides a default implementation of a handler for the <see cref="LoadAuthorizationSourceCommand"/> message.
/// </summary>
public class DefaultLoadAuthorizationSourceHandler(
    IOpenIdErrorFactory errorFactory
) : ICommandResponseHandler<LoadAuthorizationSourceCommand, IAuthorizationSource>
{
    private IOpenIdErrorFactory ErrorFactory { get; } = errorFactory;

    /// <inheritdoc />
    public async ValueTask<IAuthorizationSource> HandleAsync(
        LoadAuthorizationSourceCommand command,
        CancellationToken cancellationToken)
    {
        var openIdContext = command.OpenIdContext;
        var openIdEnvironment = openIdContext.Environment;

        var httpContext = openIdContext.Http;
        var httpRequest = httpContext.Request;

        AuthorizationSourceType sourceType;
        IEnumerable<KeyValuePair<string, StringValues>> properties;

        if (HttpMethods.IsGet(httpRequest.Method))
        {
            sourceType = AuthorizationSourceType.Query;
            properties = httpRequest.Query;
        }
        else if (HttpMethods.IsPost(httpRequest.Method))
        {
            const string expectedContentType = "application/x-www-form-urlencoded";
            if (!httpRequest.ContentType?.StartsWith(expectedContentType, StringComparison.OrdinalIgnoreCase) ?? false)
            {
                throw ErrorFactory
                    .Create(OpenIdConstants.ErrorCodes.InvalidRequest)
                    .WithDescription($"The content type of POST requests must be '{expectedContentType}', received '{httpRequest.ContentType}'.")
                    .WithStatusCode(StatusCodes.Status415UnsupportedMediaType)
                    .AsException();
            }

            sourceType = AuthorizationSourceType.Form;
            properties = await httpRequest.ReadFormAsync(cancellationToken);
        }
        else
        {
            throw ErrorFactory
                .Create(OpenIdConstants.ErrorCodes.InvalidRequest)
                .WithStatusCode(StatusCodes.Status405MethodNotAllowed)
                .AsException();
        }

        // we manually load the parameters without parsing because that will occur later
        var parameters = properties.Select(property =>
        {
            var descriptor = new ParameterDescriptor(property.Key);
            return descriptor.Loader.Load(openIdEnvironment, descriptor, property.Value);
        });

        var message = AuthorizationSource.Load(openIdEnvironment, parameters);
        message.AuthorizationSourceType = sourceType;

        return message;
    }
}
