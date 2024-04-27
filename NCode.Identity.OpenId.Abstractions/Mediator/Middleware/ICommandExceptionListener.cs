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

namespace NCode.Identity.OpenId.Mediator.Middleware;

/// <summary>
/// Defines an exception listener that accepts the original input value and the corresponding exception.
/// </summary>
/// <typeparam name="TCommand">The type of the original input value.</typeparam>
/// <typeparam name="TException">The type of the exception to listen for.</typeparam>
public interface ICommandExceptionListener<in TCommand, in TException>
    where TCommand : notnull
    where TException : Exception
{
    /// <summary>
    /// Listens for when errors occur given the original input value and the corresponding exception.
    /// </summary>
    /// <param name="command">The original input value.</param>
    /// <param name="exception">The exception to listen for.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation.</returns>
    ValueTask ListenAsync(
        TCommand command,
        TException exception,
        CancellationToken cancellationToken);
}
