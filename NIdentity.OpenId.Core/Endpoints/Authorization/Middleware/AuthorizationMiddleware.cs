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

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using NIdentity.OpenId.Endpoints.Authorization.Messages;
using NIdentity.OpenId.Endpoints.Authorization.Results;
using NIdentity.OpenId.Messages;
using NIdentity.OpenId.Results;
using NIdentity.OpenId.Stores;

namespace NIdentity.OpenId.Endpoints.Authorization.Middleware;

internal class AuthorizationMiddleware : IOpenIdEndpointMiddleware
{
    private ILogger<AuthorizationMiddleware> Logger { get; }
    private IOpenIdErrorFactory OpenIdErrorFactory { get; }
    private IClientStore ClientStore { get; }

    public AuthorizationMiddleware(ILogger<AuthorizationMiddleware> logger, IOpenIdErrorFactory openIdErrorFactory, IClientStore clientStore)
    {
        Logger = logger;
        OpenIdErrorFactory = openIdErrorFactory;
        ClientStore = clientStore;
    }

    public async ValueTask InvokeAsync(OpenIdEndpointContext context, OpenIdRequestDelegate next)
    {
        OpenIdException? openIdException = null;
        try
        {
            await next(context);
        }
        catch (OpenIdException exception)
        {
            Logger.LogWarning(
                "The authorization operation failed: StatusCode={StatusCode}; ErrorCode={ErrorCode}; ErrorDescription={ErrorDescription}; ErrorUri={ErrorUri};",
                exception.Error.StatusCode,
                exception.Error.Code,
                exception.Error.Description,
                exception.Error.Uri);

            openIdException = exception;
        }
        catch (Exception exception)
        {
            Logger.LogError(exception, "An unhandled exception occured");

            openIdException = OpenIdErrorFactory.Create(OpenIdConstants.ErrorCodes.ServerError).AsException(exception);
        }

        if (openIdException == null) return;

        var result = await DetermineErrorResultAsync(context, openIdException);
        await result.ExecuteResultAsync(context);
    }

    private IOpenIdResult GetErrorObjectResult(OpenIdException exception)
    {
        throw new NotImplementedException();
    }

    private async ValueTask<IOpenIdResult> DetermineErrorResultAsync(OpenIdEndpointContext context, OpenIdException exception)
    {
        // before we redirect, we must validate the client_id and redirect_uri
        // otherwise we must return a failure HTTP status code

        var httpContext = context.HttpContext;
        var cancellationToken = httpContext.RequestAborted;
        var messageFeature = httpContext.Features.Get<IOpenIdMessageFeature>();
        var message = messageFeature?.OpenIdMessage;
        var error = exception.Error;

        if (message == null)
        {
            return GetErrorObjectResult(exception);
        }

        if (message is IAuthorizationRequestUnion authorizationRequestUnion)
        {
            var client = authorizationRequestUnion.Client;
            var redirectUri = authorizationRequestUnion.RedirectUri;
            var responseMode = authorizationRequestUnion.ResponseMode;
            var state = authorizationRequestUnion.State;

            if (!string.IsNullOrEmpty(state))
            {
                error.State = state;
            }

            if (!client.RedirectUris.Contains(redirectUri) && !(client.AllowLoopback && redirectUri.IsLoopback))
            {
                return GetErrorObjectResult(exception);
            }

            return new AuthorizationResult(redirectUri, responseMode, error);
        }
        else
        {
            if (message.TryGetValue(OpenIdConstants.Parameters.State, out var state) && !StringValues.IsNullOrEmpty(state))
            {
                error.State = state;
            }

            if (!message.TryGetValue(OpenIdConstants.Parameters.ResponseMode, out var responseModeStringValues) || !Enum.TryParse(responseModeStringValues, out ResponseMode responseMode))
            {
                responseMode = ResponseMode.Query;
            }

            if (!message.TryGetValue(OpenIdConstants.Parameters.RedirectUri, out var redirectUrl) || !Uri.TryCreate(redirectUrl, UriKind.Absolute, out var redirectUri))
            {
                return GetErrorObjectResult(exception);
            }

            if (!message.TryGetValue(OpenIdConstants.Parameters.ClientId, out var clientId) || string.IsNullOrEmpty(clientId))
            {
                return GetErrorObjectResult(exception);
            }

            var client = await ClientStore.TryGetByClientIdAsync(clientId, cancellationToken);
            if (client == null)
            {
                return GetErrorObjectResult(exception);
            }

            if (!client.RedirectUris.Contains(redirectUri) && !(client.AllowLoopback && redirectUri.IsLoopback))
            {
                return GetErrorObjectResult(exception);
            }

            return new AuthorizationResult(redirectUri, responseMode, error);
        }
    }
}
