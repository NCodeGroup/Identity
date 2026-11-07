// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Claims;
using NCode.Identity.Cryptography;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Management.Authorization;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.Persistence.Tenants;
using NCode.Identity.OpenId.PrincipalResolution;
using NCode.Identity.OpenId.Tenants;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Management;

public class DefaultResourceOwnershipServiceTests : IDisposable
{
    private MockRepository MockRepository { get; } = new(MockBehavior.Strict);

    public void Dispose() => MockRepository.Verify();

    private static OpenIdContext CreateContext() =>
        Mock.Of<OpenIdContext>(context =>
            context.Tenant == Mock.Of<OpenIdTenant>(tenant => tenant.TenantId == "control-plane")
        );

    private static ClaimsPrincipal CreateUser(params Claim[] claims) =>
        new(new ClaimsIdentity(claims, "test"));

    [Fact]
    public async Task AssignCreatorAsync_WhenSubjectPresent_AddsOwnerAssignment()
    {
        var storeManager = MockRepository.Create<IStoreManager>();
        var store = MockRepository.Create<IRoleAssignmentStore>();
        var crypto = MockRepository.Create<ICryptoService>();
        var resolver = MockRepository.Create<IPrincipalResolver>();

        crypto
            .Setup(x => x.GenerateKey(16, BinaryEncodings.Base64Url))
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

        var service = new DefaultResourceOwnershipService(
            crypto.Object,
            resolver.Object,
            Mock.Of<IAmbientTenantAccessor>()
        );

        await service.AssignCreatorAsync(
            CreateContext(),
            CreateUser(new Claim("sub", "subject-1")),
            storeManager.Object,
            "tenant-1",
            ResourceNodeTypes.Client,
            "client-1",
            CancellationToken.None
        );

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
        var storeManager = MockRepository.Create<IStoreManager>();
        var store = MockRepository.Create<IRoleAssignmentStore>();
        var crypto = MockRepository.Create<ICryptoService>();
        var resolver = MockRepository.Create<IPrincipalResolver>();

        crypto
            .Setup(x => x.GenerateKey(16, BinaryEncodings.Base64Url))
            .Returns("assign-1")
            .Verifiable();
        // The resolver maps the raw upstream subject to a stable, server-owned principal id (ADR-1035).
        resolver
            .Setup(x =>
                x.ResolvePrincipalIdAsync(
                    It.IsAny<OpenIdContext>(),
                    It.IsAny<ClaimsPrincipal>(),
                    It.IsAny<IStoreManager>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync("principal-99")
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

        var service = new DefaultResourceOwnershipService(
            crypto.Object,
            resolver.Object,
            Mock.Of<IAmbientTenantAccessor>()
        );

        await service.AssignCreatorAsync(
            CreateContext(),
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

    [Fact]
    public async Task AssignCreatorAsync_ReturnsTheCreatorsAssignment()
    {
        var storeManager = MockRepository.Create<IStoreManager>();
        var store = MockRepository.Create<IRoleAssignmentStore>();
        var crypto = MockRepository.Create<ICryptoService>();
        var resolver = MockRepository.Create<IPrincipalResolver>();

        crypto
            .Setup(x => x.GenerateKey(16, BinaryEncodings.Base64Url))
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
            .ReturnsAsync("principal-1")
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

        var service = new DefaultResourceOwnershipService(
            crypto.Object,
            resolver.Object,
            Mock.Of<IAmbientTenantAccessor>()
        );

        var result = await service.AssignCreatorAsync(
            CreateContext(),
            CreateUser(new Claim("sub", "subject-1")),
            storeManager.Object,
            "tenant-1",
            ResourceNodeTypes.Client,
            "client-1",
            CancellationToken.None
        );

        Assert.NotNull(result);
        Assert.Same(captured, result);
        Assert.Equal("assign-1", result.AssignmentId);
        Assert.Equal("principal-1", result.PrincipalId);
        Assert.Equal(BuiltInRoles.Owner, result.RoleName);
    }

    private sealed class StubAmbientTenantAccessor : IAmbientTenantAccessor
    {
        private readonly Stack<string> _scopes = new();

        public bool IsScoped => _scopes.Count > 0;

        public string? TenantId => _scopes.TryPeek(out var tenantId) ? tenantId : null;

        public IDisposable BeginScope(string tenantId)
        {
            _scopes.Push(tenantId);
            return new Scope(_scopes);
        }

        private sealed class Scope(Stack<string> scopes) : IDisposable
        {
            public void Dispose() => scopes.Pop();
        }
    }

    [Fact]
    public async Task AssignCreatorAsync_ResolvesTheCreatorUnderTheControlPlaneScope()
    {
        var storeManager = MockRepository.Create<IStoreManager>();
        var store = MockRepository.Create<IRoleAssignmentStore>();
        var crypto = MockRepository.Create<ICryptoService>();
        var resolver = MockRepository.Create<IPrincipalResolver>();
        var openIdContext = MockRepository.Create<OpenIdContext>();
        var accessor = new StubAmbientTenantAccessor();

        openIdContext
            .Setup(x => x.Tenant)
            .Returns(Mock.Of<OpenIdTenant>(tenant => tenant.TenantId == "control-plane"))
            .Verifiable();
        crypto
            .Setup(x => x.GenerateKey(16, BinaryEncodings.Base64Url))
            .Returns("assign-1")
            .Verifiable();

        string? tenantIdWhileResolving = null;
        resolver
            .Setup(x =>
                x.ResolvePrincipalIdAsync(
                    It.IsAny<OpenIdContext>(),
                    It.IsAny<ClaimsPrincipal>(),
                    It.IsAny<IStoreManager>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback(() => tenantIdWhileResolving = accessor.TenantId)
            .ReturnsAsync("principal-1")
            .Verifiable();
        storeManager
            .Setup(x => x.GetStore<IRoleAssignmentStore>())
            .Returns(store.Object)
            .Verifiable();

        string? tenantIdWhileAdding = null;
        store
            .Setup(x =>
                x.AddAsync(It.IsAny<PersistedRoleAssignment>(), It.IsAny<CancellationToken>())
            )
            .Callback(
                (PersistedRoleAssignment _, CancellationToken _) =>
                    tenantIdWhileAdding = accessor.TenantId
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        var service = new DefaultResourceOwnershipService(crypto.Object, resolver.Object, accessor);

        using (accessor.BeginScope("route-tenant"))
        {
            await service.AssignCreatorAsync(
                openIdContext.Object,
                CreateUser(new Claim("sub", "subject-1")),
                storeManager.Object,
                "route-tenant",
                ResourceNodeTypes.Client,
                "client-1",
                CancellationToken.None
            );

            Assert.Equal("route-tenant", accessor.TenantId);
        }

        Assert.Equal("control-plane", tenantIdWhileResolving);
        Assert.Equal("route-tenant", tenantIdWhileAdding);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task AssignCreatorAsync_WhenTheCreatorHasNoSubject_AddsNoAssignment(
        string? resolvedPrincipalId
    )
    {
        var resolver = MockRepository.Create<IPrincipalResolver>();

        resolver
            .Setup(x =>
                x.ResolvePrincipalIdAsync(
                    It.IsAny<OpenIdContext>(),
                    It.IsAny<ClaimsPrincipal>(),
                    It.IsAny<IStoreManager>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(resolvedPrincipalId!)
            .Verifiable();

        // No store or crypto setups: a strict-mock failure would mean an assignment was written.
        var service = new DefaultResourceOwnershipService(
            MockRepository.Create<ICryptoService>().Object,
            resolver.Object,
            Mock.Of<IAmbientTenantAccessor>()
        );

        var result = await service.AssignCreatorAsync(
            CreateContext(),
            CreateUser(),
            MockRepository.Create<IStoreManager>().Object,
            "tenant-1",
            ResourceNodeTypes.Client,
            "client-1",
            CancellationToken.None
        );

        Assert.Null(result);
    }

    [Fact]
    public async Task AssignCreatorAsync_WhenTheCallerHasNoPrincipal_ReturnsNull()
    {
        var resolver = MockRepository.Create<IPrincipalResolver>();

        resolver
            .Setup(x =>
                x.ResolvePrincipalIdAsync(
                    It.IsAny<OpenIdContext>(),
                    It.IsAny<ClaimsPrincipal>(),
                    It.IsAny<IStoreManager>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(string.Empty)
            .Verifiable();

        var service = new DefaultResourceOwnershipService(
            MockRepository.Create<ICryptoService>().Object,
            resolver.Object,
            Mock.Of<IAmbientTenantAccessor>()
        );

        var result = await service.AssignCreatorAsync(
            CreateContext(),
            CreateUser(),
            MockRepository.Create<IStoreManager>().Object,
            "tenant-1",
            ResourceNodeTypes.Client,
            "client-1",
            CancellationToken.None
        );

        Assert.Null(result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task AddOwnerAsync_WhenThePrincipalIdIsEmpty_Throws(string? principalId)
    {
        // No store or crypto setups: a strict-mock failure would mean an assignment was written.
        var service = new DefaultResourceOwnershipService(
            MockRepository.Create<ICryptoService>().Object,
            MockRepository.Create<IPrincipalResolver>().Object,
            Mock.Of<IAmbientTenantAccessor>()
        );

        await Assert.ThrowsAnyAsync<ArgumentException>(async () =>
            await service.AddOwnerAsync(
                MockRepository.Create<IStoreManager>().Object,
                "tenant-1",
                ResourceNodeTypes.Client,
                "client-1",
                principalId!,
                CancellationToken.None
            )
        );
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
        var storeManager = MockRepository.Create<IStoreManager>();
        var store = MockRepository.Create<IRoleAssignmentStore>();

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

        storeManager
            .Setup(x => x.GetStore<IRoleAssignmentStore>())
            .Returns(store.Object)
            .Verifiable();
        store
            .Setup(x =>
                x.GetByResourceAsync(
                    ResourceNodeTypes.Client,
                    "client-1",
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync([Owner("p1", "a1"), nonOwner, Owner("p3", "a3")])
            .Verifiable();

        var service = new DefaultResourceOwnershipService(
            Mock.Of<ICryptoService>(),
            Mock.Of<IPrincipalResolver>(),
            Mock.Of<IAmbientTenantAccessor>()
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
        var storeManager = MockRepository.Create<IStoreManager>();
        var store = MockRepository.Create<IRoleAssignmentStore>();
        var crypto = MockRepository.Create<ICryptoService>();

        storeManager
            .Setup(x => x.GetStore<IRoleAssignmentStore>())
            .Returns(store.Object)
            .Verifiable();
        store
            .Setup(x =>
                x.GetByResourceAsync(
                    ResourceNodeTypes.Client,
                    "client-1",
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync([])
            .Verifiable();
        crypto
            .Setup(x => x.GenerateKey(16, BinaryEncodings.Base64Url))
            .Returns("new-assign")
            .Verifiable();
        PersistedRoleAssignment? captured = null;
        store
            .Setup(x =>
                x.AddAsync(It.IsAny<PersistedRoleAssignment>(), It.IsAny<CancellationToken>())
            )
            .Callback((PersistedRoleAssignment a, CancellationToken _) => captured = a)
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        var service = new DefaultResourceOwnershipService(
            crypto.Object,
            Mock.Of<IPrincipalResolver>(),
            Mock.Of<IAmbientTenantAccessor>()
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
        Assert.Equal("p2", result.Assignment.PrincipalId);
        Assert.Equal("new-assign", result.Assignment.AssignmentId);
        Assert.Equal(BuiltInRoles.Owner, result.Assignment.RoleName);
        Assert.True(result.IsAdded);
    }

    [Fact]
    public async Task AddOwnerAsync_WhenAlreadyOwner_ReturnsExistingWithoutAdding()
    {
        var storeManager = MockRepository.Create<IStoreManager>();
        var store = MockRepository.Create<IRoleAssignmentStore>();

        storeManager
            .Setup(x => x.GetStore<IRoleAssignmentStore>())
            .Returns(store.Object)
            .Verifiable();
        store
            .Setup(x =>
                x.GetByResourceAsync(
                    ResourceNodeTypes.Client,
                    "client-1",
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync([Owner("p1", "a1")])
            .Verifiable();

        // No AddAsync/crypto setups: a strict-mock failure would mean a duplicate owner was written.
        var service = new DefaultResourceOwnershipService(
            Mock.Of<ICryptoService>(),
            Mock.Of<IPrincipalResolver>(),
            Mock.Of<IAmbientTenantAccessor>()
        );
        var result = await service.AddOwnerAsync(
            storeManager.Object,
            "tenant-1",
            ResourceNodeTypes.Client,
            "client-1",
            "p1",
            CancellationToken.None
        );

        Assert.Equal("a1", result.Assignment.AssignmentId);
        Assert.False(result.IsAdded);
    }

    [Fact]
    public async Task RemoveOwnerAsync_WhenNotOwner_ReturnsNotFound()
    {
        var storeManager = MockRepository.Create<IStoreManager>();
        var store = MockRepository.Create<IRoleAssignmentStore>();

        storeManager
            .Setup(x => x.GetStore<IRoleAssignmentStore>())
            .Returns(store.Object)
            .Verifiable();
        store
            .Setup(x =>
                x.GetByResourceAsync(
                    ResourceNodeTypes.Client,
                    "client-1",
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync([Owner("p1", "a1")])
            .Verifiable();

        var service = new DefaultResourceOwnershipService(
            Mock.Of<ICryptoService>(),
            Mock.Of<IPrincipalResolver>(),
            Mock.Of<IAmbientTenantAccessor>()
        );
        var result = await service.RemoveOwnerAsync(
            storeManager.Object,
            ResourceNodeTypes.Client,
            "client-1",
            "p2",
            CancellationToken.None
        );

        Assert.Equal(OwnerRemovalResult.NotFound, result.Result);
        Assert.Empty(result.RemovedAssignments);
    }

    [Fact]
    public async Task RemoveOwnerAsync_WhenLastOwner_ReturnsLastOwnerForbidden()
    {
        var storeManager = MockRepository.Create<IStoreManager>();
        var store = MockRepository.Create<IRoleAssignmentStore>();

        storeManager
            .Setup(x => x.GetStore<IRoleAssignmentStore>())
            .Returns(store.Object)
            .Verifiable();
        store
            .Setup(x =>
                x.GetByResourceAsync(
                    ResourceNodeTypes.Client,
                    "client-1",
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync([Owner("p1", "a1")])
            .Verifiable();

        // No RemoveAsync setup: a strict-mock failure would mean the last owner was removed.
        var service = new DefaultResourceOwnershipService(
            Mock.Of<ICryptoService>(),
            Mock.Of<IPrincipalResolver>(),
            Mock.Of<IAmbientTenantAccessor>()
        );
        var result = await service.RemoveOwnerAsync(
            storeManager.Object,
            ResourceNodeTypes.Client,
            "client-1",
            "p1",
            CancellationToken.None
        );

        Assert.Equal(OwnerRemovalResult.LastOwnerForbidden, result.Result);
        Assert.Empty(result.RemovedAssignments);
    }

    [Fact]
    public async Task RemoveOwnerAsync_WhenAnotherOwnerRemains_Removes()
    {
        var storeManager = MockRepository.Create<IStoreManager>();
        var store = MockRepository.Create<IRoleAssignmentStore>();

        storeManager
            .Setup(x => x.GetStore<IRoleAssignmentStore>())
            .Returns(store.Object)
            .Verifiable();
        store
            .Setup(x =>
                x.GetByResourceAsync(
                    ResourceNodeTypes.Client,
                    "client-1",
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync([Owner("p1", "a1"), Owner("p2", "a2")])
            .Verifiable();
        var removed = new List<string>();
        store
            .Setup(x => x.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback((string id, CancellationToken _) => removed.Add(id))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        var service = new DefaultResourceOwnershipService(
            Mock.Of<ICryptoService>(),
            Mock.Of<IPrincipalResolver>(),
            Mock.Of<IAmbientTenantAccessor>()
        );
        var result = await service.RemoveOwnerAsync(
            storeManager.Object,
            ResourceNodeTypes.Client,
            "client-1",
            "p1",
            CancellationToken.None
        );

        Assert.Equal(OwnerRemovalResult.Removed, result.Result);
        Assert.Equal(["a1"], removed);
    }

    [Fact]
    public async Task RemoveOwnerAsync_WhenRemoved_ReportsTheRemovedAssignments()
    {
        var storeManager = MockRepository.Create<IStoreManager>();
        var store = MockRepository.Create<IRoleAssignmentStore>();

        storeManager
            .Setup(x => x.GetStore<IRoleAssignmentStore>())
            .Returns(store.Object)
            .Verifiable();
        store
            .Setup(x =>
                x.GetByResourceAsync(
                    ResourceNodeTypes.Client,
                    "client-1",
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync([Owner("p1", "a1"), Owner("p2", "a2")])
            .Verifiable();
        store
            .Setup(x => x.RemoveAsync("a1", It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        var service = new DefaultResourceOwnershipService(
            Mock.Of<ICryptoService>(),
            Mock.Of<IPrincipalResolver>(),
            Mock.Of<IAmbientTenantAccessor>()
        );
        var result = await service.RemoveOwnerAsync(
            storeManager.Object,
            ResourceNodeTypes.Client,
            "client-1",
            "p1",
            CancellationToken.None
        );

        Assert.Equal(OwnerRemovalResult.Removed, result.Result);
        var removedAssignment = Assert.Single(result.RemovedAssignments);
        Assert.Equal("a1", removedAssignment.AssignmentId);
        Assert.Equal("p1", removedAssignment.PrincipalId);
    }

    private static PersistedRoleAssignment Assignment(
        string principalId,
        string assignmentId,
        string roleName
    ) =>
        new()
        {
            TenantId = "tenant-1",
            AssignmentId = assignmentId,
            PrincipalId = principalId,
            RoleName = roleName,
            ResourceType = ResourceNodeTypes.Client,
            ResourceId = "client-1",
            ConcurrencyToken = string.Empty,
        };

    private (Mock<IStoreManager> StoreManager, Mock<IRoleAssignmentStore> Store) CreateStore(
        params PersistedRoleAssignment[] existing
    )
    {
        var storeManager = MockRepository.Create<IStoreManager>();
        var store = MockRepository.Create<IRoleAssignmentStore>();
        storeManager
            .Setup(x => x.GetStore<IRoleAssignmentStore>())
            .Returns(store.Object)
            .Verifiable();
        store
            .Setup(x =>
                x.GetByResourceAsync(
                    ResourceNodeTypes.Client,
                    "client-1",
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(existing)
            .Verifiable();
        return (storeManager, store);
    }

    [Fact]
    public async Task AddAssignmentAsync_WhenTheRoleIsAlreadyAssigned_ReturnsTheExistingAssignment()
    {
        var (storeManager, _) = CreateStore(Assignment("p1", "a1", BuiltInRoles.TenantAdmin));

        // No AddAsync/crypto setups: a strict-mock failure would mean a duplicate was written.
        var service = new DefaultResourceOwnershipService(
            Mock.Of<ICryptoService>(),
            Mock.Of<IPrincipalResolver>(),
            Mock.Of<IAmbientTenantAccessor>()
        );
        var result = await service.AddAssignmentAsync(
            storeManager.Object,
            "tenant-1",
            ResourceNodeTypes.Client,
            "client-1",
            "p1",
            BuiltInRoles.TenantAdmin,
            CancellationToken.None
        );

        Assert.Equal("a1", result.Assignment.AssignmentId);
    }

    [Fact]
    public async Task AddAssignmentAsync_WhenThePrincipalHoldsTheRole_ReportsNothingAdded()
    {
        var existing = Assignment("p1", "a1", BuiltInRoles.TenantAdmin);
        var (storeManager, _) = CreateStore(existing);

        // No AddAsync/crypto setups: a strict-mock failure would mean a duplicate was written.
        var service = new DefaultResourceOwnershipService(
            Mock.Of<ICryptoService>(),
            Mock.Of<IPrincipalResolver>(),
            Mock.Of<IAmbientTenantAccessor>()
        );
        var result = await service.AddAssignmentAsync(
            storeManager.Object,
            "tenant-1",
            ResourceNodeTypes.Client,
            "client-1",
            "p1",
            BuiltInRoles.TenantAdmin,
            CancellationToken.None
        );

        Assert.False(result.IsAdded);
        Assert.Same(existing, result.Assignment);
    }

    [Fact]
    public async Task AddAssignmentAsync_WhenAdded_ReportsTheAssignmentAdded()
    {
        var (storeManager, store) = CreateStore();
        var crypto = MockRepository.Create<ICryptoService>();
        crypto
            .Setup(x => x.GenerateKey(16, BinaryEncodings.Base64Url))
            .Returns("new-assign")
            .Verifiable();
        PersistedRoleAssignment? captured = null;
        store
            .Setup(x =>
                x.AddAsync(It.IsAny<PersistedRoleAssignment>(), It.IsAny<CancellationToken>())
            )
            .Callback((PersistedRoleAssignment a, CancellationToken _) => captured = a)
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        var service = new DefaultResourceOwnershipService(
            crypto.Object,
            Mock.Of<IPrincipalResolver>(),
            Mock.Of<IAmbientTenantAccessor>()
        );
        var result = await service.AddAssignmentAsync(
            storeManager.Object,
            "tenant-1",
            ResourceNodeTypes.Client,
            "client-1",
            "p1",
            BuiltInRoles.TenantAdmin,
            CancellationToken.None
        );

        Assert.True(result.IsAdded);
        Assert.Same(captured, result.Assignment);
        Assert.Equal("new-assign", result.Assignment.AssignmentId);
        Assert.Equal("tenant-1", result.Assignment.TenantId);
        Assert.Equal("p1", result.Assignment.PrincipalId);
        Assert.Equal(BuiltInRoles.TenantAdmin, result.Assignment.RoleName);
        Assert.Equal(ResourceNodeTypes.Client, result.Assignment.ResourceType);
        Assert.Equal("client-1", result.Assignment.ResourceId);
    }

    [Fact]
    public async Task RemoveAssignmentAsync_WhenAssigned_RemovesIt()
    {
        var (storeManager, store) = CreateStore(
            Assignment("p1", "a1", BuiltInRoles.TenantAdmin),
            Assignment("p1", "a2", BuiltInRoles.Owner)
        );
        var removed = new List<string>();
        store
            .Setup(x => x.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback((string id, CancellationToken _) => removed.Add(id))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        var service = new DefaultResourceOwnershipService(
            Mock.Of<ICryptoService>(),
            Mock.Of<IPrincipalResolver>(),
            Mock.Of<IAmbientTenantAccessor>()
        );
        var result = await service.RemoveAssignmentAsync(
            storeManager.Object,
            ResourceNodeTypes.Client,
            "client-1",
            "p1",
            BuiltInRoles.TenantAdmin,
            CancellationToken.None
        );

        Assert.Equal(["a1"], removed);
        Assert.Equal(["a1"], result.Select(assignment => assignment.AssignmentId));
    }

    [Fact]
    public async Task RemoveAssignmentAsync_ReturnsTheRemovedAssignments()
    {
        var (storeManager, store) = CreateStore(
            Assignment("p1", "a1", BuiltInRoles.TenantAdmin),
            Assignment("p2", "a2", BuiltInRoles.TenantAdmin),
            Assignment("p1", "a3", BuiltInRoles.Owner)
        );
        store
            .Setup(x => x.RemoveAsync("a1", It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        var service = new DefaultResourceOwnershipService(
            Mock.Of<ICryptoService>(),
            Mock.Of<IPrincipalResolver>(),
            Mock.Of<IAmbientTenantAccessor>()
        );
        var result = await service.RemoveAssignmentAsync(
            storeManager.Object,
            ResourceNodeTypes.Client,
            "client-1",
            "p1",
            BuiltInRoles.TenantAdmin,
            CancellationToken.None
        );

        var removedAssignment = Assert.Single(result);
        Assert.Equal("a1", removedAssignment.AssignmentId);
        Assert.Equal("p1", removedAssignment.PrincipalId);
        Assert.Equal(BuiltInRoles.TenantAdmin, removedAssignment.RoleName);
    }

    [Fact]
    public async Task RemoveAssignmentAsync_WhenMissing_ReturnsNothing()
    {
        var (storeManager, _) = CreateStore(Assignment("p1", "a1", BuiltInRoles.Owner));

        var service = new DefaultResourceOwnershipService(
            Mock.Of<ICryptoService>(),
            Mock.Of<IPrincipalResolver>(),
            Mock.Of<IAmbientTenantAccessor>()
        );
        var result = await service.RemoveAssignmentAsync(
            storeManager.Object,
            ResourceNodeTypes.Client,
            "client-1",
            "p1",
            BuiltInRoles.TenantAdmin,
            CancellationToken.None
        );

        Assert.Empty(result);
    }
}
