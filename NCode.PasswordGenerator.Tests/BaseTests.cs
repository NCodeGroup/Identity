namespace NCode.PasswordGenerator.Tests;

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
