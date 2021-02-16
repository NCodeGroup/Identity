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

using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using NIdentity.OpenId.Handlers;
using NIdentity.OpenId.Requests;
using NIdentity.OpenId.Results;

namespace NIdentity.OpenId.Playground.Handlers
{
    internal record ProcessTestEndpoint(HttpContext HttpContext) : ProcessHttpEndpoint(HttpContext);

    internal class ProcessTestEndpointHandler : IHttpEndpointHandler<ProcessTestEndpoint>
    {
        private readonly IHttpResultFactory _httpResultFactory;

        public ProcessTestEndpointHandler(IHttpResultFactory httpResultFactory)
        {
            _httpResultFactory = httpResultFactory;
        }

        /// <inheritdoc />
        public ValueTask<IHttpResult> HandleAsync(ProcessTestEndpoint request, CancellationToken cancellationToken)
        {
            return ValueTask.FromResult(_httpResultFactory.Ok());
        }
    }
}
