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

using System.Diagnostics.CodeAnalysis;
using GreenPipes;

namespace NIdentity.OpenId.Playground.Contexts
{
    internal interface IConsumeContext : PipeContext
    {
        IReceiveContext ReceiveContext { get; }

        bool TryGetRequest<T>([NotNullWhen(true)] out T? request)
            where T : class;

        bool TryGetResponse<T>([NotNullWhen(true)] out T? response)
            where T : class;
    }

    internal interface IConsumeContext<out TRequest, out TResponse> : IConsumeContext
    {
        TRequest Request { get; }

        TResponse Response { get; }
    }

    internal class ConsumeContext<TRequest, TResponse> : ScopePipeContext, IConsumeContext<TRequest, TResponse>
    {
        public IReceiveContext ReceiveContext { get; }

        public TRequest Request { get; }

        public TResponse Response { get; }

        public ConsumeContext(IReceiveContext receiveContext, TRequest request, TResponse response)
            : base(receiveContext)
        {
            ReceiveContext = receiveContext;
            Request = request;
            Response = response;
        }

        public ConsumeContext(
            IReceiveContext receiveContext,
            TRequest request,
            TResponse response,
            params object[] payloads)
            : base(receiveContext, payloads)
        {
            ReceiveContext = receiveContext;
            Request = request;
            Response = response;
        }

        public bool TryGetRequest<T>([NotNullWhen(true)] out T? request)
            where T : class
        {
            request = Request as T;
            return request != null;
        }

        public bool TryGetResponse<T>([NotNullWhen(true)] out T? response)
            where T : class
        {
            response = Response as T;
            return response != null;
        }
    }
}
