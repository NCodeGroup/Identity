// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.Secrets.Persistence.DataContracts;

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

    #region TryResolve Tests

    [Fact]
    public void TryResolve_WhenTheInstantsAreInOrder_ReturnsThemWithNoError()
    {
        var isValid = SecretLifecycle.TryResolve(
            Created,
            null,
            null,
            Expires,
            out var resolved,
            out var error
        );

        Assert.True(isValid);
        Assert.Equal((Created, Expires), resolved);
        Assert.Null(error);
    }

    [Fact]
    public void TryResolve_WhenTheInstantsAreOutOfOrder_IsFalseAndNamesThePair()
    {
        var isValid = SecretLifecycle.TryResolve(
            Created,
            Created.AddDays(2),
            Created.AddDays(1),
            Expires,
            out _,
            out var error
        );

        Assert.False(isValid);
        Assert.NotNull(error);
        Assert.Contains("cannot retire", error, StringComparison.Ordinal);
    }

    #endregion

    #region ResolveUpdate Tests

    [Fact]
    public void ResolveUpdate_WhenNeitherInstantIsNamed_KeepsTheStoredOnes()
    {
        var secret = CreateSecret(Created.AddDays(2), Created.AddDays(92));

        var (activatesWhen, retiresWhen) = SecretLifecycle.ResolveUpdate(
            secret,
            null,
            null,
            Expires
        );

        Assert.Equal(Created.AddDays(2), activatesWhen);
        Assert.Equal(Created.AddDays(92), retiresWhen);
    }

    [Fact]
    public void ResolveUpdate_WhenTheNewExpiryPrecedesTheStoredRetirement_BoundsTheRetirementByIt()
    {
        var secret = CreateSecret(Created, Expires);

        var (_, retiresWhen) = SecretLifecycle.ResolveUpdate(
            secret,
            null,
            null,
            Created.AddDays(10)
        );

        Assert.Equal(Created.AddDays(10), retiresWhen);
    }

    [Fact]
    public void ResolveUpdate_WhenAnInstantIsNamed_TakesIt()
    {
        var secret = CreateSecret(Created.AddDays(2), Created.AddDays(92));

        var (activatesWhen, retiresWhen) = SecretLifecycle.ResolveUpdate(
            secret,
            Created.AddDays(3),
            Created.AddDays(50),
            Expires
        );

        Assert.Equal(Created.AddDays(3), activatesWhen);
        Assert.Equal(Created.AddDays(50), retiresWhen);
    }

    [Fact]
    public void ResolveUpdate_WhenTheNewExpiryPrecedesTheStoredActivation_Throws()
    {
        var secret = CreateSecret(Created.AddDays(20), Created.AddDays(92));

        var exception = Assert.Throws<ArgumentException>(() =>
            SecretLifecycle.ResolveUpdate(secret, null, null, Created.AddDays(10))
        );

        Assert.Contains("cannot retire", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TryResolveUpdate_WhenTheInstantsAreOutOfOrder_IsFalseAndNamesThePair()
    {
        var secret = CreateSecret(Created, Expires);

        var isValid = SecretLifecycle.TryResolveUpdate(
            secret,
            Created.AddSeconds(-1),
            null,
            Expires,
            out _,
            out var error
        );

        Assert.False(isValid);
        Assert.NotNull(error);
        Assert.Contains("cannot activate", error, StringComparison.Ordinal);
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

    private static PersistedSecret CreateSecret(
        DateTimeOffset activatesWhen,
        DateTimeOffset retiresWhen
    ) =>
        new()
        {
            SecretId = "secret-1",
            Use = null,
            Algorithm = null,
            CreatedWhen = Created,
            ActivatesWhen = activatesWhen,
            RetiresWhen = retiresWhen,
            ExpiresWhen = Expires,
            SecretType = SecretTypes.Symmetric,
            SecretEncoding = SecretEncodings.SymmetricRaw,
            Origin = SecretOrigins.Generated,
            KeySizeBits = 256,
            EncodedValue = "AA",
        };
}
