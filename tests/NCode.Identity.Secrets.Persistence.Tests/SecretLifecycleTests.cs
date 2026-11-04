// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.Secrets.Persistence;

public class SecretLifecycleTests
{
    private static readonly DateTimeOffset Created = DateTimeOffset.UnixEpoch;
    private static readonly DateTimeOffset Expires = DateTimeOffset.UnixEpoch.AddDays(100);

    #region Resolve Tests

    [Fact]
    public void Resolve_WhenNeitherInstantIsNamed_ActivatesAtCreationAndRetiresAtExpiry()
    {
        var (activatesWhen, retiresWhen) = SecretLifecycle.Resolve(Created, null, null, Expires);

        Assert.Equal(Created, activatesWhen);
        Assert.Equal(Expires, retiresWhen);
    }

    [Fact]
    public void Resolve_WhenBothInstantsAreNamedInOrder_ReturnsThem()
    {
        var (activatesWhen, retiresWhen) = SecretLifecycle.Resolve(
            Created,
            Created.AddDays(2),
            Created.AddDays(92),
            Expires
        );

        Assert.Equal(Created.AddDays(2), activatesWhen);
        Assert.Equal(Created.AddDays(92), retiresWhen);
    }

    [Fact]
    public void Resolve_WhenTheActivationPrecedesTheCreation_Throws()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            SecretLifecycle.Resolve(Created, Created.AddSeconds(-1), null, Expires)
        );

        Assert.Contains("cannot activate", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Resolve_WhenTheRetirementPrecedesTheActivation_Throws()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            SecretLifecycle.Resolve(Created, Created.AddDays(2), Created.AddDays(1), Expires)
        );

        Assert.Contains("cannot retire", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Resolve_WhenTheExpiryPrecedesTheRetirement_Throws()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            SecretLifecycle.Resolve(Created, null, Expires.AddSeconds(1), Expires)
        );

        Assert.Contains("cannot expire", exception.Message, StringComparison.Ordinal);
    }

    #endregion

    #region TryValidate Tests

    [Fact]
    public void TryValidate_WhenNeighborsCoincide_IsValid()
    {
        var valid = SecretLifecycle.TryValidate(Created, Created, Created, Created, out var error);

        Assert.True(valid);
        Assert.Null(error);
    }

    [Fact]
    public void TryValidate_WhenTheInstantsAreOutOfOrder_NamesTheFirstPairOutOfOrder()
    {
        var valid = SecretLifecycle.TryValidate(
            Created,
            Created.AddDays(2),
            Created.AddDays(1),
            Expires,
            out var error
        );

        Assert.False(valid);
        Assert.NotNull(error);
        Assert.Contains("cannot retire", error, StringComparison.Ordinal);
    }

    #endregion
}
