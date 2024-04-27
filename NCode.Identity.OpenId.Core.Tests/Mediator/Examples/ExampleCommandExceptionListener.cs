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

using NCode.Identity.OpenId.Mediator.Middleware;

namespace NCode.Identity.OpenId.Tests.Mediator.Examples;

internal interface IExampleCommandExceptionListener :
    ICommandExceptionListener<object, Exception>
{
    // nothing
}

internal class ExampleCommandExceptionListener<TCommand, TException> :
    ICommandExceptionListener<TCommand, TException>
    where TCommand : notnull
    where TException : Exception
{
    private IExampleCommandExceptionListener Listener { get; }

    public ExampleCommandExceptionListener(IExampleCommandExceptionListener listener) =>
        Listener = listener;

    public async ValueTask ListenAsync(
        TCommand command,
        TException exception,
        CancellationToken cancellationToken) =>
        await Listener.ListenAsync(
            command,
            exception,
            cancellationToken);
}
