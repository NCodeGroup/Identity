#region Copyright Preamble

//
//    Copyright @ 2022 NCode Group
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

namespace NIdentity.OpenId.Mediator;

internal interface IResponseHandlerWrapper<TResponse>
{
    ValueTask<TResponse> HandleAsync(IRequest<TResponse> request, CancellationToken cancellationToken);
}

internal class ResponseHandlerWrapper<TRequest, TResponse> : IResponseHandlerWrapper<TResponse>
    where TRequest : IRequest<TResponse>
{
    private IRequestResponseHandler<TRequest, TResponse> Handler { get; }

    public ResponseHandlerWrapper(IRequestResponseHandler<TRequest, TResponse> handler) =>
        Handler = handler;

    public async ValueTask<TResponse> HandleAsync(IRequest<TResponse> request, CancellationToken cancellationToken) =>
        await Handler.HandleAsync((TRequest)request, cancellationToken);
}
