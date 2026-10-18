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

using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NCode.Identity.Cryptography;
using NCode.Identity.Jose;
using NCode.Identity.OpenId.Accounts.DataContracts;
using NCode.Identity.OpenId.Accounts.Stores;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Tenants;
using NCode.Identity.Secrets.Persistence;
using NCode.Identity.Server.ConformanceEnvironment;
using NCode.Persistence.Stores;
using Xunit;

namespace NCode.Identity.Server.Tests;

public class ConformanceSeedHandlerTests : BaseTests
{
    private const string WorkloadTenantId = "default";

    private ConformanceSeedHandler CreateHandler(ConformanceSeedOptions options)
    {
        // Strict mocks: on the guard paths under test the secret, crypto, and time dependencies are untouched.
        var mockSecretGenerator = CreateStrictMock<ISecretGenerator>();
        var mockCryptoService = CreateStrictMock<ICryptoService>();

        return new ConformanceSeedHandler(
            options,
            mockSecretGenerator.Object,
            mockCryptoService.Object,
            TimeProvider.System,
            NullLogger<ConformanceSeedHandler>.Instance
        );
    }

    private SeedTenantCommand CreateCommand(
        string tenantId,
        TenantPlane plane,
        IStoreManager? storeManager = null
    )
    {
        // Strict mock: the store manager is untouched on the guard paths under test.
        storeManager ??= CreateStrictMock<IStoreManager>().Object;

        var persistedTenant = new PersistedTenant
        {
            TenantId = tenantId,
            DomainName = null,
            Issuer = null,
            ConcurrencyToken = "token",
            IsDisabled = false,
            DisplayName = tenantId,
            Settings = null!,
            Secrets = null!,
        };

        return new SeedTenantCommand(
            new TenantSeedContext
            {
                Tenant = persistedTenant,
                Plane = plane,
                IsNewlyProvisioned = true,
                StoreManager = storeManager,
            }
        );
    }

    #region HandleAsync Tests

    [Fact]
    public async Task HandleAsync_WhenNothingConfiguredToSeed_DoesNothing()
    {
        var handler = CreateHandler(
            new ConformanceSeedOptions { Clients = [], TestUserPassword = null }
        );

        await handler.HandleAsync(
            CreateCommand(WorkloadTenantId, TenantPlane.Workload),
            CancellationToken.None
        );
    }

    [Fact]
    public async Task HandleAsync_WhenRootPlane_DoesNothing()
    {
        var handler = CreateHandler(
            new ConformanceSeedOptions { TestUserPassword = "conformance-password" }
        );

        await handler.HandleAsync(CreateCommand("root", TenantPlane.Root), CancellationToken.None);
    }

    [Fact]
    public async Task HandleAsync_WhenTenantDoesNotMatchConfiguredTarget_DoesNothing()
    {
        var handler = CreateHandler(
            new ConformanceSeedOptions
            {
                TestUserPassword = "conformance-password",
                TenantId = "tenant-a",
            }
        );

        await handler.HandleAsync(
            CreateCommand("tenant-b", TenantPlane.Workload),
            CancellationToken.None
        );
    }

    [Fact]
    public async Task HandleAsync_WhenTestUserConfigured_SeedsAccountWithProfileClaims()
    {
        var options = new ConformanceSeedOptions
        {
            Clients = [],
            TestUserName = "conformance-user",
            TestUserEmail = "conformance-user@localhost",
            TestUserGivenName = "Conformance",
            TestUserFamilyName = "User",
            TestUserPassword = "conformance-password",
        };
        var handler = CreateHandler(options);

        var mockStoreManager = CreateStrictMock<IStoreManager>();
        var mockAccountStore = CreateStrictMock<ILocalAccountStore>();
        mockStoreManager
            .Setup(x => x.GetStore<ILocalAccountStore>())
            .Returns(mockAccountStore.Object)
            .Verifiable();
        mockAccountStore
            .Setup(x =>
                x.GetByUserNameOrDefaultAsync("conformance-user", It.IsAny<CancellationToken>())
            )
            .ReturnsAsync((PersistedLocalAccount?)null)
            .Verifiable();

        PersistedLocalAccount? captured = null;
        mockAccountStore
            .Setup(x =>
                x.AddAsync(
                    It.IsAny<PersistedLocalAccount>(),
                    It.IsAny<ReadOnlyMemory<byte>?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback(
                (PersistedLocalAccount account, ReadOnlyMemory<byte>? _, CancellationToken _) =>
                    captured = account
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        await handler.HandleAsync(
            CreateCommand(WorkloadTenantId, TenantPlane.Workload, mockStoreManager.Object),
            CancellationToken.None
        );

        Assert.NotNull(captured);
        Assert.Equal("conformance-user@localhost", captured.Email);
        Assert.True(captured.EmailVerified);

        var claims = captured.Claims.ToDictionary(claim => claim.Type, claim => claim.Value);
        Assert.Equal("Conformance", claims[JoseClaimNames.Payload.GivenName]);
        Assert.Equal("User", claims[JoseClaimNames.Payload.FamilyName]);
        Assert.Equal("Conformance User", claims[JoseClaimNames.Payload.Name]);
    }

    #endregion
}
