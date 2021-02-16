﻿#region Copyright Preamble

//
//    Copyright @ 2021 NCode Group
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

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using NIdentity.OpenId.Messages.Authorization;
using NIdentity.OpenId.Requests;
using NIdentity.OpenId.Results;
using NIdentity.OpenId.Validation;

namespace NIdentity.OpenId.Handlers
{
    internal class ProcessAuthorizationEndpointHandler : OpenIdEndpointHandler<ProcessAuthorizationEndpoint>
    {
        private readonly IHttpResultFactory _httpResultFactory;
        private readonly IRequestResponseHandler<GetAuthorizationRequest, IAuthorizationRequest> _getAuthorizationRequestHandler;
        private readonly IEnumerable<IRequestHandler<ValidateAuthorizationRequest>> _validateAuthorizationRequestHandlers;

        public ProcessAuthorizationEndpointHandler(
            IHttpResultFactory httpResultFactory,
            IRequestResponseHandler<GetAuthorizationRequest, IAuthorizationRequest> getAuthorizationRequestHandler,
            IEnumerable<IRequestHandler<ValidateAuthorizationRequest>> validateAuthorizationRequestHandlers)
        {
            _httpResultFactory = httpResultFactory;
            _getAuthorizationRequestHandler = getAuthorizationRequestHandler;
            _validateAuthorizationRequestHandlers = validateAuthorizationRequestHandlers;
        }

        /// <inheritdoc />
        protected override async ValueTask<IHttpResult> HandleAsync(HttpContext httpContext, CancellationToken cancellationToken)
        {
            var authorizationRequest = await GetAuthorizationRequestAsync(httpContext, cancellationToken);
            try
            {
                return await HandleAuthorizationRequestAsync(authorizationRequest, cancellationToken);
            }
            catch (OpenIdException exception)
            {
                if (!string.IsNullOrEmpty(authorizationRequest.State))
                {
                    exception.WithExtensionData(OpenIdConstants.Parameters.State, authorizationRequest.State);
                }

                throw;
            }
            catch (Exception baseException)
            {
                var exception = OpenIdException.Factory.Create(OpenIdConstants.ErrorCodes.ServerError, baseException);

                if (!string.IsNullOrEmpty(authorizationRequest.State))
                {
                    exception.WithExtensionData(OpenIdConstants.Parameters.State, authorizationRequest.State);
                }

                throw exception;
            }
        }

        private async ValueTask<IAuthorizationRequest> GetAuthorizationRequestAsync(HttpContext httpContext, CancellationToken cancellationToken)
        {
            try
            {
                var request = new GetAuthorizationRequest(httpContext);
                return await _getAuthorizationRequestHandler.HandleAsync(request, cancellationToken);
            }
            catch (Exception exception) when (exception is not OpenIdException)
            {
                throw OpenIdException.Factory.Create(OpenIdConstants.ErrorCodes.ServerError, exception);
            }
        }

        private async ValueTask ValidateAuthorizationRequestAsync(IAuthorizationRequest authorizationRequest, CancellationToken cancellationToken)
        {
            var request = new ValidateAuthorizationRequest(authorizationRequest);

            foreach (var handler in _validateAuthorizationRequestHandlers)
            {
                await handler.HandleAsync(request, cancellationToken);
            }
        }

        private async ValueTask<IHttpResult> HandleAuthorizationRequestAsync(IAuthorizationRequest authorizationRequest, CancellationToken cancellationToken)
        {
            await ValidateAuthorizationRequestAsync(authorizationRequest, cancellationToken);

            return _httpResultFactory.Ok();
        }
    }
}