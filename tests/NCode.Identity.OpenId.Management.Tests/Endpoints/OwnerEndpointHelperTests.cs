// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using NCode.Identity.Cryptography;
using NCode.Identity.OpenId.Management.Authorization;
using NCode.Identity.OpenId.Management.Contracts;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Management.Endpoints;

public class OwnerEndpointHelperTests : IDisposable
{
    private MockRepository MockRepository { get; } = new(MockBehavior.Strict);

    public void Dispose() => MockRepository.Verify();

    private const string TenantId = "tenant-1";
    private const string ResourceId = "client-1";

    private sealed class TestHandler(
        IAuthorizationService authorizationService,
        IStoreManagerFactory storeManagerFactory,
        IResourceOwnershipService ownershipService
    ) : BaseOwnableApiEndpointHandler
    {
        protected override IAuthorizationService AuthorizationService { get; } =
            authorizationService;
        protected override ICryptoService CryptoService { get; } = Mock.Of<ICryptoService>();
        protected override IStoreManagerFactory StoreManagerFactory { get; } = storeManagerFactory;
        protected override IResourceOwnershipService ResourceOwnershipService { get; } =
            ownershipService;
    }

    private static HttpContext CreateHttpContext() =>
        new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity("test")) };

    private Mock<IAuthorizationService> Authz(AuthorizationResult result)
    {
        var mock = MockRepository.Create<IAuthorizationService>();
        mock.Setup(x =>
                x.AuthorizeAsync(
                    It.IsAny<ClaimsPrincipal>(),
                    It.IsAny<object?>(),
                    It.IsAny<IEnumerable<IAuthorizationRequirement>>()
                )
            )
            .ReturnsAsync(result)
            .Verifiable();
        return mock;
    }

    private (Mock<IStoreManagerFactory>, Mock<IStoreManager>) StoreManager()
    {
        var factory = MockRepository.Create<IStoreManagerFactory>();
        var manager = MockRepository.Create<IStoreManager>();
        factory
            .Setup(x => x.CreateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(manager.Object)
            .Verifiable();
        manager.Setup(x => x.DisposeAsync()).Returns(ValueTask.CompletedTask).Verifiable();
        return (factory, manager);
    }

    private static PersistedRoleAssignment Owner(string principalId, string assignmentId = "a1") =>
        new()
        {
            TenantId = TenantId,
            AssignmentId = assignmentId,
            PrincipalId = principalId,
            RoleName = BuiltInRoles.Owner,
            ResourceType = ResourceNodeTypes.Client,
            ResourceId = ResourceId,
            ConcurrencyToken = string.Empty,
        };

    private static ValueTask<bool> Exists(IStoreManager _, CancellationToken __) =>
        ValueTask.FromResult(true);

    private static ValueTask<bool> Missing(IStoreManager _, CancellationToken __) =>
        ValueTask.FromResult(false);

    [Fact]
    public async Task ProcessListOwnersAsync_WhenAuthorized_ReturnsOwners()
    {
        var authz = Authz(AuthorizationResult.Success());
        var (factory, manager) = StoreManager();
        var ownership = MockRepository.Create<IResourceOwnershipService>();
        ownership
            .Setup(x =>
                x.GetOwnersAsync(
                    manager.Object,
                    ResourceNodeTypes.Client,
                    ResourceId,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync([Owner("p1")])
            .Verifiable();

        var handler = new TestHandler(authz.Object, factory.Object, ownership.Object);
        var result = await handler.ProcessListOwnersAsync(
            CreateHttpContext(),
            TenantId,
            ResourceNodeTypes.Client,
            ResourceId,
            Exists,
            CancellationToken.None
        );

        var json = Assert.IsType<JsonHttpResult<CollectionResource<OwnerResource>>>(result);
        Assert.Single(json.Value!.Items);
    }

    [Fact]
    public async Task ProcessListOwnersAsync_WhenUnauthorized_Forbids()
    {
        var authz = Authz(AuthorizationResult.Failed());
        // A forbidden request never opens the store.
        var factory = MockRepository.Create<IStoreManagerFactory>();
        var ownership = MockRepository.Create<IResourceOwnershipService>();

        var handler = new TestHandler(authz.Object, factory.Object, ownership.Object);
        var result = await handler.ProcessListOwnersAsync(
            CreateHttpContext(),
            TenantId,
            ResourceNodeTypes.Client,
            ResourceId,
            Exists,
            CancellationToken.None
        );

        var problem = Assert.IsType<ProblemHttpResult>(result);

        Assert.Equal(StatusCodes.Status403Forbidden, problem.StatusCode);
    }

    [Fact]
    public async Task ProcessAddOwnerAsync_WhenAuthorized_ReturnsCreated()
    {
        var authz = Authz(AuthorizationResult.Success());
        var (factory, manager) = StoreManager();
        manager
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        var ownership = MockRepository.Create<IResourceOwnershipService>();
        ownership
            .Setup(x =>
                x.AddOwnerAsync(
                    manager.Object,
                    TenantId,
                    ResourceNodeTypes.Client,
                    ResourceId,
                    "p2",
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new RoleAssignmentGrant { Assignment = Owner("p2", "a2"), IsAdded = true }
            )
            .Verifiable();

        var handler = new TestHandler(authz.Object, factory.Object, ownership.Object);
        var result = await handler.ProcessAddOwnerAsync(
            CreateHttpContext(),
            TenantId,
            ResourceNodeTypes.Client,
            ResourceId,
            new AddOwnerRequest { PrincipalId = "p2" },
            "/clients/client-1/owners",
            Exists,
            CancellationToken.None
        );

        Assert.IsType<Created<OwnerResource>>(result);
    }

    [Fact]
    public async Task ProcessAddOwnerAsync_WhenResourceMissing_ReturnsNotFound()
    {
        var authz = Authz(AuthorizationResult.Success());
        var (factory, _) = StoreManager();
        var ownership = MockRepository.Create<IResourceOwnershipService>();

        var handler = new TestHandler(authz.Object, factory.Object, ownership.Object);
        var result = await handler.ProcessAddOwnerAsync(
            CreateHttpContext(),
            TenantId,
            ResourceNodeTypes.Client,
            ResourceId,
            new AddOwnerRequest { PrincipalId = "p2" },
            "/clients/client-1/owners",
            Missing,
            CancellationToken.None
        );

        Assert.IsType<NotFound>(result);
    }

    [Fact]
    public async Task ProcessRemoveOwnerAsync_WhenLastOwner_ReturnsConflict()
    {
        var authz = Authz(AuthorizationResult.Success());
        var (factory, manager) = StoreManager();
        var ownership = MockRepository.Create<IResourceOwnershipService>();
        ownership
            .Setup(x =>
                x.RemoveOwnerAsync(
                    manager.Object,
                    ResourceNodeTypes.Client,
                    ResourceId,
                    "p1",
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new OwnerRemoval
                {
                    Result = OwnerRemovalResult.LastOwnerForbidden,
                    RemovedAssignments = [],
                }
            )
            .Verifiable();

        var handler = new TestHandler(authz.Object, factory.Object, ownership.Object);
        var result = await handler.ProcessRemoveOwnerAsync(
            CreateHttpContext(),
            TenantId,
            ResourceNodeTypes.Client,
            ResourceId,
            "p1",
            CancellationToken.None
        );

        var problem = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(StatusCodes.Status409Conflict, problem.StatusCode);
    }

    [Fact]
    public async Task ProcessRemoveOwnerAsync_WhenRemoved_ReturnsNoContent()
    {
        var authz = Authz(AuthorizationResult.Success());
        var (factory, manager) = StoreManager();
        manager
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        var ownership = MockRepository.Create<IResourceOwnershipService>();
        ownership
            .Setup(x =>
                x.RemoveOwnerAsync(
                    manager.Object,
                    ResourceNodeTypes.Client,
                    ResourceId,
                    "p1",
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new OwnerRemoval
                {
                    Result = OwnerRemovalResult.Removed,
                    RemovedAssignments = [Owner("p1")],
                }
            )
            .Verifiable();

        var handler = new TestHandler(authz.Object, factory.Object, ownership.Object);
        var result = await handler.ProcessRemoveOwnerAsync(
            CreateHttpContext(),
            TenantId,
            ResourceNodeTypes.Client,
            ResourceId,
            "p1",
            CancellationToken.None
        );

        Assert.IsType<NoContent>(result);
    }
}
