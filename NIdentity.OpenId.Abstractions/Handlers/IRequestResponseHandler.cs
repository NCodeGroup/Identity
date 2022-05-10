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

namespace NIdentity.OpenId.Handlers;

/// <summary>
/// Defines a handler that accepts an input argument and returns a value.
/// </summary>
/// <typeparam name="TRequest">The type of the input argument.</typeparam>
/// <typeparam name="TResponse">The type of the return value.</typeparam>
public interface IRequestResponseHandler<in TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    /// <summary>
    /// Handles a request given an input argument and returns a value.
    /// </summary>
    /// <param name="request">The input argument to handle.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the
    /// asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the resulting
    /// value from the handler.</returns>
    ValueTask<TResponse> HandleAsync(TRequest request, CancellationToken cancellationToken);
}
