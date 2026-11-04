// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Moq;
using NCode.Identity.Dispatch;
using NCode.Identity.OpenId.SecretProtection;
using NCode.Identity.Secrets.Persistence.Protection;
using Xunit;

namespace NCode.Identity.OpenId.Core.SecretProtection;

public sealed class MasterKeyRewrapJobHandlerTests : IDisposable
{
    private static readonly DispatchContext Context = new()
    {
        DispatchId = Guid.NewGuid(),
        Attempt = 1,
        MaxAttempts = 5,
    };

    private readonly MockRepository _mocks = new(MockBehavior.Strict);
    private readonly Mock<IMasterKeyManager> _manager;

    public MasterKeyRewrapJobHandlerTests()
    {
        _manager = _mocks.Create<IMasterKeyManager>();
    }

    public void Dispose() => _mocks.Verify();

    [Fact]
    public async Task HandleAsync_RewrapsTheOwnerOfTheJob()
    {
        _manager
            .Setup(x => x.RewrapAsync(SecretOwner.ForTenant("acme"), It.IsAny<CancellationToken>()))
            .Returns(
                new ValueTask<ProtectedValueRewrapResult>(new ProtectedValueRewrapResult(3, 0))
            );

        await new MasterKeyRewrapJobHandler(_manager.Object).HandleAsync(
            new MasterKeyRewrapJob { OwnerType = "tenant", OwnerId = "acme" },
            Context,
            CancellationToken.None
        );
    }

    [Fact]
    public async Task HandleAsync_AFailure_PropagatesSoTheDispatcherRetries()
    {
        _manager
            .Setup(x => x.RewrapAsync(It.IsAny<SecretOwner>(), It.IsAny<CancellationToken>()))
            .Throws(new InvalidOperationException("boom"));

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await new MasterKeyRewrapJobHandler(_manager.Object).HandleAsync(
                new MasterKeyRewrapJob { OwnerType = "tenant", OwnerId = "acme" },
                Context,
                CancellationToken.None
            )
        );
    }
}
