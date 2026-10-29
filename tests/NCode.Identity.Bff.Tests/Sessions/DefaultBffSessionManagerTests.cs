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

using Moq;
using NCode.Identity.Bff.Auditing;
using NCode.Identity.Bff.Sessions;
using NCode.Identity.Bff.Tests.Diagnostics;
using Xunit;

namespace NCode.Identity.Bff.Tests.Sessions;

public class DefaultBffSessionManagerTests : BaseTests
{
    private Mock<IBffSessionStore> MockStore { get; }
    private Mock<IBffAuditRecorder> MockRecorder { get; }
    private DefaultBffSessionManager Manager { get; }

    public DefaultBffSessionManagerTests()
    {
        MockStore = CreateStrictMock<IBffSessionStore>();
        MockRecorder = CreateStrictMock<IBffAuditRecorder>();
        Manager = new DefaultBffSessionManager(
            BffTestTelemetry.Instrument(MockStore.Object),
            MockRecorder.Object
        );
    }

    private static BffSessionMetadata CreateMetadata() =>
        new()
        {
            Frontend = "default",
            Issuer = "https://op.example",
            Subject = "sub-1",
            SessionId = "sid-1",
            CreatedAt = new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero),
            LastAccessedAt = new DateTimeOffset(2026, 1, 2, 3, 4, 6, TimeSpan.Zero),
            AbsoluteExpiresAt = new DateTimeOffset(2026, 1, 2, 11, 4, 5, TimeSpan.Zero),
            IdleExpiresAt = new DateTimeOffset(2026, 1, 2, 4, 4, 6, TimeSpan.Zero),
        };

    private static BffSessionPayload CreatePayload() =>
        new()
        {
            IdToken = "id-token",
            RefreshToken = "refresh",
            LogoutGuard = "guard",
        };

    #region CreateAsync Tests

    [Fact]
    public async Task CreateAsync_Created_StoresVersionOne()
    {
        BffSessionRecord? stored = null;
        MockStore
            .Setup(x =>
                x.TryCreateAsync(It.IsAny<BffSessionRecord>(), It.IsAny<CancellationToken>())
            )
            .Callback<BffSessionRecord, CancellationToken>((record, _) => stored = record)
            .ReturnsAsync(true)
            .Verifiable();

        var state = await Manager.CreateAsync(
            CreateMetadata(),
            CreatePayload(),
            CancellationToken.None
        );

        Assert.NotNull(stored);
        Assert.Equal(1, stored.Version);
        Assert.Equal(1, state.Version);
        Assert.Equal(stored.StoreKey, state.StoreKey);
        Assert.True(BffSessionCodec.TryGetStoreKey(state.Handle, out var storeKey));
        Assert.Equal(storeKey, state.StoreKey);
    }

    [Fact]
    public async Task CreateAsync_CollisionOnce_RetriesWithNewHandle()
    {
        var storeKeys = new List<string>();
        MockStore
            .SetupSequence(x =>
                x.TryCreateAsync(It.IsAny<BffSessionRecord>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(false)
            .ReturnsAsync(true);

        var state = await Manager.CreateAsync(
            CreateMetadata(),
            CreatePayload(),
            CancellationToken.None
        );

        storeKeys.Add(state.StoreKey);
        Assert.Single(storeKeys);
        MockStore.Verify(
            x => x.TryCreateAsync(It.IsAny<BffSessionRecord>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2)
        );
    }

    [Fact]
    public async Task CreateAsync_CollisionTwice_Throws()
    {
        MockStore
            .Setup(x =>
                x.TryCreateAsync(It.IsAny<BffSessionRecord>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(false)
            .Verifiable();

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await Manager.CreateAsync(CreateMetadata(), CreatePayload(), CancellationToken.None)
        );
    }

    #endregion

    #region LoadAsync Tests

    [Fact]
    public async Task LoadAsync_MalformedHandle_ReturnsNull()
    {
        var result = await Manager.LoadAsync("short", CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task LoadAsync_AbsentRecord_ReturnsNull()
    {
        var handle = BffKeyDerivation.CreateSecret();
        BffSessionCodec.TryGetStoreKey(handle, out var storeKey);
        MockStore
            .Setup(x => x.GetAsync(storeKey!, It.IsAny<CancellationToken>()))
            .ReturnsAsync((BffSessionRecord?)null)
            .Verifiable();

        var result = await Manager.LoadAsync(handle, CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task LoadAsync_UndecodableRecord_ReturnsNull()
    {
        var handle = BffKeyDerivation.CreateSecret();
        var other = BffKeyDerivation.CreateSecret();
        var record = BffSessionCodec.Encode(other, 1, CreateMetadata(), CreatePayload());
        var mismatched = new BffSessionRecord(
            BffSessionCodec.TryGetStoreKey(handle, out var storeKey) ? storeKey : string.Empty,
            record.Version,
            record.Metadata,
            record.ProtectedPayload
        );
        MockStore
            .Setup(x => x.GetAsync(mismatched.StoreKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(mismatched)
            .Verifiable();

        var result = await Manager.LoadAsync(handle, CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task LoadAsync_ValidRecord_ReturnsState()
    {
        var handle = BffKeyDerivation.CreateSecret();
        var record = BffSessionCodec.Encode(handle, 4, CreateMetadata(), CreatePayload());
        MockStore
            .Setup(x => x.GetAsync(record.StoreKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(record)
            .Verifiable();

        var result = await Manager.LoadAsync(handle, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(handle, result.Handle);
        Assert.Equal(record.StoreKey, result.StoreKey);
        Assert.Equal(4, result.Version);
        Assert.Equal("refresh", result.Payload.RefreshToken);
        Assert.Equal("sub-1", result.Metadata.Subject);
    }

    #endregion

    #region UpdateAsync Tests

    private static BffSessionState CreateState(string handle, long version, string refreshToken)
    {
        BffSessionCodec.TryGetStoreKey(handle, out var storeKey);
        return new BffSessionState(
            handle,
            storeKey!,
            version,
            CreateMetadata(),
            CreatePayload() with
            {
                RefreshToken = refreshToken,
            }
        );
    }

    [Fact]
    public async Task UpdateAsync_FirstTrySucceeds_ReturnsNextWithBumpedVersion()
    {
        var handle = BffKeyDerivation.CreateSecret();
        var current = CreateState(handle, 3, "old");
        BffSessionRecord? stored = null;
        MockStore
            .Setup(x =>
                x.TryReplaceAsync(It.IsAny<BffSessionRecord>(), 3, It.IsAny<CancellationToken>())
            )
            .Callback<BffSessionRecord, long, CancellationToken>((record, _, _) => stored = record)
            .ReturnsAsync(true)
            .Verifiable();

        var result = await Manager.UpdateAsync(
            current,
            state => state with { Payload = state.Payload with { RefreshToken = "new" } },
            CancellationToken.None
        );

        Assert.NotNull(result);
        Assert.NotNull(stored);
        Assert.Equal(4, result.Version);
        Assert.Equal(4, stored.Version);
        Assert.Equal("new", result.Payload.RefreshToken);
    }

    [Fact]
    public async Task UpdateAsync_UpdateReturnsNull_ReturnsStateUnchanged()
    {
        var current = CreateState(BffKeyDerivation.CreateSecret(), 3, "old");

        var result = await Manager.UpdateAsync(current, _ => null, CancellationToken.None);

        Assert.Same(current, result);
    }

    [Fact]
    public async Task UpdateAsync_ConflictThenSuccess_ReloadsAndRetries()
    {
        var handle = BffKeyDerivation.CreateSecret();
        var current = CreateState(handle, 3, "old");
        var storeKey = current.StoreKey;
        MockStore
            .Setup(x =>
                x.TryReplaceAsync(It.IsAny<BffSessionRecord>(), 3, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(false)
            .Verifiable();
        MockStore
            .SetupSequence(x => x.GetAsync(storeKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                BffSessionCodec.Encode(
                    handle,
                    7,
                    CreateMetadata(),
                    CreatePayload() with
                    {
                        RefreshToken = "concurrent",
                    }
                )
            );
        MockStore
            .Setup(x =>
                x.TryReplaceAsync(It.IsAny<BffSessionRecord>(), 7, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(true)
            .Verifiable();

        var seen = new List<string?>();
        var result = await Manager.UpdateAsync(
            current,
            state =>
            {
                seen.Add(state.Payload.RefreshToken);
                return state;
            },
            CancellationToken.None
        );

        Assert.NotNull(result);
        Assert.Equal(8, result.Version);
        Assert.Equal(["old", "concurrent"], seen);
    }

    [Fact]
    public async Task UpdateAsync_ConflictThenSessionGone_ReturnsNull()
    {
        var handle = BffKeyDerivation.CreateSecret();
        var current = CreateState(handle, 3, "old");
        MockStore
            .Setup(x =>
                x.TryReplaceAsync(It.IsAny<BffSessionRecord>(), 3, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(false)
            .Verifiable();
        MockStore
            .Setup(x => x.GetAsync(current.StoreKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync((BffSessionRecord?)null)
            .Verifiable();

        var result = await Manager.UpdateAsync(current, state => state, CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task UpdateAsync_FiveConflicts_Throws()
    {
        var handle = BffKeyDerivation.CreateSecret();
        var current = CreateState(handle, 1, "old");
        MockStore
            .Setup(x =>
                x.TryReplaceAsync(
                    It.IsAny<BffSessionRecord>(),
                    It.IsAny<long>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(false);
        MockStore
            .SetupSequence(x => x.GetAsync(current.StoreKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(BffSessionCodec.Encode(handle, 2, CreateMetadata(), CreatePayload()))
            .ReturnsAsync(BffSessionCodec.Encode(handle, 3, CreateMetadata(), CreatePayload()))
            .ReturnsAsync(BffSessionCodec.Encode(handle, 4, CreateMetadata(), CreatePayload()))
            .ReturnsAsync(BffSessionCodec.Encode(handle, 5, CreateMetadata(), CreatePayload()));

        await Assert.ThrowsAsync<BffStoreUnavailableException>(async () =>
            await Manager.UpdateAsync(current, state => state, CancellationToken.None)
        );

        MockStore.Verify(
            x =>
                x.TryReplaceAsync(
                    It.IsAny<BffSessionRecord>(),
                    It.IsAny<long>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Exactly(5)
        );
    }

    #endregion

    #region EndAsync Tests

    [Fact]
    public async Task EndAsync_Always_RemovesThenRecords()
    {
        var metadata = CreateMetadata();
        var order = new List<string>();
        MockStore
            .Setup(x => x.RemoveAsync("key", metadata, It.IsAny<CancellationToken>()))
            .Callback(() => order.Add("remove"))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        MockRecorder
            .Setup(x => x.SessionEndedAsync(null, "sub-1", "logout", It.IsAny<CancellationToken>()))
            .Callback(() => order.Add("record"))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        await Manager.EndAsync(null, "key", metadata, "logout", CancellationToken.None);

        Assert.Equal(["remove", "record"], order);
    }

    #endregion
}
