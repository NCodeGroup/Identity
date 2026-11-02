// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Mediator.Middleware;

namespace NCode.Identity.OpenId.Core.Tests.Mediator.Examples;

internal interface IExampleCommandExceptionListener : ICommandExceptionListener<object, Exception>
{
    // nothing
}

internal class ExampleCommandExceptionListener<TCommand, TException>
    : ICommandExceptionListener<TCommand, TException>
    where TCommand : notnull
    where TException : Exception
{
    private IExampleCommandExceptionListener Listener { get; }

    public ExampleCommandExceptionListener(IExampleCommandExceptionListener listener) =>
        Listener = listener;

    public async ValueTask ListenAsync(
        TCommand command,
        TException exception,
        CancellationToken cancellationToken
    ) => await Listener.ListenAsync(command, exception, cancellationToken);
}
