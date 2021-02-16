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

using NIdentity.OpenId.Requests;
using NIdentity.OpenId.Results;

namespace NIdentity.OpenId.Handlers
{
    /// <summary>
    /// Defines a handler that accepts an input argument derived from <see cref="ProcessHttpEndpoint"/>> and returns an <see cref="IHttpResult"/>.
    /// </summary>
    /// <typeparam name="TRequest">The type of input argument which must derive from <see cref="ProcessHttpEndpoint"/>.</typeparam>
    public interface IHttpEndpointHandler<in TRequest> : IRequestResponseHandler<TRequest, IHttpResult>
        where TRequest : ProcessHttpEndpoint
    {
        // nothing
    }
}
