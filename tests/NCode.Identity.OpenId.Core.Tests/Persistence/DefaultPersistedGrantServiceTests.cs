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

        var mockContext = CreateStrictMock<OpenIdContext>();
        mockContext.SetupGet(x => x.Environment).Returns(MockEnvironment.Object).Verifiable();
        OpenIdContext = mockContext.Object;

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
    private OpenIdContext OpenIdContext { get; }
    private JsonSerializerOptions JsonOptions { get; } = new(JsonSerializerDefaults.Web);
    private DefaultPersistedGrantService Service { get; }

    #region Scaffolding

    private PersistedGrantId CreateGrantId() =>
        Service.CreateGrantId(TenantId, GrantType, GrantKey);

    private static PersistedGrant CreateEnvelope(DateTimeOffset? consumedWhen = null) =>
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
            RevokedWhen = null,
            ConsumedWhen = consumedWhen,
            PayloadJson = JsonElement.Parse("{}"),
        };

    private void SetupLookup(PersistedGrant envelope)
    {
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

        MockStoreManagerFactory
            .Setup(x => x.CreateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(MockStoreManager.Object)
            .Verifiable();

        MockStoreManager
            .Setup(x => x.GetStore<IGrantStore>())
            .Returns(MockGrantStore.Object)
            .Verifiable();
        MockStoreManager.Setup(x => x.DisposeAsync()).Returns(ValueTask.CompletedTask).Verifiable();

        MockGrantStore
            .Setup(x => x.GetOrDefaultAsync(GrantType, HashedKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(envelope)
            .Verifiable();
    }

    private void SetupDeserialize()
    {
        MockEnvironment.SetupGet(x => x.JsonSerializerOptions).Returns(JsonOptions).Verifiable();

        MockPayloadSerializer
            .Setup(x => x.Deserialize<string>(It.IsAny<JsonElement>(), JsonOptions))
            .Returns("payload")
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

        var result = await Service.ConsumeOnceOrDefault<string>(
            OpenIdContext,
            CreateGrantId(),
            CancellationToken.None
        );

        Assert.Null(result);
    }

    #endregion
}
