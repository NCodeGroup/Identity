using System.Diagnostics.CodeAnalysis;

namespace NCode.Jose.Tests;

public class BaseTests : IAsyncDisposable
{
    private MockRepository MockRepository { get; } = new(MockBehavior.Strict);

    public async ValueTask DisposeAsync()
    {
        await DisposeAsyncCore();
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    [SuppressMessage("Style", "VSTHRD200:Use \"Async\" suffix for async methods", Justification = "That isn't the recommended MS pattern.")]
    protected virtual ValueTask DisposeAsyncCore()
    {
        return ValueTask.CompletedTask;
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposing) return;
        MockRepository.Verify();
    }

    protected Mock<T> CreateStrictMock<T>(params object[] args)
        where T : class =>
        MockRepository.Create<T>(args);

    protected Mock<T> CreateLooseMock<T>(params object[] args)
        where T : class =>
        MockRepository.Create<T>(MockBehavior.Loose, args);

    protected Mock<T> CreatePartialMock<T>(params object[] args)
        where T : class
    {
        var mock = CreateLooseMock<T>(args);
        mock.CallBase = true;
        return mock;
    }
}
