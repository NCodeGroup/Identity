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
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using NCode.Identity.Logic;
using NCode.Identity.OpenId.Management.Authorization;
using NCode.Identity.OpenId.Management.Contracts;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Management.Endpoints;

public class OwnerEndpointHelperTests
{
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

    private static Mock<IAuthorizationService> Authz(AuthorizationResult result)
    {
        var mock = new Mock<IAuthorizationService>(MockBehavior.Strict);
        mock.Setup(x =>
                x.AuthorizeAsync(
                    It.IsAny<ClaimsPrincipal>(),
                    It.IsAny<object?>(),
                    It.IsAny<IEnumerable<IAuthorizationRequirement>>()
                )
            )
            .ReturnsAsync(result);
        return mock;
    }

    private static (Mock<IStoreManagerFactory>, Mock<IStoreManager>) StoreManager()
    {
        var factory = new Mock<IStoreManagerFactory>(MockBehavior.Strict);
        var manager = new Mock<IStoreManager>(MockBehavior.Strict);
        factory
            .Setup(x => x.CreateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(manager.Object);
        manager.Setup(x => x.DisposeAsync()).Returns(ValueTask.CompletedTask);
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
        var ownership = new Mock<IResourceOwnershipService>(MockBehavior.Strict);
        ownership
            .Setup(x =>
                x.GetOwnersAsync(
                    manager.Object,
                    ResourceNodeTypes.Client,
                    ResourceId,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync([Owner("p1")]);

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
        var (factory, _) = StoreManager();
        var ownership = new Mock<IResourceOwnershipService>(MockBehavior.Strict);

        var handler = new TestHandler(authz.Object, factory.Object, ownership.Object);
        var result = await handler.ProcessListOwnersAsync(
            CreateHttpContext(),
            TenantId,
            ResourceNodeTypes.Client,
            ResourceId,
            Exists,
            CancellationToken.None
        );

        Assert.IsType<ForbidHttpResult>(result);
    }

    [Fact]
    public async Task ProcessAddOwnerAsync_WhenAuthorized_ReturnsCreated()
    {
        var authz = Authz(AuthorizationResult.Success());
        var (factory, manager) = StoreManager();
        manager
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);
        var ownership = new Mock<IResourceOwnershipService>(MockBehavior.Strict);
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
            .ReturnsAsync(Owner("p2", "a2"));

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
        var ownership = new Mock<IResourceOwnershipService>(MockBehavior.Strict);

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
        var ownership = new Mock<IResourceOwnershipService>(MockBehavior.Strict);
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
            .ReturnsAsync(OwnerRemovalResult.LastOwnerForbidden);

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
            .Returns(ValueTask.CompletedTask);
        var ownership = new Mock<IResourceOwnershipService>(MockBehavior.Strict);
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
            .ReturnsAsync(OwnerRemovalResult.Removed);

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
