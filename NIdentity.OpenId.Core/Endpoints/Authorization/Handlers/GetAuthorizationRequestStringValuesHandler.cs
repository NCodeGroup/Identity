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

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using NIdentity.OpenId.Endpoints.Authorization.Commands;
using NIdentity.OpenId.Endpoints.Authorization.Messages;
using NIdentity.OpenId.Mediator;
using NIdentity.OpenId.Messages;
using NIdentity.OpenId.Results;

namespace NIdentity.OpenId.Endpoints.Authorization.Handlers;

internal class GetAuthorizationRequestStringValuesHandler : ICommandResponseHandler<GetAuthorizationCommandStringValuesCommand, IAuthorizationRequestStringValues>
{
    private IOpenIdContext OpenIdContext { get; }

    public GetAuthorizationRequestStringValuesHandler(IOpenIdContext openIdContext)
    {
        OpenIdContext = openIdContext;
    }

    public async ValueTask<IAuthorizationRequestStringValues> HandleAsync(GetAuthorizationCommandStringValuesCommand command, CancellationToken cancellationToken)
    {
        var endpointContext = command.EndpointContext;
        var httpContext = endpointContext.HttpContext;
        var httpRequest = httpContext.Request;

        AuthorizationSource source;
        IEnumerable<KeyValuePair<string, StringValues>> properties;

        if (HttpMethods.IsGet(httpRequest.Method))
        {
            source = AuthorizationSource.Query;
            properties = httpRequest.Query;
        }
        else if (HttpMethods.IsPost(httpRequest.Method))
        {
            const string expectedContentType = "application/x-www-form-urlencoded";
            if (!httpRequest.ContentType?.StartsWith(expectedContentType, StringComparison.OrdinalIgnoreCase) ?? false)
            {
                throw OpenIdContext.ErrorFactory
                    .Create(OpenIdConstants.ErrorCodes.InvalidRequest)
                    .WithDescription($"The content type of the request must be '{expectedContentType}'. Received '{httpRequest.ContentType}'.")
                    .WithStatusCode(StatusCodes.Status415UnsupportedMediaType)
                    .AsException();
            }

            source = AuthorizationSource.Form;
            properties = await httpRequest.ReadFormAsync(cancellationToken);
        }
        else
        {
            throw OpenIdContext.ErrorFactory
                .Create(OpenIdConstants.ErrorCodes.InvalidRequest)
                .WithStatusCode(StatusCodes.Status405MethodNotAllowed)
                .AsException();
        }

        return new AuthorizationRequestStringValues(source, OpenIdContext, properties);
    }
}
