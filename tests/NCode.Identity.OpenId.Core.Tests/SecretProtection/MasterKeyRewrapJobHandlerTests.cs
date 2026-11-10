// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Moq;
using NCode.Identity.Dispatch;
using NCode.Identity.OpenId.Core.Tests;
using NCode.Identity.OpenId.SecretProtection;
using NCode.Identity.Secrets.Persistence.Protection;
using Xunit;

namespace NCode.Identity.OpenId.Core.SecretProtection;

public sealed class MasterKeyRewrapJobHandlerTests : BaseTests
{
    private static DispatchContext Context { get; } =
        new()
        {
            DispatchId = Guid.NewGuid(),
            Attempt = 1,
            MaxAttempts = 5,
        };

    private Mock<IMasterKeyManager> Manager { get; }

    public MasterKeyRewrapJobHandlerTests()
    {
        Manager = CreateStrictMock<IMasterKeyManager>();
    }

    #region HandleAsync Tests

    [Fact]
    public async Task HandleAsync_RewrapsTheOwnerOfTheJob()
    {
        Manager
            .Setup(x => x.RewrapAsync(SecretOwner.ForTenant("acme"), It.IsAny<CancellationToken>()))
            .Returns(
                new ValueTask<ProtectedValueRewrapResult>(new ProtectedValueRewrapResult(3, 0))
            )
            .Verifiable();

        await new MasterKeyRewrapJobHandler(Manager.Object).HandleAsync(
            new MasterKeyRewrapJob { OwnerType = "tenant", OwnerId = "acme" },
            Context,
            CancellationToken.None
        );
    }

    [Fact]
    public async Task HandleAsync_AFailure_PropagatesSoTheDispatcherRetries()
    {
        Manager
            .Setup(x => x.RewrapAsync(It.IsAny<SecretOwner>(), It.IsAny<CancellationToken>()))
            .Throws(new InvalidOperationException("boom"))
            .Verifiable();

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await new MasterKeyRewrapJobHandler(Manager.Object).HandleAsync(
                new MasterKeyRewrapJob { OwnerType = "tenant", OwnerId = "acme" },
                Context,
                CancellationToken.None
            )
        );
    }

    #endregion
}
