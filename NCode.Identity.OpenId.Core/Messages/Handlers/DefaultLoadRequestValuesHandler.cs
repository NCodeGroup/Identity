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
using NCode.Identity.OpenId.Errors;
using NCode.Identity.OpenId.Mediator;
using NCode.Identity.OpenId.Messages.Commands;

namespace NCode.Identity.OpenId.Messages.Handlers;

/// <summary>
/// Provides a default implementation of a handler for the <see cref="LoadRequestValuesCommand"/> message.
/// </summary>
public class DefaultLoadRequestValuesHandler(
    IOpenIdErrorFactory errorFactory
) : ICommandResponseHandler<LoadRequestValuesCommand, IRequestValues>
{
    private IOpenIdErrorFactory ErrorFactory { get; } = errorFactory;

    /// <inheritdoc />
    public async ValueTask<IRequestValues> HandleAsync(
        LoadRequestValuesCommand command,
        CancellationToken cancellationToken)
    {
        var openIdContext = command.OpenIdContext;
        var httpContext = openIdContext.Http;
        var httpRequest = httpContext.Request;

        if (HttpMethods.IsGet(httpRequest.Method))
        {
            return new RequestValuesUsingQuery(httpRequest.Query);
        }

        if (HttpMethods.IsPost(httpRequest.Method))
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

            var form = await httpRequest.ReadFormAsync(cancellationToken);
            return new RequestValuesUsingForm(form);
        }

        throw ErrorFactory
            .Create(OpenIdConstants.ErrorCodes.InvalidRequest)
            .WithStatusCode(StatusCodes.Status405MethodNotAllowed)
            .AsException();
    }
}
