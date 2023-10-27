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
/// Provides the ability to dispatch a command and optionally receive a response.
/// </summary>
public interface IMediator
{
    /// <summary>
    /// Asynchronously send a command to a handler that doesn't return a value.
    /// </summary>
    /// <param name="command">The input value to handle.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the
    /// asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation.</returns>
    ValueTask SendAsync(
        ICommand command,
        CancellationToken cancellationToken);

    /// <summary>
    /// Asynchronously send a command to a handler that returns a value.
    /// </summary>
    /// <param name="command">The input value to handle.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the
    /// asynchronous operation.</param>
    /// <typeparam name="TResponse">The type of the return value.</typeparam>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the resulting
    /// value from the handler.</returns>
    ValueTask<TResponse> SendAsync<TResponse>(
        ICommand<TResponse> command,
        CancellationToken cancellationToken);
}
