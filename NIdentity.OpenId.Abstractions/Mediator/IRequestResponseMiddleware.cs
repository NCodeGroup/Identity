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

namespace NIdentity.OpenId.Mediator;

/// <summary>
/// Represents a function that can process the remaining middleware in the request-response pipeline.
/// </summary>
/// <typeparam name="TResponse">The type of the return value.</typeparam>
/// <remarks>
/// Declared without arguments so that <see cref="IRequestResponseMiddleware{TRequest,TResponse}"/> can be contravariant in DI.
/// </remarks>
public delegate ValueTask<TResponse> RequestResponseMiddlewareDelegate<TResponse>();

/// <summary>
/// Defines a middleware component that can be added to the request-response pipeline.
/// </summary>
/// <typeparam name="TRequest">The type of the input value.</typeparam>
/// <typeparam name="TResponse">The type of the return value.</typeparam>
public interface IRequestResponseMiddleware<in TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    /// <summary>
    /// Middleware method handler.
    /// </summary>
    /// <param name="request">The input value to handle.</param>
    /// <param name="next">The delegate representing the remaining middleware in the request-response pipeline.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the resulting
    /// value from the handler.</returns>
    ValueTask<TResponse> HandleAsync(TRequest request, RequestResponseMiddlewareDelegate<TResponse> next, CancellationToken cancellationToken);
}
