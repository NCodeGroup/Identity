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

using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;

namespace NIdentity.OpenId.Mediator;

internal class MediatorImpl : IMediator
{
    private ConcurrentDictionary<Type, ObjectFactory> FactoryCache { get; } = new();
    private IServiceProvider ServiceProvider { get; }

    public MediatorImpl(IServiceProvider serviceProvider)
    {
        ServiceProvider = serviceProvider;
    }

    private ObjectFactory GetFactory(Type typeOfWrapper) =>
        FactoryCache.GetOrAdd(typeOfWrapper, type => ActivatorUtilities.CreateFactory(type, Type.EmptyTypes));

    public async ValueTask PublishAsync(IRequest request, CancellationToken cancellationToken)
    {
        var typeOfWrapper = typeof(RequestHandlerWrapper<>).MakeGenericType(request.GetType());
        var factory = GetFactory(typeOfWrapper);
        var wrapper = (IRequestHandlerWrapper)factory(ServiceProvider, Array.Empty<object>());
        await wrapper.HandleAsync(request, cancellationToken);
    }

    public async ValueTask<TResponse> SendAsync<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken)
    {
        var requestType = request.GetType();
        var isRequestResponse = typeof(IRequest).IsAssignableFrom(requestType);
        var typeOfWrapper = isRequestResponse ?
            typeof(RequestResponseHandlerWrapper<,>).MakeGenericType(requestType, typeof(TResponse)) :
            typeof(ResponseHandlerWrapper<,>).MakeGenericType(requestType, typeof(TResponse));
        var factory = GetFactory(typeOfWrapper);
        var wrapper = (IResponseHandlerWrapper<TResponse>)factory(ServiceProvider, Array.Empty<object>());
        return await wrapper.HandleAsync(request, cancellationToken);
    }
}
