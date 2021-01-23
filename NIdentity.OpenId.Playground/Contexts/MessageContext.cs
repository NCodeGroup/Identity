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
using System.Diagnostics.CodeAnalysis;
using GreenPipes;
using NIdentity.OpenId.Messages;

namespace NIdentity.OpenId.Playground.Contexts
{
    internal interface IMessageContext : PipeContext
    {
        IHttpPipeContext HttpPipeContext { get; }

        IServiceProvider ServiceProvider { get; }

        bool TryGetRequest<T>([NotNullWhen(true)] out T? request)
            where T : class, IOpenIdMessage;

        bool TryGetResponse<T>([NotNullWhen(true)] out T? response)
            where T : class, IOpenIdMessage;
    }

    internal interface IMessageContext<out TRequest, out TResponse> : IMessageContext
    {
        TRequest Request { get; }

        TResponse Response { get; }
    }

    internal class MessageContext<TRequest, TResponse> : ScopePipeContext, IMessageContext<TRequest, TResponse>
        where TRequest : IOpenIdMessage
        where TResponse : IOpenIdMessage
    {
        public IHttpPipeContext HttpPipeContext { get; }

        public IServiceProvider ServiceProvider => HttpPipeContext.ServiceProvider;

        public TRequest Request { get; }

        public TResponse Response { get; }

        public MessageContext(IHttpPipeContext context, TRequest request, TResponse response)
            : base(context)
        {
            HttpPipeContext = context;
            Request = request;
            Response = response;
        }

        public MessageContext(IHttpPipeContext context, TRequest request, TResponse response, params object[] payloads)
            : base(context, payloads)
        {
            HttpPipeContext = context;
            Request = request;
            Response = response;
        }

        public bool TryGetRequest<T>([NotNullWhen(true)] out T? request)
            where T : class, IOpenIdMessage
        {
            request = Request as T;
            return request != null;
        }

        public bool TryGetResponse<T>([NotNullWhen(true)] out T? response)
            where T : class, IOpenIdMessage
        {
            response = Response as T;
            return response != null;
        }
    }
}
