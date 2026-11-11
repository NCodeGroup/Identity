// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Moq;
using NCode.Identity.Persistence.Stores;
using NCode.Identity.Secrets.KeyWrapping;
using NCode.Identity.Secrets.Persistence.Auditing;
using NCode.Identity.Secrets.Persistence.DataContracts;
using NCode.Identity.Secrets.Persistence.Logging;
using NCode.Identity.Secrets.Persistence.Options;

namespace NCode.Identity.Secrets.Persistence.Protection;

public class DefaultOwnerRootKeyManagerTests : BaseTests
{
    private const string Reference = "kms://tenant-a/root";

    private const string OtherReference = "kms://tenant-a/other";

    private const string Identity = "test-identity-a";

    private const string OtherIdentity = "test-identity-b";

    private const string Binding = "tenant:tenant-a";

    private static SecretOwner Owner { get; } = SecretOwner.ForTenant("Tenant-A");
    private static SecretOwner NormalizedOwner { get; } = Owner.Normalize();

    private Mock<ISecretProtectorProvider> Protectors { get; }
    private Mock<IMasterKeyManager> MasterKeys { get; }
    private Mock<IMasterKeyRewrapper> Rewrapper { get; }
    private Mock<IOwnerRootKeyChangeObserver> Observer { get; }
    private List<IOwnerRootKeyPolicy> Policies { get; } = [new PlatformOwnerRootKeyPolicy()];
    private FakeReferencedKeyWrapper Kms { get; } = new();
    private InMemoryOwnerRootKeyStores Stores { get; } = new();
    private FakeTimeProvider Time { get; } =
        new(new DateTimeOffset(2026, 11, 5, 12, 0, 0, TimeSpan.Zero));
    private SecretProtectionOptions Options { get; } =
        new() { OwnerRootKeys = new OwnerRootKeyOptions { Providers = ["fake-kms"] } };

    public DefaultOwnerRootKeyManagerTests()
    {
        Protectors = CreateStrictMock<ISecretProtectorProvider>();
        MasterKeys = CreateStrictMock<IMasterKeyManager>();
        Rewrapper = CreateStrictMock<IMasterKeyRewrapper>();
        Observer = CreateStrictMock<IOwnerRootKeyChangeObserver>();
    }

    private DefaultOwnerRootKeyManager CreateManager(
        ILogger<DefaultOwnerRootKeyManager>? logger = null,
        IReferencedKeyWrapper? wrapper = null
    )
    {
        var catalog = new DefaultKeyWrapperCatalog(
            [wrapper ?? Kms],
            Microsoft.Extensions.Options.Options.Create(Options)
        );
        return new DefaultOwnerRootKeyManager(
            catalog,
            Stores,
            Protectors.Object,
            MasterKeys.Object,
            Rewrapper.Object,
            Policies,
            [Observer.Object],
            Microsoft.Extensions.Options.Options.Create(Options),
            Time,
            logger ?? NullLogger<DefaultOwnerRootKeyManager>.Instance
        );
    }

    // The logger is loose because the manager logs other events on the way; only the deferred completion is verified.
    private Mock<ILogger<DefaultOwnerRootKeyManager>> ExpectCompletionDeferredLog()
    {
        var logger = CreateLooseMock<ILogger<DefaultOwnerRootKeyManager>>();
        logger.Setup(x => x.IsEnabled(It.IsAny<LogLevel>())).Returns(true);
        logger
            .Setup(x =>
                x.Log(
                    LogLevel.Warning,
                    It.Is<EventId>(id => id.Id == EventIds.OwnerRootKeyCompletionDeferred),
                    It.IsAny<It.IsAnyType>(),
                    It.IsAny<InvalidOperationException>(),
                    (Func<It.IsAnyType, Exception?, string>)It.IsAny<object>()
                )
            )
            .Verifiable(Times.Once());
        return logger;
    }

    // The observer runs after the commit, so cancelling the caller's token from it models a caller that disconnects
    // right after the change is committed.
    private void ExpectEvictAndCancelTheCaller(CancellationTokenSource caller)
    {
        Protectors.Setup(x => x.Evict(NormalizedOwner)).Verifiable(Times.AtLeastOnce());
        Observer
            .Setup(x => x.ChangedAsync(NormalizedOwner, It.IsAny<CancellationToken>()))
            .Callback(
                (SecretOwner _, CancellationToken token) =>
                {
                    Assert.NotEqual(caller.Token, token);
                    caller.Cancel();
                }
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable(Times.AtLeastOnce());
    }

    private PersistedOwnerRootKey CreateRootKey(
        string state = OwnerRootKeyStates.Active,
        string reference = Reference,
        string? accessIdentity = null
    ) =>
        new()
        {
            OwnerType = NormalizedOwner.OwnerType,
            OwnerId = NormalizedOwner.OwnerId,
            KeyWrapperName = "fake-kms",
            KeyReference = reference,
            AccessIdentity = accessIdentity,
            State = state,
            CreatedWhen = Time.GetUtcNow().AddDays(-1),
            StateChangedWhen = Time.GetUtcNow().AddDays(-1),
            ValidatedWhen = Time.GetUtcNow().AddMinutes(-90),
        };

    private static OwnerRootKeyRequest Request(
        string reference = Reference,
        string? accessIdentity = null,
        string wrapper = "fake-kms"
    ) => new(wrapper, reference) { AccessIdentity = accessIdentity };

    private static PersistedMasterKey CreateRow(
        int version,
        DateTimeOffset createdWhen,
        DateTimeOffset? retiredWhen = null,
        string? rootKeyReference = null,
        string? rootKeyAccessIdentity = null
    ) =>
        new()
        {
            KeyId = Guid.NewGuid(),
            OwnerType = NormalizedOwner.OwnerType,
            OwnerId = NormalizedOwner.OwnerId,
            Version = version,
            CreatedWhen = createdWhen,
            RetiredWhen = retiredWhen,
            RootKeyProvider = rootKeyReference is null ? "local" : "fake-kms",
            RootKeyId = "root",
            RootKeyReference = rootKeyReference,
            RootKeyAccessIdentity = rootKeyAccessIdentity,
            RootWrappedKey = "wrapped",
            KeyCheck = "check",
        };

    private MasterKeyRotation CreateRotation(string reference = Reference) =>
        new(
            NormalizedOwner,
            new MasterKeyInfo(
                Guid.NewGuid(),
                NormalizedOwner,
                2,
                Time.GetUtcNow(),
                null,
                "fake-kms",
                "root",
                null
            )
            {
                RootKeyReference = reference,
            },
            null
        );

    private void ExpectEvictAndNotify(Times? times = null)
    {
        Protectors.Setup(x => x.Evict(NormalizedOwner)).Verifiable(times ?? Times.AtLeastOnce());
        Observer
            .Setup(x => x.ChangedAsync(NormalizedOwner, It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask)
            .Verifiable(times ?? Times.AtLeastOnce());
    }

    private void ExpectRewrap(MasterKeyRewrapResult result) =>
        Rewrapper
            .Setup(x =>
                x.RewrapAsync(
                    It.Is<MasterKeyRewrapRequest>(request =>
                        request.NonconformingOnly
                        && !request.DryRun
                        && !request.RecoveryOnly
                        && request.Owner == NormalizedOwner
                    ),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(new ValueTask<MasterKeyRewrapResult>(result))
            .Verifiable();

    #region SetAsync Tests

    [Fact]
    public async Task Set_NewKey_ProbesStoresRotatesConformsAndNotifies()
    {
        var oldRow = CreateRow(1, Time.GetUtcNow().AddDays(-400));
        Stores.Seed(oldRow);
        ExpectEvictAndNotify();
        var rotation = CreateRotation();
        MasterKeys
            .Setup(x => x.RotateAsync(NormalizedOwner, It.IsAny<CancellationToken>()))
            .Returns(new ValueTask<MasterKeyRotation>(rotation))
            .Verifiable();
        ExpectRewrap(new MasterKeyRewrapResult(1, 1, 0, 0));

        var change = await CreateManager().SetAsync(Owner, Request(), CancellationToken.None);

        var stored = Assert.Single(Stores.RootKeys);
        Assert.Equal(OwnerRootKeyStates.Active, stored.State);
        Assert.Equal(Reference, stored.KeyReference);
        Assert.Equal(Time.GetUtcNow(), stored.CreatedWhen);
        Assert.Equal(Time.GetUtcNow(), stored.ValidatedWhen);
        Assert.Equal(RootKeyChanges.Set, change.Change);
        Assert.Same(rotation, change.Rotation);
        Assert.Equal(new MasterKeyRewrapResult(1, 1, 0, 0), change.Rewrap);
        Assert.Equal(Reference, change.RootKey.KeyReference);
        Assert.Equal(1, Kms.WrapCount);
    }

    [Theory]
    [InlineData("local", Reference)]
    [InlineData("fake-kms", "https://not-a-kms-reference")]
    public async Task Set_ProviderNotAllowedOrReferenceMalformed_ThrowsAndWritesNothing(
        string wrapper,
        string reference
    )
    {
        var manager = CreateManager();

        var exception = await Assert.ThrowsAsync<ArgumentException>(async () =>
            await manager.SetAsync(
                Owner,
                Request(reference, wrapper: wrapper),
                CancellationToken.None
            )
        );

        Assert.Equal("request", exception.ParamName);
        Assert.Empty(Stores.RootKeys);
        Assert.Equal(0, Stores.SaveCount);
        Assert.Equal(0, Kms.WrapCount);
    }

    [Fact]
    public async Task Set_ProbeFailure_ThrowsAndWritesNothing()
    {
        Kms.Revoke(Reference);

        var exception = await Assert.ThrowsAsync<OwnerRootKeyProbeException>(async () =>
            await CreateManager().SetAsync(Owner, Request(), CancellationToken.None)
        );

        Assert.Equal(Reference, exception.KeyReference);
        Assert.Contains("revoked", exception.Failure);
        Assert.Empty(Stores.RootKeys);
    }

    [Fact]
    public async Task Set_AfterARecentRotation_ThrowsTooSoonBeforeAnyWrite()
    {
        Stores.Seed(CreateRow(1, Time.GetUtcNow().AddDays(-400), Time.GetUtcNow().AddMinutes(-2)));
        Stores.Seed(CreateRow(2, Time.GetUtcNow().AddMinutes(-2)));

        var exception = await Assert.ThrowsAsync<MasterKeyRotationTooSoonException>(async () =>
            await CreateManager().SetAsync(Owner, Request(), CancellationToken.None)
        );

        Assert.Equal(TimeSpan.FromMinutes(8), exception.RetryAfter);
        Assert.Empty(Stores.RootKeys);
        Assert.Equal(0, Kms.WrapCount);
    }

    [Fact]
    public async Task Set_SameKeyWhileUnavailable_RestoresItWithoutARotationWhenTheActiveKeyConforms()
    {
        Stores.Seed(CreateRootKey(OwnerRootKeyStates.Unavailable));
        Stores.Seed(CreateRow(1, Time.GetUtcNow().AddDays(-1), rootKeyReference: Reference));
        ExpectEvictAndNotify();
        ExpectRewrap(new MasterKeyRewrapResult(1, 0, 0, 0));

        var change = await CreateManager().SetAsync(Owner, Request(), CancellationToken.None);

        Assert.Equal(RootKeyChanges.Restored, change.Change);
        Assert.Null(change.Rotation);
        Assert.Equal(OwnerRootKeyStates.Active, Assert.Single(Stores.RootKeys).State);
        Assert.Equal(Time.GetUtcNow(), change.RootKey.StateChangedWhen);
    }

    [Fact]
    public async Task Set_SameActiveKey_ChangesNothingAndKeepsItsCreationTime()
    {
        var existing = Stores.Seed(CreateRootKey());
        Stores.Seed(CreateRow(1, Time.GetUtcNow().AddDays(-1), rootKeyReference: Reference));
        ExpectEvictAndNotify();
        ExpectRewrap(new MasterKeyRewrapResult(1, 0, 0, 0));

        var change = await CreateManager().SetAsync(Owner, Request(), CancellationToken.None);

        Assert.Null(change.Change);
        Assert.Null(change.Rotation);
        Assert.Equal(existing.CreatedWhen, change.RootKey.CreatedWhen);
    }

    [Fact]
    public async Task Set_AnotherKey_RotatesOntoIt()
    {
        Stores.Seed(CreateRootKey(reference: OtherReference));
        Stores.Seed(CreateRow(1, Time.GetUtcNow().AddDays(-30), rootKeyReference: OtherReference));
        ExpectEvictAndNotify();
        MasterKeys
            .Setup(x => x.RotateAsync(NormalizedOwner, It.IsAny<CancellationToken>()))
            .Returns(new ValueTask<MasterKeyRotation>(CreateRotation()))
            .Verifiable();
        ExpectRewrap(new MasterKeyRewrapResult(2, 1, 0, 0));

        var change = await CreateManager().SetAsync(Owner, Request(), CancellationToken.None);

        Assert.Equal(RootKeyChanges.Set, change.Change);
        Assert.NotNull(change.Rotation);
        var stored = Assert.Single(Stores.RootKeys);
        Assert.Equal(Reference, stored.KeyReference);
        Assert.Equal(Time.GetUtcNow(), stored.CreatedWhen);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Set_RotationLostToARaceOrTooSoon_IsDeferredToTheValidationService(
        bool tooSoon
    )
    {
        Stores.Seed(CreateRow(1, Time.GetUtcNow().AddDays(-30)));
        ExpectEvictAndNotify();
        Exception failure = tooSoon
            ? new MasterKeyRotationTooSoonException(NormalizedOwner, TimeSpan.FromMinutes(1))
            : new StoreConcurrencyException("lost");
        MasterKeys
            .Setup(x => x.RotateAsync(NormalizedOwner, It.IsAny<CancellationToken>()))
            .Returns(
                new ValueTask<MasterKeyRotation>(Task.FromException<MasterKeyRotation>(failure))
            )
            .Verifiable();
        ExpectRewrap(new MasterKeyRewrapResult(1, 1, 0, 0));

        var change = await CreateManager().SetAsync(Owner, Request(), CancellationToken.None);

        Assert.Null(change.Rotation);
        Assert.Equal(RootKeyChanges.Set, change.Change);
        Assert.Equal(OwnerRootKeyStates.Active, Assert.Single(Stores.RootKeys).State);
    }

    [Fact]
    public async Task Set_TheCallersTokenIsCancelledAfterTheCommit_StillCompletesOnATokenOfItsOwn()
    {
        Stores.Seed(CreateRow(1, Time.GetUtcNow().AddDays(-400)));
        using var caller = new CancellationTokenSource();
        ExpectEvictAndCancelTheCaller(caller);
        var rotation = CreateRotation();
        var rotateToken = CancellationToken.None;
        MasterKeys
            .Setup(x => x.RotateAsync(NormalizedOwner, It.IsAny<CancellationToken>()))
            .Callback((SecretOwner _, CancellationToken token) => rotateToken = token)
            .Returns(new ValueTask<MasterKeyRotation>(rotation))
            .Verifiable();
        ExpectRewrap(new MasterKeyRewrapResult(1, 1, 0, 0));

        var change = await CreateManager().SetAsync(Owner, Request(), caller.Token);

        Assert.True(caller.IsCancellationRequested);
        Assert.Same(rotation, change.Rotation);
        Assert.Equal(new MasterKeyRewrapResult(1, 1, 0, 0), change.Rewrap);
        Assert.NotEqual(caller.Token, rotateToken);
        Assert.False(rotateToken.IsCancellationRequested);
    }

    [Fact]
    public async Task Set_TheCompletionThrows_ReturnsTheCommittedBindingWithNoRotationAndLogsIt()
    {
        Stores.Seed(CreateRow(1, Time.GetUtcNow().AddDays(-400)));
        ExpectEvictAndNotify();
        MasterKeys
            .Setup(x => x.RotateAsync(NormalizedOwner, It.IsAny<CancellationToken>()))
            .Returns(
                new ValueTask<MasterKeyRotation>(
                    Task.FromException<MasterKeyRotation>(
                        new InvalidOperationException("The completion failed.")
                    )
                )
            )
            .Verifiable();
        var logger = ExpectCompletionDeferredLog();

        var change = await CreateManager(logger.Object)
            .SetAsync(Owner, Request(), CancellationToken.None);

        Assert.Null(change.Rotation);
        Assert.Equal(new MasterKeyRewrapResult(0, 0, 0, 0), change.Rewrap);
        Assert.Equal(RootKeyChanges.Set, change.Change);
        Assert.Equal(Reference, change.RootKey.KeyReference);
        var stored = Assert.Single(Stores.RootKeys);
        Assert.Equal(OwnerRootKeyStates.Active, stored.State);
        Assert.Equal(Reference, stored.KeyReference);
    }

    [Fact]
    public async Task Set_SameKeyFailingItsProbe_RecordsTheOwnerUnavailableAndThrows()
    {
        Stores.Seed(CreateRootKey());
        ExpectEvictAndNotify(Times.Once());
        Kms.Revoke(Reference);

        var exception = await Assert.ThrowsAsync<OwnerRootKeyProbeException>(async () =>
            await CreateManager().SetAsync(Owner, Request(), CancellationToken.None)
        );

        Assert.Equal(OwnerRootKeyStates.Unavailable, exception.UnavailableRootKey?.State);
        var stored = Assert.Single(Stores.RootKeys);
        Assert.Equal(OwnerRootKeyStates.Unavailable, stored.State);
        Assert.Equal(Time.GetUtcNow(), stored.FailedWhen);
        Assert.Contains("revoked", stored.Failure);
    }

    [Fact]
    public async Task Set_SameKeyAndSameAccessIdentityFailingItsProbe_RecordsTheOwnerUnavailableAndThrows()
    {
        Stores.Seed(CreateRootKey(accessIdentity: Identity));
        ExpectEvictAndNotify(Times.Once());
        Kms.Revoke(Reference);

        var exception = await Assert.ThrowsAsync<OwnerRootKeyProbeException>(async () =>
            await CreateManager()
                .SetAsync(Owner, Request(accessIdentity: Identity), CancellationToken.None)
        );

        Assert.Equal(OwnerRootKeyStates.Unavailable, exception.UnavailableRootKey?.State);
        var stored = Assert.Single(Stores.RootKeys);
        Assert.Equal(OwnerRootKeyStates.Unavailable, stored.State);
        Assert.Equal(Identity, stored.AccessIdentity);
        Assert.Equal(Time.GetUtcNow(), stored.FailedWhen);
        Assert.Contains("revoked", stored.Failure);
    }

    [Fact]
    public async Task Set_SameKeyWithAnotherAccessIdentityFailingItsProbe_ThrowsAndLeavesTheStoredBindingAlone()
    {
        var existing = Stores.Seed(CreateRootKey(accessIdentity: Identity));
        var wrapper = new IdentityRecordingKeyWrapper(Kms);
        wrapper.Refuse(OtherIdentity);

        var exception = await Assert.ThrowsAsync<OwnerRootKeyProbeException>(async () =>
            await CreateManager(wrapper: wrapper)
                .SetAsync(Owner, Request(accessIdentity: OtherIdentity), CancellationToken.None)
        );

        Assert.Null(exception.UnavailableRootKey);
        Assert.Contains("is refused", exception.Failure);
        var stored = Assert.Single(Stores.RootKeys);
        Assert.Equal(OwnerRootKeyStates.Active, stored.State);
        Assert.Equal(Identity, stored.AccessIdentity);
        Assert.Null(stored.FailedWhen);
        Assert.Null(stored.Failure);
        Assert.Equal(existing.StateChangedWhen, stored.StateChangedWhen);
    }

    [Fact]
    public async Task Set_PlatformOwner_ThrowsNotAllowedAndCallsNoWrapper()
    {
        var platform = new SecretOwner("Platform", SecretOwner.PlatformOwnerId);

        var exception = await Assert.ThrowsAsync<OwnerRootKeyNotAllowedException>(async () =>
            await CreateManager().SetAsync(platform, Request(), CancellationToken.None)
        );

        Assert.Equal(SecretOwner.Platform, exception.Owner);
        Assert.Contains("Data Protection key ring", exception.Reason);
        Assert.Equal(exception.Reason, exception.Message);
        Assert.Empty(Stores.RootKeys);
        Assert.Equal(0, Stores.SaveCount);
        Assert.Equal(0, Kms.WrapCount);
    }

    [Fact]
    public async Task Set_OwnerRefusedByAnyRegisteredPolicy_ThrowsNotAllowedBeforeAnyProbe()
    {
        Policies.Add(new RefusingPolicy("tenant-a", "Not this tenant."));

        var exception = await Assert.ThrowsAsync<OwnerRootKeyNotAllowedException>(async () =>
            await CreateManager().SetAsync(Owner, Request(), CancellationToken.None)
        );

        Assert.Equal("Not this tenant.", exception.Reason);
        Assert.Empty(Stores.RootKeys);
        Assert.Equal(0, Kms.WrapCount);
    }

    [Fact]
    public async Task Set_ReferenceLongerThanItsColumn_ThrowsBeforeTheProbe()
    {
        var reference = "kms://" + new string('a', PersistedOwnerRootKey.MaxKeyReferenceLength - 5);
        Assert.Equal(PersistedOwnerRootKey.MaxKeyReferenceLength + 1, reference.Length);

        var exception = await Assert.ThrowsAsync<ArgumentException>(async () =>
            await CreateManager().SetAsync(Owner, Request(reference), CancellationToken.None)
        );

        Assert.Equal("request", exception.ParamName);
        Assert.Contains("at most 2048 characters", exception.Message);
        Assert.Empty(Stores.RootKeys);
        Assert.Equal(0, Kms.WrapCount);
    }

    [Fact]
    public async Task Set_NewKeyWithAnAccessIdentity_ProbesThroughItAndStoresIt()
    {
        Stores.Seed(CreateRow(1, Time.GetUtcNow().AddDays(-400)));
        ExpectEvictAndNotify();
        MasterKeys
            .Setup(x => x.RotateAsync(NormalizedOwner, It.IsAny<CancellationToken>()))
            .Returns(new ValueTask<MasterKeyRotation>(CreateRotation()))
            .Verifiable();
        ExpectRewrap(new MasterKeyRewrapResult(1, 1, 0, 0));

        var change = await CreateManager()
            .SetAsync(Owner, Request(accessIdentity: Identity), CancellationToken.None);

        Assert.Equal(RootKeyChanges.Set, change.Change);
        Assert.Equal(Identity, Assert.Single(Stores.RootKeys).AccessIdentity);
        Assert.Equal(Identity, change.RootKey.AccessIdentity);
        Assert.Equal(new KeyAccess(Identity, Binding), Kms.LastWrapAccess);
    }

    [Fact]
    public async Task Set_OnlyTheAccessIdentityChanges_ProbesThroughItStoresItAndRewrapsWithoutRotating()
    {
        var existing = Stores.Seed(CreateRootKey(accessIdentity: Identity));
        Stores.Seed(
            CreateRow(
                1,
                Time.GetUtcNow().AddDays(-1),
                rootKeyReference: Reference,
                rootKeyAccessIdentity: Identity
            )
        );
        ExpectEvictAndNotify();
        ExpectRewrap(new MasterKeyRewrapResult(1, 1, 0, 0));

        var change = await CreateManager()
            .SetAsync(Owner, Request(accessIdentity: OtherIdentity), CancellationToken.None);

        Assert.Equal(RootKeyChanges.Set, change.Change);
        Assert.Null(change.Rotation);
        Assert.Equal(new MasterKeyRewrapResult(1, 1, 0, 0), change.Rewrap);
        Assert.Equal(OtherIdentity, Assert.Single(Stores.RootKeys).AccessIdentity);
        Assert.Equal(OtherIdentity, change.RootKey.AccessIdentity);
        Assert.Equal(existing.CreatedWhen, change.RootKey.CreatedWhen);
        Assert.Equal(new KeyAccess(OtherIdentity, Binding), Kms.LastWrapAccess);
    }

    [Fact]
    public async Task Set_AnotherAccessIdentityForTheSameKeyAfterARecentRotation_SkipsTheSpacingCheck()
    {
        Stores.Seed(CreateRootKey(accessIdentity: Identity));
        Stores.Seed(
            CreateRow(
                1,
                Time.GetUtcNow().AddDays(-400),
                Time.GetUtcNow().AddMinutes(-2),
                Reference,
                Identity
            )
        );
        Stores.Seed(CreateRow(2, Time.GetUtcNow().AddMinutes(-2), null, Reference, Identity));
        ExpectEvictAndNotify();
        ExpectRewrap(new MasterKeyRewrapResult(2, 1, 0, 0));

        var change = await CreateManager()
            .SetAsync(Owner, Request(accessIdentity: OtherIdentity), CancellationToken.None);

        Assert.Equal(RootKeyChanges.Set, change.Change);
        Assert.Null(change.Rotation);
        Assert.Equal(OtherIdentity, Assert.Single(Stores.RootKeys).AccessIdentity);
    }

    [Fact]
    public async Task Set_AccessIdentityTheWrapperRejects_ThrowsBeforeAnythingIsStored()
    {
        var manager = CreateManager();

        var exception = await Assert.ThrowsAsync<ArgumentException>(async () =>
            await manager.SetAsync(
                Owner,
                Request(accessIdentity: "not-an-identity"),
                CancellationToken.None
            )
        );

        Assert.Equal("request", exception.ParamName);
        Assert.Contains("The access identity must start with test-identity-.", exception.Message);
        Assert.Empty(Stores.RootKeys);
        Assert.Equal(0, Stores.SaveCount);
        Assert.Equal(0, Kms.WrapCount);
    }

    [Fact]
    public async Task Set_AccessIdentityLongerThanItsColumn_ThrowsBeforeTheProbe()
    {
        var identity =
            "test-identity-" + new string('a', PersistedOwnerRootKey.MaxAccessIdentityLength);

        var exception = await Assert.ThrowsAsync<ArgumentException>(async () =>
            await CreateManager()
                .SetAsync(Owner, Request(accessIdentity: identity), CancellationToken.None)
        );

        Assert.Equal("request", exception.ParamName);
        Assert.Contains("at most 1024 characters", exception.Message);
        Assert.Empty(Stores.RootKeys);
        Assert.Equal(0, Kms.WrapCount);
    }

    #endregion

    #region RemoveAsync Tests

    [Fact]
    public async Task Remove_NoRootKey_ReturnsNothing()
    {
        var removal = await CreateManager().RemoveAsync(Owner, CancellationToken.None);

        Assert.Null(removal.Remaining);
        Assert.False(removal.Removed);
    }

    [Fact]
    public async Task Remove_MovesTheKeyToRemovingConformsAndDeletesTheBinding()
    {
        Stores.Seed(CreateRootKey());
        ExpectEvictAndNotify();
        ExpectRewrap(new MasterKeyRewrapResult(2, 2, 0, 0));

        var removal = await CreateManager().RemoveAsync(Owner, CancellationToken.None);

        Assert.True(removal.Removed);
        Assert.Null(removal.Remaining);
        Assert.Empty(Stores.RootKeys);
        Assert.Equal(new MasterKeyRewrapResult(2, 2, 0, 0), removal.Rewrap);
    }

    [Fact]
    public async Task Remove_TheCallersTokenIsCancelledAfterTheCommit_StillCompletesTheRemoval()
    {
        Stores.Seed(CreateRootKey());
        using var caller = new CancellationTokenSource();
        ExpectEvictAndCancelTheCaller(caller);
        ExpectRewrap(new MasterKeyRewrapResult(2, 2, 0, 0));

        var removal = await CreateManager().RemoveAsync(Owner, caller.Token);

        Assert.True(caller.IsCancellationRequested);
        Assert.True(removal.Removed);
        Assert.Null(removal.Remaining);
        Assert.Empty(Stores.RootKeys);
    }

    [Fact]
    public async Task Remove_TheCompletionThrows_ReturnsTheRemovingBindingWithZeroCountsAndLogsIt()
    {
        Stores.Seed(CreateRootKey());
        ExpectEvictAndNotify();
        Rewrapper
            .Setup(x =>
                x.RewrapAsync(It.IsAny<MasterKeyRewrapRequest>(), It.IsAny<CancellationToken>())
            )
            .Returns(
                new ValueTask<MasterKeyRewrapResult>(
                    Task.FromException<MasterKeyRewrapResult>(
                        new InvalidOperationException("The completion failed.")
                    )
                )
            )
            .Verifiable();
        var logger = ExpectCompletionDeferredLog();

        var removal = await CreateManager(logger.Object).RemoveAsync(Owner, CancellationToken.None);

        Assert.True(removal.Removed);
        Assert.Equal(OwnerRootKeyStates.Removing, removal.Remaining?.State);
        Assert.Equal(new MasterKeyRewrapResult(0, 0, 0, 0), removal.Rewrap);
        Assert.Equal(OwnerRootKeyStates.Removing, Assert.Single(Stores.RootKeys).State);
    }

    [Fact]
    public async Task Remove_WithEveryRowUnderTheDeploymentRoot_DeletesTheBinding()
    {
        Stores.Seed(CreateRootKey());
        Stores.Seed(CreateRow(1, Time.GetUtcNow().AddDays(-1)));
        ExpectEvictAndNotify();
        ExpectRewrap(new MasterKeyRewrapResult(1, 1, 0, 0));

        var removal = await CreateManager().RemoveAsync(Owner, CancellationToken.None);

        Assert.True(removal.Removed);
        Assert.Null(removal.Remaining);
        Assert.Empty(Stores.RootKeys);
    }

    [Fact]
    public async Task Remove_WithARowStillUnderTheOwnersKeyAndNoReportedFailure_StaysRemovingAndRecordsTheFailure()
    {
        Stores.Seed(CreateRootKey());
        Stores.Seed(CreateRow(1, Time.GetUtcNow().AddDays(-1), rootKeyReference: Reference));
        ExpectEvictAndNotify();
        ExpectRewrap(new MasterKeyRewrapResult(1, 1, 0, 0));

        var removal = await CreateManager().RemoveAsync(Owner, CancellationToken.None);

        Assert.True(removal.Removed);
        Assert.Equal(OwnerRootKeyStates.Removing, removal.Remaining?.State);
        var stored = Assert.Single(Stores.RootKeys);
        Assert.Equal(OwnerRootKeyStates.Removing, stored.State);
        Assert.Equal(Time.GetUtcNow(), stored.FailedWhen);
        Assert.Equal("1 master keys remain under the owner root key", stored.Failure);
    }

    [Fact]
    public async Task Validate_RemovingKeyWithARowStillUnderTheOwnersKey_KeepsTheBinding()
    {
        Stores.Seed(CreateRootKey(OwnerRootKeyStates.Removing));
        Stores.Seed(CreateRow(1, Time.GetUtcNow().AddDays(-1), rootKeyReference: Reference));
        ExpectRewrap(new MasterKeyRewrapResult(1, 1, 0, 0));

        var validation = await CreateManager().ValidateAsync(Owner, CancellationToken.None);

        Assert.Equal(OwnerRootKeyStates.Removing, validation.RootKey?.State);
        var stored = Assert.Single(Stores.RootKeys);
        Assert.Equal(OwnerRootKeyStates.Removing, stored.State);
        Assert.NotNull(stored.Failure);
    }

    [Fact]
    public async Task Remove_WithRowsThatCannotBeMoved_StaysRemovingAndRecordsTheFailure()
    {
        Stores.Seed(CreateRootKey());
        ExpectEvictAndNotify();
        ExpectRewrap(new MasterKeyRewrapResult(2, 1, 0, 1));

        var removal = await CreateManager().RemoveAsync(Owner, CancellationToken.None);

        Assert.True(removal.Removed);
        Assert.Equal(OwnerRootKeyStates.Removing, removal.Remaining?.State);
        var stored = Assert.Single(Stores.RootKeys);
        Assert.Equal(OwnerRootKeyStates.Removing, stored.State);
        Assert.Equal(Time.GetUtcNow(), stored.FailedWhen);
        Assert.Equal("1 master keys remain under the owner root key", stored.Failure);
    }

    #endregion

    #region ValidateAsync Tests

    [Fact]
    public async Task Validate_NoRootKey_ReturnsNothing()
    {
        var validation = await CreateManager().ValidateAsync(Owner, CancellationToken.None);

        Assert.Null(validation.RootKey);
        Assert.Null(validation.Change);
    }

    [Fact]
    public async Task Validate_Success_RecordsTheValidationAndChangesNothing()
    {
        Stores.Seed(CreateRootKey());
        Stores.Seed(CreateRow(1, Time.GetUtcNow().AddDays(-1), rootKeyReference: Reference));
        ExpectRewrap(new MasterKeyRewrapResult(1, 0, 0, 0));

        var validation = await CreateManager().ValidateAsync(Owner, CancellationToken.None);

        Assert.Null(validation.Change);
        Assert.Equal(Time.GetUtcNow(), validation.RootKey?.ValidatedWhen);
        Assert.Equal(OwnerRootKeyStates.Active, validation.RootKey?.State);
    }

    [Fact]
    public async Task Validate_Failure_MakesTheOwnerUnavailableAndTellsEveryReplica()
    {
        Stores.Seed(CreateRootKey());
        ExpectEvictAndNotify(Times.Once());
        Kms.Revoke(Reference);

        var validation = await CreateManager().ValidateAsync(Owner, CancellationToken.None);

        Assert.Equal(RootKeyChanges.Unavailable, validation.Change);
        Assert.Equal(OwnerRootKeyStates.Unavailable, validation.RootKey?.State);
        Assert.Equal(Time.GetUtcNow(), validation.RootKey?.FailedWhen);
        Assert.Contains("revoked", validation.RootKey?.Failure);
        Assert.Null(validation.Rotation);
    }

    [Fact]
    public async Task Validate_RepeatedFailure_RecordsItWithoutAnotherChange()
    {
        Stores.Seed(CreateRootKey(OwnerRootKeyStates.Unavailable));
        Kms.Revoke(Reference);

        var validation = await CreateManager().ValidateAsync(Owner, CancellationToken.None);

        Assert.Null(validation.Change);
        Assert.Equal(Time.GetUtcNow(), validation.RootKey?.FailedWhen);
    }

    [Fact]
    public async Task Validate_SuccessAfterAFailure_RestoresTheOwnerAndCompletes()
    {
        Stores.Seed(CreateRootKey(OwnerRootKeyStates.Unavailable));
        Stores.Seed(CreateRow(1, Time.GetUtcNow().AddDays(-1), rootKeyReference: Reference));
        ExpectEvictAndNotify(Times.Once());
        ExpectRewrap(new MasterKeyRewrapResult(1, 0, 0, 0));

        var validation = await CreateManager().ValidateAsync(Owner, CancellationToken.None);

        Assert.Equal(RootKeyChanges.Restored, validation.Change);
        Assert.Equal(OwnerRootKeyStates.Active, validation.RootKey?.State);
        Assert.Null(validation.RootKey?.Failure);
    }

    [Fact]
    public async Task Validate_ActiveKeyWithoutAKeyUnderIt_CompletesTheDeferredRotation()
    {
        Stores.Seed(CreateRootKey());
        Stores.Seed(CreateRow(1, Time.GetUtcNow().AddDays(-30)));
        var rotation = CreateRotation();
        MasterKeys
            .Setup(x => x.RotateAsync(NormalizedOwner, It.IsAny<CancellationToken>()))
            .Returns(new ValueTask<MasterKeyRotation>(rotation))
            .Verifiable();
        ExpectRewrap(new MasterKeyRewrapResult(1, 1, 0, 0));

        var validation = await CreateManager().ValidateAsync(Owner, CancellationToken.None);

        Assert.Same(rotation, validation.Rotation);
        Assert.Null(validation.Change);
    }

    [Fact]
    public async Task Validate_RemovingKey_CompletesTheRemovalWithoutAProbe()
    {
        Stores.Seed(CreateRootKey(OwnerRootKeyStates.Removing));
        ExpectEvictAndNotify(Times.Once());
        ExpectRewrap(new MasterKeyRewrapResult(1, 1, 0, 0));
        Kms.Revoke(Reference);

        var validation = await CreateManager().ValidateAsync(Owner, CancellationToken.None);

        Assert.Null(validation.RootKey);
        Assert.Empty(Stores.RootKeys);
        Assert.Equal(0, Kms.WrapCount);
    }

    [Fact]
    public async Task Validate_RootKeyWithAnAccessIdentity_ProbesThroughTheStoredAccess()
    {
        Stores.Seed(CreateRootKey(accessIdentity: Identity));
        Stores.Seed(
            CreateRow(
                1,
                Time.GetUtcNow().AddDays(-1),
                rootKeyReference: Reference,
                rootKeyAccessIdentity: Identity
            )
        );
        ExpectRewrap(new MasterKeyRewrapResult(1, 0, 0, 0));

        var validation = await CreateManager().ValidateAsync(Owner, CancellationToken.None);

        Assert.Null(validation.Change);
        Assert.Equal(new KeyAccess(Identity, Binding), Kms.LastWrapAccess);
    }

    [Fact]
    public async Task Validate_PlatformOwner_ReturnsNothing()
    {
        var validation = await CreateManager()
            .ValidateAsync(SecretOwner.Platform, CancellationToken.None);

        Assert.Null(validation.RootKey);
        Assert.Null(validation.Change);
    }

    #endregion

    #region Policy Tests

    [Fact]
    public void AllowedKeyWrapperNames_AreTheConfiguredProvidersInOrdinalOrder()
    {
        var manager = CreateManager();

        Options.OwnerRootKeys.Providers = ["b-kms", "a-kms"];

        Assert.Equal(["a-kms", "b-kms"], manager.AllowedKeyWrapperNames);
    }

    [Fact]
    public void TryValidateKeyReference_ByWrapperAndReference_ReportsWhyItIsRefused()
    {
        var manager = CreateManager();

        Assert.True(manager.TryValidateKeyReference("fake-kms", Reference, out var none));
        Assert.Null(none);

        Assert.False(manager.TryValidateKeyReference("local", Reference, out var notAllowed));
        Assert.Contains("not allowed", notAllowed);

        Assert.False(manager.TryValidateKeyReference("fake-kms", "nope", out var malformed));
        Assert.Equal("The key reference must start with kms://.", malformed);

        Options.OwnerRootKeys.Providers = ["missing"];
        Assert.False(manager.TryValidateKeyReference("missing", Reference, out var notReferenced));
        Assert.Contains("does not wrap under a key", notReferenced);
    }

    [Fact]
    public void TryValidateAccessIdentity_ByWrapperAndIdentity_ReportsWhyItIsRefused()
    {
        var manager = CreateManager();

        Assert.True(manager.TryValidateAccessIdentity("fake-kms", Identity, out var none));
        Assert.Null(none);

        Assert.False(manager.TryValidateAccessIdentity("local", Identity, out var notAllowed));
        Assert.Contains("not allowed", notAllowed);

        Assert.False(manager.TryValidateAccessIdentity("fake-kms", "nope", out var malformed));
        Assert.Equal("The access identity must start with test-identity-.", malformed);

        var tooLong =
            "test-identity-" + new string('a', PersistedOwnerRootKey.MaxAccessIdentityLength);
        Assert.False(manager.TryValidateAccessIdentity("fake-kms", tooLong, out var length));
        Assert.Equal("The access identity must be at most 1024 characters.", length);

        Options.OwnerRootKeys.Providers = ["missing"];
        Assert.False(manager.TryValidateAccessIdentity("missing", Identity, out var notReferenced));
        Assert.Contains("does not wrap under a key", notReferenced);
    }

    [Fact]
    public void GetAccessBinding_ByOwner_IsTheNormalizedOwner()
    {
        Assert.Equal(Binding, CreateManager().GetAccessBinding(Owner));
    }

    [Fact]
    public void GetAccessBinding_DefaultOwner_Throws()
    {
        var manager = CreateManager();

        var exception = Assert.Throws<ArgumentException>(() => manager.GetAccessBinding(default));

        Assert.Equal("owner", exception.ParamName);
    }

    [Fact]
    public void CanBringRootKey_AnOwnerNoPolicyRefuses_IsAllowed()
    {
        Assert.True(CreateManager().CanBringRootKey(Owner, out var reason));
        Assert.Null(reason);
    }

    [Fact]
    public void CanBringRootKey_ThePlatformOwner_ReportsThePlatformPolicyReason()
    {
        Assert.False(CreateManager().CanBringRootKey(SecretOwner.Platform, out var reason));
        Assert.Equal(
            "The platform's master keys protect the deployment's Data Protection key ring and the host's own data, so they are always wrapped by the deployment's root key.",
            reason
        );
    }

    [Fact]
    public void CanBringRootKey_SeveralPoliciesRefuse_ReportsTheFirstReason()
    {
        Policies.Add(new RefusingPolicy("tenant-a", "First."));
        Policies.Add(new RefusingPolicy("tenant-a", "Second."));

        Assert.False(CreateManager().CanBringRootKey(Owner, out var reason));
        Assert.Equal("First.", reason);
    }

    [Fact]
    public void CanBringRootKey_ThePolicySeesTheNormalizedOwner()
    {
        Policies.Add(new RefusingPolicy("tenant-a", "Normalized."));

        Assert.False(CreateManager().CanBringRootKey(Owner, out var reason));
        Assert.Equal("Normalized.", reason);
    }

    [Fact]
    public async Task Get_ReturnsTheRootKeyWithoutMaterial()
    {
        Stores.Seed(CreateRootKey());

        var info = await CreateManager().GetAsync(Owner, CancellationToken.None);

        Assert.Equal(Reference, info?.KeyReference);
        Assert.Null(
            await CreateManager().GetAsync(SecretOwner.ForTenant("other"), CancellationToken.None)
        );
    }

    #endregion

    // A policy that refuses the one owner id it names, compared on the normalized id the manager hands it.
    private sealed class RefusingPolicy(string ownerId, string refusal) : IOwnerRootKeyPolicy
    {
        public bool CanBringRootKey(SecretOwner owner, [NotNullWhen(false)] out string? reason)
        {
            if (string.Equals(owner.OwnerId, ownerId, StringComparison.Ordinal))
            {
                reason = refusal;
                return false;
            }

            reason = null;
            return true;
        }
    }
}
