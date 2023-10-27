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

namespace NIdentity.OpenId.Mediator.Middleware;

/// <summary>
/// Defines an exception handler that accepts the original input value, the corresponding exception, and possibly swallows
/// the exception by returning a response.
/// </summary>
/// <typeparam name="TCommand">The type of the original input value.</typeparam>
/// <typeparam name="TException">The type of the exception to handle.</typeparam>
/// <typeparam name="TResponse">The type of the return value.</typeparam>
public interface ICommandResponseExceptionHandler<in TCommand, in TException, TResponse>
    where TCommand : ICommand<TResponse>
    where TException : Exception
{
    /// <summary>
    /// Handles an exception given the original input value and the corresponding exception.
    /// </summary>
    /// <param name="command">The original input value.</param>
    /// <param name="exception">The exception to handle.</param>
    /// <param name="state">The resulting state for handling the exception.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation.</returns>
    ValueTask HandleAsync(
        TCommand command,
        TException exception,
        CommandResponseExceptionHandlerState<TResponse> state,
        CancellationToken cancellationToken);
}
