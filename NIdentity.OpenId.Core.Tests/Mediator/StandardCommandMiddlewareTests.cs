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

using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using NIdentity.OpenId.Core.Tests.Mediator.Examples;
using NIdentity.OpenId.Mediator;
using NIdentity.OpenId.Mediator.Middleware;
using NIdentity.OpenId.Mediator.Wrappers;
using Xunit;

namespace NIdentity.OpenId.Core.Tests.Mediator;

public class StandardCommandMiddlewareTests : BaseTests
{
    private IServiceCollection Services { get; } = new ServiceCollection();

    private ServiceProvider? ServiceProviderOrNull { get; set; }

    private IServiceProvider ServiceProvider => ServiceProviderOrNull ??= Services.BuildServiceProvider();

    public StandardCommandMiddlewareTests()
    {
        Services.AddSingleton(typeof(ICommandMiddleware<>), typeof(StandardCommandMiddleware<>));
        Services.AddTransient(typeof(CommandExceptionListenerWrapper<,>));
        Services.AddTransient(typeof(CommandExceptionHandlerWrapper<,>));
    }

    protected override void Dispose(bool disposing)
    {
        ServiceProviderOrNull?.Dispose();
        base.Dispose(disposing);
    }

    private ICommandMiddleware<TCommand> CreateMiddleware<TCommand>()
        where TCommand : ICommand =>
        ServiceProvider.GetRequiredService<ICommandMiddleware<TCommand>>();

    private static ValueTask EmptyNextAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task Handle_GivenPreProcessAsync()
    {
        var command = new ExampleCommand();

        var mockCommandPreProcessor = CreateStrictMock<ICommandPreProcessor<ICommand>>();
        mockCommandPreProcessor
            .Setup(x => x.PreProcessAsync(command, default))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        Services.AddSingleton(typeof(ICommandPreProcessor<ICommand>), mockCommandPreProcessor.Object);

        var middleware = CreateMiddleware<ICommand>();
        await middleware.HandleAsync(command, EmptyNextAsync, default);
    }

    [Fact]
    public async Task Handle_GivenPostProcessAsync()
    {
        var command = new ExampleCommand();

        var mockCommandPostProcessor = CreateStrictMock<ICommandPostProcessor<ICommand>>();
        mockCommandPostProcessor
            .Setup(x => x.PostProcessAsync(command, default))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        Services.AddSingleton(typeof(ICommandPostProcessor<ICommand>), mockCommandPostProcessor.Object);

        var middleware = CreateMiddleware<ICommand>();
        await middleware.HandleAsync(command, EmptyNextAsync, default);
    }

    [Fact]
    public async Task Handle_GivenExceptionListenerAsync()
    {
        var command = new ExampleCommand();
        var exception = new InvalidOperationException();

        var mockExceptionListener = CreateStrictMock<IExampleCommandExceptionListener>();
        mockExceptionListener
            .Setup(x => x.ListenAsync(command, exception, default))
            .Callback([AssertionMethod](
                object argCommand,
                Exception argException,
                CancellationToken _) =>
            {
                Assert.Same(command, argCommand);
                Assert.Same(exception, argException);
            })
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        Services.AddSingleton(mockExceptionListener.Object);
        Services.AddSingleton(typeof(ICommandExceptionListener<,>), typeof(ExampleCommandExceptionListener<,>));

        var middleware = CreateMiddleware<ICommand>();

        var result = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await middleware.HandleAsync(command, () => throw exception, default)
        );
        Assert.Same(exception, result);
    }

    [Fact]
    public async Task Handle_GivenExceptionHandlerAsync()
    {
        var command = new ExampleCommand();
        var exception = new InvalidOperationException();

        var mockExceptionHandler = CreateStrictMock<IExampleCommandExceptionHandler>();
        mockExceptionHandler
            .Setup(x => x.HandleAsync(command, exception, It.IsAny<CommandExceptionHandlerState>(), default))
            .Callback([AssertionMethod](
                ICommand argCommand,
                Exception argException,
                CommandExceptionHandlerState argState,
                CancellationToken _) =>
            {
                Assert.Same(command, argCommand);
                Assert.Same(exception, argException);
                argState.SetHandled();
            })
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        Services.AddSingleton(mockExceptionHandler.Object);
        Services.AddSingleton(typeof(ICommandExceptionHandler<,>), typeof(ExampleCommandExceptionHandler<,>));

        var middleware = CreateMiddleware<ICommand>();
        await middleware.HandleAsync(command, () => throw exception, default);
    }
}
