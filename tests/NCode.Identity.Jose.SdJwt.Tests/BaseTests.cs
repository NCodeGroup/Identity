// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;

namespace NCode.Identity.Jose.SdJwt.Tests;

public class BaseTests : IAsyncLifetime
{
    private MockRepository MockRepository { get; } = new(MockBehavior.Strict);

    public Task InitializeAsync() => Task.CompletedTask;

    [SuppressMessage(
        "Style",
        "VSTHRD200:Use \"Async\" suffix for async methods",
        Justification = "The xUnit lifetime names the method."
    )]
    public async Task DisposeAsync()
    {
        await DisposeAsyncCore();
        MockRepository.Verify();
    }

    protected virtual ValueTask DisposeAsyncCore() => ValueTask.CompletedTask;

    protected Mock<T> CreateStrictMock<T>(params object[] args)
        where T : class => MockRepository.Create<T>(args);

    protected Mock<T> CreateLooseMock<T>(params object[] args)
        where T : class => MockRepository.Create<T>(MockBehavior.Loose, args);
}
