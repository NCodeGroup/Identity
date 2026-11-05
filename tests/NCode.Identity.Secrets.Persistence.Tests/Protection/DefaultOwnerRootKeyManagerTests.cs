// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Moq;
using NCode.Identity.Secrets.Persistence.Auditing;
using NCode.Identity.Secrets.Persistence.DataContracts;
using NCode.Identity.Secrets.Persistence.Options;
using NCode.Persistence.Stores;

namespace NCode.Identity.Secrets.Persistence.Protection;

public class DefaultOwnerRootKeyManagerTests : IDisposable
{
    private const string Reference = "kms://tenant-a/root";

    private const string OtherReference = "kms://tenant-a/other";

    private static readonly SecretOwner Owner = SecretOwner.ForTenant("Tenant-A");
    private static readonly SecretOwner NormalizedOwner = Owner.Normalize();

    private readonly MockRepository _mocks = new(MockBehavior.Strict);
    private readonly Mock<ISecretProtectorProvider> _protectors;
    private readonly Mock<IMasterKeyManager> _masterKeys;
    private readonly Mock<IMasterKeyRewrapper> _rewrapper;
    private readonly Mock<IOwnerRootKeyChangeObserver> _observer;
    private readonly FakeReferencedKeyWrapper _kms = new();
    private readonly InMemoryOwnerRootKeyStores _stores = new();
    private readonly FakeTimeProvider _time = new(
        new DateTimeOffset(2026, 11, 5, 12, 0, 0, TimeSpan.Zero)
    );
    private readonly SecretProtectionOptions _options = new()
    {
        OwnerRootKeys = new OwnerRootKeyOptions { Providers = ["fake-kms"] },
    };

    public DefaultOwnerRootKeyManagerTests()
    {
        _protectors = _mocks.Create<ISecretProtectorProvider>();
        _masterKeys = _mocks.Create<IMasterKeyManager>();
        _rewrapper = _mocks.Create<IMasterKeyRewrapper>();
        _observer = _mocks.Create<IOwnerRootKeyChangeObserver>();
    }

    public void Dispose()
    {
        _mocks.Verify();
    }

    private DefaultOwnerRootKeyManager CreateManager()
    {
        var catalog = new DefaultKeyWrapperCatalog(
            [_kms],
            Microsoft.Extensions.Options.Options.Create(_options)
        );
        return new DefaultOwnerRootKeyManager(
            catalog,
            _stores,
            _protectors.Object,
            _masterKeys.Object,
            _rewrapper.Object,
            [_observer.Object],
            Microsoft.Extensions.Options.Options.Create(_options),
            _time,
            NullLogger<DefaultOwnerRootKeyManager>.Instance
        );
    }

    private PersistedOwnerRootKey CreateRootKey(
        string state = OwnerRootKeyStates.Active,
        string reference = Reference
    ) =>
        new()
        {
            OwnerType = NormalizedOwner.OwnerType,
            OwnerId = NormalizedOwner.OwnerId,
            KeyWrapperName = "fake-kms",
            KeyReference = reference,
            State = state,
            CreatedWhen = _time.GetUtcNow().AddDays(-1),
            StateChangedWhen = _time.GetUtcNow().AddDays(-1),
            ValidatedWhen = _time.GetUtcNow().AddMinutes(-90),
        };

    private static PersistedMasterKey CreateRow(
        int version,
        DateTimeOffset createdWhen,
        DateTimeOffset? retiredWhen = null,
        string? rootKeyReference = null
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
                _time.GetUtcNow(),
                null,
                "fake-kms",
                "root",
                null,
                reference
            ),
            null
        );

    private void ExpectEvictAndNotify(Times? times = null)
    {
        _protectors.Setup(x => x.Evict(NormalizedOwner)).Verifiable(times ?? Times.AtLeastOnce());
        _observer
            .Setup(x => x.ChangedAsync(NormalizedOwner, It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask)
            .Verifiable(times ?? Times.AtLeastOnce());
    }

    private void ExpectRewrap(MasterKeyRewrapResult result) =>
        _rewrapper
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
        var oldRow = CreateRow(1, _time.GetUtcNow().AddDays(-400));
        _stores.Seed(oldRow);
        ExpectEvictAndNotify();
        var rotation = CreateRotation();
        _masterKeys
            .Setup(x => x.RotateAsync(NormalizedOwner, It.IsAny<CancellationToken>()))
            .Returns(new ValueTask<MasterKeyRotation>(rotation))
            .Verifiable();
        ExpectRewrap(new MasterKeyRewrapResult(1, 1, 0, 0));

        var change = await CreateManager()
            .SetAsync(Owner, "fake-kms", Reference, CancellationToken.None);

        var stored = Assert.Single(_stores.RootKeys);
        Assert.Equal(OwnerRootKeyStates.Active, stored.State);
        Assert.Equal(Reference, stored.KeyReference);
        Assert.Equal(_time.GetUtcNow(), stored.CreatedWhen);
        Assert.Equal(_time.GetUtcNow(), stored.ValidatedWhen);
        Assert.Equal(RootKeyChanges.Set, change.Change);
        Assert.Same(rotation, change.Rotation);
        Assert.Equal(new MasterKeyRewrapResult(1, 1, 0, 0), change.Rewrap);
        Assert.Equal(Reference, change.RootKey.KeyReference);
        Assert.Equal(1, _kms.WrapCount);
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
            await manager.SetAsync(Owner, wrapper, reference, CancellationToken.None)
        );

        Assert.Equal("keyReference", exception.ParamName);
        Assert.Empty(_stores.RootKeys);
        Assert.Equal(0, _stores.SaveCount);
        Assert.Equal(0, _kms.WrapCount);
    }

    [Fact]
    public async Task Set_ProbeFailure_ThrowsAndWritesNothing()
    {
        _kms.Revoke(Reference);

        var exception = await Assert.ThrowsAsync<OwnerRootKeyProbeException>(async () =>
            await CreateManager().SetAsync(Owner, "fake-kms", Reference, CancellationToken.None)
        );

        Assert.Equal(Reference, exception.KeyReference);
        Assert.Contains("revoked", exception.Failure);
        Assert.Empty(_stores.RootKeys);
    }

    [Fact]
    public async Task Set_AfterARecentRotation_ThrowsTooSoonBeforeAnyWrite()
    {
        _stores.Seed(
            CreateRow(1, _time.GetUtcNow().AddDays(-400), _time.GetUtcNow().AddMinutes(-2))
        );
        _stores.Seed(CreateRow(2, _time.GetUtcNow().AddMinutes(-2)));

        var exception = await Assert.ThrowsAsync<MasterKeyRotationTooSoonException>(async () =>
            await CreateManager().SetAsync(Owner, "fake-kms", Reference, CancellationToken.None)
        );

        Assert.Equal(TimeSpan.FromMinutes(8), exception.RetryAfter);
        Assert.Empty(_stores.RootKeys);
        Assert.Equal(0, _kms.WrapCount);
    }

    [Fact]
    public async Task Set_SameKeyWhileUnavailable_RestoresItWithoutARotationWhenTheActiveKeyConforms()
    {
        _stores.Seed(CreateRootKey(OwnerRootKeyStates.Unavailable));
        _stores.Seed(CreateRow(1, _time.GetUtcNow().AddDays(-1), rootKeyReference: Reference));
        ExpectEvictAndNotify();
        ExpectRewrap(new MasterKeyRewrapResult(1, 0, 0, 0));

        var change = await CreateManager()
            .SetAsync(Owner, "fake-kms", Reference, CancellationToken.None);

        Assert.Equal(RootKeyChanges.Restored, change.Change);
        Assert.Null(change.Rotation);
        Assert.Equal(OwnerRootKeyStates.Active, Assert.Single(_stores.RootKeys).State);
        Assert.Equal(_time.GetUtcNow(), change.RootKey.StateChangedWhen);
    }

    [Fact]
    public async Task Set_SameActiveKey_ChangesNothingAndKeepsItsCreationTime()
    {
        var existing = _stores.Seed(CreateRootKey());
        _stores.Seed(CreateRow(1, _time.GetUtcNow().AddDays(-1), rootKeyReference: Reference));
        ExpectEvictAndNotify();
        ExpectRewrap(new MasterKeyRewrapResult(1, 0, 0, 0));

        var change = await CreateManager()
            .SetAsync(Owner, "fake-kms", Reference, CancellationToken.None);

        Assert.Null(change.Change);
        Assert.Null(change.Rotation);
        Assert.Equal(existing.CreatedWhen, change.RootKey.CreatedWhen);
    }

    [Fact]
    public async Task Set_AnotherKey_RotatesOntoIt()
    {
        _stores.Seed(CreateRootKey(reference: OtherReference));
        _stores.Seed(
            CreateRow(1, _time.GetUtcNow().AddDays(-30), rootKeyReference: OtherReference)
        );
        ExpectEvictAndNotify();
        _masterKeys
            .Setup(x => x.RotateAsync(NormalizedOwner, It.IsAny<CancellationToken>()))
            .Returns(new ValueTask<MasterKeyRotation>(CreateRotation()))
            .Verifiable();
        ExpectRewrap(new MasterKeyRewrapResult(2, 1, 0, 0));

        var change = await CreateManager()
            .SetAsync(Owner, "fake-kms", Reference, CancellationToken.None);

        Assert.Equal(RootKeyChanges.Set, change.Change);
        Assert.NotNull(change.Rotation);
        var stored = Assert.Single(_stores.RootKeys);
        Assert.Equal(Reference, stored.KeyReference);
        Assert.Equal(_time.GetUtcNow(), stored.CreatedWhen);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Set_RotationLostToARaceOrTooSoon_IsDeferredToTheValidationService(
        bool tooSoon
    )
    {
        _stores.Seed(CreateRow(1, _time.GetUtcNow().AddDays(-30)));
        ExpectEvictAndNotify();
        Exception failure = tooSoon
            ? new MasterKeyRotationTooSoonException(NormalizedOwner, TimeSpan.FromMinutes(1))
            : new StoreConcurrencyException("lost");
        _masterKeys
            .Setup(x => x.RotateAsync(NormalizedOwner, It.IsAny<CancellationToken>()))
            .Returns(
                new ValueTask<MasterKeyRotation>(Task.FromException<MasterKeyRotation>(failure))
            )
            .Verifiable();
        ExpectRewrap(new MasterKeyRewrapResult(1, 1, 0, 0));

        var change = await CreateManager()
            .SetAsync(Owner, "fake-kms", Reference, CancellationToken.None);

        Assert.Null(change.Rotation);
        Assert.Equal(RootKeyChanges.Set, change.Change);
        Assert.Equal(OwnerRootKeyStates.Active, Assert.Single(_stores.RootKeys).State);
    }

    [Fact]
    public async Task Set_SameKeyFailingItsProbe_RecordsTheOwnerUnavailableAndThrows()
    {
        _stores.Seed(CreateRootKey());
        ExpectEvictAndNotify(Times.Once());
        _kms.Revoke(Reference);

        var exception = await Assert.ThrowsAsync<OwnerRootKeyProbeException>(async () =>
            await CreateManager().SetAsync(Owner, "fake-kms", Reference, CancellationToken.None)
        );

        Assert.Equal(OwnerRootKeyStates.Unavailable, exception.UnavailableRootKey?.State);
        var stored = Assert.Single(_stores.RootKeys);
        Assert.Equal(OwnerRootKeyStates.Unavailable, stored.State);
        Assert.Equal(_time.GetUtcNow(), stored.FailedWhen);
        Assert.Contains("revoked", stored.Failure);
    }

    [Fact]
    public async Task Set_ServerOwner_Throws()
    {
        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await CreateManager()
                .SetAsync(
                    SecretOwner.ForServer("Server-1"),
                    "fake-kms",
                    Reference,
                    CancellationToken.None
                )
        );
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
        _stores.Seed(CreateRootKey());
        ExpectEvictAndNotify();
        ExpectRewrap(new MasterKeyRewrapResult(2, 2, 0, 0));

        var removal = await CreateManager().RemoveAsync(Owner, CancellationToken.None);

        Assert.True(removal.Removed);
        Assert.Null(removal.Remaining);
        Assert.Empty(_stores.RootKeys);
        Assert.Equal(new MasterKeyRewrapResult(2, 2, 0, 0), removal.Rewrap);
    }

    [Fact]
    public async Task Remove_WithRowsThatCannotBeMoved_StaysRemovingAndRecordsTheFailure()
    {
        _stores.Seed(CreateRootKey());
        ExpectEvictAndNotify();
        ExpectRewrap(new MasterKeyRewrapResult(2, 1, 0, 1));

        var removal = await CreateManager().RemoveAsync(Owner, CancellationToken.None);

        Assert.True(removal.Removed);
        Assert.Equal(OwnerRootKeyStates.Removing, removal.Remaining?.State);
        var stored = Assert.Single(_stores.RootKeys);
        Assert.Equal(OwnerRootKeyStates.Removing, stored.State);
        Assert.Equal(_time.GetUtcNow(), stored.FailedWhen);
        Assert.Contains("1 master keys could not be unwrapped", stored.Failure);
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
        _stores.Seed(CreateRootKey());
        _stores.Seed(CreateRow(1, _time.GetUtcNow().AddDays(-1), rootKeyReference: Reference));
        ExpectRewrap(new MasterKeyRewrapResult(1, 0, 0, 0));

        var validation = await CreateManager().ValidateAsync(Owner, CancellationToken.None);

        Assert.Null(validation.Change);
        Assert.Equal(_time.GetUtcNow(), validation.RootKey?.ValidatedWhen);
        Assert.Equal(OwnerRootKeyStates.Active, validation.RootKey?.State);
    }

    [Fact]
    public async Task Validate_Failure_MakesTheOwnerUnavailableAndTellsEveryReplica()
    {
        _stores.Seed(CreateRootKey());
        ExpectEvictAndNotify(Times.Once());
        _kms.Revoke(Reference);

        var validation = await CreateManager().ValidateAsync(Owner, CancellationToken.None);

        Assert.Equal(RootKeyChanges.Unavailable, validation.Change);
        Assert.Equal(OwnerRootKeyStates.Unavailable, validation.RootKey?.State);
        Assert.Equal(_time.GetUtcNow(), validation.RootKey?.FailedWhen);
        Assert.Contains("revoked", validation.RootKey?.Failure);
        Assert.Null(validation.Rotation);
    }

    [Fact]
    public async Task Validate_RepeatedFailure_RecordsItWithoutAnotherChange()
    {
        _stores.Seed(CreateRootKey(OwnerRootKeyStates.Unavailable));
        _kms.Revoke(Reference);

        var validation = await CreateManager().ValidateAsync(Owner, CancellationToken.None);

        Assert.Null(validation.Change);
        Assert.Equal(_time.GetUtcNow(), validation.RootKey?.FailedWhen);
    }

    [Fact]
    public async Task Validate_SuccessAfterAFailure_RestoresTheOwnerAndCompletes()
    {
        _stores.Seed(CreateRootKey(OwnerRootKeyStates.Unavailable));
        _stores.Seed(CreateRow(1, _time.GetUtcNow().AddDays(-1), rootKeyReference: Reference));
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
        _stores.Seed(CreateRootKey());
        _stores.Seed(CreateRow(1, _time.GetUtcNow().AddDays(-30)));
        var rotation = CreateRotation();
        _masterKeys
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
        _stores.Seed(CreateRootKey(OwnerRootKeyStates.Removing));
        ExpectEvictAndNotify(Times.Once());
        ExpectRewrap(new MasterKeyRewrapResult(1, 1, 0, 0));
        _kms.Revoke(Reference);

        var validation = await CreateManager().ValidateAsync(Owner, CancellationToken.None);

        Assert.Null(validation.RootKey);
        Assert.Empty(_stores.RootKeys);
        Assert.Equal(0, _kms.WrapCount);
    }

    [Fact]
    public async Task Validate_ServerOwner_Throws()
    {
        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await CreateManager()
                .ValidateAsync(SecretOwner.ForServer("server-1"), CancellationToken.None)
        );
    }

    #endregion

    #region Policy Tests

    [Fact]
    public void AllowedKeyWrapperNames_AreTheConfiguredProvidersInOrdinalOrder()
    {
        var manager = CreateManager();

        _options.OwnerRootKeys.Providers = ["b-kms", "a-kms"];

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

        _options.OwnerRootKeys.Providers = ["missing"];
        Assert.False(manager.TryValidateKeyReference("missing", Reference, out var notReferenced));
        Assert.Contains("does not wrap under a key", notReferenced);
    }

    [Fact]
    public async Task Get_ReturnsTheRootKeyWithoutMaterial()
    {
        _stores.Seed(CreateRootKey());

        var info = await CreateManager().GetAsync(Owner, CancellationToken.None);

        Assert.Equal(Reference, info?.KeyReference);
        Assert.Null(
            await CreateManager().GetAsync(SecretOwner.ForTenant("other"), CancellationToken.None)
        );
    }

    #endregion
}
