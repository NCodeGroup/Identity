#region Copyright Preamble

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
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using NIdentity.OpenId.Requests;
using NIdentity.OpenId.Results;
using NIdentity.OpenId.Validation;

namespace NIdentity.OpenId.Handlers
{
    internal abstract class OpenIdEndpointHandler<TRequest> : IHttpEndpointHandler<TRequest>
        where TRequest : ProcessHttpEndpoint
    {
        /// <inheritdoc />
        public async ValueTask<IHttpResult> HandleAsync(TRequest request, CancellationToken cancellationToken)
        {
            try
            {
                return await HandleAsync(request.HttpContext, cancellationToken);
            }
            catch (OpenIdException exception)
            {
                var httpResultFactory = request.HttpContext.RequestServices.GetRequiredService<IHttpResultFactory>();
                var statusCode = exception.ErrorDetails.StatusCode ?? StatusCodes.Status500InternalServerError;
                var httpResult = httpResultFactory.Object(statusCode, exception.ErrorDetails);
                return httpResult;
            }
            catch (Exception baseException)
            {
                var httpResultFactory = request.HttpContext.RequestServices.GetRequiredService<IHttpResultFactory>();
                var exception = OpenIdException.Factory.Create(OpenIdConstants.ErrorCodes.ServerError, baseException);
                var statusCode = exception.ErrorDetails.StatusCode ?? StatusCodes.Status500InternalServerError;
                var httpResult = httpResultFactory.Object(statusCode, exception.ErrorDetails);
                return httpResult;
            }
        }

        protected abstract ValueTask<IHttpResult> HandleAsync(HttpContext httpContext, CancellationToken cancellationToken);
    }
}
