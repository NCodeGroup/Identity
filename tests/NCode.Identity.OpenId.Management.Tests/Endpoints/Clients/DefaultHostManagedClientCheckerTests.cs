// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.OpenId.Management.Clients;

namespace NCode.Identity.OpenId.Management.Endpoints.Clients;

public sealed class DefaultHostManagedClientCheckerTests
{
    private const string TenantId = "tenant-1";
    private const string ClientId = "client-1";

    private MockRepository MockRepository { get; } = new(MockBehavior.Strict);

    #region IsHostManaged Tests

    [Fact]
    public void IsHostManaged_WhenNoPolicyIsRegistered_ReturnsFalse()
    {
        var checker = new DefaultHostManagedClientChecker([]);

        Assert.False(checker.IsHostManaged(TenantId, ClientId));
    }

    [Fact]
    public void IsHostManaged_WhenAPolicyNamesTheClient_ReturnsTrue()
    {
        var mockPolicy = MockRepository.Create<IHostManagedClientPolicy>();
        mockPolicy.Setup(x => x.IsHostManaged(TenantId, ClientId)).Returns(true).Verifiable();
        var checker = new DefaultHostManagedClientChecker([mockPolicy.Object]);

        Assert.True(checker.IsHostManaged(TenantId, ClientId));

        MockRepository.Verify();
    }

    [Fact]
    public void IsHostManaged_WhenPoliciesNameOtherClients_ReturnsFalse()
    {
        var mockFirst = MockRepository.Create<IHostManagedClientPolicy>();
        var mockSecond = MockRepository.Create<IHostManagedClientPolicy>();
        mockFirst.Setup(x => x.IsHostManaged(TenantId, ClientId)).Returns(false).Verifiable();
        mockSecond.Setup(x => x.IsHostManaged(TenantId, ClientId)).Returns(false).Verifiable();
        var checker = new DefaultHostManagedClientChecker([mockFirst.Object, mockSecond.Object]);

        Assert.False(checker.IsHostManaged(TenantId, ClientId));

        MockRepository.Verify();
    }

    #endregion
}
