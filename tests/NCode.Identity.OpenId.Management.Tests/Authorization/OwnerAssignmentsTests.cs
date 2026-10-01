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
using NCode.Identity.OpenId.Management.Authorization;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Management;

public class OwnerAssignmentsTests
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

        crypto
            .Setup(x => x.GenerateKey(16, BinaryEncodingType.Base64Url))
            .Returns("assign-1")
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

        await OwnerAssignments.AssignCreatorAsync(
            CreateUser(new Claim("sub", "subject-1")),
            storeManager.Object,
            crypto.Object,
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
    public async Task AssignCreatorAsync_WhenNoSubjectClaim_DoesNothing()
    {
        var mocks = new MockRepository(MockBehavior.Strict);
        var storeManager = mocks.Create<IStoreManager>();
        var crypto = mocks.Create<ICryptoService>();

        // No setups: a strict-mock failure would mean an owner assignment was attempted without a principal.
        await OwnerAssignments.AssignCreatorAsync(
            CreateUser(),
            storeManager.Object,
            crypto.Object,
            "tenant-1",
            ResourceNodeTypes.Client,
            "client-1",
            CancellationToken.None
        );
    }
}
