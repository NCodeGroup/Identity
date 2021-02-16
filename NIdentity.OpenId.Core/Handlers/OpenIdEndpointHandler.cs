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
using NIdentity.OpenId.Logic;
using NIdentity.OpenId.Requests;
using NIdentity.OpenId.Results;

namespace NIdentity.OpenId.Handlers
{
    internal abstract class OpenIdEndpointHandler<TRequest> : IHttpEndpointHandler<TRequest>
        where TRequest : ProcessHttpEndpoint
    {
        /// <inheritdoc />
        public async ValueTask<IHttpResult> HandleAsync(TRequest request, CancellationToken cancellationToken)
        {
            var httpContext = request.HttpContext;
            try
            {
                return await HandleAsync(httpContext, cancellationToken);
            }
            catch (Exception exception)
            {
                var exceptionService = httpContext.RequestServices.GetRequiredService<IExceptionService>();
                return exceptionService.GetHttpResultForException(exception);
            }
        }

        protected abstract ValueTask<IHttpResult> HandleAsync(HttpContext httpContext, CancellationToken cancellationToken);
    }
}
