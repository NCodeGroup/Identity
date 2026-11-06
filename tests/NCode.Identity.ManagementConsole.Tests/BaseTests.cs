// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Moq;

namespace NCode.Identity.ManagementConsole.Tests;

public class BaseTests : IDisposable
{
    private MockRepository MockRepository { get; } = new(MockBehavior.Strict);

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposing)
            return;
        MockRepository.Verify();
    }

    protected Mock<T> CreateStrictMock<T>(params object[] args)
        where T : class => MockRepository.Create<T>(args);

    protected static Mock<T> CreateLooseMock<T>()
        where T : class => new(MockBehavior.Loose);
}
