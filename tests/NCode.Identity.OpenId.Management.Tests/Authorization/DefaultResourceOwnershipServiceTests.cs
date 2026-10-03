#region Copyright Preamble

// Copyright @ 2026 NCode Group
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

using System.Security.Claims;
using NCode.Identity;
using NCode.Identity.Logic;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Management.Authorization;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.PrincipalResolution;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Management;

public class DefaultResourceOwnershipServiceTests
{
    private static ClaimsPrincipal CreateUser(params Claim[] claims) =>
        new(new ClaimsIdentity(claims, "test"));

    [Fact]
    public async Task AssignCreatorAsync_WhenSubjectPresent_AddsOwnerAssignment()
    {
        var mocks = new MockRepository(MockBehavior.Strict);
        var storeManager = mocks.Create<IStoreManager>();
        var store = mocks.Create<IRoleAssignmentStore>();
        var crypto = mocks.Create<ICryptoService>();
        var resolver = mocks.Create<IPrincipalResolver>();

        crypto
            .Setup(x => x.GenerateKey(16, BinaryEncodingType.Base64Url))
            .Returns("assign-1")
            .Verifiable();
        resolver
            .Setup(x =>
                x.ResolvePrincipalIdAsync(
                    It.IsAny<OpenIdContext>(),
                    It.IsAny<ClaimsPrincipal>(),
                    It.IsAny<IStoreManager>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync("subject-1")
            .Verifiable();
        storeManager
            .Setup(x => x.GetStore<IRoleAssignmentStore>())
            .Returns(store.Object)
            .Verifiable();

        PersistedRoleAssignment? captured = null;
        store
            .Setup(x =>
                x.AddAsync(It.IsAny<PersistedRoleAssignment>(), It.IsAny<CancellationToken>())
            )
            .Callback(
                (PersistedRoleAssignment assignment, CancellationToken _) => captured = assignment
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        var service = new DefaultResourceOwnershipService(crypto.Object, resolver.Object);

        await service.AssignCreatorAsync(
            Mock.Of<OpenIdContext>(),
            CreateUser(new Claim("sub", "subject-1")),
            storeManager.Object,
            "tenant-1",
            ResourceNodeTypes.Client,
            "client-1",
            CancellationToken.None
        );

        mocks.Verify();
        Assert.NotNull(captured);
        Assert.Equal("tenant-1", captured.TenantId);
        Assert.Equal("assign-1", captured.AssignmentId);
        Assert.Equal("subject-1", captured.PrincipalId);
        Assert.Equal(BuiltInRoles.Owner, captured.RoleName);
        Assert.Equal(ResourceNodeTypes.Client, captured.ResourceType);
        Assert.Equal("client-1", captured.ResourceId);
    }

    [Fact]
    public async Task AssignCreatorAsync_UsesResolvedPrincipalIdNotRawSubject()
    {
        var mocks = new MockRepository(MockBehavior.Strict);
        var storeManager = mocks.Create<IStoreManager>();
        var store = mocks.Create<IRoleAssignmentStore>();
        var crypto = mocks.Create<ICryptoService>();
        var resolver = mocks.Create<IPrincipalResolver>();

        crypto.Setup(x => x.GenerateKey(16, BinaryEncodingType.Base64Url)).Returns("assign-1");
        // The resolver maps the raw upstream subject to a stable, server-owned principal id (ADR-0035).
        resolver
            .Setup(x =>
                x.ResolvePrincipalIdAsync(
                    It.IsAny<OpenIdContext>(),
                    It.IsAny<ClaimsPrincipal>(),
                    It.IsAny<IStoreManager>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync("principal-99");
        storeManager.Setup(x => x.GetStore<IRoleAssignmentStore>()).Returns(store.Object);

        PersistedRoleAssignment? captured = null;
        store
            .Setup(x =>
                x.AddAsync(It.IsAny<PersistedRoleAssignment>(), It.IsAny<CancellationToken>())
            )
            .Callback(
                (PersistedRoleAssignment assignment, CancellationToken _) => captured = assignment
            )
            .Returns(ValueTask.CompletedTask);

        var service = new DefaultResourceOwnershipService(crypto.Object, resolver.Object);

        await service.AssignCreatorAsync(
            Mock.Of<OpenIdContext>(),
            CreateUser(new Claim("sub", "raw-upstream-subject")),
            storeManager.Object,
            "tenant-1",
            ResourceNodeTypes.Client,
            "client-1",
            CancellationToken.None
        );

        Assert.NotNull(captured);
        Assert.Equal("principal-99", captured.PrincipalId);
    }

    private static PersistedRoleAssignment Owner(string principalId, string assignmentId) =>
        new()
        {
            TenantId = "tenant-1",
            AssignmentId = assignmentId,
            PrincipalId = principalId,
            RoleName = BuiltInRoles.Owner,
            ResourceType = ResourceNodeTypes.Client,
            ResourceId = "client-1",
            ConcurrencyToken = string.Empty,
        };

    [Fact]
    public async Task GetOwnersAsync_ReturnsOnlyOwnerAssignments()
    {
        var mocks = new MockRepository(MockBehavior.Strict);
        var storeManager = mocks.Create<IStoreManager>();
        var store = mocks.Create<IRoleAssignmentStore>();

        var nonOwner = new PersistedRoleAssignment
        {
            TenantId = "tenant-1",
            AssignmentId = "a-admin",
            PrincipalId = "admin",
            RoleName = BuiltInRoles.TenantAdmin,
            ResourceType = ResourceNodeTypes.Client,
            ResourceId = "client-1",
            ConcurrencyToken = string.Empty,
        };

        storeManager.Setup(x => x.GetStore<IRoleAssignmentStore>()).Returns(store.Object);
        store
            .Setup(x =>
                x.GetByResourceAsync(
                    ResourceNodeTypes.Client,
                    "client-1",
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync([Owner("p1", "a1"), nonOwner, Owner("p3", "a3")]);

        var service = new DefaultResourceOwnershipService(
            Mock.Of<ICryptoService>(),
            Mock.Of<IPrincipalResolver>()
        );
        var owners = await service.GetOwnersAsync(
            storeManager.Object,
            ResourceNodeTypes.Client,
            "client-1",
            CancellationToken.None
        );

        Assert.Equal(["p1", "p3"], owners.Select(owner => owner.PrincipalId));
    }

    [Fact]
    public async Task AddOwnerAsync_WhenNotYetOwner_AddsAssignment()
    {
        var mocks = new MockRepository(MockBehavior.Strict);
        var storeManager = mocks.Create<IStoreManager>();
        var store = mocks.Create<IRoleAssignmentStore>();
        var crypto = mocks.Create<ICryptoService>();

        storeManager.Setup(x => x.GetStore<IRoleAssignmentStore>()).Returns(store.Object);
        store
            .Setup(x =>
                x.GetByResourceAsync(
                    ResourceNodeTypes.Client,
                    "client-1",
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync([]);
        crypto.Setup(x => x.GenerateKey(16, BinaryEncodingType.Base64Url)).Returns("new-assign");
        PersistedRoleAssignment? captured = null;
        store
            .Setup(x =>
                x.AddAsync(It.IsAny<PersistedRoleAssignment>(), It.IsAny<CancellationToken>())
            )
            .Callback((PersistedRoleAssignment a, CancellationToken _) => captured = a)
            .Returns(ValueTask.CompletedTask);

        var service = new DefaultResourceOwnershipService(
            crypto.Object,
            Mock.Of<IPrincipalResolver>()
        );
        var result = await service.AddOwnerAsync(
            storeManager.Object,
            "tenant-1",
            ResourceNodeTypes.Client,
            "client-1",
            "p2",
            CancellationToken.None
        );

        Assert.NotNull(captured);
        Assert.Equal("p2", result.PrincipalId);
        Assert.Equal("new-assign", result.AssignmentId);
        Assert.Equal(BuiltInRoles.Owner, result.RoleName);
    }

    [Fact]
    public async Task AddOwnerAsync_WhenAlreadyOwner_ReturnsExistingWithoutAdding()
    {
        var mocks = new MockRepository(MockBehavior.Strict);
        var storeManager = mocks.Create<IStoreManager>();
        var store = mocks.Create<IRoleAssignmentStore>();

        storeManager.Setup(x => x.GetStore<IRoleAssignmentStore>()).Returns(store.Object);
        store
            .Setup(x =>
                x.GetByResourceAsync(
                    ResourceNodeTypes.Client,
                    "client-1",
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync([Owner("p1", "a1")]);

        // No AddAsync/crypto setups: a strict-mock failure would mean a duplicate owner was written.
        var service = new DefaultResourceOwnershipService(
            Mock.Of<ICryptoService>(),
            Mock.Of<IPrincipalResolver>()
        );
        var result = await service.AddOwnerAsync(
            storeManager.Object,
            "tenant-1",
            ResourceNodeTypes.Client,
            "client-1",
            "p1",
            CancellationToken.None
        );

        Assert.Equal("a1", result.AssignmentId);
    }

    [Fact]
    public async Task RemoveOwnerAsync_WhenNotOwner_ReturnsNotFound()
    {
        var mocks = new MockRepository(MockBehavior.Strict);
        var storeManager = mocks.Create<IStoreManager>();
        var store = mocks.Create<IRoleAssignmentStore>();

        storeManager.Setup(x => x.GetStore<IRoleAssignmentStore>()).Returns(store.Object);
        store
            .Setup(x =>
                x.GetByResourceAsync(
                    ResourceNodeTypes.Client,
                    "client-1",
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync([Owner("p1", "a1")]);

        var service = new DefaultResourceOwnershipService(
            Mock.Of<ICryptoService>(),
            Mock.Of<IPrincipalResolver>()
        );
        var result = await service.RemoveOwnerAsync(
            storeManager.Object,
            ResourceNodeTypes.Client,
            "client-1",
            "p2",
            CancellationToken.None
        );

        Assert.Equal(OwnerRemovalResult.NotFound, result);
    }

    [Fact]
    public async Task RemoveOwnerAsync_WhenLastOwner_ReturnsLastOwnerForbidden()
    {
        var mocks = new MockRepository(MockBehavior.Strict);
        var storeManager = mocks.Create<IStoreManager>();
        var store = mocks.Create<IRoleAssignmentStore>();

        storeManager.Setup(x => x.GetStore<IRoleAssignmentStore>()).Returns(store.Object);
        store
            .Setup(x =>
                x.GetByResourceAsync(
                    ResourceNodeTypes.Client,
                    "client-1",
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync([Owner("p1", "a1")]);

        // No RemoveAsync setup: a strict-mock failure would mean the last owner was removed.
        var service = new DefaultResourceOwnershipService(
            Mock.Of<ICryptoService>(),
            Mock.Of<IPrincipalResolver>()
        );
        var result = await service.RemoveOwnerAsync(
            storeManager.Object,
            ResourceNodeTypes.Client,
            "client-1",
            "p1",
            CancellationToken.None
        );

        Assert.Equal(OwnerRemovalResult.LastOwnerForbidden, result);
    }

    [Fact]
    public async Task RemoveOwnerAsync_WhenAnotherOwnerRemains_Removes()
    {
        var mocks = new MockRepository(MockBehavior.Strict);
        var storeManager = mocks.Create<IStoreManager>();
        var store = mocks.Create<IRoleAssignmentStore>();

        storeManager.Setup(x => x.GetStore<IRoleAssignmentStore>()).Returns(store.Object);
        store
            .Setup(x =>
                x.GetByResourceAsync(
                    ResourceNodeTypes.Client,
                    "client-1",
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync([Owner("p1", "a1"), Owner("p2", "a2")]);
        var removed = new List<string>();
        store
            .Setup(x => x.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback((string id, CancellationToken _) => removed.Add(id))
            .Returns(ValueTask.CompletedTask);

        var service = new DefaultResourceOwnershipService(
            Mock.Of<ICryptoService>(),
            Mock.Of<IPrincipalResolver>()
        );
        var result = await service.RemoveOwnerAsync(
            storeManager.Object,
            ResourceNodeTypes.Client,
            "client-1",
            "p1",
            CancellationToken.None
        );

        Assert.Equal(OwnerRemovalResult.Removed, result);
        Assert.Equal(["a1"], removed);
    }
}
