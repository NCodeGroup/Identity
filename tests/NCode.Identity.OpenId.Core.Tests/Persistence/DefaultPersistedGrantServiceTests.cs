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

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Moq;
using NCode.Identity.Cryptography;
using NCode.Identity.OpenId.Authentication.Persistence;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Environments;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Persistence.Stores;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Persistence;

public class DefaultPersistedGrantServiceTests : BaseTests
{
    private const string TenantId = "tenant-1";
    private const string GrantType = "authorization_code";
    private const string GrantKey = "grant-key";
    private const string HashedKey = "hashed";

    public DefaultPersistedGrantServiceTests()
    {
        MockCryptoService = CreateStrictMock<ICryptoService>();
        MockStoreManagerFactory = CreateStrictMock<IStoreManagerFactory>();
        MockStoreManager = CreateStrictMock<IStoreManager>();
        MockGrantStore = CreateStrictMock<IGrantStore>();
        MockPayloadSerializer = CreateStrictMock<IPersistedPayloadSerializer>();
        MockEnvironment = CreateStrictMock<OpenIdEnvironment>();

        MockContext = CreateStrictMock<OpenIdContext>();
        OpenIdContext = MockContext.Object;

        Service = new DefaultPersistedGrantService(
            TimeProvider.System,
            MockCryptoService.Object,
            MockStoreManagerFactory.Object,
            MockPayloadSerializer.Object
        );
    }

    private Mock<ICryptoService> MockCryptoService { get; }
    private Mock<IStoreManagerFactory> MockStoreManagerFactory { get; }
    private Mock<IStoreManager> MockStoreManager { get; }
    private Mock<IGrantStore> MockGrantStore { get; }
    private Mock<IPersistedPayloadSerializer> MockPayloadSerializer { get; }
    private Mock<OpenIdEnvironment> MockEnvironment { get; }
    private Mock<OpenIdContext> MockContext { get; }
    private OpenIdContext OpenIdContext { get; }
    private JsonSerializerOptions JsonOptions { get; } = new(JsonSerializerDefaults.Web);
    private DefaultPersistedGrantService Service { get; }

    #region Scaffolding

    private PersistedGrantId CreateGrantId() =>
        Service.CreateGrantId(TenantId, GrantType, GrantKey);

    private static PersistedGrant CreateEnvelope(
        DateTimeOffset? consumedWhen = null,
        DateTimeOffset? revokedWhen = null
    ) =>
        new()
        {
            GrantType = GrantType,
            HashedKey = HashedKey,
            GrantId = "grant-1",
            TenantId = TenantId,
            ClientId = "client-1",
            SubjectId = "subject-1",
            SessionId = null,
            RefreshTokenLineageId = null,
            CreatedWhen = DateTimeOffset.UtcNow,
            ExpiresWhen = DateTimeOffset.UtcNow.AddHours(1),
            RevokedWhen = revokedWhen,
            ConsumedWhen = consumedWhen,
            PayloadJson = JsonElement.Parse("{}"),
        };

    private static PersistedGrant<string> CreateSuccessor() =>
        new()
        {
            Status = PersistedGrantStatus.Active,
            TenantId = TenantId,
            ClientId = "client-1",
            SubjectId = "subject-1",
            SessionId = null,
            RefreshTokenLineageId = "lineage-1",
            Payload = "successor",
        };

    private void SetupEnvironment() =>
        MockContext.SetupGet(x => x.Environment).Returns(MockEnvironment.Object).Verifiable();

    private void SetupStoreManager()
    {
        MockStoreManagerFactory
            .Setup(x => x.CreateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(MockStoreManager.Object)
            .Verifiable();

        MockStoreManager
            .Setup(x => x.GetStore<IGrantStore>())
            .Returns(MockGrantStore.Object)
            .Verifiable();
        MockStoreManager.Setup(x => x.DisposeAsync()).Returns(ValueTask.CompletedTask).Verifiable();
    }

    private void SetupHashValue() =>
        MockCryptoService
            .Setup(x =>
                x.HashValue(
                    It.IsAny<string>(),
                    HashAlgorithmName.SHA256,
                    BinaryEncodings.Base64,
                    It.IsAny<Encoding?>()
                )
            )
            .Returns(HashedKey)
            .Verifiable();

    private void SetupLookup(PersistedGrant envelope)
    {
        SetupHashValue();

        SetupStoreManager();

        MockGrantStore
            .Setup(x => x.GetOrDefaultAsync(GrantType, HashedKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(envelope)
            .Verifiable();
    }

    private void SetupLookupSequence(params PersistedGrant[] envelopes)
    {
        SetupHashValue();

        SetupStoreManager();

        var sequence = MockGrantStore.SetupSequence(x =>
            x.GetOrDefaultAsync(GrantType, HashedKey, It.IsAny<CancellationToken>())
        );
        foreach (var envelope in envelopes)
        {
            sequence = sequence.ReturnsAsync(envelope);
        }
    }

    private void SetupDeserialize()
    {
        SetupEnvironment();

        MockEnvironment.SetupGet(x => x.JsonSerializerOptions).Returns(JsonOptions).Verifiable();

        MockPayloadSerializer
            .Setup(x => x.Deserialize<string>(It.IsAny<JsonElement>(), JsonOptions))
            .Returns("payload")
            .Verifiable();
    }

    private void SetupSerialize()
    {
        SetupEnvironment();

        MockEnvironment.SetupGet(x => x.JsonSerializerOptions).Returns(JsonOptions).Verifiable();

        MockPayloadSerializer
            .Setup(x => x.Serialize("successor", JsonOptions))
            .Returns(JsonElement.Parse("{}"))
            .Verifiable();
    }

    private void SetupSerializeUpdatedPayload()
    {
        SetupEnvironment();

        MockEnvironment.SetupGet(x => x.JsonSerializerOptions).Returns(JsonOptions).Verifiable();

        MockPayloadSerializer
            .Setup(x => x.Serialize("payload", JsonOptions))
            .Returns(JsonElement.Parse("{\"a\":1}"))
            .Verifiable();
    }

    private void SetupUpdate(PersistedGrant envelope, ValueTask result) =>
        MockGrantStore
            .Setup(x => x.UpdateAsync(envelope, It.IsAny<CancellationToken>()))
            .Returns(result)
            .Verifiable();

    private void SetupSaveChanges(ValueTask result) =>
        MockStoreManager
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(result)
            .Verifiable();

    #endregion

    #region ConsumeOnceOrDefault Tests

    [Fact]
    public async Task ConsumeOnceOrDefault_WhenTheGrantIsActive_ConsumesAndReturnsIt()
    {
        var envelope = CreateEnvelope();
        SetupLookup(envelope);
        SetupDeserialize();
        SetupUpdate(envelope, ValueTask.CompletedTask);
        SetupSaveChanges(ValueTask.CompletedTask);

        var result = await Service.ConsumeOnceOrDefault<string>(
            OpenIdContext,
            CreateGrantId(),
            CancellationToken.None
        );

        Assert.NotNull(result);
        Assert.Equal(PersistedGrantStatus.Active, result.Value.Status);
        Assert.Equal("payload", result.Value.Payload);
        Assert.NotNull(result.Value.ConsumedWhen);
        Assert.Equal(envelope.ConsumedWhen, result.Value.ConsumedWhen);
    }

    [Fact]
    public async Task ConsumeOnceOrDefault_WhenAnotherRequestSavesFirst_ReturnsNull()
    {
        var envelope = CreateEnvelope();
        SetupLookup(envelope);
        SetupDeserialize();
        SetupUpdate(envelope, ValueTask.CompletedTask);
        SetupSaveChanges(ValueTask.FromException(new StoreConcurrencyException("conflict")));

        var result = await Service.ConsumeOnceOrDefault<string>(
            OpenIdContext,
            CreateGrantId(),
            CancellationToken.None
        );

        Assert.Null(result);
    }

    [Fact]
    public async Task ConsumeOnceOrDefault_WhenTheStoreSeesAStaleToken_ReturnsNull()
    {
        var envelope = CreateEnvelope();
        SetupLookup(envelope);
        SetupDeserialize();
        SetupUpdate(envelope, ValueTask.FromException(new StoreConcurrencyException("conflict")));

        var result = await Service.ConsumeOnceOrDefault<string>(
            OpenIdContext,
            CreateGrantId(),
            CancellationToken.None
        );

        Assert.Null(result);
    }

    [Fact]
    public async Task ConsumeOnceOrDefault_WhenAlreadyConsumed_ReturnsNull()
    {
        var envelope = CreateEnvelope(consumedWhen: DateTimeOffset.UtcNow.AddMinutes(-1));
        SetupLookup(envelope);
        SetupEnvironment();

        var result = await Service.ConsumeOnceOrDefault<string>(
            OpenIdContext,
            CreateGrantId(),
            CancellationToken.None
        );

        Assert.Null(result);
    }

    #endregion

    #region SetRevokedOnceAsync Tests

    [Fact]
    public async Task SetRevokedOnceAsync_WhenTheFirstSaveLosesARace_ReadsTheGrantAgainAndRevokesIt()
    {
        var first = CreateEnvelope();
        var second = CreateEnvelope();
        var revokedWhen = DateTimeOffset.UtcNow;
        SetupLookupSequence(first, second);
        MockGrantStore
            .Setup(x => x.UpdateAsync(It.IsAny<PersistedGrant>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask)
            .Verifiable(Times.Exactly(2));
        MockStoreManager
            .SetupSequence(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new StoreConcurrencyException("conflict"))
            .Returns(ValueTask.CompletedTask);

        await Service.SetRevokedOnceAsync(
            OpenIdContext,
            CreateGrantId(),
            revokedWhen,
            CancellationToken.None
        );

        Assert.Equal(revokedWhen, second.RevokedWhen);
    }

    [Fact]
    public async Task SetRevokedOnceAsync_WhenAConcurrentRequestRevokedItFirst_Returns()
    {
        var first = CreateEnvelope();
        var second = CreateEnvelope(revokedWhen: DateTimeOffset.UtcNow.AddMinutes(-1));
        SetupLookupSequence(first, second);
        MockGrantStore
            .Setup(x => x.UpdateAsync(It.IsAny<PersistedGrant>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask)
            .Verifiable(Times.Once());
        SetupSaveChanges(ValueTask.FromException(new StoreConcurrencyException("conflict")));

        await Service.SetRevokedOnceAsync(
            OpenIdContext,
            CreateGrantId(),
            DateTimeOffset.UtcNow,
            CancellationToken.None
        );
    }

    [Fact]
    public async Task SetRevokedOnceAsync_WhenEverySaveLosesARace_Throws()
    {
        SetupLookupSequence(CreateEnvelope(), CreateEnvelope(), CreateEnvelope());
        MockGrantStore
            .Setup(x => x.UpdateAsync(It.IsAny<PersistedGrant>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask)
            .Verifiable(Times.Exactly(3));
        SetupSaveChanges(ValueTask.FromException(new StoreConcurrencyException("conflict")));

        await Assert.ThrowsAsync<StoreConcurrencyException>(async () =>
            await Service.SetRevokedOnceAsync(
                OpenIdContext,
                CreateGrantId(),
                DateTimeOffset.UtcNow,
                CancellationToken.None
            )
        );
    }

    #endregion

    #region TryRevokeOnceAsync Tests

    [Fact]
    public async Task TryRevokeOnceAsync_WhenTheGrantIsActive_RevokesItAndReturnsTrue()
    {
        var envelope = CreateEnvelope();
        var revokedWhen = DateTimeOffset.UtcNow;
        SetupLookup(envelope);
        SetupUpdate(envelope, ValueTask.CompletedTask);
        SetupSaveChanges(ValueTask.CompletedTask);

        var result = await Service.TryRevokeOnceAsync(
            OpenIdContext,
            CreateGrantId(),
            revokedWhen,
            CancellationToken.None
        );

        Assert.True(result);
        Assert.Equal(revokedWhen, envelope.RevokedWhen);
    }

    [Fact]
    public async Task TryRevokeOnceAsync_WhenAnotherRequestSpentTheGrantFirst_ReturnsFalse()
    {
        SetupLookupSequence(CreateEnvelope(), CreateEnvelope(revokedWhen: DateTimeOffset.UtcNow));
        MockGrantStore
            .Setup(x => x.UpdateAsync(It.IsAny<PersistedGrant>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask)
            .Verifiable(Times.Once());
        SetupSaveChanges(ValueTask.FromException(new StoreConcurrencyException("conflict")));

        var result = await Service.TryRevokeOnceAsync(
            OpenIdContext,
            CreateGrantId(),
            DateTimeOffset.UtcNow,
            CancellationToken.None
        );

        Assert.False(result);
    }

    [Fact]
    public async Task TryRevokeOnceAsync_WhenAConflictingWriteLeftTheGrantActive_ReadsItAgainAndRevokesIt()
    {
        var first = CreateEnvelope();
        var second = CreateEnvelope();
        var revokedWhen = DateTimeOffset.UtcNow;
        SetupLookupSequence(first, second);
        MockGrantStore
            .Setup(x => x.UpdateAsync(It.IsAny<PersistedGrant>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask)
            .Verifiable(Times.Exactly(2));
        MockStoreManager
            .SetupSequence(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new StoreConcurrencyException("conflict"))
            .Returns(ValueTask.CompletedTask);

        var result = await Service.TryRevokeOnceAsync(
            OpenIdContext,
            CreateGrantId(),
            revokedWhen,
            CancellationToken.None
        );

        Assert.True(result);
        Assert.Equal(revokedWhen, second.RevokedWhen);
    }

    [Fact]
    public async Task TryRevokeOnceAsync_WhenTheGrantIsRevoked_ReturnsFalse()
    {
        var envelope = CreateEnvelope(revokedWhen: DateTimeOffset.UtcNow.AddMinutes(-1));
        SetupLookup(envelope);

        var result = await Service.TryRevokeOnceAsync(
            OpenIdContext,
            CreateGrantId(),
            DateTimeOffset.UtcNow,
            CancellationToken.None
        );

        Assert.False(result);
    }

    #endregion

    #region ReplaceOnceOrDefault Tests

    [Fact]
    public async Task ReplaceOnceOrDefault_WhenTheReplacedGrantIsActive_RevokesItAndStoresTheSuccessor()
    {
        var replaced = CreateEnvelope();
        var createdWhen = DateTimeOffset.UtcNow;
        SetupLookup(replaced);
        SetupSerialize();
        MockCryptoService
            .Setup(x => x.GenerateKey(16, BinaryEncodings.Base64Url))
            .Returns("grant-2")
            .Verifiable();
        SetupUpdate(replaced, ValueTask.CompletedTask);
        MockGrantStore
            .Setup(x =>
                x.AddAsync(
                    It.Is<PersistedGrant>(g => g.GrantId == "grant-2" && g.RevokedWhen == null),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        SetupSaveChanges(ValueTask.CompletedTask);

        var result = await Service.ReplaceOnceOrDefault(
            OpenIdContext,
            CreateGrantId(),
            CreateGrantId(),
            CreateSuccessor(),
            createdWhen,
            TimeSpan.FromHours(1),
            CancellationToken.None
        );

        Assert.Equal("grant-2", result);
        Assert.Equal(createdWhen, replaced.RevokedWhen);
        Assert.Equal(createdWhen, replaced.ConsumedWhen);
    }

    [Fact]
    public async Task ReplaceOnceOrDefault_WhenAnotherRequestSpentTheGrantFirst_ReturnsNull()
    {
        SetupLookupSequence(CreateEnvelope(), CreateEnvelope(revokedWhen: DateTimeOffset.UtcNow));
        SetupSerialize();
        MockCryptoService
            .Setup(x => x.GenerateKey(16, BinaryEncodings.Base64Url))
            .Returns("grant-2")
            .Verifiable();
        MockGrantStore
            .Setup(x => x.UpdateAsync(It.IsAny<PersistedGrant>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask)
            .Verifiable(Times.Once());
        MockGrantStore
            .Setup(x =>
                x.AddAsync(
                    It.Is<PersistedGrant>(g => g.GrantId == "grant-2" && g.RevokedWhen == null),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable(Times.Once());
        SetupSaveChanges(ValueTask.FromException(new StoreConcurrencyException("conflict")));

        var result = await Service.ReplaceOnceOrDefault(
            OpenIdContext,
            CreateGrantId(),
            CreateGrantId(),
            CreateSuccessor(),
            DateTimeOffset.UtcNow,
            TimeSpan.FromHours(1),
            CancellationToken.None
        );

        Assert.Null(result);
    }

    [Fact]
    public async Task ReplaceOnceOrDefault_WhenAConflictingWriteLeftTheGrantActive_ReadsItAgainAndStoresTheSuccessor()
    {
        var first = CreateEnvelope();
        var second = CreateEnvelope();
        var createdWhen = DateTimeOffset.UtcNow;
        SetupLookupSequence(first, second);
        SetupSerialize();
        MockCryptoService
            .Setup(x => x.GenerateKey(16, BinaryEncodings.Base64Url))
            .Returns("grant-2")
            .Verifiable();
        MockGrantStore
            .Setup(x => x.UpdateAsync(It.IsAny<PersistedGrant>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask)
            .Verifiable(Times.Exactly(2));
        MockGrantStore
            .Setup(x =>
                x.AddAsync(
                    It.Is<PersistedGrant>(g => g.GrantId == "grant-2" && g.RevokedWhen == null),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable(Times.Exactly(2));
        MockStoreManager
            .SetupSequence(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new StoreConcurrencyException("conflict"))
            .Returns(ValueTask.CompletedTask);

        var result = await Service.ReplaceOnceOrDefault(
            OpenIdContext,
            CreateGrantId(),
            CreateGrantId(),
            CreateSuccessor(),
            createdWhen,
            TimeSpan.FromHours(1),
            CancellationToken.None
        );

        Assert.Equal("grant-2", result);
        Assert.Equal(createdWhen, second.RevokedWhen);
        Assert.Equal(createdWhen, second.ConsumedWhen);
    }

    [Fact]
    public async Task ReplaceOnceOrDefault_WhenTheReplacedGrantIsRevoked_ReturnsNull()
    {
        var replaced = CreateEnvelope(revokedWhen: DateTimeOffset.UtcNow.AddMinutes(-1));
        SetupLookup(replaced);

        var result = await Service.ReplaceOnceOrDefault(
            OpenIdContext,
            CreateGrantId(),
            CreateGrantId(),
            CreateSuccessor(),
            DateTimeOffset.UtcNow,
            TimeSpan.FromHours(1),
            CancellationToken.None
        );

        Assert.Null(result);
    }

    #endregion

    #region AddToRefreshTokenLineageOrDefault Tests

    [Fact]
    public async Task AddToRefreshTokenLineageOrDefault_WhenAGrantOfTheLineageIsActive_UpdatesItAndStoresTheGrant()
    {
        var active = CreateEnvelope();
        SetupHashValue();
        SetupStoreManager();
        SetupSerialize();
        MockCryptoService
            .Setup(x => x.GenerateKey(16, BinaryEncodings.Base64Url))
            .Returns("grant-2")
            .Verifiable();
        MockGrantStore
            .Setup(x =>
                x.GetActiveByRefreshTokenLineageAsync("lineage-1", It.IsAny<CancellationToken>())
            )
            .ReturnsAsync([active])
            .Verifiable();
        SetupUpdate(active, ValueTask.CompletedTask);
        MockGrantStore
            .Setup(x =>
                x.AddAsync(
                    It.Is<PersistedGrant>(g =>
                        g.GrantId == "grant-2"
                        && g.RefreshTokenLineageId == "lineage-1"
                        && g.RevokedWhen == null
                    ),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        SetupSaveChanges(ValueTask.CompletedTask);

        var result = await Service.AddToRefreshTokenLineageOrDefault(
            OpenIdContext,
            CreateGrantId(),
            CreateSuccessor(),
            DateTimeOffset.UtcNow,
            TimeSpan.FromHours(1),
            CancellationToken.None
        );

        Assert.Equal("grant-2", result);
    }

    [Fact]
    public async Task AddToRefreshTokenLineageOrDefault_WhenNoGrantOfTheLineageIsActive_ReturnsNull()
    {
        SetupStoreManager();
        MockGrantStore
            .Setup(x =>
                x.GetActiveByRefreshTokenLineageAsync("lineage-1", It.IsAny<CancellationToken>())
            )
            .ReturnsAsync([CreateEnvelope(revokedWhen: DateTimeOffset.UtcNow.AddMinutes(-1))])
            .Verifiable();

        var result = await Service.AddToRefreshTokenLineageOrDefault(
            OpenIdContext,
            CreateGrantId(),
            CreateSuccessor(),
            DateTimeOffset.UtcNow,
            TimeSpan.FromHours(1),
            CancellationToken.None
        );

        Assert.Null(result);
    }

    [Fact]
    public async Task AddToRefreshTokenLineageOrDefault_WhenTheSaveLosesARace_ReadsTheLineageAgain()
    {
        var active = CreateEnvelope();
        SetupHashValue();
        SetupStoreManager();
        SetupSerialize();
        MockCryptoService
            .Setup(x => x.GenerateKey(16, BinaryEncodings.Base64Url))
            .Returns("grant-2")
            .Verifiable();
        MockGrantStore
            .SetupSequence(x =>
                x.GetActiveByRefreshTokenLineageAsync("lineage-1", It.IsAny<CancellationToken>())
            )
            .ReturnsAsync([active])
            .ReturnsAsync([]);
        SetupUpdate(active, ValueTask.CompletedTask);
        MockGrantStore
            .Setup(x => x.AddAsync(It.IsAny<PersistedGrant>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        SetupSaveChanges(ValueTask.FromException(new StoreConcurrencyException("conflict")));

        var result = await Service.AddToRefreshTokenLineageOrDefault(
            OpenIdContext,
            CreateGrantId(),
            CreateSuccessor(),
            DateTimeOffset.UtcNow,
            TimeSpan.FromHours(1),
            CancellationToken.None
        );

        Assert.Null(result);
    }

    [Fact]
    public async Task AddToRefreshTokenLineageOrDefault_WhenTheGrantNamesNoLineage_Throws()
    {
        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await Service.AddToRefreshTokenLineageOrDefault(
                OpenIdContext,
                CreateGrantId(),
                CreateSuccessor() with
                {
                    RefreshTokenLineageId = null,
                },
                DateTimeOffset.UtcNow,
                TimeSpan.FromHours(1),
                CancellationToken.None
            )
        );
    }

    #endregion

    #region RevokeByRefreshTokenLineageAsync Tests

    [Fact]
    public async Task RevokeByRefreshTokenLineageAsync_WhenTheFirstSaveLosesARace_ReadsTheLineageAgain()
    {
        SetupStoreManager();
        MockGrantStore
            .Setup(x =>
                x.RevokeByRefreshTokenLineageAsync(
                    "lineage-1",
                    It.IsAny<DateTimeOffset>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(2L)
            .Verifiable(Times.Exactly(2));
        MockStoreManager
            .SetupSequence(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new StoreConcurrencyException("conflict"))
            .Returns(ValueTask.CompletedTask);

        var result = await Service.RevokeByRefreshTokenLineageAsync(
            OpenIdContext,
            "lineage-1",
            DateTimeOffset.UtcNow,
            CancellationToken.None
        );

        Assert.Equal(2L, result);
    }

    [Fact]
    public async Task RevokeByRefreshTokenLineageAsync_WhenEverySaveLosesARace_Throws()
    {
        SetupStoreManager();
        MockGrantStore
            .Setup(x =>
                x.RevokeByRefreshTokenLineageAsync(
                    "lineage-1",
                    It.IsAny<DateTimeOffset>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(2L)
            .Verifiable(Times.Exactly(3));
        SetupSaveChanges(ValueTask.FromException(new StoreConcurrencyException("conflict")));

        await Assert.ThrowsAsync<StoreConcurrencyException>(async () =>
            await Service.RevokeByRefreshTokenLineageAsync(
                OpenIdContext,
                "lineage-1",
                DateTimeOffset.UtcNow,
                CancellationToken.None
            )
        );
    }

    #endregion

    #region RevokeWhereAsync Tests

    [Fact]
    public async Task RevokeWhereAsync_WhenTheFirstSaveLosesARace_ReadsTheGrantsAgain()
    {
        SetupStoreManager();
        MockGrantStore
            .Setup(x =>
                x.RevokeWhereAsync(
                    It.Is<GrantFilter>(f => f.SessionId == "sid-1"),
                    It.IsAny<DateTimeOffset>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(2L)
            .Verifiable(Times.Exactly(2));
        MockStoreManager
            .SetupSequence(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new StoreConcurrencyException("conflict"))
            .Returns(ValueTask.CompletedTask);

        var result = await Service.RevokeWhereAsync(
            OpenIdContext,
            new GrantFilter { SessionId = "sid-1" },
            DateTimeOffset.UtcNow,
            CancellationToken.None
        );

        Assert.Equal(2L, result);
    }

    [Fact]
    public async Task RevokeWhereAsync_WhenEverySaveLosesARace_Throws()
    {
        SetupStoreManager();
        MockGrantStore
            .Setup(x =>
                x.RevokeWhereAsync(
                    It.Is<GrantFilter>(f => f.SessionId == "sid-1"),
                    It.IsAny<DateTimeOffset>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(2L)
            .Verifiable(Times.Exactly(3));
        SetupSaveChanges(ValueTask.FromException(new StoreConcurrencyException("conflict")));

        await Assert.ThrowsAsync<StoreConcurrencyException>(async () =>
            await Service.RevokeWhereAsync(
                OpenIdContext,
                new GrantFilter { SessionId = "sid-1" },
                DateTimeOffset.UtcNow,
                CancellationToken.None
            )
        );
    }

    [Fact]
    public async Task RevokeWhereAsync_WhenTheFilterIsEmpty_Throws()
    {
        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await Service.RevokeWhereAsync(
                OpenIdContext,
                default(GrantFilter),
                DateTimeOffset.UtcNow,
                CancellationToken.None
            )
        );
    }

    #endregion

    #region UpdateExpirationAsync Tests

    [Fact]
    public async Task UpdateExpirationAsync_WhenAnotherRequestSavesFirst_Returns()
    {
        var envelope = CreateEnvelope();
        SetupLookup(envelope);
        SetupUpdate(envelope, ValueTask.CompletedTask);
        SetupSaveChanges(ValueTask.FromException(new StoreConcurrencyException("conflict")));

        await Service.UpdateExpirationAsync(
            OpenIdContext,
            CreateGrantId(),
            DateTimeOffset.UtcNow.AddHours(2),
            CancellationToken.None
        );
    }

    #endregion

    #region UpdatePayloadAndExpirationAsync Tests

    [Fact]
    public async Task UpdatePayloadAndExpirationAsync_WhenTheGrantIsActive_ReplacesThePayloadAndTheExpiration()
    {
        var envelope = CreateEnvelope();
        var expiresWhen = DateTimeOffset.UtcNow.AddHours(2);
        SetupLookup(envelope);
        SetupSerializeUpdatedPayload();
        SetupUpdate(envelope, ValueTask.CompletedTask);
        SetupSaveChanges(ValueTask.CompletedTask);

        var updated = await Service.UpdatePayloadAndExpirationAsync(
            OpenIdContext,
            CreateGrantId(),
            "payload",
            expiresWhen,
            CancellationToken.None
        );

        Assert.True(updated);
        Assert.Equal(expiresWhen, envelope.ExpiresWhen);
        Assert.Equal("{\"a\":1}", envelope.PayloadJson.GetRawText());
    }

    [Fact]
    public async Task UpdatePayloadAndExpirationAsync_WhenTheGrantIsRevoked_ReturnsFalse()
    {
        var envelope = CreateEnvelope(revokedWhen: DateTimeOffset.UtcNow);
        SetupLookup(envelope);

        var updated = await Service.UpdatePayloadAndExpirationAsync(
            OpenIdContext,
            CreateGrantId(),
            "payload",
            DateTimeOffset.UtcNow.AddHours(2),
            CancellationToken.None
        );

        Assert.False(updated);
    }

    [Fact]
    public async Task UpdatePayloadAndExpirationAsync_WhenAnotherRequestSavesFirst_ReturnsFalse()
    {
        var envelope = CreateEnvelope();
        SetupLookup(envelope);
        SetupSerializeUpdatedPayload();
        SetupUpdate(envelope, ValueTask.CompletedTask);
        SetupSaveChanges(ValueTask.FromException(new StoreConcurrencyException("conflict")));

        var updated = await Service.UpdatePayloadAndExpirationAsync(
            OpenIdContext,
            CreateGrantId(),
            "payload",
            DateTimeOffset.UtcNow.AddHours(2),
            CancellationToken.None
        );

        Assert.False(updated);
    }

    #endregion
}
