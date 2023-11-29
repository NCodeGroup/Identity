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

using NIdentity.OpenId.Mediator.Middleware;

namespace NIdentity.OpenId.Mediator.Wrappers;

internal interface ICommandExceptionListenerWrapper<in TCommand>
    where TCommand : notnull
{
    ValueTask ListenAsync(
        TCommand command,
        Exception exception,
        CancellationToken cancellationToken);
}

internal class CommandExceptionListenerWrapper<TCommand, TException>(
    IEnumerable<ICommandExceptionListener<TCommand, TException>> listeners
) : ICommandExceptionListenerWrapper<TCommand>
    where TCommand : notnull
    where TException : Exception
{
    private IEnumerable<ICommandExceptionListener<TCommand, TException>> Listeners { get; } = listeners;

    public async ValueTask ListenAsync(
        TCommand command,
        Exception exception,
        CancellationToken cancellationToken)
    {
        foreach (var listener in Listeners)
        {
            await listener.ListenAsync(
                command,
                (TException)exception,
                cancellationToken);
        }
    }
}
