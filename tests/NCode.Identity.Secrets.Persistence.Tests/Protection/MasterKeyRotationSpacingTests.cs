// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.Secrets.Persistence.DataContracts;

namespace NCode.Identity.Secrets.Persistence.Protection;

public class MasterKeyRotationSpacingTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 5, 12, 0, 0, TimeSpan.Zero);

    private static PersistedMasterKey CreateRow(DateTimeOffset? retiredWhen) =>
        new()
        {
            KeyId = Guid.NewGuid(),
            OwnerType = "tenant",
            OwnerId = "acme",
            Version = 1,
            CreatedWhen = Now.AddDays(-100),
            RetiredWhen = retiredWhen,
            RootKeyProvider = "local",
            RootKeyId = "root",
            RootWrappedKey = "wrapped",
            KeyCheck = "check",
        };

    [Fact]
    public void GetRemaining_NoRetiredKey_IsNull()
    {
        Assert.Null(
            MasterKeyRotationSpacing.GetRemaining([CreateRow(null)], TimeSpan.FromMinutes(5), Now)
        );
        Assert.Null(MasterKeyRotationSpacing.GetRemaining([], TimeSpan.FromMinutes(5), Now));
    }

    [Fact]
    public void GetRemaining_WithinTwiceTheRefreshInterval_IsTheTimeLeft()
    {
        var remaining = MasterKeyRotationSpacing.GetRemaining(
            [CreateRow(Now.AddMinutes(-4)), CreateRow(Now.AddDays(-30))],
            TimeSpan.FromMinutes(5),
            Now
        );

        Assert.Equal(TimeSpan.FromMinutes(6), remaining);
    }

    [Fact]
    public void GetRemaining_AfterTwiceTheRefreshInterval_IsNull()
    {
        Assert.Null(
            MasterKeyRotationSpacing.GetRemaining(
                [CreateRow(Now.AddMinutes(-10))],
                TimeSpan.FromMinutes(5),
                Now
            )
        );
    }
}
