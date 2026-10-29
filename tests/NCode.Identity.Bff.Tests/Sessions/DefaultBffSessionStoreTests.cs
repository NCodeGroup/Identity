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
using NCode.Identity.Bff.Sessions;
using NCode.Identity.SharedState;
using Xunit;

namespace NCode.Identity.Bff.Tests.Sessions;

public class DefaultBffSessionStoreTests : BaseTests
{
    private const string StoreKey = "store-key";

    private static readonly DateTimeOffset Absolute = new(2026, 1, 2, 11, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Idle = new(2026, 1, 2, 4, 0, 0, TimeSpan.Zero);

    private Mock<IExpiringValueSet> MockValues { get; }
    private Mock<IExpiringIndexSet> MockIndexes { get; }
    private DefaultBffSessionStore Store { get; }

    public DefaultBffSessionStoreTests()
    {
        MockValues = CreateStrictMock<IExpiringValueSet>();
        MockIndexes = CreateStrictMock<IExpiringIndexSet>();
        Store = new DefaultBffSessionStore(MockValues.Object, MockIndexes.Object);
    }

    private static BffSessionMetadata CreateMetadata(string? sessionId = "sid-1") =>
        new()
        {
            Frontend = "default",
            Issuer = "https://op.example",
            Subject = "sub-1",
            SessionId = sessionId,
            CreatedAt = new DateTimeOffset(2026, 1, 2, 3, 0, 0, TimeSpan.Zero),
            LastAccessedAt = new DateTimeOffset(2026, 1, 2, 3, 0, 0, TimeSpan.Zero),
            AbsoluteExpiresAt = Absolute,
            IdleExpiresAt = Idle,
        };

    private static BffSessionRecord CreateRecord(string? sessionId = "sid-1") =>
        new(StoreKey, 1, CreateMetadata(sessionId), new byte[] { 1, 2, 3 });

    #region TryCreateAsync Tests

    [Fact]
    public async Task TryCreateAsync_Added_AddsTheValueAndBothIndexMembers()
    {
        var metadata = CreateMetadata();
        var sidIndex = BffStoreKeys.SessionIdIndex(metadata.Issuer, "sid-1");
        var subIndex = BffStoreKeys.SubjectIndex(metadata.Issuer, metadata.Subject);

        MockValues
            .Setup(x =>
                x.TryAddAsync(
                    "bff:session:" + StoreKey,
                    It.IsAny<ReadOnlyMemory<byte>>(),
                    Idle,
                    CancellationToken.None
                )
            )
            .ReturnsAsync(true)
            .Verifiable();
        MockIndexes
            .Setup(x => x.AddMemberAsync(sidIndex, StoreKey, Absolute, CancellationToken.None))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        MockIndexes
            .Setup(x => x.AddMemberAsync(subIndex, StoreKey, Absolute, CancellationToken.None))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        var result = await Store.TryCreateAsync(CreateRecord(), CancellationToken.None);

        Assert.True(result);
    }

    [Fact]
    public async Task TryCreateAsync_NoSessionId_AddsOnlyTheSubjectMember()
    {
        var metadata = CreateMetadata(null);
        var subIndex = BffStoreKeys.SubjectIndex(metadata.Issuer, metadata.Subject);

        MockValues
            .Setup(x =>
                x.TryAddAsync(
                    "bff:session:" + StoreKey,
                    It.IsAny<ReadOnlyMemory<byte>>(),
                    Idle,
                    CancellationToken.None
                )
            )
            .ReturnsAsync(true)
            .Verifiable();
        MockIndexes
            .Setup(x => x.AddMemberAsync(subIndex, StoreKey, Absolute, CancellationToken.None))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        var result = await Store.TryCreateAsync(CreateRecord(null), CancellationToken.None);

        Assert.True(result);
    }

    [Fact]
    public async Task TryCreateAsync_KeyExists_ReturnsFalseWithoutIndexing()
    {
        MockValues
            .Setup(x =>
                x.TryAddAsync(
                    "bff:session:" + StoreKey,
                    It.IsAny<ReadOnlyMemory<byte>>(),
                    Idle,
                    CancellationToken.None
                )
            )
            .ReturnsAsync(false)
            .Verifiable();

        var result = await Store.TryCreateAsync(CreateRecord(), CancellationToken.None);

        Assert.False(result);
    }

    [Fact]
    public async Task TryCreateAsync_AdapterThrows_ThrowsStoreUnavailable()
    {
        MockValues
            .Setup(x =>
                x.TryAddAsync(
                    It.IsAny<string>(),
                    It.IsAny<ReadOnlyMemory<byte>>(),
                    It.IsAny<DateTimeOffset>(),
                    CancellationToken.None
                )
            )
            .ThrowsAsync(new InvalidOperationException("boom"))
            .Verifiable();

        var exception = await Assert.ThrowsAsync<BffStoreUnavailableException>(async () =>
            await Store.TryCreateAsync(CreateRecord(), CancellationToken.None)
        );

        Assert.IsType<InvalidOperationException>(exception.InnerException);
    }

    [Fact]
    public async Task TryCreateAsync_Canceled_PropagatesTheCancellation()
    {
        MockValues
            .Setup(x =>
                x.TryAddAsync(
                    It.IsAny<string>(),
                    It.IsAny<ReadOnlyMemory<byte>>(),
                    It.IsAny<DateTimeOffset>(),
                    CancellationToken.None
                )
            )
            .ThrowsAsync(new OperationCanceledException())
            .Verifiable();

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await Store.TryCreateAsync(CreateRecord(), CancellationToken.None)
        );
    }

    #endregion

    #region GetAsync Tests

    [Fact]
    public async Task GetAsync_Present_ReturnsTheRecordWithTheStoredVersion()
    {
        var value = BffSessionRecordSerializer.Serialize(CreateMetadata(), new byte[] { 9, 8 });
        MockValues
            .Setup(x => x.GetAsync("bff:session:" + StoreKey, CancellationToken.None))
            .ReturnsAsync(new ExpiringValue(value, 4))
            .Verifiable();

        var record = await Store.GetAsync(StoreKey, CancellationToken.None);

        Assert.NotNull(record);
        Assert.Equal(StoreKey, record.StoreKey);
        Assert.Equal(4, record.Version);
        Assert.Equal("sub-1", record.Metadata.Subject);
        Assert.Equal(new byte[] { 9, 8 }, record.ProtectedPayload.ToArray());
    }

    [Fact]
    public async Task GetAsync_Absent_ReturnsNull()
    {
        MockValues
            .Setup(x => x.GetAsync("bff:session:" + StoreKey, CancellationToken.None))
            .ReturnsAsync((ExpiringValue?)null)
            .Verifiable();

        var record = await Store.GetAsync(StoreKey, CancellationToken.None);

        Assert.Null(record);
    }

    [Fact]
    public async Task GetAsync_Malformed_ReturnsNull()
    {
        MockValues
            .Setup(x => x.GetAsync("bff:session:" + StoreKey, CancellationToken.None))
            .ReturnsAsync(new ExpiringValue(new byte[] { 2, 0 }, 1))
            .Verifiable();

        var record = await Store.GetAsync(StoreKey, CancellationToken.None);

        Assert.Null(record);
    }

    #endregion

    #region TryReplaceAsync Tests

    [Fact]
    public async Task TryReplaceAsync_Replaced_UsesTheMinimumExpiry()
    {
        MockValues
            .Setup(x =>
                x.TryReplaceAsync(
                    "bff:session:" + StoreKey,
                    3,
                    It.IsAny<ReadOnlyMemory<byte>>(),
                    Idle,
                    CancellationToken.None
                )
            )
            .ReturnsAsync(true)
            .Verifiable();

        var result = await Store.TryReplaceAsync(CreateRecord(), 3, CancellationToken.None);

        Assert.True(result);
    }

    [Fact]
    public async Task TryReplaceAsync_Conflict_ReturnsFalse()
    {
        MockValues
            .Setup(x =>
                x.TryReplaceAsync(
                    "bff:session:" + StoreKey,
                    3,
                    It.IsAny<ReadOnlyMemory<byte>>(),
                    Idle,
                    CancellationToken.None
                )
            )
            .ReturnsAsync(false)
            .Verifiable();

        var result = await Store.TryReplaceAsync(CreateRecord(), 3, CancellationToken.None);

        Assert.False(result);
    }

    #endregion

    #region RemoveAsync Tests

    [Fact]
    public async Task RemoveAsync_WithSessionId_RemovesTheValueAndBothMembers()
    {
        var metadata = CreateMetadata();
        var sidIndex = BffStoreKeys.SessionIdIndex(metadata.Issuer, "sid-1");
        var subIndex = BffStoreKeys.SubjectIndex(metadata.Issuer, metadata.Subject);

        MockValues
            .Setup(x => x.RemoveAsync("bff:session:" + StoreKey, CancellationToken.None))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        MockIndexes
            .Setup(x => x.RemoveMemberAsync(sidIndex, StoreKey, CancellationToken.None))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        MockIndexes
            .Setup(x => x.RemoveMemberAsync(subIndex, StoreKey, CancellationToken.None))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        await Store.RemoveAsync(StoreKey, metadata, CancellationToken.None);
    }

    [Fact]
    public async Task RemoveAsync_AdapterThrows_ThrowsStoreUnavailable()
    {
        MockValues
            .Setup(x => x.RemoveAsync("bff:session:" + StoreKey, CancellationToken.None))
            .ThrowsAsync(new InvalidOperationException("boom"))
            .Verifiable();

        await Assert.ThrowsAsync<BffStoreUnavailableException>(async () =>
            await Store.RemoveAsync(StoreKey, CreateMetadata(), CancellationToken.None)
        );
    }

    #endregion

    #region Lookup Tests

    [Fact]
    public async Task FindBySessionIdAsync_Called_ReturnsTheIndexMembers()
    {
        var indexKey = BffStoreKeys.SessionIdIndex("iss", "sid-1");
        IReadOnlyList<string> members = ["a", "b"];
        MockIndexes
            .Setup(x => x.GetMembersAsync(indexKey, CancellationToken.None))
            .ReturnsAsync(members)
            .Verifiable();

        var result = await Store.FindBySessionIdAsync("iss", "sid-1", CancellationToken.None);

        Assert.Equal(members, result);
    }

    [Fact]
    public async Task FindBySubjectAsync_Called_ReturnsTheIndexMembers()
    {
        var indexKey = BffStoreKeys.SubjectIndex("iss", "sub-1");
        IReadOnlyList<string> members = ["a"];
        MockIndexes
            .Setup(x => x.GetMembersAsync(indexKey, CancellationToken.None))
            .ReturnsAsync(members)
            .Verifiable();

        var result = await Store.FindBySubjectAsync("iss", "sub-1", CancellationToken.None);

        Assert.Equal(members, result);
    }

    [Fact]
    public async Task FindBySubjectAsync_AdapterThrows_ThrowsStoreUnavailable()
    {
        MockIndexes
            .Setup(x => x.GetMembersAsync(It.IsAny<string>(), CancellationToken.None))
            .ThrowsAsync(new InvalidOperationException("boom"))
            .Verifiable();

        await Assert.ThrowsAsync<BffStoreUnavailableException>(async () =>
            await Store.FindBySubjectAsync("iss", "sub-1", CancellationToken.None)
        );
    }

    #endregion

    #region Transaction Tests

    [Fact]
    public async Task TryAddTransactionAsync_Called_AddsUnderTheTransactionKey()
    {
        var bytes = new byte[] { 1, 2 };
        MockValues
            .Setup(x =>
                x.TryAddAsync(
                    "bff:txn:t1",
                    It.Is<ReadOnlyMemory<byte>>(v => v.ToArray().SequenceEqual(bytes)),
                    Idle,
                    CancellationToken.None
                )
            )
            .ReturnsAsync(true)
            .Verifiable();

        var result = await Store.TryAddTransactionAsync("t1", bytes, Idle, CancellationToken.None);

        Assert.True(result);
    }

    [Fact]
    public async Task TryTakeTransactionAsync_Present_ReturnsTheValue()
    {
        MockValues
            .Setup(x => x.TryTakeAsync("bff:txn:t1", CancellationToken.None))
            .ReturnsAsync(new ExpiringValue(new byte[] { 5, 6 }, 1))
            .Verifiable();

        var result = await Store.TryTakeTransactionAsync("t1", CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(new byte[] { 5, 6 }, result.Value.ToArray());
    }

    [Fact]
    public async Task TryTakeTransactionAsync_Absent_ReturnsNull()
    {
        MockValues
            .Setup(x => x.TryTakeAsync("bff:txn:t1", CancellationToken.None))
            .ReturnsAsync((ExpiringValue?)null)
            .Verifiable();

        var result = await Store.TryTakeTransactionAsync("t1", CancellationToken.None);

        Assert.Null(result);
    }

    #endregion

    #region Lease Tests

    [Fact]
    public async Task TryAcquireRefreshLeaseAsync_Called_AddsTheMarker()
    {
        MockValues
            .Setup(x =>
                x.TryAddAsync(
                    "bff:lease:l1",
                    It.Is<ReadOnlyMemory<byte>>(v =>
                        v.ToArray().SequenceEqual(new byte[] { 0x31 })
                    ),
                    Idle,
                    CancellationToken.None
                )
            )
            .ReturnsAsync(true)
            .Verifiable();

        var result = await Store.TryAcquireRefreshLeaseAsync("l1", Idle, CancellationToken.None);

        Assert.True(result);
    }

    [Fact]
    public async Task IsRefreshLeaseHeldAsync_Present_ReturnsTrue()
    {
        MockValues
            .Setup(x => x.GetAsync("bff:lease:l1", CancellationToken.None))
            .ReturnsAsync(new ExpiringValue(new byte[] { 0x31 }, 1))
            .Verifiable();

        var result = await Store.IsRefreshLeaseHeldAsync("l1", CancellationToken.None);

        Assert.True(result);
    }

    [Fact]
    public async Task IsRefreshLeaseHeldAsync_Absent_ReturnsFalse()
    {
        MockValues
            .Setup(x => x.GetAsync("bff:lease:l1", CancellationToken.None))
            .ReturnsAsync((ExpiringValue?)null)
            .Verifiable();

        var result = await Store.IsRefreshLeaseHeldAsync("l1", CancellationToken.None);

        Assert.False(result);
    }

    [Fact]
    public async Task ReleaseRefreshLeaseAsync_Called_RemovesTheLease()
    {
        MockValues
            .Setup(x => x.RemoveAsync("bff:lease:l1", CancellationToken.None))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        await Store.ReleaseRefreshLeaseAsync("l1", CancellationToken.None);
    }

    #endregion

    #region BffStoreKeys Tests

    [Fact]
    public void SessionIdIndex_DifferentSplit_ReturnsDifferentKeys()
    {
        var first = BffStoreKeys.SessionIdIndex("ab", "c");
        var second = BffStoreKeys.SessionIdIndex("a", "bc");

        Assert.NotEqual(first, second);
        Assert.StartsWith("bff:sid:", first);
        Assert.StartsWith("bff:sub:", BffStoreKeys.SubjectIndex("ab", "c"));
    }

    #endregion
}
